"""Install the selected 903 native bed prefab and its verified asset closure."""
import hashlib
import json
import os
import re
from pathlib import Path

from official_source import OfficialSource

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "unity/Assets/Official/Home/Native903/Bed"
RECEIPT = ROOT / "assets/reference/home-bed-native-source-manifest.json"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def guid(path: Path) -> str:
    match = re.search(r"^guid: ([0-9a-f]{32})\s*$", path.read_text("utf8"), re.M)
    if not match:
        raise ValueError("Unity GUID missing: " + str(path))
    return match.group(1)


def folder_meta(path: Path) -> None:
    target = Path(str(path) + ".meta")
    if not target.exists():
        value = hashlib.md5(("zjcs:home-bed-native-folder:" + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        target.write_text(
            "fileFormatVersion: 2\n"
            + "guid: " + value + "\n"
            + "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n",
            "utf8", newline="\n")


def build() -> None:
    version = OfficialSource().version
    if version != "903_156948":
        raise ValueError("Selected native bed source changed: " + version)
    cache = ROOT / ".tools/source-packages/current" / version / "home-bed-native"
    export = cache / "native-export/ExportedProject/Assets"
    prefab = export / "Prefabs/Home/Dynamic/Furniture/Bed/Bed.prefab"
    export_receipt = cache / "native-export-source-manifest.json"
    code = ROOT / ".tools/source-packages/current" / version / "official-code"
    if not prefab.is_file() or not export_receipt.is_file():
        raise FileNotFoundError("Verified native bed export is missing")
    if json.loads(export_receipt.read_text("utf8"))["manifest"] != version:
        raise ValueError("Native export receipt version differs")

    # Index only the exported asset metadata, then walk the exact GUID closure
    # from the prefab. Assembly-CSharp remains the already verified external
    # plugin; it is intentionally not copied into game resources.
    by_guid = {}
    for base, dirs, files in os.walk(export):
        dirs[:] = [name for name in dirs if name not in {"Editor", "Plugins"}]
        for name in files:
            if not name.endswith(".meta"):
                continue
            path = Path(base) / name
            text = path.read_text("utf8", errors="ignore")
            match = re.search(r"^guid: ([0-9a-f]{32})\s*$", text, re.M)
            if match:
                by_guid[match.group(1)] = path

    closure = []
    pending = [prefab]
    seen = set()
    while pending:
        path = pending.pop()
        if path in seen or not path.is_file():
            continue
        seen.add(path)
        closure.append(path)
        text = path.read_text("utf8", errors="ignore")
        for value in set(re.findall(r"guid: ([0-9a-f]{32})", text)):
            meta = by_guid.get(value)
            if meta is None:
                continue
            dependency = meta.with_suffix("")
            if dependency.is_file() and dependency not in seen:
                pending.append(dependency)
    closure.sort()

    DEST.mkdir(parents=True, exist_ok=True)
    folder_meta(DEST)
    outputs = {}
    inputs = {export_receipt.relative_to(ROOT).as_posix(): sha(export_receipt),
              (ROOT / "scripts/prepare_home_bed_native.py").relative_to(ROOT).as_posix():
                  sha(ROOT / "scripts/prepare_home_bed_native.py")}
    for name in ("FurnitureBase.cs", "BaseHangUpFurniture.cs", "BedFurniture.cs"):
        path = code / "client-source-resolved" / name
        if not path.is_file():
            raise FileNotFoundError(str(path))
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)

    for source in closure:
        relative = source.relative_to(export)
        target = DEST / "Dependencies" / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
        source_meta = Path(str(source) + ".meta")
        if not source_meta.is_file():
            raise FileNotFoundError(str(source_meta))
        target_meta = Path(str(target) + ".meta")
        target_meta.write_bytes(source_meta.read_bytes())
        inputs[source.relative_to(ROOT).as_posix()] = sha(source)
        inputs[source_meta.relative_to(ROOT).as_posix()] = sha(source_meta)
        outputs[target.relative_to(ROOT).as_posix()] = sha(target)
        outputs[target_meta.relative_to(ROOT).as_posix()] = sha(target_meta)
    # Keep the prefab at a stable Resources path for the native scene owner.
    source_prefab = DEST / "Dependencies/Prefabs/Home/Dynamic/Furniture/Bed/Bed.prefab"
    resource_prefab = ROOT / "unity/Assets/Game/Resources/Home/NativeHomeBed.prefab"
    resource_meta = Path(str(resource_prefab) + ".meta")
    resource_prefab.parent.mkdir(parents=True, exist_ok=True)
    resource_prefab.write_bytes(source_prefab.read_bytes())
    prefab_guid = hashlib.md5(b"zjcs:home-native-bed-prefab-903").hexdigest()
    resource_meta.write_text(
        "fileFormatVersion: 2\nguid: " + prefab_guid +
        "\nPrefabImporter:\n  externalObjects: {}\n", "utf8", newline="\n")
    outputs[resource_prefab.relative_to(ROOT).as_posix()] = sha(resource_prefab)
    outputs[resource_meta.relative_to(ROOT).as_posix()] = sha(resource_meta)
    inputs[source_prefab.relative_to(ROOT).as_posix()] = sha(source_prefab)
    for directory in sorted((path for path in DEST.rglob("*") if path.is_dir())):
        folder_meta(directory)
    outputs[Path(str(DEST) + ".meta").relative_to(ROOT).as_posix()] = sha(Path(str(DEST) + ".meta"))
    for meta in sorted(DEST.rglob("*.meta")):
        outputs[meta.relative_to(ROOT).as_posix()] = sha(meta)

    receipt = {
        "manifest": version,
        "inputs": inputs,
        "outputs": outputs,
        "prefab": {"path": resource_prefab.relative_to(ROOT).as_posix(), "guid": prefab_guid,
                    "source": prefab.relative_to(ROOT).as_posix(),
                    "source_sha256": sha(prefab), "closure_count": len(closure)},
        "generator_sha256": sha(ROOT / "scripts/prepare_home_bed_native.py"),
        "scope": "903 native Bed prefab and exact serialized dependency closure; offline actor binding is separate.",
    }
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", "utf8", newline="\n")
    print("Installed native bed prefab closure:", len(closure), "assets")


if __name__ == "__main__":
    build()
