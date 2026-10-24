"""Copy Home assets from an AssetRipper Unity export with their GUID closure."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys


ROOT = Path(__file__).resolve().parents[1]
GUID = re.compile(r"guid: ([0-9a-f]{32})")
BUILTIN_GUIDS = {
    "0000000000000000e000000000000000",
    "0000000000000000f000000000000000",
    "00000000000000000000000000000000",
}


def digest(path: Path) -> str:
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def collect_assets(asset_root: Path) -> tuple[dict[str, Path], set[str], dict[str, set[Path]]]:
    by_guid: dict[str, Path] = {}
    for meta in asset_root.rglob("*.meta"):
        match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf-8", errors="replace"), re.M)
        asset = meta.with_suffix("")
        if match and asset.is_file():
            guid = match.group(1)
            if guid in by_guid:
                raise ValueError(f"Duplicate exported GUID {guid}: {by_guid[guid]} and {asset}")
            by_guid[guid] = asset

    roots = set()
    for pattern in (
        "Scenes/Main.unity",
        "Prefabs/Home/**/*.prefab",
        "Prefabs/Home/**/*.anim",
        "Prefabs/UI/Home/**/*.prefab",
        "Prefabs/UI/Mail/**/*.prefab",
        "Prefabs/UI/Main/Main_HomeUI.controller",
        "Prefabs/UI/Main/Main_TravelNotesPanel.controller",
        "GameObject/Home*.prefab",
    ):
        roots.update(path for path in asset_root.glob(pattern) if path.is_file())
    if not roots:
        raise ValueError("AssetRipper export has no Home scene or Home prefab roots")

    selected = set(roots)
    pending = list(roots)
    unresolved: dict[str, set[Path]] = {}
    while pending:
        asset = pending.pop()
        try:
            source = asset.read_text(encoding="utf-8")
        except (UnicodeDecodeError, OSError):
            continue
        for guid in GUID.findall(source):
            if guid in BUILTIN_GUIDS:
                continue
            dependency = by_guid.get(guid)
            if dependency is None:
                unresolved.setdefault(guid, set()).add(asset)
            elif dependency not in selected:
                selected.add(dependency)
                pending.append(dependency)
    return by_guid, selected, unresolved


def verify_builtin_font_texture(staged_inputs: Path) -> dict:
    manifest = json.loads((staged_inputs / "source-manifest.json").read_text(encoding="utf-8"))
    builtin = next((item for item in manifest.get("builtin_inputs", [])
                    if item.get("name") == "assets/bin/Data/unity default resources"), None)
    if builtin is None:
        raise ValueError("The pinned APK unity default resources input was not staged")
    path = staged_inputs / builtin["output"]
    if not path.is_file() or path.stat().st_size != builtin["raw_size"] or digest(path) != builtin["raw_sha256"]:
        raise ValueError("The staged Unity default resources do not match the APK source hash")
    sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
    import UnityPy
    environment = UnityPy.load(str(path))
    match = next((obj.read() for obj in environment.objects
                  if obj.type.name == "Texture2D" and obj.path_id == 10103), None)
    if match is None or match.m_Name != "Font Texture" or match.m_Width != 0 or match.m_Height != 0:
        raise ValueError("APK Unity default resources pathID 10103 is not the expected Font Texture")
    return {"apk_entry": builtin["name"], "sha256": builtin["raw_sha256"],
            "object_path_id": 10103, "object_name": match.m_Name,
            "unity_guid": "0000000000000000e000000000000000"}


def guid_from_meta(path: Path) -> str | None:
    match = re.search(r"^guid: ([0-9a-f]{32})$", path.read_text(encoding="utf-8", errors="replace"), re.M)
    return match.group(1) if match else None


def finalize_payload(output_assets: Path, records: list[dict]) -> tuple[list[dict], list[dict], list[dict]]:
    official_root = ROOT / "unity/Assets/Official"
    previous_guid_paths = {}
    for meta in official_root.rglob("*.meta"):
        if meta.is_relative_to(output_assets):
            continue
        guid = guid_from_meta(meta)
        asset = meta.with_suffix("")
        if guid and asset.is_file():
            previous_guid_paths[guid] = asset

    shared_bindings = []
    shared_paths = set()
    for record in records:
        relative = record["path"]
        asset = output_assets / Path(relative)
        meta = asset.with_name(asset.name + ".meta")
        guid = guid_from_meta(meta) if meta.exists() else None
        existing = previous_guid_paths.get(guid)
        if existing is None:
            continue
        if digest(asset) != digest(existing):
            raise ValueError("Official Home asset GUID conflicts with another module: " + relative)
        asset.unlink()
        meta.unlink()
        shared_paths.add(relative)
        shared_bindings.append({"path": relative, "guid": guid,
                                "shared_asset": existing.relative_to(ROOT).as_posix(),
                                "sha256": digest(existing)})

    records = [record for record in records if record["path"] not in shared_paths]
    folders = []
    for directory in sorted((path for path in output_assets.rglob("*") if path.is_dir()),
                            key=lambda path: len(path.parts), reverse=True):
        folder_meta = directory.with_name(directory.name + ".meta")
        if not folder_meta.exists():
            relative = directory.relative_to(output_assets.parent).as_posix()
            guid = hashlib.sha256(("zjcs-home-folder:" + relative).encode("utf-8")).hexdigest()[:32]
            while guid in previous_guid_paths:
                guid = hashlib.sha256((guid + relative).encode("utf-8")).hexdigest()[:32]
            content = ("fileFormatVersion: 2\n"
                       f"guid: {guid}\n"
                       "folderAsset: yes\n"
                       "DefaultImporter:\n"
                       "  externalObjects: {}\n"
                       "  userData:\n"
                       "  assetBundleName:\n"
                       "  assetBundleVariant:\n")
            folder_meta.write_text(content, encoding="utf-8")
        folders.append({"path": directory.relative_to(output_assets.parent).as_posix(),
                        "guid": guid_from_meta(folder_meta), "sha256": digest(folder_meta)})

    root_meta = output_assets.with_name(output_assets.name + ".meta")
    if not root_meta.exists():
        relative = output_assets.relative_to(ROOT / "unity/Assets").as_posix()
        guid = hashlib.sha256(("zjcs-home-folder:" + relative).encode("utf-8")).hexdigest()[:32]
        content = ("fileFormatVersion: 2\n"
                   f"guid: {guid}\n"
                   "folderAsset: yes\n"
                   "DefaultImporter:\n"
                   "  externalObjects: {}\n"
                   "  userData:\n"
                   "  assetBundleName:\n"
                   "  assetBundleVariant:\n")
        root_meta.write_text(content, encoding="utf-8")
    folders.append({"path": "Assets/Official/Home", "guid": guid_from_meta(root_meta),
                    "sha256": digest(root_meta)})
    return records, folders, shared_bindings


def clear_previous_generated_payload(output_assets: Path, manifest_path: Path) -> None:
    if not output_assets.exists() or not any(output_assets.iterdir()):
        return
    if not manifest_path.is_file():
        raise FileExistsError("Refusing to overwrite Home payload without its migration manifest")
    previous = json.loads(manifest_path.read_text(encoding="utf-8"))
    if previous.get("module") != "city-and-home" or not previous.get("outputs"):
        raise ValueError("Existing Home payload manifest is not a generated Home migration manifest")
    owned = set()
    for record in previous["outputs"]:
        relative = Path(record["path"])
        if relative.is_absolute() or ".." in relative.parts:
            raise ValueError("Existing Home payload manifest contains an unsafe path")
        asset = output_assets / relative
        meta = asset.with_name(asset.name + ".meta")
        if not asset.is_file() or digest(asset) != record["sha256"]:
            raise ValueError("Existing generated Home asset changed; preserving it: " + str(asset))
        if meta.is_file() and record.get("guid") and guid_from_meta(meta) != record["guid"]:
            raise ValueError("Existing generated Home metadata changed; preserving it: " + str(meta))
        owned.add(asset.resolve())
        owned.add(meta.resolve())
    for folder in previous.get("folders", []):
        folder_path = Path(folder["path"])
        if folder_path.as_posix() == "Assets/Official/Home":
            meta = output_assets.with_name(output_assets.name + ".meta")
        else:
            meta = output_assets.parent / folder_path
            meta = meta.with_name(meta.name + ".meta")
        if meta.is_file() and digest(meta) == folder.get("sha256"):
            owned.add(meta.resolve())
    existing = {path.resolve() for path in output_assets.rglob("*") if path.is_file()}
    unexpected = existing - owned
    if unexpected:
        raise ValueError("Unregistered files exist in generated Home payload; preserving them: "
                         + str(sorted(unexpected)[0]))
    for path in sorted((output_assets / record["path"] for record in previous["outputs"]),
                       key=lambda item: len(item.parts), reverse=True):
        path.unlink()
        meta = path.with_name(path.name + ".meta")
        if meta.exists():
            meta.unlink()
    for path in sorted((item for item in output_assets.rglob("*") if item.is_dir()),
                       key=lambda item: len(item.parts), reverse=True):
        if not any(path.iterdir()):
            path.rmdir()
    root_meta = output_assets.with_name(output_assets.name + ".meta")
    if root_meta.exists() and root_meta.resolve() in owned:
        root_meta.unlink()
    if output_assets.exists() and not any(output_assets.iterdir()):
        output_assets.rmdir()


def reconcile_imported_guid_collisions(manifest_path: Path, output_assets: Path) -> int:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("module") != "city-and-home":
        raise ValueError("Not a Home migration manifest")
    external = {}
    official_root = ROOT / "unity/Assets/Official"
    for meta in official_root.rglob("*.meta"):
        if meta.is_relative_to(output_assets):
            continue
        guid = guid_from_meta(meta)
        asset = meta.with_suffix("")
        if guid and asset.is_file():
            external.setdefault(guid, []).append(asset)

    kept = []
    shared = manifest.setdefault("shared_bindings", [])
    reconciled = 0
    for output in manifest["outputs"]:
        asset = output_assets / Path(output["path"])
        meta = asset.with_name(asset.name + ".meta")
        current_guid = guid_from_meta(meta) if meta.is_file() else None
        if current_guid == output.get("guid"):
            kept.append(output)
            continue
        candidates = external.get(output.get("guid"), [])
        match = next((candidate for candidate in candidates
                      if candidate.is_file() and asset.is_file()
                      and digest(candidate) == output["sha256"] == digest(asset)), None)
        if match is None:
            raise ValueError("Unity changed a Home GUID without a verified shared source match: " + output["path"])
        asset.unlink()
        if meta.exists():
            meta.unlink()
        shared.append({"path": output["path"], "guid": output["guid"],
                       "shared_asset": match.relative_to(ROOT).as_posix(),
                       "sha256": output["sha256"]})
        reconciled += 1
    manifest["outputs"] = kept
    empty_folders = set()
    for folder in manifest.get("folders", []):
        if folder.get("path") == "Assets/Official/Home":
            continue
        directory = output_assets.parent / Path(folder["path"])
        if not directory.exists():
            folder_meta = directory.with_name(directory.name + ".meta")
            if folder_meta.is_file() and digest(folder_meta) == folder.get("sha256"):
                folder_meta.unlink()
            empty_folders.add(folder["path"])
    for directory in sorted((path for path in output_assets.rglob("*") if path.is_dir()),
                            key=lambda path: len(path.parts), reverse=True):
        if not any(directory.iterdir()):
            empty_folders.add(directory.relative_to(output_assets.parent).as_posix())
            folder_meta = directory.with_name(directory.name + ".meta")
            recorded = next((folder for folder in manifest.get("folders", [])
                             if folder.get("path") == directory.relative_to(output_assets.parent).as_posix()), None)
            if folder_meta.is_file() and recorded and digest(folder_meta) == recorded.get("sha256"):
                folder_meta.unlink()
            directory.rmdir()
    folders = []
    for directory in output_assets.rglob("*"):
        if directory.is_dir():
            meta = directory.with_name(directory.name + ".meta")
            if meta.is_file():
                folders.append({"path": directory.relative_to(output_assets.parent).as_posix(),
                                "guid": guid_from_meta(meta), "sha256": digest(meta)})
    root_meta = output_assets.with_name(output_assets.name + ".meta")
    if root_meta.is_file():
        folders.append({"path": "Assets/Official/Home", "guid": guid_from_meta(root_meta),
                        "sha256": digest(root_meta)})
    manifest["folders"] = folders
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return reconciled


def record_unity_import_hashes(manifest_path: Path, output_assets: Path) -> int:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("module") != "city-and-home" or manifest.get("source_manifest") != "854_156383":
        raise ValueError("Not a pinned Home migration manifest")

    normalized_paths = {"Packages/com.unity.render-pipelines.universal/Shaders/2D/Sprite-Lit-Default.asset"}
    recorded = 0
    for output in manifest.get("outputs", []):
        relative = Path(output["path"])
        if relative.is_absolute() or ".." in relative.parts:
            raise ValueError("Home migration manifest contains an unsafe path")
        asset = output_assets / relative
        if not asset.is_file():
            raise FileNotFoundError("Unity-imported Home asset is missing: " + str(asset))
        imported_hash = digest(asset)
        if imported_hash == output.get("sha256"):
            output.pop("unity_import_sha256", None)
            continue
        if relative.as_posix() not in normalized_paths:
            raise ValueError("Unity changed an unapproved Home asset: " + relative.as_posix())
        output["unity_import_sha256"] = imported_hash
        output["unity_import_bytes"] = asset.stat().st_size
        recorded += 1

    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return recorded


def migrate(export_assets: Path, staged_inputs: Path, output_assets: Path, manifest_path: Path) -> dict:
    export_assets = export_assets.resolve()
    staged_inputs = staged_inputs.resolve()
    output_assets = output_assets.resolve()
    if not export_assets.is_dir() or not staged_inputs.is_dir():
        raise FileNotFoundError("Expected AssetRipper Assets and verified converter inputs directories")
    input_manifest = json.loads((staged_inputs / "source-manifest.json").read_text(encoding="utf-8"))
    if input_manifest.get("manifest") != "854_156383":
        raise ValueError("Home converter inputs do not use the pinned original manifest")
    clear_previous_generated_payload(output_assets, manifest_path)

    _, selected, unresolved = collect_assets(export_assets)
    builtin_font = verify_builtin_font_texture(staged_inputs)
    remaps = {}
    if unresolved:
        if len(unresolved) != 1:
            raise ValueError("Exported Home dependency closure has unresolved GUIDs: "
                             + ", ".join(sorted(unresolved)[:20]))
        missing_guid, owners = next(iter(unresolved.items()))
        if not owners or any("MPLUSRounded1c-Medium" not in str(owner) or owner.suffix != ".mat"
                             for owner in owners):
            raise ValueError("Exported Home dependency closure has unresolved GUIDs: " + missing_guid)
        remaps = {missing_guid: builtin_font}
    output_assets.mkdir(parents=True, exist_ok=True)
    records = []
    reference_remaps = []
    for source in sorted(selected):
        relative = source.relative_to(export_assets)
        target = output_assets / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        content = source.read_bytes()
        for missing_guid, mapping in remaps.items():
            if source in unresolved[missing_guid]:
                text = content.decode("utf-8")
                old = "{fileID: 2800000, guid: " + missing_guid + ", type: 3}"
                new = "{fileID: 10103, guid: " + mapping["unity_guid"] + ", type: 0}"
                if text.count(old) != 1:
                    raise ValueError("M PLUS font material does not contain the expected Font Texture reference: "
                                     + str(relative))
                content = text.replace(old, new, 1).encode("utf-8")
                reference_remaps.append({"output": relative.as_posix(), "from_guid": missing_guid,
                                         "to_guid": mapping["unity_guid"], "file_id": 10103,
                                         "source_builtin": mapping["apk_entry"]})
        target.write_bytes(content)
        shutil.copystat(source, target)
        meta = source.with_name(source.name + ".meta")
        if meta.exists():
            shutil.copy2(meta, target.with_name(target.name + ".meta"))
        records.append({
            "path": relative.as_posix(),
            "bytes": target.stat().st_size,
            "sha256": digest(target),
            "guid": next((match.group(1) for match in [re.search(
                r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf-8", errors="replace"), re.M
            )] if match), None) if meta.exists() else None,
        })

    records, folders, shared_bindings = finalize_payload(output_assets, records)

    manifest = {
        "format": 1,
        "module": "city-and-home",
        "source_manifest": "854_156383",
        "inputs": input_manifest["inputs"],
        "builtin_inputs": input_manifest.get("builtin_inputs", []),
        "converter": {
            "name": "AssetRipper",
            "version": "1.3.14.0",
            "source_unity": "2022.3.57f1",
            "target_unity": "2022.3.57f1c2",
            "shader_export_mode": "Yaml",
            "selection": "Home scene, Home/Theme/Furniture/RandomReward prefabs, Home and Mail UI, and GUID dependency closure",
        },
        "reference_remaps": reference_remaps,
        "folders": folders,
        "shared_bindings": shared_bindings,
        "outputs": records,
    }
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return manifest


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export-assets", type=Path, default=ROOT / "output/home-native-export/ExportedProject/Assets")
    parser.add_argument("--staged-inputs", type=Path, default=ROOT / "output/home-native-inputs")
    parser.add_argument("--output-assets", type=Path, default=ROOT / "unity/Assets/Official/Home")
    parser.add_argument("--manifest", type=Path, default=ROOT / "assets/reference/home-unity/source-manifest.json")
    parser.add_argument("--reconcile-imported-guid-collisions", action="store_true",
                        help="Deduplicate imported script files only when the existing official GUID and hash match")
    parser.add_argument("--record-unity-import-hashes", action="store_true",
                        help="Record Unity's known deterministic Shader YAML normalization without replacing conversion hashes")
    args = parser.parse_args()
    try:
        if args.record_unity_import_hashes:
            count = record_unity_import_hashes(args.manifest.resolve(), args.output_assets.resolve())
            print(f"Recorded Unity-normalized hashes for {count} Home assets; conversion hashes remain intact")
            return 0
        if args.reconcile_imported_guid_collisions:
            count = reconcile_imported_guid_collisions(args.manifest.resolve(), args.output_assets.resolve())
            print(f"Deduplicated {count} Home files against identical official assets with their original GUIDs")
            return 0
        manifest = migrate(args.export_assets, args.staged_inputs, args.output_assets, args.manifest)
    except (OSError, ValueError, KeyError) as error:
        print("ERROR: " + str(error), file=sys.stderr)
        return 1
    print(f"Migrated {len(manifest['outputs'])} Unity assets with GUID metadata to {args.output_assets}")
    print(f"Source manifest: {args.manifest}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
