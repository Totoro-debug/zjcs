"""Import the pinned official daily dungeon result UI into Windows Resources."""
from __future__ import annotations

import hashlib
from functools import lru_cache
import json
from pathlib import Path
import re
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
UNITY = ROOT / "unity"
EXPORT = ROOT / ".tools/source-packages/current/884_156692/native-conversion/native-export-all/ExportedProject/Assets"
SOURCE_MANIFEST = ROOT / ".tools/source-packages/current/884_156692/native-conversion/source-manifest.json"
OFFICIAL = UNITY / "Assets/Official/Dungeons/884_156692"
RESOURCES = UNITY / "Assets/Game/Resources/Dungeons/UI/884_156692/Native"
GRADE_RESOURCES = RESOURCES / "GradeSprites"
RECEIPT = ROOT / "assets/reference/dungeon-ui/884_156692/daily-dungeon-result-ui-source-manifest.json"
GUID_LINE = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)
SCRIPT = re.compile(r"m_Script: \{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: 3\}")
GUID = re.compile(r"guid: ([0-9a-f]{32})")
BUILTIN = {
    "0000000000000000e000000000000000",
    "0000000000000000f000000000000000",
    "00000000000000000000000000000000",
}

# Source script references from the 884 export. Values are local package script
# GUIDs or migrated local C# type names. Unknown server/client components remain
# unbound; their visuals and transforms are preserved in the converted prefab.
PACKAGE_SCRIPT_GUIDS = {
    ("d3e719b59ab71ba3f6b398058c866280", 1392445389): "4e29b1a8efbd4b44bb3f3716e73f07ff",  # Button
    ("d3e719b59ab71ba3f6b398058c866280", 1741964061): "3245ec927659c4140ac4f8d17403cc18",  # ContentSizeFitter
    ("d3e719b59ab71ba3f6b398058c866280", 1301386320): "dc42784cf147c0c48a680349fa168899",  # GraphicRaycaster
    ("d3e719b59ab71ba3f6b398058c866280", -2095666955): "8a8695521f0d02e499659fee002a26c2",  # GridLayoutGroup
    ("d3e719b59ab71ba3f6b398058c866280", -405508275): "30649d3a9faa99c48a7b1166b86bf2a0",  # HorizontalLayoutGroup
    ("d3e719b59ab71ba3f6b398058c866280", -765806418): "fe87c0e1cc204ed48ad3b37840f39efc",  # Image
    ("d3e719b59ab71ba3f6b398058c866280", -98529514): "1344c3c82d62a2a41a3576d8abb8e3ea",  # RawImage
    ("d3e719b59ab71ba3f6b398058c866280", 1679637790): "306cc8c2b49d7114eaa3623786fc2126",  # LayoutElement
    ("d3e719b59ab71ba3f6b398058c866280", -146154839): "3312d7739989d2b4e91e6319e9a96d76",  # RectMask2D
    ("d3e719b59ab71ba3f6b398058c866280", -113659843): "67db9e8f0e2ae9c40bc1e2b64352a6b4",  # Slider
    ("d3e719b59ab71ba3f6b398058c866280", 1297475563): "59f8146938fff824cb5fd77236b75775",  # VerticalLayoutGroup
    ("67dfb1fdfb2b407222eda8e23ac8b724", 1453722849): "f4688fdb7df04437aeb418b961361dc5",  # TextMeshProUGUI
}
LOCAL_TYPE_REMAPS = {
    ("39197805e5534dfcbda4e4ad194e81d7", -1222992475): "LoopVerticalScrollRect",
    ("7aa338e2f5263348de25eb85a28aad0d", 1857479261): "WorldPopupWindow",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", -381060941): "WorldTabItem",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", 1229101730): "WorldTabGroup",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", -671063624): "WorldTabAnimatorTransition",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", 582128686): "WorldLayoutElementClamper",
    ("222c78078da8821ea09910124a9225a2", 756123093): "WorldUI/HorizontalLayoutGroupJSZX.cs",
    ("7aa338e2f5263348de25eb85a28aad0d", -736795330): "WorldSafeArea",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", -1486259055): "EndPanelAnimEventReceiver",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", -2141185159): "GameEndPanelBase",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", -1878802300): "UIBloomGroup",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", -1285569583): "DailyDungeonResultPanelAdapter",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", 940834681): "DailyDungeonResultPlayerRowAdapter",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", -1525041435): "DailyDungeonResultItemIconAdapter",
    ("06dd3d5416bf43ea368cc39ff96e6d5a", 1943691264): "StoryNodes/StoryNodeRegister.cs",
}

ROOTS = {
    "Assets/Prefabs/UI/DailyDungeon/DailyDungeonTopPanel.prefab":
        "defaultpackage_assets_prefabs_ui_dailydungeon.bundle",
    "Assets/Prefabs/UI/Top/TopRankTeamItem.prefab":
        "defaultpackage_assets_prefabs_ui_top.bundle",
    "Assets/Prefabs/UI/Fight/GameEndPanelDailyDungeon.prefab":
        "defaultpackage_assets_prefabs_ui_fight_gameendpaneldailydungeon.bundle",
    "Assets/Prefabs/UI/DailyDungeon/DailyDungeonEndPanelItem.prefab":
        "defaultpackage_assets_prefabs_ui_dailydungeon.bundle",
    "Assets/Prefabs/UI/DailyDungeon/DailyDungeonEndPaneAwardlItem.prefab":
        "defaultpackage_assets_prefabs_ui_dailydungeon.bundle",
    "Assets/Prefabs/UI/Award/AwardPanelItem.prefab":
        "defaultpackage_assets_prefabs_ui_award.bundle",
    "Assets/Prefabs/UI/Item/ItemAwardIcon.prefab":
        "defaultpackage_assets_prefabs_ui_item.bundle",
    "Assets/Prefabs/UI/Item/ItemIcon.prefab":
        "defaultpackage_assets_prefabs_ui_item.bundle",
    "Assets/Prefabs/UI/ItemDetail/EquipDetailDialog.prefab":
        "defaultpackage_assets_prefabs_ui_itemdetail.bundle",
    "Assets/Prefabs/UI/ItemDetail/ItemDetailDialog.prefab":
        "defaultpackage_assets_prefabs_ui_itemdetail.bundle",
    "Assets/Prefabs/UI/ItemDetail/CommonEquipPieceUseDetailDialog.prefab":
        "defaultpackage_assets_prefabs_ui_itemdetail.bundle",
    "Assets/Prefabs/UI/ItemDetail/CommonItemPieceUseDetailDialog.prefab":
        "defaultpackage_assets_prefabs_ui_itemdetail.bundle",
}
GRADE_SETTINGS = EXPORT / "ScriptableConfig/Modules/DailyDungeon/DailyDungeonSettings.asset"
GRADE_SETTINGS_SOURCE = "Assets/ScriptableConfig/Modules/DailyDungeon/DailyDungeonSettings.asset"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def digest(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def meta_guid(path: Path) -> str | None:
    match = GUID_LINE.search(path.read_text(encoding="utf-8", errors="replace"))
    return match.group(1) if match else None


def write_folder_meta(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)
    meta = path.with_name(path.name + ".meta")
    if meta.exists():
        return
    relative = path.relative_to(UNITY / "Assets").as_posix()
    folder_guid = hashlib.sha256(("zjcs/daily-dungeon-result-ui-folder/" + relative).encode()).hexdigest()[:32]
    meta.write_text("fileFormatVersion: 2\n"
                    f"guid: {folder_guid}\nfolderAsset: yes\nDefaultImporter:\n"
                    "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",
                    encoding="utf-8", newline="\n")


def write_folder_tree(path: Path) -> None:
    assets = UNITY / "Assets"
    folder = path
    while folder != assets:
        if not folder.is_relative_to(assets):
            raise ValueError("Unity folder path is outside Assets: " + str(path))
        write_folder_meta(folder)
        folder = folder.parent


def write_text_asset_meta(path: Path, previous_record: dict | None = None) -> Path:
    relative = path.relative_to(UNITY / "Assets").as_posix()
    guid = hashlib.sha256(("zjcs/daily-dungeon-result-ui-text/" + relative).encode()).hexdigest()[:32]
    meta = path.with_name(path.name + ".meta")
    expected = ("fileFormatVersion: 2\n"
                f"guid: {guid}\nTextScriptImporter:\n"
                "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")
    if meta.exists():
        meta_text = meta.read_text(encoding="utf-8")
        preserves_prior_catalog = (previous_record
            and previous_record.get("path") == path.relative_to(ROOT).as_posix()
            and previous_record.get("sha256") == sha(path)
            and "TextScriptImporter:" in meta_text)
        if (meta_guid(meta) != guid and not preserves_prior_catalog) \
                or "TextScriptImporter:" not in meta_text:
            raise ValueError("Refusing to replace changed daily dungeon grade catalog metadata: " + str(meta))
        return meta
    meta.write_text(expected, encoding="utf-8", newline="\n")
    return meta


@lru_cache(maxsize=None)
def local_type_guid(type_name: str) -> str | None:
    suffix = type_name.replace("\\", "/")
    if not suffix.endswith(".cs"):
        suffix += ".cs"
    matches = []
    for root in (UNITY / "Assets/Game/Runtime/Official", UNITY / "Assets/Game/Runtime/Offline",
                 UNITY / "Assets/Game/Runtime/Presentation"):
        if not root.is_dir():
            continue
        for path in root.rglob(Path(suffix).name):
            if not path.as_posix().endswith("/" + suffix):
                continue
            meta = path.with_name(path.name + ".meta")
            value = meta_guid(meta) if meta.is_file() else None
            if value:
                matches.append((path, value))
    if len(matches) > 1:
        raise ValueError("Migrated UI type is ambiguous: " + type_name + " "
                         + ", ".join(path.as_posix() for path, _ in matches))
    return matches[0][1] if matches else None


def runtime_prefab_bytes(source: Path, prefab_guid_map: dict[str, str], asset_guid_map: dict[str, str]) -> bytes:
    text = source.read_text(encoding="utf-8")

    def remap_script(match: re.Match[str]) -> str:
        file_id = int(match.group(1))
        source_guid = match.group(2)
        package_guid = PACKAGE_SCRIPT_GUIDS.get((source_guid, file_id))
        if package_guid:
            return f"m_Script: {{fileID: 11500000, guid: {package_guid}, type: 3}}"
        local_type = LOCAL_TYPE_REMAPS.get((source_guid, file_id))
        if local_type:
            local_guid = local_type_guid(local_type)
            if local_guid:
                return f"m_Script: {{fileID: 11500000, guid: {local_guid}, type: 3}}"
        return match.group(0)

    text = SCRIPT.sub(remap_script, text)
    references = dict(asset_guid_map)
    references.update(prefab_guid_map)
    for source_guid, target_guid in references.items():
        if source_guid != target_guid:
            text = text.replace("guid: " + source_guid + ",", "guid: " + target_guid + ",")
    return text.encode("utf-8")


def verify_native_roots():
    sys.path.insert(0, str(ROOT / "scripts"))
    from official_apk import Apk

    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Daily dungeon result UI requires the selected 884_156692 source profile")
    if apk.source.apk_version != "881_156673" or apk.source.apk_sha256 != (
            "f8f54a38ec74b6ea09f39109282e23671fd91bf704e7802063cfcdba21c5c431"):
        raise ValueError("The APK input differs from the verified daily dungeon UI source")

    records = []
    bundle_objects: dict[str, dict[str, list[tuple[str, object]]]] = {}
    for path, bundle in ROOTS.items():
        if bundle not in apk.index:
            raise ValueError("The selected official source omits bundle " + bundle)
        if bundle not in bundle_objects:
            bundle_objects[bundle] = {}
            for name, pointer in apk.container(bundle):
                key = name.casefold()
                bundle_objects[bundle].setdefault(key, []).append((name, pointer.deref()))
        matches = bundle_objects[bundle].get(path.casefold(), [])
        if len(matches) != 1:
            raise ValueError("The selected official source does not resolve exactly one prefab: " + path)
        actual_path, obj = matches[0]
        if obj.type.name != "GameObject":
            raise ValueError("Official daily dungeon UI root is not a GameObject: " + actual_path)
        provenance = apk.provenance(obj)
        if provenance.get("manifest") != apk.version:
            raise ValueError("Official daily dungeon UI root came from a different cloud manifest: " + path)
        export = EXPORT / path.removeprefix("Assets/")
        export_meta = export.with_name(export.name + ".meta")
        if not export.is_file() or not export_meta.is_file() or not meta_guid(export_meta):
            raise FileNotFoundError("The verified 884 AssetRipper prefab or metadata is missing: " + str(export))
        records.append({
            "path": path,
            "bundle": bundle,
            "source": provenance,
            "payload_sha256": digest(obj.get_raw_data()),
            "exported_prefab": export.relative_to(ROOT).as_posix(),
            "exported_prefab_sha256": sha(export),
            "asset_ripper_guid": meta_guid(export_meta),
        })

    if not GRADE_SETTINGS.is_file() or not GRADE_SETTINGS.with_name(GRADE_SETTINGS.name + ".meta").is_file():
        raise FileNotFoundError("The verified 884 daily dungeon settings export is missing: " + str(GRADE_SETTINGS))
    source_manifest = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    if source_manifest.get("manifest") != apk.version or source_manifest.get("apk_sha256") != apk.source.apk_sha256:
        raise ValueError("The 884 conversion source manifest differs from the pinned APK profile")
    catalog = source_manifest.get("catalogs", {}).get("DefaultPackage", {})
    current_assets = {row.get("guid"): row for row in catalog.get("current", []) if row.get("guid")}
    settings_row = next((row for row in catalog.get("current", [])
                         if row.get("path", "").casefold() == GRADE_SETTINGS_SOURCE.casefold()), None)
    if settings_row is None:
        raise ValueError("The selected official source catalog omits DailyDungeonSettings")
    settings_bundle = settings_row["bundle"]
    if settings_bundle not in apk.index:
        raise ValueError("The selected official source omits " + settings_bundle)
    if settings_bundle not in bundle_objects:
        bundle_objects[settings_bundle] = {}
        for name, pointer in apk.container(settings_bundle):
            key = name.casefold()
            bundle_objects[settings_bundle].setdefault(key, []).append((name, pointer.deref()))
    settings_objects = bundle_objects[settings_bundle].get(GRADE_SETTINGS_SOURCE.casefold(), [])
    if len(settings_objects) != 1 or settings_objects[0][1].type.name != "MonoBehaviour":
        raise ValueError("The selected official source does not resolve DailyDungeonSettings")
    settings_object = settings_objects[0]
    settings_provenance = apk.provenance(settings_object[1])
    if settings_provenance.get("manifest") != apk.version:
        raise ValueError("DailyDungeonSettings came from a different cloud manifest")

    settings_text = GRADE_SETTINGS.read_text(encoding="utf-8")
    if "  GradeDatas:\n" not in settings_text or "  Datas:\n" not in settings_text:
        raise ValueError("The 884 DailyDungeonSettings export has no serialized grade rows")
    grade_section = settings_text.split("  GradeDatas:\n", 1)[1].split("  Datas:\n", 1)[0]
    grade_blocks = re.findall(r"(?ms)^  - ChestId:.*?(?=^  - ChestId:|\Z)", grade_section)
    grades_by_guid: dict[str, set[int]] = {}
    for block in grade_blocks:
        grade = re.search(r"(?m)^    GradeType: (-?\d+)\s*$", block)
        asset = re.search(r"(?ms)^    EndPanelGradeSpriteProxy:\s*\n      mAssetGuid: ([0-9a-f]{32})", block)
        if not grade or not asset:
            raise ValueError("A configured daily dungeon grade has no original end-panel Sprite GUID")
        grades_by_guid.setdefault(asset.group(1), set()).add(int(grade.group(1)))
    if not grades_by_guid:
        raise ValueError("The selected official daily dungeon settings has no end-panel grade Sprites")

    apk_assets = {row.get("guid"): row for row in catalog.get("apk", []) if row.get("guid")}
    grade_sprite_records = []
    sprite_bundles: dict[str, dict[str, list[tuple[str, object]]]] = {}
    for asset_guid, grade_types in sorted(grades_by_guid.items()):
        row = current_assets.get(asset_guid)
        if row is None:
            raise ValueError("The selected 884 address catalog omits grade Sprite GUID " + asset_guid)
        apk_row = apk_assets.get(asset_guid)
        if apk_row != row:
            raise ValueError("Official grade Sprite changed between the pinned APK and selected 884 catalog: " + asset_guid)
        bundle = row["bundle"]
        if bundle not in apk.index:
            raise ValueError("The selected official source omits grade Sprite bundle " + bundle)
        if bundle not in sprite_bundles:
            sprite_bundles[bundle] = {}
            for name, pointer in apk.container(bundle):
                key = name.casefold()
                sprite_bundles[bundle].setdefault(key, []).append((name, pointer.deref()))
        matches = [match for match in sprite_bundles[bundle].get(row["path"].casefold(), [])
                   if match[1].type.name == "Sprite"]
        if len(matches) != 1:
            raise ValueError("Official grade Sprite is missing from its verified bundle: " + row["path"])
        found = matches[0]
        provenance = apk.provenance(found[1])
        if provenance.get("manifest") != apk.version:
            raise ValueError("Official grade Sprite came from a different cloud manifest: " + row["path"])
        export = EXPORT / row["path"].removeprefix("Assets/")
        export_meta = export.with_name(export.name + ".meta")
        if not export.is_file() or not export_meta.is_file() or not meta_guid(export_meta):
            raise FileNotFoundError("The verified 884 grade Sprite export or metadata is missing: " + str(export))
        grade_sprite_records.append({
            "asset_guid": asset_guid,
            "grade_types": sorted(grade_types),
            "address": row["address"],
            "path": row["path"],
            "bundle": bundle,
            "source": provenance,
            "exported_asset": export.relative_to(ROOT).as_posix(),
            "exported_asset_sha256": sha(export),
            "asset_ripper_guid": meta_guid(export_meta),
        })
    return apk, records, settings_row, settings_provenance, grade_sprite_records


def build() -> dict:
    apk, native_roots, settings_source, settings_provenance, grade_sprites = verify_native_roots()
    if not EXPORT.is_dir():
        raise FileNotFoundError("The verified 884 AssetRipper Unity export is missing: " + str(EXPORT))

    previous_receipt = None
    for prior_path in (RECEIPT, ROOT / "assets/reference/dungeon-ui/daily-dungeon-result-ui-source-manifest.json"):
        if prior_path.is_file():
            candidate = json.loads(prior_path.read_text(encoding="utf-8"))
            if candidate.get("manifest") == apk.version and candidate.get("apk_sha256") == apk.source.apk_sha256:
                previous_receipt = candidate
                break

    previous_outputs = {}
    previous_runtime_prefabs = {}
    previous_runtime_prefab_rows = {}
    if previous_receipt:
        previous_outputs = {row.get("path"): row for row in previous_receipt.get("outputs", [])
            if row.get("path") and row.get("source_sha256") and row.get("guid")}
        previous_runtime_prefabs = {row.get("path"): row.get("sha256")
            for row in previous_receipt.get("runtime_prefabs", []) if row.get("path") and row.get("sha256")}
        previous_runtime_prefab_rows = {row.get("path"): row
            for row in previous_receipt.get("runtime_prefabs", [])
            if row.get("path") and row.get("source_guid") and row.get("source_sha256")}

    prior_grade_settings = (previous_receipt or {}).get("grade_settings", {})
    can_reuse_guid_index = bool(previous_receipt
        and previous_receipt.get("native_roots") == native_roots
        and (previous_receipt.get("official_grade_sprites") or []) == grade_sprites
        and prior_grade_settings.get("path") == GRADE_SETTINGS_SOURCE
        and prior_grade_settings.get("source") == settings_provenance
        and prior_grade_settings.get("address") == settings_source["address"]
        and prior_grade_settings.get("bundle") == settings_source["bundle"]
        and prior_grade_settings.get("exported_asset_sha256") == sha(GRADE_SETTINGS))

    by_guid: dict[str, Path] = {}
    asset_output_paths: dict[str, Path] = {}
    if can_reuse_guid_index:
        cached_rows = []
        cached_rows.extend({"source": row.get("source"), "guid": row.get("source_guid"),
                            "sha256": row.get("source_sha256")}
                           for row in previous_receipt.get("outputs", []))
        cached_rows.extend({"source": row.get("source"), "guid": row.get("guid"),
                            "sha256": row.get("sha256")}
                           for row in previous_receipt.get("shared_assets", []))
        cached_rows.extend(previous_receipt.get("skipped_android_assemblies", []))
        if not cached_rows:
            can_reuse_guid_index = False
        for row in cached_rows:
            relative = row.get("source")
            asset_guid = row.get("guid")
            expected_hash = row.get("sha256")
            source = EXPORT / relative if relative else None
            source_meta = source.with_name(source.name + ".meta") if source else None
            if not (source and source.is_file() and source_meta.is_file()
                    and asset_guid and expected_hash
                    and meta_guid(source_meta) == asset_guid and sha(source) == expected_hash):
                can_reuse_guid_index = False
                break
            if asset_guid in by_guid and by_guid[asset_guid] != source:
                can_reuse_guid_index = False
                break
            by_guid[asset_guid] = source
        if can_reuse_guid_index:
            for row in previous_receipt.get("shared_assets", []):
                shared_asset = ROOT / row.get("shared_asset", "")
                shared_meta = shared_asset.with_name(shared_asset.name + ".meta")
                if not (shared_asset.is_file() and shared_meta.is_file()
                        and meta_guid(shared_meta) == row.get("guid")):
                    can_reuse_guid_index = False
                    break
        if can_reuse_guid_index:
            asset_output_paths = {row["source"]: ROOT / row["path"]
                for row in previous_receipt.get("outputs", []) if row.get("source") and row.get("path")}

    if not can_reuse_guid_index:
        from prepare_dungeon_ui_assets import INPUTS, asset_guid_index
        by_guid = asset_guid_index(sha(INPUTS))

    roots = set()
    for row in native_roots:
        source = ROOT / row["exported_prefab"]
        roots.add(source)
    seeds = roots | {ROOT / row["exported_asset"] for row in grade_sprites}

    def resolve_dependencies(index: dict[str, Path]):
        selected = set(seeds)
        pending = list(selected)
        unresolved: dict[str, set[Path]] = {}
        while pending:
            source = pending.pop()
            try:
                content = source.read_text(encoding="utf-8")
            except UnicodeDecodeError:
                continue
            for reference in GUID.findall(content):
                if reference in BUILTIN:
                    continue
                dependency = index.get(reference)
                if dependency is None:
                    unresolved.setdefault(reference, set()).add(source)
                elif dependency not in selected:
                    selected.add(dependency)
                    pending.append(dependency)
        return selected, unresolved

    selected, unresolved = resolve_dependencies(by_guid)
    if can_reuse_guid_index and (unresolved or selected != set(by_guid.values())):
        from prepare_dungeon_ui_assets import INPUTS, asset_guid_index
        by_guid = asset_guid_index(sha(INPUTS))
        selected, unresolved = resolve_dependencies(by_guid)
    if unresolved:
        guid, users = next(iter(unresolved.items()))
        raise ValueError("Official 884 daily dungeon UI dependency is unresolved: " + guid + " in "
                         + str(next(iter(users))))

    selected_source_guids = {}
    source_prefab_guids = {}
    source_prefab_paths = {}
    for source in selected:
        source_meta = source.with_name(source.name + ".meta")
        source_guid = meta_guid(source_meta)
        if not source_guid:
            raise ValueError("Official 884 UI asset has no stable GUID: " + str(source))
        if source_guid in selected_source_guids:
            raise ValueError("Duplicate source GUID inside the 884 UI dependency closure: " + source_guid)
        selected_source_guids[source_guid] = source
        if source.suffix.casefold() == ".prefab":
            relative = source.relative_to(EXPORT)
            runtime_guid = uuid.uuid5(
                uuid.NAMESPACE_URL, "zjcs/dungeons/ui/884_156692/" + relative.as_posix()).hex
            runtime_path = (RESOURCES / relative).relative_to(ROOT).as_posix()
            previous = previous_runtime_prefab_rows.get(runtime_path)
            existing_target = ROOT / runtime_path
            existing_meta = existing_target.with_name(existing_target.name + ".meta")
            pinned_prefab = False
            # Reuse the existing GUID only for a pinned source; the prefab bytes
            # are checked against the current conversion before any replacement.
            if (previous and previous.get("source_guid") == source_guid
                    and previous.get("source_sha256") == sha(source)
                    and existing_target.is_file() and existing_meta.is_file()):
                prior_imported_guid = meta_guid(existing_meta)
                if (prior_imported_guid and previous.get("guid") == prior_imported_guid
                        and previous.get("sha256") == sha(existing_target)):
                    runtime_guid = prior_imported_guid
                    pinned_prefab = True
            if not pinned_prefab and (existing_target.exists() or existing_meta.exists()):
                # Preserve a prefab already imported at the canonical Resource
                # path by another session; bind this closure under a stable GUID.
                runtime_guid = uuid.uuid5(uuid.NAMESPACE_URL,
                    "zjcs/dungeons/ui/884_156692/prefab-conflict/" + relative.as_posix()
                    + "/" + source_guid).hex
                runtime_path = (RESOURCES / "GuidConflicts" / runtime_guid / relative).relative_to(ROOT).as_posix()
            source_prefab_guids[source_guid] = runtime_guid
            source_prefab_paths[source_guid] = runtime_path

    # Reuse byte-identical original assets already imported by the project. A
    # differing asset with the same source GUID receives a private 884 GUID.
    external: dict[str, tuple[Path, str]] = {}
    official_root = UNITY / "Assets/Official"
    selected_guids = set(selected_source_guids)
    if can_reuse_guid_index:
        for row in previous_receipt.get("shared_assets", []):
            external[row["guid"]] = (ROOT / row["shared_asset"], row["sha256"])
        for row in previous_receipt.get("outputs", []):
            if row.get("guid") != row.get("source_guid"):
                external[row["source_guid"]] = (ROOT / row["path"], "pinned-conflict")
    else:
        for meta in official_root.rglob("*.meta"):
            value = meta_guid(meta)
            if value in selected_guids:
                asset = meta.with_suffix("")
                if asset.is_file():
                    external[value] = (asset, sha(asset))

    # The main UI importer may already have converted this exact 884 source.
    # Reuse its verified output and GUID so both importers share Windows bindings.
    pinned_assets = {}
    ui_receipt_path = ROOT / "assets/reference/dungeon-ui/884_156692/source-manifest.json"
    if ui_receipt_path.is_file():
        ui_receipt = json.loads(ui_receipt_path.read_text(encoding="utf-8"))
        if ui_receipt.get("manifest") != apk.version:
            raise ValueError("Daily dungeon UI receipt has a different source version")
        for row in ui_receipt.get("outputs", []):
            if not all(row.get(key) for key in ("source", "source_guid", "source_sha256", "guid", "sha256", "meta_sha256", "path")):
                continue
            imported = ROOT / row["path"]
            imported_meta = imported.with_name(imported.name + ".meta")
            if (imported.is_file() and imported_meta.is_file()
                    and sha(imported) == row["sha256"]
                    and sha(imported_meta) == row["meta_sha256"]
                    and meta_guid(imported_meta) == row["guid"]):
                pinned_assets[row["source_guid"]] = row

    asset_guid_map: dict[str, str] = {}
    for source in selected:
        source_guid = meta_guid(source.with_name(source.name + ".meta"))
        if not source_guid:
            raise ValueError("Official 884 UI asset has no stable GUID: " + str(source))
        pinned = pinned_assets.get(source_guid)
        if pinned and pinned["source_sha256"] == sha(source):
            asset_guid_map[source_guid] = pinned["guid"]
            continue
        existing = external.get(source_guid)
        if existing and existing[1] == sha(source):
            asset_guid_map[source_guid] = source_guid
            continue
        relative = source.relative_to(EXPORT)
        asset_guid_map[source_guid] = uuid.uuid5(uuid.NAMESPACE_URL,
            "zjcs/dungeons/ui/884_156692/asset/" + relative.as_posix()).hex \
            if existing and existing[1] != sha(source) else source_guid

    copied_outputs = []
    shared_assets = []
    skipped_dlls = []
    for source in sorted(selected):
        relative = source.relative_to(EXPORT)
        source_meta = source.with_name(source.name + ".meta")
        source_guid = meta_guid(source_meta)
        if source.suffix.casefold() in (".dll", ".pdb"):
            skipped_dlls.append({"source": relative.as_posix(), "guid": source_guid, "sha256": sha(source)})
            continue
        pinned = pinned_assets.get(source_guid)
        if pinned and pinned["source_sha256"] == sha(source):
            copied_outputs.append(dict(pinned, preserved_existing=True))
            continue
        existing = external.get(source_guid)
        if existing and existing[1] == sha(source):
            shared_assets.append({"source": relative.as_posix(), "guid": source_guid,
                                  "shared_asset": existing[0].relative_to(ROOT).as_posix(),
                                  "sha256": sha(source),
                                  "shared_asset_sha256": sha(existing[0])})
            continue

        assigned_guid = asset_guid_map[source_guid]
        target = asset_output_paths.get(relative.as_posix())
        if target is None:
            target = OFFICIAL / ("GuidConflicts" if existing else "") / relative
            target_meta = target.with_name(target.name + ".meta")
            if (existing and target_meta.is_file()
                    and meta_guid(target_meta) != assigned_guid):
                # Another selected source package can already occupy the same
                # relative conflict path with its own isolated GUID. Keep both
                # assets by nesting this source under its deterministic GUID.
                target = OFFICIAL / "GuidConflicts" / assigned_guid / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        folder = OFFICIAL
        for part in target.relative_to(OFFICIAL).parent.parts:
            folder = folder / part
            write_folder_meta(folder)
        payload = source.read_bytes()
        try:
            text = payload.decode("utf-8")
        except UnicodeDecodeError:
            pass
        else:
            for source_reference, target_reference in asset_guid_map.items():
                if source_reference != target_reference:
                    text = text.replace("guid: " + source_reference + ",",
                                        "guid: " + target_reference + ",")
            payload = text.encode("utf-8")
        target_meta = target.with_name(target.name + ".meta")
        preserved_existing = False
        if target.exists() and digest(target.read_bytes()) != digest(payload):
            target_path = target.relative_to(ROOT).as_posix()
            previous = previous_outputs.get(target_path)
            pinned = (previous
                and previous.get("source_sha256") == sha(source)
                and previous.get("guid") == assigned_guid
                and target_meta.is_file()
                and previous.get("meta_sha256") == sha(target_meta)
                and meta_guid(target_meta) == assigned_guid)
            if pinned:
                preserved_existing = True
            elif existing and not (len(target.relative_to(OFFICIAL).parts) > 1
                                    and target.relative_to(OFFICIAL).parts[0] == "GuidConflicts"
                                    and target.relative_to(OFFICIAL).parts[1] == assigned_guid):
                # A previously imported package owns the default conflict path.
                # Preserve it and place this source under its deterministic GUID.
                target = OFFICIAL / "GuidConflicts" / assigned_guid / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                folder = OFFICIAL
                for part in target.relative_to(OFFICIAL).parent.parts:
                    folder = folder / part
                    write_folder_meta(folder)
                target_meta = target.with_name(target.name + ".meta")
            else:
                raise ValueError("Refusing to overwrite changed official daily dungeon UI asset: " + str(target))
        if not target.exists():
            target.write_bytes(payload)
        expected_meta = source_meta.read_text(encoding="utf-8")
        if assigned_guid != source_guid:
            expected_meta, count = GUID_LINE.subn("guid: " + assigned_guid, expected_meta, count=1)
            if count != 1:
                raise ValueError("Could not assign isolated 884 asset GUID: " + str(source_meta))
        if target_meta.exists() and meta_guid(target_meta) != assigned_guid:
            raise ValueError("Refusing to overwrite changed official Unity metadata: " + str(target_meta))
        if not target_meta.exists():
            target_meta.write_text(expected_meta, encoding="utf-8", newline="\n")
        copied_outputs.append({"path": target.relative_to(ROOT).as_posix(),
                               "source": relative.as_posix(), "source_guid": source_guid,
                               "guid": assigned_guid, "bytes": source.stat().st_size,
                               "source_sha256": sha(source), "sha256": sha(target),
                               "meta_sha256": sha(target_meta),
                               "preserved_existing": preserved_existing})

    prefab_guid_map = dict(source_prefab_guids)
    resource_prefabs = []
    for source in sorted(path for path in selected if path.suffix.casefold() == ".prefab"):
        relative = source.relative_to(EXPORT)
        source_meta = source.with_name(source.name + ".meta")
        source_guid = meta_guid(source_meta)
        runtime_guid = source_prefab_guids[source_guid]
        target = ROOT / source_prefab_paths[source_guid]
        target.parent.mkdir(parents=True, exist_ok=True)
        folder = RESOURCES
        for part in target.relative_to(RESOURCES).parent.parts:
            folder = folder / part
            write_folder_meta(folder)
        payload = runtime_prefab_bytes(source, prefab_guid_map, asset_guid_map)
        if target.exists() and target.read_bytes() != payload:
            target_path = target.relative_to(ROOT).as_posix()
            if not previous_runtime_prefabs.get(target_path) or sha(target) != previous_runtime_prefabs[target_path]:
                raise ValueError("Refusing to overwrite changed runtime daily dungeon prefab: " + str(target))
            target.write_bytes(payload)
        elif not target.exists():
            target.write_bytes(payload)
        target_meta = target.with_name(target.name + ".meta")
        if target_meta.exists() and meta_guid(target_meta) != runtime_guid:
            raise ValueError("Runtime 884 prefab GUID changed: " + str(target_meta))
        if not target_meta.exists():
            target_meta.write_text("fileFormatVersion: 2\n"
                                   f"guid: {runtime_guid}\nPrefabImporter:\n"
                                   "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",
                                   encoding="utf-8", newline="\n")
        resource_prefabs.append({
            "path": target.relative_to(ROOT).as_posix(),
            "resource": "Dungeons/UI/884_156692/Native/"
                + target.relative_to(RESOURCES).with_suffix("").as_posix(),
            "source": relative.as_posix(), "source_guid": source_guid, "guid": runtime_guid,
            "source_sha256": sha(source), "sha256": sha(target), "meta_sha256": sha(target_meta),
            "conversion": "884-prefab-guid-remap-and-local-ugui-script-bindings-v5-loopscroll-tabs-layout",
        })

    grade_resource_records = []
    grade_catalog_rows = []
    for row in grade_sprites:
        source = ROOT / row["exported_asset"]
        source_meta = source.with_name(source.name + ".meta")
        sprite_name = source.stem
        target = GRADE_RESOURCES / (sprite_name + source.suffix)
        target.parent.mkdir(parents=True, exist_ok=True)
        write_folder_meta(GRADE_RESOURCES)
        if target.exists() and sha(target) != sha(source):
            raise ValueError("Refusing to overwrite changed daily dungeon grade Sprite: " + str(target))
        if not target.exists():
            target.write_bytes(source.read_bytes())
        runtime_guid = uuid.uuid5(uuid.NAMESPACE_URL,
            "zjcs/dungeons/ui/884_156692/grade-sprite/" + row["asset_guid"]).hex
        target_meta = target.with_name(target.name + ".meta")
        expected_meta, count = GUID_LINE.subn("guid: " + runtime_guid,
            source_meta.read_text(encoding="utf-8"), count=1)
        if count != 1:
            raise ValueError("Could not assign isolated daily dungeon grade Sprite GUID: " + str(source_meta))
        if target_meta.exists() and meta_guid(target_meta) != runtime_guid:
            raise ValueError("Runtime daily dungeon grade Sprite GUID changed: " + str(target_meta))
        if not target_meta.exists():
            target_meta.write_text(expected_meta, encoding="utf-8", newline="\n")
        resource_path = "Dungeons/UI/884_156692/Native/GradeSprites/" + sprite_name
        for grade_type in row["grade_types"]:
            grade_catalog_rows.append({"gradeType": grade_type, "assetGuid": row["asset_guid"],
                                       "spriteName": sprite_name, "resourcePath": resource_path})
        grade_resource_records.append({
            "path": target.relative_to(ROOT).as_posix(),
            "source": row["path"],
            "asset_guid": row["asset_guid"],
            "grade_types": row["grade_types"],
            "resource_path": resource_path,
            "guid": runtime_guid,
            "sha256": sha(target),
            "meta_sha256": sha(target_meta),
        })

    grade_catalog_path = RESOURCES / "grade-sprites.json"
    grade_catalog = {"manifest": apk.version, "gradeSprites": sorted(grade_catalog_rows,
                      key=lambda value: value["gradeType"])}
    grade_catalog_payload = json.dumps(grade_catalog, ensure_ascii=False, indent=2) + "\n"
    if grade_catalog_path.exists() and grade_catalog_path.read_text(encoding="utf-8") != grade_catalog_payload:
        raise ValueError("Refusing to overwrite changed official daily dungeon grade Sprite catalog")
    if not grade_catalog_path.exists():
        grade_catalog_path.write_text(grade_catalog_payload, encoding="utf-8", newline="\n")

    for path in (OFFICIAL, RESOURCES):
        write_folder_tree(path)
    grade_catalog_meta = write_text_asset_meta(
        grade_catalog_path, (previous_receipt or {}).get("runtime_grade_catalog"))
    output_files = copied_outputs
    receipt = {
        "manifest": apk.version,
        "apk_manifest": apk.source.apk_version,
        "apk_sha256": apk.source.apk_sha256,
        "generator": {"path": Path(__file__).resolve().relative_to(ROOT).as_posix(),
                      "sha256": sha(Path(__file__).resolve())},
        "operation": "Resolve original UI roots APK-first from the pinned APK and its verified selected CDN source; copy each AssetRipper asset and importer metadata without overwriting changed files. An existing converted file is reused only when the pinned prior receipt confirms the source hash, assigned GUID, and unchanged importer metadata; the actual on-disk asset hash is recorded. Preserve an existing runtime prefab GUID when its source and prior output hashes match, and remap nested references to that GUID. Clone dependent prefabs into a Windows Resources tree and bind migrated UGUI/TMP components. Android DLL payloads are recorded but not copied into Unity/Player.",
        "native_roots": native_roots,
        "grade_settings": {"path": GRADE_SETTINGS_SOURCE, "source": settings_provenance,
                           "address": settings_source["address"], "bundle": settings_source["bundle"],
                           "exported_asset": GRADE_SETTINGS.relative_to(ROOT).as_posix(),
                           "exported_asset_sha256": sha(GRADE_SETTINGS)},
        "official_grade_sprites": grade_sprites,
        "ripper_export_root": EXPORT.relative_to(ROOT).as_posix(),
        "outputs": output_files,
        "shared_assets": shared_assets,
        "skipped_android_assemblies": skipped_dlls,
        "runtime_prefabs": resource_prefabs,
        "runtime_grade_sprites": grade_resource_records,
        "runtime_grade_catalog": {"path": grade_catalog_path.relative_to(ROOT).as_posix(),
                                   "guid": meta_guid(grade_catalog_meta),
                                   "sha256": sha(grade_catalog_path),
                                   "meta_sha256": sha(grade_catalog_meta), "records": grade_catalog_rows},
        "counts": {"roots": len(roots), "dependency_assets": len(copied_outputs),
                   "shared_assets": len(shared_assets), "runtime_prefabs": len(resource_prefabs),
                   "grade_sprites": len(grade_resource_records),
                   "skipped_android_assemblies": len(skipped_dlls)},
    }
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n",
                       encoding="utf-8", newline="\n")
    print("Imported official daily dungeon result UI:", receipt["counts"])
    return receipt


if __name__ == "__main__":
    build()
