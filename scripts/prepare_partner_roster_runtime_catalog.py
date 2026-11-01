"""Bind the already installed official roster prefab, card sprites and texts for runtime."""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def guid(meta: Path) -> str:
    for line in meta.read_text("utf8").splitlines():
        if line.startswith("guid: "):
            return line[6:].strip()
    raise ValueError(f"Missing Unity GUID: {meta}")


def pointer(guid_value: str, file_id: int, kind: int) -> str:
    return f"{{fileID: {file_id}, guid: {guid_value}, type: {kind}}}"


def main() -> None:
    card = ROOT / "unity/Assets/Official/Partners/UI/Card/partner-card-images.json"
    prefab = ROOT / "unity/Assets/Official/Partners/UI/Roster/NpcRosterContent.prefab"
    roster_texts = ROOT / "unity/Assets/Official/Partners/UI/Roster/partner-roster-texts.json"
    script = ROOT / "unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerRosterAssetCatalog.cs"
    image_script = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionImageCatalog.cs"
    image_asset = ROOT / "unity/Assets/Game/Resources/Partners/OfficialPartnerRosterImages.asset"
    image_meta = Path(str(image_asset) + ".meta")
    catalog = ROOT / "unity/Assets/Game/Resources/Partners/OfficialPartnerRosterAssetCatalog.asset"
    catalog_meta = Path(str(catalog) + ".meta")

    card_data = json.loads(card.read_text("utf8"))
    sprites = card_data.get("sprites", [])
    if not sprites:
        raise ValueError("Official partner card sprite manifest is empty")
    image_guid = hashlib.md5(b"zjcs:partner-roster-images").hexdigest()
    image_meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {image_guid}\n"
        "NativeFormatImporter:\n"
        "  externalObjects: {}\n"
        "  mainObjectFileID: 11400000\n", encoding="utf8", newline="\n")
    lines = [
        "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000",
        "MonoBehaviour:", "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1", "  m_EditorHideFlags: 0",
        f"  m_Script: {pointer(guid(Path(str(image_script) + '.meta')), 11500000, 3)}",
        "  m_Name: OfficialPartnerRosterImages", "  m_EditorClassIdentifier: ''", "  Entries:"]
    for sprite in sprites:
        address = sprite["address"]
        sprite_guid = sprite["pointer"]["guid"]
        lines.extend([
            f"  - Path: Assets/Image/{address}",
            f"    AssetGuid: {sprite_guid}",
            f"    Asset: {pointer(sprite_guid, sprite['pointer'].get('fileID', 21300000), 2)}"])
    image_asset.write_text("\n".join(lines) + "\n", encoding="utf8", newline="\n")

    catalog_guid = hashlib.md5(b"zjcs:partner-roster-asset-catalog").hexdigest()
    root_match = re.search(r"^--- !u!1 &(\d+)$", prefab.read_text("utf8"), re.MULTILINE)
    if root_match is None:
        raise ValueError("Official roster prefab has no GameObject root")
    catalog_meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {catalog_guid}\n"
        "NativeFormatImporter:\n"
        "  externalObjects: {}\n"
        "  mainObjectFileID: 11400000\n", encoding="utf8", newline="\n")
    catalog_lines = [
        "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
        "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1", "  m_EditorHideFlags: 0",
        f"  m_Script: {pointer(guid(Path(str(script) + '.meta')), 11500000, 3)}",
        "  m_Name: OfficialPartnerRosterAssetCatalog", "  m_EditorClassIdentifier: ''",
        f"  rosterPrefab: {pointer(guid(Path(str(prefab) + '.meta')), int(root_match.group(1)), 3)}",
        f"  images: {pointer(image_guid, 11400000, 2)}",
        f"  cardTexts: {pointer(guid(Path(str(card) + '.meta')), 4900000, 3)}",
        f"  rosterTexts: {pointer(guid(Path(str(roster_texts) + '.meta')), 4900000, 3)}"]
    catalog.write_text("\n".join(catalog_lines) + "\n", encoding="utf8", newline="\n")
    manifest = ROOT / "assets/reference/partner-roster-runtime-source-manifest.json"
    outputs = {}
    for path in (image_asset, image_meta, catalog, catalog_meta):
        outputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    manifest.write_text(json.dumps({
        "manifest": card_data.get("manifest"),
        "generator_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "inputs": {
            card.relative_to(ROOT).as_posix(): hashlib.sha256(card.read_bytes()).hexdigest(),
            prefab.relative_to(ROOT).as_posix(): hashlib.sha256(prefab.read_bytes()).hexdigest(),
            roster_texts.relative_to(ROOT).as_posix(): hashlib.sha256(roster_texts.read_bytes()).hexdigest(),
        },
        "outputs": outputs,
        "scope": "Explicit Resources bindings for the exact original NpcRosterContent subtree and its installed card sprite payload.",
    }, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print(f"Bound official partner roster prefab and {len(sprites)} card sprites.")


if __name__ == "__main__":
    main()
