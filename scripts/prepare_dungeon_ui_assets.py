"""Install the current official dungeon and arena UI prefab dependency closure."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
EXPORT = ROOT / "output/dungeons-ui-ripper.zip/ExportedProject/Assets"
INPUTS = ROOT / "output/dungeons-ui-871-inputs/source-manifest.json"
OFFICIAL = ROOT / "unity/Assets/Official/Dungeons"
RESOURCES = ROOT / "unity/Assets/Game/Resources/Dungeons/UI"
RECEIPT = ROOT / "assets/reference/dungeon-ui/source-manifest.json"
TRAVEL_SOURCE = ROOT / "unity/Assets/Official/Home/Prefabs/UI/Home/TravelNotesPanel.prefab"
TRAVEL_RUNTIME = ROOT / "unity/Assets/Game/Resources/Travel/TravelNotesPanel.prefab"
GUID = re.compile(r"guid: ([0-9a-f]{32})")
META_GUID = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)
BUILTIN = {"0000000000000000e000000000000000", "0000000000000000f000000000000000",
           "00000000000000000000000000000000"}
RUNTIME_CONVERSION = "dungeon-ui-ugui-tmp-v1"
SCRIPT_GUID_MAPPINGS = {
    "18d0a90695249463551c00f45766e642": "4e29b1a8efbd4b44bb3f3716e73f07ff",  # Button
    "16c0ec1e295995dd9a2d2df2d7b6cc7e": "31a19414c41e5ae4aae2af33fee712f6",  # Mask
    "19fbb4a32b59286cd89b624f52ff7943": "30649d3a9faa99c48a7b1166b86bf2a0",  # HorizontalLayoutGroup
    "21c7954052da7655d96bff866c2b7662": "3245ec927659c4140ac4f8d17403cc18",  # ContentSizeFitter
    "4293fd42559bc8bb1c81715179adf536": "306cc8c2b49d7114eaa3623786fc2126",  # LayoutElement
    "f18f2cc2fe71a3a6d76a570588d5047c": "59f8146938fff824cb5fd77236b75775",  # VerticalLayoutGroup
    "3cf5a44414476512e00c3e7a2569a919": "fe87c0e1cc204ed48ad3b37840f39efc",  # Image
    "3f96b1d166d19b209697e35b35d65c76": "f4688fdb7df04437aeb418b961361dc5",  # TextMeshProUGUI
    "86fe8f3fc59dc06ea6b45a1bbee64682": "dc42784cf147c0c48a680349fa168899",  # GraphicRaycaster
    "69dacd7a039c12e90cde90bbae65247a": "3312d7739989d2b4e91e6319e9a96d76",  # RectMask2D
    "76ccfb4bfe5eebba5766d6f50f76ae37": "cfabb0440166ab443bba8876756fdfa9",  # Shadow
    "c0467acf10a7d1a8e81912a9a7988750": "1aa08ab6e0800fa44ae55d278d1423e3",  # ScrollRect
}


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def guid(path: Path) -> str | None:
    match = META_GUID.search(path.read_text(encoding="utf-8", errors="replace"))
    return match.group(1) if match else None


def convert_runtime_prefab(source: Path) -> bytes:
    return convert_runtime_prefab_text(source.read_text(encoding="utf-8"))


def convert_runtime_prefab_text(value: str) -> bytes:
    for source_guid, runtime_guid in SCRIPT_GUID_MAPPINGS.items():
        value = value.replace("guid: " + source_guid + ", type: 3",
                              "guid: " + runtime_guid + ", type: 3")
    return value.encode("utf-8")


def unity_folder_meta(path: Path) -> None:
    meta = path.with_name(path.name + ".meta")
    if meta.exists():
        return
    relative = path.relative_to(ROOT / "unity/Assets").as_posix()
    value = hashlib.sha256(("zjcs-dungeon-ui-folder:" + relative).encode()).hexdigest()[:32]
    meta.write_text("fileFormatVersion: 2\n"
                    f"guid: {value}\n"
                    "folderAsset: yes\nDefaultImporter:\n"
                    "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",
                    encoding="utf-8")


def build() -> dict:
    source_manifest = json.loads(INPUTS.read_text(encoding="utf-8"))
    if source_manifest.get("manifest") != "871_156598":
        raise ValueError("Dungeon UI inputs are not from the active official manifest")
    if not EXPORT.is_dir():
        raise FileNotFoundError("AssetRipper Unity export is missing: " + str(EXPORT))

    by_guid: dict[str, Path] = {}
    for meta in EXPORT.rglob("*.meta"):
        asset = meta.with_suffix("")
        asset_guid = guid(meta)
        if asset_guid and asset.is_file():
            if asset_guid in by_guid:
                raise ValueError("Duplicate AssetRipper GUID " + asset_guid)
            by_guid[asset_guid] = asset

    roots = set()
    roots.update((EXPORT / "Prefabs/UI/DailyDungeon").glob("*.prefab"))
    roots.update((EXPORT / "Prefabs/UI/DailyDungeon").glob("*.anim"))
    roots.update((EXPORT / "Prefabs/UI/Arena").glob("*.prefab"))
    roots.update((EXPORT / "Prefabs/UI/Top").glob("Arena*.prefab"))
    roots.update((EXPORT / "Prefabs/UI/Team").glob("TeamInvitePanel*.prefab"))
    roots = {path for path in roots if path.is_file()}
    if len(roots) < 30:
        raise ValueError("The official dungeon/arena prefab set is incomplete")
    requested_bundles = list(source_manifest["requested_bundles"])
    team_bundle = "defaultpackage_assets_prefabs_ui_team.bundle"
    if any(path.parent.name == "Team" for path in roots):
        if not any(team_bundle.removesuffix(".bundle") in item.get("name", "")
                   for item in source_manifest.get("inputs", [])):
            raise ValueError("The official team invite bundle is missing from verified inputs")
        requested_bundles.append(team_bundle)

    selected = set(roots)
    pending = list(roots)
    unresolved: dict[str, set[Path]] = {}
    while pending:
        asset = pending.pop()
        try:
            content = asset.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        for reference in GUID.findall(content):
            if reference in BUILTIN:
                continue
            dependency = by_guid.get(reference)
            if dependency is None:
                unresolved.setdefault(reference, set()).add(asset)
            elif dependency not in selected:
                selected.add(dependency)
                pending.append(dependency)
    if unresolved:
        sample = next(iter(unresolved.items()))
        raise ValueError("Official UI dependency is unresolved: " + sample[0] + " in "
                         + str(next(iter(sample[1]))))

    external: dict[str, Path] = {}
    official_root = ROOT / "unity/Assets/Official"
    for meta in official_root.rglob("*.meta"):
        asset = meta.with_suffix("")
        asset_guid = guid(meta)
        if asset_guid and asset.is_file() and not asset.is_relative_to(OFFICIAL):
            external[asset_guid] = asset

    assets_root = ROOT / "unity/Assets"
    unity_folder_meta(assets_root / "Official/Dungeons")
    unity_folder_meta(RESOURCES.parent)
    unity_folder_meta(RESOURCES)
    output_records = []
    shared_records = []
    resource_records = []
    for source in sorted(selected):
        relative = source.relative_to(EXPORT)
        source_meta = source.with_name(source.name + ".meta")
        asset_guid = guid(source_meta)
        if not asset_guid:
            raise ValueError("AssetRipper asset has no Unity GUID: " + str(source))
        existing = external.get(asset_guid)
        if existing:
            if digest(existing) != digest(source):
                raise ValueError("Official asset GUID conflicts with different local content: " + asset_guid)
            shared_records.append({"source": relative.as_posix(), "guid": asset_guid,
                                  "shared_asset": existing.relative_to(ROOT).as_posix(),
                                  "sha256": digest(existing)})
            official_target = existing
        else:
            official_target = OFFICIAL / relative
            official_target.parent.mkdir(parents=True, exist_ok=True)
            folder = OFFICIAL
            for part in relative.parent.parts:
                folder = folder / part
                unity_folder_meta(folder)
            if official_target.exists() and digest(official_target) != digest(source):
                raise ValueError("Refusing to overwrite changed dungeon UI asset: " + str(official_target))
            if not official_target.exists():
                official_target.write_bytes(source.read_bytes())
            official_meta = official_target.with_name(official_target.name + ".meta")
            if official_meta.exists() and guid(official_meta) != asset_guid:
                raise ValueError("Refusing to overwrite changed Unity metadata: " + str(official_meta))
            if not official_meta.exists():
                official_meta.write_bytes(source_meta.read_bytes())
            external[asset_guid] = official_target
            output_records.append({"path": official_target.relative_to(ROOT).as_posix(),
                                   "source": relative.as_posix(), "guid": asset_guid,
                                   "bytes": source.stat().st_size, "sha256": digest(source),
                                   "meta_sha256": digest(official_meta)})

        if source in roots and source.suffix == ".prefab":
            resource_target = RESOURCES / source.name
            resource_target.parent.mkdir(parents=True, exist_ok=True)
            converted = convert_runtime_prefab(source)
            converted_digest = hashlib.sha256(converted).hexdigest()
            allowed_existing = {digest(source), converted_digest}
            if resource_target.exists() and digest(resource_target) not in allowed_existing:
                raise ValueError("Refusing to overwrite changed runtime prefab: " + str(resource_target))
            if not resource_target.exists() or digest(resource_target) != converted_digest:
                resource_target.write_bytes(converted)
            resource_meta = resource_target.with_name(resource_target.name + ".meta")
            runtime_guid = uuid.uuid5(uuid.NAMESPACE_URL,
                "zjcs/dungeons/ui/871_156598/" + source.name).hex
            if resource_meta.exists() and guid(resource_meta) != runtime_guid:
                raise ValueError("Runtime prefab GUID changed: " + str(resource_meta))
            if not resource_meta.exists():
                resource_meta.write_text("fileFormatVersion: 2\n"
                    f"guid: {runtime_guid}\nPrefabImporter:\n"
                    "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",
                    encoding="utf-8")
            resource_records.append({"path": resource_target.relative_to(ROOT).as_posix(),
                                     "source": relative.as_posix(), "guid": runtime_guid,
                                     "conversion": RUNTIME_CONVERSION,
                                     "source_sha256": digest(source),
                                     "sha256": digest(resource_target),
                                     "official_guid": asset_guid})

    if not TRAVEL_SOURCE.is_file() or not TRAVEL_RUNTIME.is_file():
        raise FileNotFoundError("The official TravelNotesPanel source or runtime prefab is missing")
    travel_source_guid = guid(TRAVEL_SOURCE.with_name(TRAVEL_SOURCE.name + ".meta"))
    travel_runtime_guid = guid(TRAVEL_RUNTIME.with_name(TRAVEL_RUNTIME.name + ".meta"))
    if not travel_source_guid or not travel_runtime_guid:
        raise ValueError("The TravelNotesPanel prefab metadata is incomplete")
    travel_before = TRAVEL_RUNTIME.read_bytes()
    converted_travel = convert_runtime_prefab_text(travel_before.decode("utf-8"))
    if converted_travel != travel_before:
        TRAVEL_RUNTIME.write_bytes(converted_travel)
    resource_records.append({
        "path": TRAVEL_RUNTIME.relative_to(ROOT).as_posix(),
        "source": TRAVEL_SOURCE.relative_to(ROOT).as_posix(),
        "guid": travel_runtime_guid,
        "conversion": RUNTIME_CONVERSION,
        "source_sha256": digest(TRAVEL_SOURCE),
        "input_runtime_sha256": hashlib.sha256(travel_before).hexdigest(),
        "sha256": digest(TRAVEL_RUNTIME),
        "official_guid": travel_source_guid,
    })

    receipt = {
        "manifest": "871_156598",
        "input_manifest": INPUTS.relative_to(ROOT).as_posix(),
        "requested_bundles": requested_bundles,
        "inputs": [item for item in source_manifest["inputs"]
                   if any(bundle.removesuffix(".bundle") in item.get("name", "")
                          for bundle in requested_bundles)],
        "ripper_export_root": EXPORT.relative_to(ROOT).as_posix(),
        "outputs": output_records,
        "shared_bindings": shared_records,
        "runtime_prefabs": resource_records,
        "prefab_roots": sorted(path.relative_to(EXPORT).as_posix() for path in roots),
        "counts": {"prefabs": len(roots), "dependency_assets": len(output_records),
                   "shared_assets": len(shared_records)},
    }
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return receipt


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, indent=2))
