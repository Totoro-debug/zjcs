"""Restore and record the selected 884 Map14 roadblock UI prefabs."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import msgpack
from pathlib import Path

from official_apk import Apk, ROOT

VERSION = "884_156692"
RESOURCE_VERSION = "202610291150884"
BUNDLE = "defaultpackage_assets_prefabs_ui_roadblock.bundle"
BUNDLE_MD5 = "3ef54ddace54f23957a86297bf2bb012"
BUNDLE_SHA256 = "76c0577920b734bfada1a0993d72885dd77886209e697cae8440e070c09b572c"
EXPORT_ROOTS = {
    "RoadblockTeamPropStageDialog": (3188668159187020033,
        "253d452a1c9b4163bcaff29e48635c649d7cbcdc0d6cabf06d8faacc77c14bf5",
        "36545b8acf1283cf68a17b37f024000e45e97f0f3b656994366e1eb3d8b49508",
        "f0886ec1df7e1424f9637748995fa0ae"),
    "RoadblockTeamPropStageDialogItem": (-8879869485790403775,
        "9615951d1a6a772f510ca0851e2204fa581068941292e82a554b9c24652ed633",
        "37cd62a8f692fae60614084d53ac7d242d1756669872226f8c35cf3d48e049fa",
        "df68aebfe1a5fb94d8c68aa8904bcf6a"),
}
ROADBLOCKS = {
    701: (8625, 6, (205, 96), 415, 102, 6215),
    2701: (8626, 26, (129, 108), 411, 110, 7280),
    3801: (8634, 37, (106, 63), 419, 112, 7525),
    6401: (8637, 63, (81, 177), 417, 121, 8680),
    9101: (8633, 90, (127, 242), 418, 130, 10055),
}


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def file_record(path: Path, relative_to: Path = ROOT) -> dict:
    raw = path.read_bytes()
    return dict(path=path.relative_to(relative_to).as_posix(), sha256=sha(raw), bytes=len(raw))


def verify_map14_source_compatibility() -> dict:
    old_root = ROOT / "unity/Assets/Official/World/Map/14/SharedSerialization/Resources"
    current_root = ROOT / "unity/Assets/Official/World/Shared/SharedWorldDataCurrent/Resources"
    old_catalog_path = old_root / "Map14/SharedSerialization/catalog.json"
    current_catalog_path = current_root / "MainlineSharedWorld/14/catalog.json"
    old_catalog = json.loads(old_catalog_path.read_bytes())
    current_catalog = json.loads(current_catalog_path.read_bytes())
    old_entries = {entry["OriginalPath"]: entry for entry in old_catalog["Entries"]}
    current_entries = {entry["OriginalPath"]: entry for entry in current_catalog["Entries"]}
    shared_paths = sorted(set(old_entries) & set(current_entries))
    if len(shared_paths) != 34:
        raise ValueError("Selected 884 Map14 shared tables differ from the recorded 871 overlap")
    for original_path in shared_paths:
        old_entry, current_entry = old_entries[original_path], current_entries[original_path]
        old_raw = (old_root / (old_entry["Resource"] + ".bytes")).read_bytes()
        current_raw = (current_root / (current_entry["Resource"] + ".bytes")).read_bytes()
        if (sha(old_raw) != old_entry["Sha256"] or sha(current_raw) != current_entry["Sha256"]
                or old_raw != current_raw):
            raise ValueError("Selected 884 Map14 table differs from the recorded 871 input: " + original_path)

    old_map_info = old_entries.get("Config/MapInfoWrap/mapinfowrap_14.g.bytes")
    current_map_info_path = current_root / "MainlineSharedWorld/Payload/MapInfoWrap/mapinfowrap_14.g.bytes"
    current_map_info = current_map_info_path.read_bytes()
    current_manifest_path = ROOT / "assets/reference/official-map/mainline-shared-world-data-source-manifest.json"
    current_manifest = json.loads(current_manifest_path.read_bytes())
    current_map_info_record = next((entry for entry in current_manifest.get("outputs", [])
        if entry.get("path") == current_map_info_path.relative_to(ROOT).as_posix()), None)
    if (current_manifest.get("manifest") != VERSION or old_map_info is None
            or sha(current_map_info) != old_map_info["Sha256"]
            or not current_map_info_record or current_map_info_record.get("sha256") != sha(current_map_info)):
        raise ValueError("Selected 884 Map14 MapInfoWrap differs from the recorded 871 placement input")
    old_map_info_raw = (old_root / (old_map_info["Resource"] + ".bytes")).read_bytes()
    if old_map_info_raw != current_map_info:
        raise ValueError("Selected 884 Map14 MapInfoWrap bytes differ from the 871 map source")

    definition_path = ROOT / "unity/Assets/Official/World/Map/14/map14.json"
    components_path = ROOT / "unity/Assets/Official/World/Map/14/Source/entities.json"
    catalog_receipt_path = ROOT / "assets/reference/official-map/map14-entity-catalog-source-manifest.json"
    catalog_receipt = json.loads(catalog_receipt_path.read_bytes())
    if catalog_receipt.get("manifest") != "871_156598" or catalog_receipt.get("mapClassId") != 14:
        raise ValueError("The legacy Map14 entity receipt belongs to another source")
    receipt_inputs = {entry["path"]: entry["sha256"] for entry in catalog_receipt.get("inputs", [])}
    for path in (definition_path, components_path):
        if receipt_inputs.get(path.relative_to(ROOT).as_posix()) != sha(path.read_bytes()):
            raise ValueError("The legacy Map14 source input differs from its receipt: " + str(path))
    definition = json.loads(definition_path.read_bytes())
    components = json.loads(components_path.read_bytes())
    tables_path = ROOT / ".tools/source-packages/current" / VERSION / "official-data/tables.json"
    tables = json.loads(tables_path.read_bytes())
    level_number = next((row for row in tables.get("level_number", []) if row.get("Id") == 330), None)
    if level_number is None:
        raise ValueError("Selected 884 level_number row 330 is absent")
    rules_path = ROOT / "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMap14RoadblockRules.cs"
    rules_source = rules_path.read_text(encoding="utf-8")
    source_rules = [tuple(map(int, values)) for values in re.findall(
        r"new OfficialMainlineRoadblockRule\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)",
        rules_source)]
    expected_rules = [(placement_id, values[0], values[1], values[3], values[5])
        for placement_id, values in ROADBLOCKS.items()]
    if source_rules != expected_rules:
        raise ValueError("Map14 runtime roadblock rules differ from the verified source entries")
    placements = {entry["Id"]: entry for entry in definition.get("Entities", [])}
    door_components = {}
    for original_path in ("Config/EC/WorldDoor_part1.g.bytes", "Config/EC/WorldDoor_part2.g.bytes"):
        entry = old_entries[original_path]
        payload = (old_root / (entry["Resource"] + ".bytes")).read_bytes()
        for entity in msgpack.unpackb(payload, raw=False, strict_map_key=False)[0]:
            if entity[1] in {values[0] for values in ROADBLOCKS.values()}:
                decoded = {}
                for name, raw in entity[2]:
                    if name not in ("Common.GridTransformComponent", "Common.StageResultOverrideBodyGroundTypeComponent"):
                        continue
                    unpacker = msgpack.Unpacker(raw=False, strict_map_key=False)
                    unpacker.feed(raw)
                    decoded[name] = next(unpacker)
                door_components[entity[1]] = decoded
    roadblock_records = []
    for placement_id, (class_id, region, grid, property_type, number_index, threshold) in ROADBLOCKS.items():
        placement = placements.get(placement_id)
        component = components.get(str(placement_id))
        if placement is None or component is None:
            raise ValueError("Recorded Map14 roadblock source is missing: " + str(placement_id))
        spawn = component["LoEntityComponent"]["SpanwInfo"]
        position = component["LoGridTransformComponent"]["Pos"]
        if (placement.get("EntityId") != class_id or placement.get("Region") != region
                or placement.get("Grid") != list(grid) or spawn.get("ClassId") != class_id
                or [position.get("X"), position.get("Y")] != list(grid)):
            raise ValueError("Recorded Map14 roadblock source identity differs: " + str(placement_id))
        numbers = level_number.get("Nums", [])
        if number_index >= len(numbers) or numbers[number_index] != threshold:
            raise ValueError("Map14 roadblock threshold differs from 884 level_number row 330: "
                + str(placement_id))
        decoded = door_components[class_id]
        body = decoded["Common.GridTransformComponent"][0]
        offsets = [[0, 0], [1, 0], [-1, 0]] if class_id in (8625, 8626) else [[0, 0], [0, 1], [0, -1]]
        expected_body = [[offset, 0 if index == 0 else 2, True, False]
                         for index, offset in enumerate(offsets)]
        stage_ground = decoded["Common.StageResultOverrideBodyGroundTypeComponent"]
        blocked_body = [[offset, 2, False, False] for offset in offsets]
        expected_ground = [[[blocked_body]], [[0, False], [0, True]]]
        if body != expected_body or stage_ground != expected_ground:
            raise ValueError("Original roadblock body/revert operations differ: " + str(class_id))
        roadblock_records.append(dict(placementId=placement_id, entityClassId=class_id,
            region=region, grid=list(grid), propertyType=property_type,
            propertyStandard=threshold, levelNumberId=330, levelNumberIndex=number_index,
            closedBodyGroundTypes=[2, 2, 2], completedBody=expected_body,
            stageGroundOperations=stage_ground))

    placement_expected_path = ROOT / "unity/Assets/Official/World/Shared/SharedMapConfiguration/Resources/MainlineSharedWorld/MapConfiguration/placement-expected.json"
    expected = json.loads(placement_expected_path.read_bytes())
    map_expected = next((entry for entry in expected.get("Maps", []) if entry.get("MapClassId") == 14), None)
    if map_expected is None or map_expected.get("EntityCount") != 2961:
        raise ValueError("Selected 884 Map14 placement digest is absent")

    return dict(legacyManifest="871_156598", currentManifest=VERSION,
        sharedSerializedTableCount=len(shared_paths),
        sharedSerializedTableSha256={entry: old_entries[entry]["Sha256"] for entry in shared_paths},
        mapInfoWrap=dict(legacySha256=old_map_info["Sha256"], currentSha256=sha(current_map_info),
            identicalBytes=True, currentSourceManifest=current_manifest_path.relative_to(ROOT).as_posix()),
        currentPlacementDigest=dict(mapClassId=14, entityCount=map_expected["EntityCount"],
            entitySha256=map_expected["EntitySha256"],
            source=placement_expected_path.relative_to(ROOT).as_posix()),
        roadblocks=roadblock_records)


def restore(verify_only: bool = False) -> dict:
    source_profile = json.loads((ROOT / "official-source.json").read_bytes())
    if source_profile.get("active") != VERSION:
        raise ValueError("The active official source is not the pinned 884 Map14 roadblock source")
    profile = source_profile["profiles"][VERSION]
    if profile.get("resource_version") != RESOURCE_VERSION:
        raise ValueError("The selected cloud resource version changed")

    export_manifest_path = ROOT / ".tools/source-packages/current" / VERSION \
        / "native-conversion/native-export-all-source-manifest.json"
    export = json.loads(export_manifest_path.read_bytes())
    if export.get("manifest") != VERSION or not export.get("completed") \
            or export.get("export_directory") != "native-export-all":
        raise ValueError("The selected AssetRipper export is incomplete or belongs to another version")
    export_outputs = {value["path"]: value for value in export.get("outputs", [])}
    exported_assets = export_manifest_path.parent / export["export_directory"] / "ExportedProject/Assets"
    destination = ROOT / "unity/Assets/Game/Resources/Map14/UI"

    prefab_outputs = []
    object_sources = []
    with Apk.current() as apk:
        if apk.version != VERSION:
            raise ValueError("APK reader selected a different official source")
        for name, (path_id, expected_sha, expected_meta_sha, guid) in EXPORT_ROOTS.items():
            asset_path = Path("Prefabs/UI/Roadblock") / (name + ".prefab")
            source = exported_assets / asset_path
            source_meta = Path(str(source) + ".meta")
            source_record = export_outputs.get("ExportedProject/Assets/" + asset_path.as_posix())
            meta_record = export_outputs.get("ExportedProject/Assets/" + asset_path.as_posix() + ".meta")
            if not source.is_file() or not source_meta.is_file() or source_record is None or meta_record is None:
                raise FileNotFoundError("Official roadblock prefab export is incomplete: " + name)
            prefab_raw = source.read_bytes()
            meta_raw = source_meta.read_bytes()
            if len(prefab_raw) != source_record.get("bytes") or sha(prefab_raw) != source_record.get("sha256") \
                    or sha(prefab_raw) != expected_sha:
                raise ValueError("Roadblock prefab differs from its completed native export receipt: " + name)
            if len(meta_raw) != meta_record.get("bytes") or sha(meta_raw) != meta_record.get("sha256") \
                    or sha(meta_raw) != expected_meta_sha or ("guid: " + guid).encode() not in meta_raw:
                raise ValueError("Roadblock prefab metadata differs from its native export receipt: " + name)

            apk_object = apk.asset(BUNDLE, "/" + name + ".prefab")
            provenance = apk.provenance(apk_object)
            if apk_object.path_id != path_id or provenance.get("manifest") != VERSION \
                    or provenance.get("origin") != "official_cdn" \
                    or provenance.get("bundle_hash") != BUNDLE_MD5 \
                    or provenance.get("bundle_sha256") != BUNDLE_SHA256:
                raise ValueError("Roadblock prefab object does not match the selected CDN input: " + name)
            object_sources.append(dict(name=name, path_id=str(path_id),
                object_sha256=sha(apk_object.get_raw_data()), provenance=provenance))

            target = destination / (name + ".prefab")
            target_meta = Path(str(target) + ".meta")
            # Raw Android exports prove the input identity. The Windows prefab
            # is restored by ImportMap14Roadblocks with explicit native bindings.
            for output_file in (target, target_meta):
                if not output_file.is_file():
                    raise ValueError("The Windows roadblock binding is missing: " + name)
                prefab_outputs.append(file_record(output_file))

    # Account for the complete Windows UI/native binding, including metadata
    # that carries its explicit GUID references; keep the source bundles separate.
    windows_paths = set()
    for name in ("8625_14_DialogIcon", "8626_14_DialogIcon", "8633_14_DialogIcon",
                 "8634_14_DialogIcon", "8637_14_DialogIcon", "RuleDialog", "TipInfo1"):
        windows_paths.update((destination / (name + ".prefab"), destination / (name + ".prefab.meta")))
    for name in ("roadblock-texts", "roadblock-tip-graph"):
        windows_paths.update((destination / (name + ".json"), destination / (name + ".json.meta")))
    catalog = ROOT / "unity/Assets/Game/Resources/Map14/Native/roadblock-display-catalog.asset"
    windows_paths.update((catalog, Path(str(catalog) + ".meta")))
    for folder in (destination / "RoadblockAnimations", ROOT / "unity/Assets/Official/World/Map/14/UI/Roadblock"):
        windows_paths.add(Path(str(folder) + ".meta"))
        windows_paths.update(path for path in folder.rglob("*") if path.is_file())
    for path in sorted(windows_paths):
        if not path.is_file():
            raise FileNotFoundError("A complete Windows roadblock binding is required: " + str(path))
        prefab_outputs.append(file_record(path))

    input_paths = [
        ROOT / "official-source.json",
        export_manifest_path,
        ROOT / "assets/reference/official-map/map14-entity-catalog-source-manifest.json",
        ROOT / "assets/reference/official-map/map14-source-manifest.json",
        ROOT / "unity/Assets/Official/World/Map/14/map14.json",
        ROOT / "unity/Assets/Official/World/Map/14/Source/entities.json",
        ROOT / "unity/Assets/Official/World/Map/14/SharedSerialization/Resources/Map14/SharedSerialization/catalog.json",
        ROOT / "unity/Assets/Official/World/Shared/SharedWorldDataCurrent/Resources/MainlineSharedWorld/14/catalog.json",
        ROOT / "unity/Assets/Official/World/Shared/SharedWorldDataCurrent/Resources/MainlineSharedWorld/Payload/MapInfoWrap/mapinfowrap_14.g.bytes",
        ROOT / "unity/Assets/Official/World/Shared/SharedMapConfiguration/Resources/MainlineSharedWorld/MapConfiguration/placement-expected.json",
        ROOT / "assets/reference/official-map/mainline-shared-world-data-source-manifest.json",
        ROOT / ".tools/source-packages/current" / VERSION / "official-data/tables.json",
        ROOT / ".tools/source-packages/current" / VERSION / "official-code/client-source/RoadblockPropStageTouchUI.cs",
        ROOT / ".tools/source-packages/current" / VERSION / "official-code/client-source/RoadblockTeamPropStageTouchUI.cs",
        ROOT / ".tools/source-packages/current" / VERSION / "official-code/client-source/RoadblockTeamPropStageTouchUIItem.cs",
        ROOT / ".tools/source-packages/current" / VERSION / "official-code/client-source/ConditionItem.cs",
        ROOT / ".tools/source-packages/current" / VERSION / "official-code/client-source/EcStageResultOverrideBodyGroundTypeView.cs",
        ROOT / "unity/Assets/Official/Combat/rules-source-manifest.json",
        ROOT / "assets/reference/npc-assistance/team-ui-source-manifest.json",
        ROOT / "assets/reference/battle-hud/assist-selection-source-manifest.json",
        ROOT / "assets/reference/npc-assistance/roadblock-ui-source-manifest.json",
        ROOT / "assets/reference/npc-assistance/roadblock-native-source-manifest.json",
        ROOT / "assets/reference/npc-assistance/roadblock-supplements-source-manifest.json",
    ]
    for path in input_paths:
        if not path.is_file():
            raise FileNotFoundError("Roadblock input is missing: " + str(path))

    integration_paths = [
        "scripts/prepare_map14_roadblock_resources.py",
        "scripts/prepare_map14_roadblock_ui.py",
        "scripts/prepare_map14_roadblock_native.py",
        "scripts/prepare_map14_roadblock_native_inputs.py",
        "scripts/prepare_map14_roadblock_supplements.py",
        "unity/Assets/Game/Editor/OfficialMap11UiTools.Roadblocks.cs",
        "unity/Assets/Game/Runtime/Official/WorldUI/WorldConditionItem.cs",
        "unity/Assets/Game/Runtime/Official/EntityWorldStages/EcStageResultOverrideBodyGroundTypeView.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMap14RoadblockRules.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMap14RoadblockDialogView.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Roadblocks.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.RoadblockDisplays.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialRoadblockDisplayCatalog.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/RoadblockTipGraphRuntime.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Interactions.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.SharedWorld.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Persistence.cs",
        "unity/Assets/Game/Runtime/Presentation/World/Mainline/MainlineMapEntityPresentation.cs",
        "unity/Assets/Game/Runtime/Offline/World/OfflineNpcAssistanceProgress.cs",
        "unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleAssistSelectionView.cs",
        "unity/Assets/Game/Runtime/Official/TeamUI/PropInfo.cs",
        "unity/Assets/Game/Runtime/Official/TeamUI/TeamInvitePlayerInfoView.cs",
        "unity/Assets/Game/Runtime/Official/TeamUI/TeamInviteRobotComparer.cs",
        "unity/Assets/Game/Runtime/Official/TeamUI/TeamInviteComparer.cs",
    ]
    inputs = [file_record(path) for path in input_paths]
    integration = []
    for path in integration_paths:
        raw = (ROOT / path).read_bytes().replace(b"\r\n", b"\n")
        integration.append(dict(path=path, sha256=sha(raw), bytes=len(raw), normalization="LF"))
    source_compatibility = verify_map14_source_compatibility()
    result = dict(
        format=1,
        manifest=VERSION,
        resourceVersion=RESOURCE_VERSION,
        bundle=dict(name=BUNDLE, md5=BUNDLE_MD5, sha256=BUNDLE_SHA256,
                    url="https://zhangjcsdl.leiting.com/patch/202610291150884/Android/DefaultPackage/"
                        + BUNDLE_MD5 + ".bundle"),
        apk=dict(path=source_profile["profiles"][VERSION]["apk"]["path"],
                 sha256=source_profile["profiles"][VERSION]["apk"]["sha256"],
                 manifest=source_profile["profiles"][VERSION]["apk"]["manifest"]),
        operation="Verify original 884 APK/CDN object identities and the completed raw AssetRipper export. Record separately the Windows prefab restored through the shared native UI converter; never overwrite that binding with raw Android YAML.",
        inputs=inputs,
        prefabObjects=object_sources,
        nativeExport=dict(path=export_manifest_path.relative_to(ROOT).as_posix(),
                          sha256=sha(export_manifest_path.read_bytes()),
                          converter=export.get("converter"), directory=export["export_directory"]),
        outputs=prefab_outputs,
        integration=integration,
        sourceCompatibility=source_compatibility,
        runtimeBinding=dict(dialog="Resources.Load<GameObject>(\"Map14/UI/RoadblockTeamPropStageDialog\")",
                            item="RoadblockTeamPropStageDialogItem.prefab is retained from the same bundle; the original dialog hierarchy carries its slots."),
        limitations=["Windows prefab hashes record explicit converter output; source identity verification alone does not prove visual fidelity or interaction.",
                     "The 871 Map14 placement identity is checked against the byte-identical 884 MapInfoWrap, then each roadblock is compared with the loaded 884 placement metadata at runtime."],
    )
    receipt = ROOT / "assets/reference/npc-assistance/roadblock-source-manifest.json"
    receipt.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify-only", action="store_true")
    args = parser.parse_args()
    result = restore(verify_only=args.verify_only)
    print("Map14 roadblock prefab files verified:", len(result["outputs"]))
    print("Source receipt:", "assets/reference/npc-assistance/roadblock-source-manifest.json")


if __name__ == "__main__":
    main()
