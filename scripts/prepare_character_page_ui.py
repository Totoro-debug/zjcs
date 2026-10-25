"""Install the pinned official character page prefab and its dependency closure."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import uuid


ROOT = Path(__file__).resolve().parents[1]
EXPORT = ROOT / "output/dungeons-ui-ripper.zip/ExportedProject/Assets"
INPUTS = ROOT / "output/dungeons-ui-871-inputs/source-manifest.json"
OFFICIAL = ROOT / "unity/Assets/Official/Character/PageUI"
RESOURCES = ROOT / "unity/Assets/Game/Resources/Character"
RESOURCE_PREFAB = RESOURCES / "CharacterPanel.prefab"
RECEIPT = ROOT / "assets/reference/character-ui/source-manifest.json"
SOURCE_PREFAB = "Prefabs/UI/Character/CharacterPanel.prefab"
SOURCE_MANIFEST = "871_156598"
SOURCE_APK_SHA256 = "ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9"
REQUIRED_BUNDLES = {
    "defaultpackage_assets_prefabs_ui_character.bundle": "3cf170a685b29b8aa194eade334f439b",
    "defaultpackage_assets_prefabs_ui_characterpanel.bundle": "fe6de6cb2f7ab226f88e99635576d244",
}
GUID = re.compile(r"guid: ([0-9a-f]{32})")
META_GUID = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)
ASSET_BUNDLE = re.compile(r"^  assetBundleName: (.*)$", re.M)
SCRIPT_REFERENCE = re.compile(r"^  m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})", re.M)
BUILTIN = {"0000000000000000e000000000000000", "0000000000000000f000000000000000",
           "00000000000000000000000000000000"}
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


def asset_guid(meta: Path) -> str | None:
    match = META_GUID.search(meta.read_text(encoding="utf-8", errors="replace"))
    return match.group(1) if match else None


def ensure_folder_meta(path: Path) -> None:
    meta = path.with_name(path.name + ".meta")
    if meta.exists():
        return
    relative = path.relative_to(ROOT / "unity/Assets").as_posix()
    value = hashlib.sha256(("zjcs-character-page-folder:" + relative).encode()).hexdigest()[:32]
    meta.write_text("fileFormatVersion: 2\n" + f"guid: {value}\n"
                    "folderAsset: yes\nDefaultImporter:\n"
                    "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",
                    encoding="utf-8")


def convert_prefab(source: Path) -> bytes:
    value = source.read_text(encoding="utf-8")
    for source_guid, unity_guid in SCRIPT_GUID_MAPPINGS.items():
        value = value.replace("guid: " + source_guid + ", type: 3",
                              "guid: " + unity_guid + ", type: 3")
    return value.encode("utf-8")


def validate_inputs(manifest: dict) -> dict[str, dict]:
    if manifest.get("manifest") != SOURCE_MANIFEST:
        raise ValueError("Character page inputs do not match the active official resource manifest")
    if not any(item.get("apk_sha256") == SOURCE_APK_SHA256 for item in manifest.get("inputs", [])):
        raise ValueError("Character page input set does not match the pinned APK")
    bundles = {item.get("name"): item for item in manifest.get("inputs", [])}
    for name, expected_hash in REQUIRED_BUNDLES.items():
        row = bundles.get(name)
        if row is None or row.get("raw_md5") != expected_hash:
            raise ValueError("Pinned character UI bundle is missing or changed: " + name)
    return bundles


def bundle_key(name: str) -> str:
    return name if name.endswith((".bundle", ".rawfile")) else name + ".bundle"


def build() -> dict:
    source_manifest = json.loads(INPUTS.read_text(encoding="utf-8"))
    bundles = validate_inputs(source_manifest)
    if not EXPORT.is_dir():
        raise FileNotFoundError("AssetRipper character UI export is missing: " + str(EXPORT))

    source = EXPORT / SOURCE_PREFAB
    if not source.is_file() or not Path(str(source) + ".meta").is_file():
        raise FileNotFoundError("Official CharacterPanel prefab was not exported: " + str(source))
    if asset_guid(Path(str(source) + ".meta")) is None:
        raise ValueError("Official CharacterPanel prefab has no AssetRipper GUID")

    by_guid: dict[str, Path] = {}
    for meta in EXPORT.rglob("*.meta"):
        guid = asset_guid(meta)
        asset = meta.with_suffix("")
        if not guid or not asset.is_file():
            continue
        if guid in by_guid and by_guid[guid] != asset:
            raise ValueError("Duplicate AssetRipper GUID " + guid)
        by_guid[guid] = asset

    selected: set[Path] = set()
    pending = [source]
    omitted_scripts: set[str] = set()
    unresolved: set[str] = set()
    referenced_bundles = {"defaultpackage_assets_prefabs_ui_character.bundle"}
    while pending:
        asset = pending.pop()
        if asset in selected:
            continue
        selected.add(asset)
        try:
            content = asset.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        meta_path = Path(str(asset) + ".meta")
        meta_content = meta_path.read_text(encoding="utf-8", errors="replace")
        bundle_match = ASSET_BUNDLE.search(meta_content)
        if bundle_match and bundle_match.group(1):
            referenced_bundles.add(bundle_key(bundle_match.group(1)))
        script_guids = set(SCRIPT_REFERENCE.findall(content))
        for reference in GUID.findall(content):
            if reference in BUILTIN:
                continue
            dependency = by_guid.get(reference)
            if dependency is None:
                if reference in script_guids:
                    unresolved.add("script:" + reference)
                else:
                    unresolved.add(reference)
            elif dependency.suffix.lower() == ".cs":
                omitted_scripts.add(dependency.name)
            else:
                pending.append(dependency)

    if unresolved:
        raise ValueError("Official character prefab has unresolved references: "
                         + ", ".join(sorted(unresolved)[:20]))
    missing_bundles = referenced_bundles.difference(bundles)
    if missing_bundles:
        raise ValueError("Character page dependencies are not covered by verified inputs: "
                         + ", ".join(sorted(missing_bundles)))

    external: dict[str, Path] = {}
    official_root = ROOT / "unity/Assets/Official"
    for meta in official_root.rglob("*.meta"):
        guid = asset_guid(meta)
        asset = meta.with_suffix("")
        if guid and asset.is_file():
            if guid in external and external[guid] != asset:
                if digest(external[guid]) != digest(asset):
                    raise ValueError("Duplicate local official GUID has different payloads: " + guid)
            else:
                external[guid] = asset

    ensure_folder_meta(OFFICIAL)
    ensure_folder_meta(RESOURCES)
    copied = []
    shared = []
    for asset in sorted(selected):
        meta = Path(str(asset) + ".meta")
        guid = asset_guid(meta)
        if not guid:
            raise ValueError("AssetRipper dependency has no GUID: " + str(asset))
        if asset == source:
            target = OFFICIAL / SOURCE_PREFAB
        else:
            target = OFFICIAL / asset.relative_to(EXPORT)
        target.parent.mkdir(parents=True, exist_ok=True)
        for parent in [target.parent, *target.parent.parents]:
            if parent == ROOT / "unity/Assets":
                break
            if parent.is_relative_to(ROOT / "unity/Assets"):
                ensure_folder_meta(parent)
        existing = external.get(guid)
        if existing is not None:
            if digest(existing) != digest(asset):
                raise ValueError("Official asset GUID conflicts with different local payload: " + guid)
            shared.append({"source": asset.relative_to(EXPORT).as_posix(),
                           "guid": guid, "asset": existing.relative_to(ROOT).as_posix(),
                           "sha256": digest(existing)})
            continue
        if target.exists() and digest(target) != digest(asset):
            raise ValueError("Refusing to overwrite changed official character asset: " + str(target))
        if not target.exists():
            target.write_bytes(asset.read_bytes())
        target_meta = Path(str(target) + ".meta")
        if target_meta.exists() and asset_guid(target_meta) != guid:
            raise ValueError("Refusing to overwrite changed character asset metadata: " + str(target_meta))
        if not target_meta.exists():
            target_meta.write_bytes(meta.read_bytes())
        external[guid] = target
        copied.append({"path": target.relative_to(ROOT).as_posix(),
                       "source": asset.relative_to(EXPORT).as_posix(), "guid": guid,
                       "bytes": asset.stat().st_size, "sha256": digest(asset),
                       "meta_sha256": digest(target_meta)})

    converted = convert_prefab(source)
    RESOURCE_PREFAB.parent.mkdir(parents=True, exist_ok=True)
    runtime_hash = hashlib.sha256(converted).hexdigest()
    if RESOURCE_PREFAB.exists() and digest(RESOURCE_PREFAB) not in {digest(source), runtime_hash}:
        raise ValueError("Refusing to overwrite changed runtime CharacterPanel prefab")
    if not RESOURCE_PREFAB.exists() or digest(RESOURCE_PREFAB) != runtime_hash:
        RESOURCE_PREFAB.write_bytes(converted)
    resource_meta = Path(str(RESOURCE_PREFAB) + ".meta")
    runtime_guid = uuid.uuid5(uuid.NAMESPACE_URL,
        "zjcs/character/ui/871_156598/CharacterPanel.prefab").hex
    if resource_meta.exists() and asset_guid(resource_meta) != runtime_guid:
        raise ValueError("Runtime CharacterPanel prefab GUID changed")
    if not resource_meta.exists():
        resource_meta.write_text("fileFormatVersion: 2\n" + f"guid: {runtime_guid}\n"
            "PrefabImporter:\n  externalObjects: {}\n  userData: official-character-panel-871_156598\n"
            "  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")

    receipt = {
        "manifest": SOURCE_MANIFEST,
        "apk_manifest": "854_156383",
        "apk_sha256": SOURCE_APK_SHA256,
        "input_manifest": INPUTS.relative_to(ROOT).as_posix(),
        "ripper_export_root": EXPORT.relative_to(ROOT).as_posix(),
        "root_prefab": SOURCE_PREFAB,
        "root_bundle": REQUIRED_BUNDLES["defaultpackage_assets_prefabs_ui_character.bundle"],
        "dependency_bundles": [
            {"name": name, "md5": bundles[name]["raw_md5"], "bytes": bundles[name]["raw_size"]}
            for name in sorted(referenced_bundles)
        ],
        "omitted_official_scripts": sorted(omitted_scripts),
        "outputs": copied,
        "shared_bindings": shared,
        "runtime_prefab": {
            "path": RESOURCE_PREFAB.relative_to(ROOT).as_posix(),
            "source_sha256": digest(source), "sha256": digest(RESOURCE_PREFAB),
            "meta_sha256": digest(resource_meta), "guid": runtime_guid,
            "conversion": "unity-ugui-script-guid-remap-v1",
        },
    }
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return receipt


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, indent=2))
