"""Audit mainline entity prefab script references against pinned client code."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
import subprocess
from pathlib import Path, PurePosixPath

from Crypto.Hash import MD4

ROOT = Path(__file__).resolve().parents[1]
CLIENT_SCRIPT_GUID = "06dd3d5416bf43ea368cc39ff96e6d5a"
SCRIPT_REFERENCE = re.compile(
    r"(?m)^\s*m_Script:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]{32})"
)
TYPE_DECLARATION = re.compile(
    r"\b(?:class|struct|interface|enum)\s+([A-Za-z_]\w*)"
)
NAMESPACE = re.compile(r"(?m)^\s*namespace\s+([A-Za-z_][\w.]*)")
GUID = re.compile(r"(?m)^guid:\s*([0-9a-f]{32})\s*$")


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def script_file_id(namespace: str, type_name: str) -> int:
    raw = ("s\0\0\0" + namespace + type_name).encode("utf-8")
    return struct.unpack("<i", MD4.new(raw).digest()[:4])[0]


def parse_script_references(text: str) -> list[tuple[int, str]]:
    return [(int(file_id), guid) for file_id, guid in SCRIPT_REFERENCE.findall(text)]


def read_types(path: Path) -> list[tuple[str, str]]:
    text = path.read_text(encoding="utf-8-sig", errors="ignore")
    namespace_match = NAMESPACE.search(text)
    namespace = namespace_match.group(1) if namespace_match else ""
    return [(namespace, name) for name in TYPE_DECLARATION.findall(text)]


def source_identity(path: Path, root: Path) -> dict:
    raw = path.read_bytes()
    return {"path": path.relative_to(root).as_posix(), "sha256": sha(raw), "bytes": len(raw)}


def build(map_id: int, roots_path: Path, export_path: Path, source_root: Path,
          runtime_root: Path, repository_root: Path = ROOT) -> dict:
    repository_root = repository_root.resolve()
    roots_path, export_path = roots_path.resolve(), export_path.resolve()
    source_root, runtime_root = source_root.resolve(), runtime_root.resolve()
    roots = json.loads(roots_path.read_bytes())
    export = json.loads(export_path.read_bytes())
    if roots.get("mapClassId") != map_id or roots.get("manifest") != export.get("manifest"):
        raise ValueError("Map identity and source version must match the selected export")
    if not export.get("completed"):
        raise ValueError("Native export is not complete")
    export_directory = export.get("export_directory")
    if not isinstance(export_directory, str) or "\\" in export_directory \
            or PurePosixPath(export_directory).is_absolute() or ".." in PurePosixPath(export_directory).parts:
        raise ValueError("Native export directory is invalid")

    outputs = {row.get("path"): row for row in export.get("outputs", []) if isinstance(row, dict)}
    asset_root = export_path.parent / export_directory / "ExportedProject/Assets"
    references: dict[int, set[str]] = {}
    for prefab in roots.get("prefabs", []):
        relative = prefab.get("path", "")
        pure = PurePosixPath(relative)
        if "\\" in relative or not relative.startswith("Assets/Prefabs/") \
                or pure.is_absolute() or ".." in pure.parts or not relative.endswith(".prefab"):
            raise ValueError("Selected prefab path is invalid: " + relative)
        output_path = "ExportedProject/" + relative
        proof = outputs.get(output_path)
        path = asset_root / PurePosixPath(relative).relative_to("Assets")
        if proof is None or not path.is_file():
            raise FileNotFoundError("Selected native prefab output is missing: " + output_path)
        raw = path.read_bytes()
        if len(raw) != proof.get("bytes") or sha(raw) != proof.get("sha256"):
            raise ValueError("Selected native prefab differs from its export receipt: " + output_path)
        for file_id, guid in parse_script_references(raw.decode("utf-8-sig", errors="ignore")):
            if guid == CLIENT_SCRIPT_GUID and file_id:
                references.setdefault(file_id, set()).add(relative)

    if not references:
        raise ValueError("Selected prefab roots contain no client Assembly-CSharp scripts")

    source_by_id: dict[int, dict] = {}
    source_directories = [source_root / name for name in
                          ("client-source", "common-source", "game-framework-source")
                          if (source_root / name).is_dir()]
    if not source_directories:
        source_directories = [source_root]
    for source_directory in source_directories:
        for path in source_directory.rglob("*.cs"):
            try:
                types = read_types(path)
            except OSError:
                continue
            for namespace, type_name in types:
                file_id = script_file_id(namespace, type_name)
                if file_id in references:
                    row = source_by_id.setdefault(file_id, {"namespace": namespace, "type": type_name, "paths": []})
                    if (row["namespace"], row["type"]) != (namespace, type_name):
                        raise ValueError("Client MonoScript local ID collision: " + str(file_id))
                    row["paths"].append(source_identity(path, repository_root))

    required_types = {(row["namespace"], row["type"]) for row in source_by_id.values()}
    runtime_by_type: dict[tuple[str, str], list[dict]] = {}
    dirty_runtime_paths: set[str] = set()
    try:
        runtime_relative = runtime_root.relative_to(repository_root).as_posix()
        status = subprocess.run(
            ["git", "status", "--porcelain", "--untracked-files=all", "--", runtime_relative],
            cwd=repository_root, check=False, capture_output=True, text=True,
        )
        if status.returncode == 0:
            dirty_runtime_paths = {line[3:].strip().replace("\\", "/")
                                   for line in status.stdout.splitlines() if len(line) > 3}
    except (OSError, ValueError):
        pass
    for path in runtime_root.rglob("*.cs"):
        if path.relative_to(repository_root).as_posix() in dirty_runtime_paths:
            continue
        try:
            types = read_types(path)
            meta_path = Path(str(path) + ".meta")
            if not meta_path.is_file():
                continue
            meta_raw = meta_path.read_bytes()
            match = GUID.search(meta_raw.decode("utf-8-sig", errors="ignore"))
            if match is None:
                continue
            for namespace, type_name in types:
                key = (namespace, type_name)
                if key in required_types:
                    runtime_by_type.setdefault(key, []).append({
                        "script": source_identity(path, repository_root),
                        "meta": source_identity(meta_path, repository_root),
                        "guid": match.group(1),
                    })
        except OSError:
            continue

    scripts = []
    for file_id in sorted(references):
        source = source_by_id.get(file_id)
        candidates = runtime_by_type.get((source["namespace"], source["type"]), []) if source else []
        scripts.append({
            "fileID": file_id,
            "type": source["type"] if source else None,
            "namespace": source["namespace"] if source else None,
            "source": source["paths"] if source else [],
            "runtime": candidates,
            "prefabs": sorted(references[file_id]),
        })

    return {
        "format": 1,
        "manifest": roots["manifest"],
        "mapClassId": map_id,
        "roots_receipt": source_identity(roots_path, repository_root),
        "export_receipt": source_identity(export_path, repository_root),
        "source_root": source_root.relative_to(repository_root).as_posix(),
        "source_directories": [path.relative_to(repository_root).as_posix() for path in source_directories],
        "runtime_root": runtime_root.relative_to(repository_root).as_posix(),
        "runtime_source_policy": "Only clean tracked working-tree files are considered; modified and untracked runtime files are excluded.",
        "generator": source_identity(Path(__file__), ROOT),
        "counts": {
            "client_script_ids": len(scripts),
            "source_resolved": sum(bool(row["source"]) for row in scripts),
            "runtime_type_matches": sum(bool(row["runtime"]) for row in scripts),
            "runtime_type_missing": sum(not row["runtime"] for row in scripts),
        },
        "scripts": scripts,
        "limitations": [
            "Type-name and namespace matches do not prove serialized field compatibility, Windows asset readiness, or runtime behavior.",
            "The source and runtime paths are evidence for follow-up binding work; this audit does not rewrite prefab YAML.",
        ],
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map-id", type=int, required=True)
    parser.add_argument("--roots-manifest", type=Path)
    parser.add_argument("--export-manifest", type=Path)
    parser.add_argument("--source-root", type=Path)
    parser.add_argument("--runtime-root", type=Path, default=ROOT / "unity/Assets/Game/Runtime")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    roots_path = args.roots_manifest or ROOT / "assets/reference/official-map" / (
        "map" + str(args.map_id) + "-entity-prefab-roots-source-manifest.json")
    roots = json.loads(roots_path.read_bytes())
    base = ROOT / ".tools/source-packages/current" / roots["manifest"]
    export_path = args.export_manifest or base / "native-conversion/native-export-all-source-manifest.json"
    source_root = args.source_root or base / "official-code"
    result = build(args.map_id, roots_path, export_path, source_root, args.runtime_root)
    output = args.output or ROOT / "assets/reference/official-map" / (
        "map" + str(args.map_id) + "-entity-script-bindings-source-manifest.json")
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Audited mainline entity script bindings:", result["counts"])
    print("Output receipt:", output.relative_to(ROOT))


if __name__ == "__main__":
    main()
