"""Verify AssetRipper prefab outputs selected by the mainline root receipt.

The resulting receipt proves the exact Android-derived prefab YAML inputs and
metadata used by a later Windows conversion. It does not make them runnable.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parents[1]


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def source_path(path: Path, repository_root: Path) -> str:
    return path.relative_to(repository_root).as_posix()


def checked_relative(value: str, prefix: str) -> str:
    path = PurePosixPath(value)
    if "\\" in value or path.is_absolute() or ".." in path.parts or not value.startswith(prefix):
        raise ValueError("Selected prefab path is outside the expected export root: " + value)
    return path.as_posix()


def build(roots_manifest_path: Path, export_manifest_path: Path, repository_root: Path = ROOT) -> dict:
    repository_root = repository_root.resolve()
    roots_manifest_path = roots_manifest_path.resolve()
    export_manifest_path = export_manifest_path.resolve()
    roots = json.loads(roots_manifest_path.read_bytes())
    export = json.loads(export_manifest_path.read_bytes())
    if roots.get("manifest") != export.get("manifest") or not export.get("completed"):
        raise ValueError("Prefab roots and completed native export must use the same source version")
    if not isinstance(roots.get("mapClassId"), int) or roots["mapClassId"] <= 0:
        raise ValueError("Prefab root receipt has no valid map identity")
    export_directory = export.get("export_directory")
    if not isinstance(export_directory, str) or not export_directory or "\\" in export_directory \
            or PurePosixPath(export_directory).is_absolute() \
            or ".." in PurePosixPath(export_directory).parts:
        raise ValueError("Native export directory is invalid")

    outputs = {}
    for entry in export.get("outputs", []):
        path = entry.get("path")
        if not isinstance(path, str) or path in outputs:
            raise ValueError("Native export output paths must be present and unique")
        outputs[path] = entry

    asset_root = export_manifest_path.parent / export_directory / "ExportedProject/Assets"
    records = []
    seen = set()
    for prefab in roots.get("prefabs", []):
        relative = checked_relative(prefab.get("path", ""), "Assets/Prefabs/")
        if not relative.lower().endswith(".prefab") or relative in seen:
            raise ValueError("Prefab root paths must be unique prefab assets")
        seen.add(relative)
        rows = []
        for suffix in ("", ".meta"):
            asset_path = relative + suffix
            exported_path = "ExportedProject/" + asset_path
            entry = outputs.get(exported_path)
            file_path = asset_root / asset_path.removeprefix("Assets/")
            if entry is None or not file_path.is_file():
                raise FileNotFoundError("Selected native prefab output is missing: " + exported_path)
            raw = file_path.read_bytes()
            digest = sha(raw)
            if digest != entry.get("sha256") or len(raw) != entry.get("bytes"):
                raise ValueError("Selected native prefab output differs from its export receipt: " + exported_path)
            rows.append(dict(path=source_path(file_path, repository_root), sha256=digest, bytes=len(raw)))
        records.append(dict(path=relative, source=prefab.get("source"), object_sha256=prefab.get("object_sha256"),
                            prefab=rows[0], meta=rows[1]))

    if not records:
        raise ValueError("The official map has no selected entity prefab roots")
    return dict(
        format=1,
        manifest=roots["manifest"],
        mapClassId=roots["mapClassId"],
        roots_receipt=dict(path=source_path(roots_manifest_path, repository_root),
                           sha256=sha(roots_manifest_path.read_bytes())),
        export_receipt=dict(path=source_path(export_manifest_path, repository_root),
                            sha256=sha(export_manifest_path.read_bytes())),
        verifier=dict(path="scripts/" + Path(__file__).name, sha256=sha(Path(__file__).read_bytes())),
        export_directory=export_directory,
        counts=dict(prefabs=len(records), prefab_files=len(records), metadata_files=len(records)),
        outputs=records,
        limitations=["Android-derived AssetRipper YAML and metadata only; Windows script bindings, shaders, "
                     "runtime assets and entity host are not supplied by this receipt."],
        operation="Verify the selected official entity prefab roots and metadata against the completed "
                  "AssetRipper export output hashes. This receipt identifies inputs for native Windows conversion.",
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map-id", type=int, required=True)
    parser.add_argument("--export-manifest", type=Path)
    args = parser.parse_args()
    roots_path = ROOT / "assets/reference/official-map" / ("map" + str(args.map_id) + "-entity-prefab-roots-source-manifest.json")
    roots = json.loads(roots_path.read_bytes())
    if roots.get("mapClassId") != args.map_id:
        raise ValueError("Selected entity root receipt belongs to another map")
    export_path = args.export_manifest or ROOT / ".tools/source-packages/current" / roots["manifest"] \
        / "native-conversion/native-export-all-source-manifest.json"
    result = build(roots_path, export_path)
    output = ROOT / "assets/reference/official-map" / ("map" + str(args.map_id) + "-native-entity-prefab-exports-source-manifest.json")
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Verified selected native entity prefab exports:", result["counts"])
    print("Output receipt:", output.relative_to(ROOT))


if __name__ == "__main__":
    main()
