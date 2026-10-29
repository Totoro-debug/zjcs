"""Install official daily challenge prefabs and their verified AssetRipper dependencies."""
from __future__ import annotations

import hashlib
import argparse
import json
import re
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ASSET_ROOT = ROOT / "unity/Assets"
EXPORT_ASSETS = ROOT / "output/daily-challenges-assetripper-expanded/ExportedProject/Assets"
STAGED_INPUTS = ROOT / "output/daily-challenges-inputs-20261024-expanded"
STAGED_MANIFEST = STAGED_INPUTS / "source-manifest.json"
PAYLOAD = ASSET_ROOT / "Official/DailyChallenges"
REFERENCE = ROOT / "assets/reference/daily-challenges/source-manifest.json"
TEXT_EXTENSIONS = {
    ".anim", ".asset", ".controller", ".guiskin", ".mat", ".mask",
    ".overridecontroller", ".prefab", ".rendertexture", ".shader",
    ".shadervariants", ".spriteatlas", ".terrainlayer",
}
GUID_LINE = re.compile(r"(?m)^guid: ([0-9a-f]{32})$")
GUID_REFERENCE = re.compile(r"guid: ([0-9a-f]{32})")
PREFABS = (
    "Prefabs/UI/DailyActivity/DailyActivityPanel.prefab",
    "Prefabs/UI/ActivityDomainBoss/ActivityDomainBossPanel.prefab",
    "Prefabs/UI/ActivityDomainBoss/ActivityDomainBossContent.prefab",
    "Prefabs/UI/ActivityDomainBoss/ActivityDomainBossNumCell.prefab",
    "Prefabs/UI/ActivityDomainBoss/ActivityDomainBossDialog.prefab",
    "Prefabs/UI/ActivityDomainBoss/ActivityDomainBossTeamConfirmEnterPanel.prefab",
    "Prefabs/UI/ActivityDomainBoss/ActivityDomainBossTeamMemberConfirmPanelItem.prefab",
    "Prefabs/UI/ActivityFestivalAnniversary/FunnyBoss/AnniversaryFunnyBossEntryPanel.prefab",
    "Prefabs/UI/ActivityFestivalAnniversary/FunnyBoss/AnniversaryFunnyBossEntryDialog.prefab",
    "Prefabs/UI/ActivityDomainFunnyBoss/ActivityDomainFunnyBossTeamConfirmEnterPanel.prefab",
    "Prefabs/UI/ActivityDomainFunnyBoss/ActivityDomainFunnyBossTeamMemberConfirmPanelItem.prefab",
    "Prefabs/UI/Fight/FightMainPanel/ExtModel/FightMainPanelActivityDomainBoss.prefab",
    "Prefabs/UI/Fight/FightMainPanel/ExtModel/FightMainPanelActivityFunnyBoss.prefab",
    "Prefabs/UI/Fight/GameEndPanelActivityDomainBoss.prefab",
    "Prefabs/UI/Fight/GameEndPanelActivityFunnyBoss.prefab",
)
RUNTIME_PREFABS = {
    "Prefabs/UI/DailyActivity/DailyActivityPanel.prefab":
        "DailyChallenges/DailyActivityPanel.prefab",
    "Prefabs/UI/ActivityDomainBoss/ActivityDomainBossPanel.prefab":
        "DailyChallenges/ActivityDomainBossPanel.prefab",
    "Prefabs/UI/ActivityDomainBoss/ActivityDomainBossNumCell.prefab":
        "DailyChallenges/ActivityDomainBossNumCell.prefab",
    "Prefabs/UI/ActivityFestivalAnniversary/FunnyBoss/AnniversaryFunnyBossEntryPanel.prefab":
        "DailyChallenges/AnniversaryFunnyBossEntryPanel.prefab",
    "Prefabs/UI/Fight/GameEndPanelActivityDomainBoss.prefab":
        "DailyChallenges/GameEndPanelActivityDomainBoss.prefab",
    "Prefabs/UI/Fight/GameEndPanelActivityFunnyBoss.prefab":
        "DailyChallenges/GameEndPanelActivityFunnyBoss.prefab",
}
BUNDLES = {
    "defaultpackage_assets_animation_ui_activitydomainboss.bundle",
    "defaultpackage_assets_prefabs_ui_dailyactivity.bundle",
    "defaultpackage_assets_config_luban_activitydomainboss.bundle",
    "defaultpackage_assets_config_luban_activitydomainfunnyboss.bundle",
    "defaultpackage_assets_image_ui_activityfestivalanniversary.bundle",
    "defaultpackage_assets_prefabs_ui_activitydomainboss.bundle",
    "defaultpackage_assets_prefabs_ui_activitydomainfunnyboss.bundle",
    "defaultpackage_assets_prefabs_ui_activityfestivalanniversary.bundle",
    "defaultpackage_assets_prefabs_ui_fight_fightmainpanel_extmodel_fightmainpanelactivitydomainboss.bundle",
    "defaultpackage_assets_prefabs_ui_fight_fightmainpanel_extmodel_fightmainpanelactivityfunnyboss.bundle",
    "defaultpackage_assets_prefabs_ui_fight_gameendpanelactivitydomainboss.bundle",
    "defaultpackage_assets_prefabs_ui_fight_gameendpanelactivityfunnyboss.bundle",
}


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def stable_guid(relative: str) -> str:
    return hashlib.md5(("zjcs:" + relative.replace("\\", "/").lower()).encode("utf-8")).hexdigest()


def verify_inputs() -> list[dict]:
    staged = json.loads(STAGED_MANIFEST.read_text(encoding="utf-8"))
    if staged.get("manifest") != "871_156598":
        raise ValueError("Daily challenge bundle inputs are not from current manifest 871_156598.")
    requested = set(staged.get("requested_bundles", []))
    if not BUNDLES.issubset(requested):
        raise ValueError("The staged bundle closure does not include every daily challenge source bundle.")
    inputs = staged.get("inputs", [])
    by_name = {Path(item.get("output", "")).name: item for item in inputs}
    for bundle in BUNDLES:
        if bundle not in by_name:
            raise ValueError("A daily challenge bundle is absent from its staged source manifest: " + bundle)
    return inputs


def install_runtime_mappings() -> None:
    manifest = json.loads(REFERENCE.read_text(encoding="utf-8"))
    unity_assets = manifest.get("unity_assets", {})
    if unity_assets.get("manifest") != "871_156598":
        raise ValueError("Daily challenge source manifest is not current manifest 871_156598.")
    outputs = unity_assets.get("outputs", [])
    output_by_path = {item.get("path"): item for item in outputs}
    for source_path, runtime_path in RUNTIME_PREFABS.items():
        source = PAYLOAD / source_path
        expected = output_by_path.get(source.relative_to(ROOT).as_posix())
        if not source.is_file() or expected is None or sha(source.read_bytes()) != expected.get("sha256"):
            raise ValueError("Runtime prefab source is missing or does not match its manifest: " + str(source))
        target = ASSET_ROOT / "Game/Resources" / runtime_path
        target_meta = Path(str(target) + ".meta")
        target.parent.mkdir(parents=True, exist_ok=True)
        data = source.read_bytes()
        if target.exists() and target.read_bytes() != data:
            raise ValueError("Refusing to overwrite changed runtime challenge prefab: " + str(target))
        if not target.exists():
            target.write_bytes(data)
        meta_text = "fileFormatVersion: 2\nguid: " + stable_guid("Game/Resources/" + runtime_path) + "\n"
        if target_meta.exists() and target_meta.read_text(encoding="utf-8") != meta_text:
            raise ValueError("Refusing to overwrite changed runtime challenge metadata: " + str(target_meta))
        if not target_meta.exists():
            target_meta.write_text(meta_text, encoding="utf-8", newline="\n")
        output_by_path[target.relative_to(ROOT).as_posix()] = {
            "path": target.relative_to(ROOT).as_posix(),
            "bytes": len(data),
            "sha256": sha(data),
            "meta_sha256": sha(target_meta.read_bytes()),
        }
    unity_assets["runtime_prefabs"] = RUNTIME_PREFABS
    unity_assets["outputs"] = list(output_by_path.values())
    REFERENCE.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
                         encoding="utf-8", newline="\n")
    print("Verified and installed", len(RUNTIME_PREFABS), "daily challenge runtime prefabs.")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime-only", action="store_true")
    if parser.parse_args().runtime_only:
        install_runtime_mappings()
        return
    if not EXPORT_ASSETS.is_dir():
        raise FileNotFoundError("Run AssetRipper on the verified daily challenge bundle inputs first.")
    inputs = verify_inputs()
    guid_to_asset: dict[str, Path] = {}
    for meta in EXPORT_ASSETS.rglob("*.meta"):
        if not meta.is_file():
            continue
        asset = meta.with_suffix("")
        if not asset.is_file():
            continue
        match = GUID_LINE.search(meta.read_text(encoding="utf-8", errors="ignore"))
        if match is None:
            continue
        guid = match.group(1)
        if guid in guid_to_asset and guid_to_asset[guid] != asset:
            raise ValueError("AssetRipper produced duplicate source GUID " + guid)
        guid_to_asset[guid] = asset

    pending = [EXPORT_ASSETS / path for path in PREFABS]
    for path in pending:
        if not path.is_file() or not Path(str(path) + ".meta").is_file():
            raise FileNotFoundError("AssetRipper did not export the official daily challenge prefab: " + str(path))

    selected: set[Path] = set()
    omitted_scripts: set[str] = set()
    unresolved: set[str] = set()
    while pending:
        asset = pending.pop()
        if asset in selected:
            continue
        selected.add(asset)
        if asset.suffix.lower() not in TEXT_EXTENSIONS:
            continue
        for guid in GUID_REFERENCE.findall(asset.read_text(encoding="utf-8", errors="ignore")):
            if guid == "0" * 32:
                continue
            dependency = guid_to_asset.get(guid)
            if dependency is None:
                unresolved.add(guid)
            elif dependency.suffix.lower() == ".cs":
                omitted_scripts.add(dependency.name)
            else:
                pending.append(dependency)
    if unresolved:
        raise ValueError("Unresolved non-script daily challenge prefab dependencies: " + ", ".join(sorted(unresolved)))

    guid_remap: dict[str, str] = {}
    for source in selected:
        source_guid = GUID_LINE.search(Path(str(source) + ".meta").read_text(encoding="utf-8", errors="strict"))
        if source_guid is None:
            raise ValueError("AssetRipper asset metadata has no GUID: " + str(source))
        relative = source.relative_to(EXPORT_ASSETS).as_posix()
        target_relative = "Official/DailyChallenges/" + relative
        guid_remap[source_guid.group(1)] = stable_guid(target_relative)

    outputs = []
    for source in sorted(selected):
        relative = source.relative_to(EXPORT_ASSETS)
        target = PAYLOAD / relative
        target_meta = Path(str(target) + ".meta")
        target.parent.mkdir(parents=True, exist_ok=True)
        data = source.read_bytes()
        if source.suffix.lower() in TEXT_EXTENSIONS:
            text = data.decode("utf-8")
            for old_guid, new_guid in guid_remap.items():
                text = text.replace("guid: " + old_guid, "guid: " + new_guid)
            data = text.encode("utf-8")
        if target.exists() and target.read_bytes() != data:
            raise ValueError("Refusing to overwrite changed daily challenge asset: " + str(target))
        if not target.exists():
            target.write_bytes(data)
        source_meta = Path(str(source) + ".meta")
        meta_text = source_meta.read_text(encoding="utf-8", errors="strict")
        old_guid = GUID_LINE.search(meta_text).group(1)
        meta_text = GUID_LINE.sub("guid: " + guid_remap[old_guid], meta_text, count=1)
        if target_meta.exists():
            existing_meta = target_meta.read_text(encoding="utf-8")
            existing_guid = GUID_LINE.search(existing_meta)
            if existing_guid is None or existing_guid.group(1) != guid_remap[old_guid]:
                raise ValueError("Refusing to overwrite metadata with a different stable GUID: " + str(target_meta))
        if not target_meta.exists():
            target_meta.write_text(meta_text, encoding="utf-8", newline="\n")
        outputs.append({
            "path": target.relative_to(ROOT).as_posix(),
            "bytes": len(data),
            "sha256": sha(data),
            "meta_sha256": sha(target_meta.read_bytes()),
        })

    for source_path, runtime_path in RUNTIME_PREFABS.items():
        source = PAYLOAD / source_path
        if not source.is_file():
            raise FileNotFoundError("Runtime challenge prefab was not installed: " + str(source))
        target = ASSET_ROOT / "Game/Resources" / runtime_path
        target_meta = Path(str(target) + ".meta")
        target.parent.mkdir(parents=True, exist_ok=True)
        data = source.read_bytes()
        if target.exists() and target.read_bytes() != data:
            raise ValueError("Refusing to overwrite changed runtime challenge prefab: " + str(target))
        if not target.exists():
            target.write_bytes(data)
        meta_text = "fileFormatVersion: 2\nguid: " + stable_guid("Game/Resources/" + runtime_path) + "\n"
        if target_meta.exists() and target_meta.read_text(encoding="utf-8") != meta_text:
            raise ValueError("Refusing to overwrite changed runtime challenge metadata: " + str(target_meta))
        if not target_meta.exists():
            target_meta.write_text(meta_text, encoding="utf-8", newline="\n")
        outputs.append({
            "path": target.relative_to(ROOT).as_posix(),
            "bytes": len(data),
            "sha256": sha(data),
            "meta_sha256": sha(target_meta.read_bytes()),
        })

    folder_metadata = []
    for folder in sorted((path for path in PAYLOAD.rglob("*") if path.is_dir()), key=lambda path: path.as_posix()):
        relative = folder.relative_to(PAYLOAD).as_posix()
        target_meta = Path(str(folder) + ".meta")
        guid = stable_guid("Official/DailyChallenges/" + relative)
        meta_text = (
            "fileFormatVersion: 2\n"
            "guid: " + guid + "\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n"
        )
        if target_meta.exists():
            existing_meta = target_meta.read_text(encoding="utf-8")
            existing_guid = GUID_LINE.search(existing_meta)
            if existing_guid is None:
                raise ValueError("Existing folder metadata has no GUID: " + str(target_meta))
            guid = existing_guid.group(1)
        else:
            target_meta.write_text(meta_text, encoding="utf-8", newline="\n")
        folder_metadata.append({
            "path": target_meta.relative_to(ROOT).as_posix(),
            "guid": guid,
            "meta_sha256": sha(target_meta.read_bytes()),
        })

    unity_assets = {
        "manifest": "871_156598",
        "asset_converter": "AssetRipper 1.3.14",
        "source_bundles": [item for item in inputs if item.get("name") in BUNDLES],
        "cab_dependencies": len(inputs) - len(BUNDLES),
        "prefabs": list(PREFABS),
        "runtime_prefabs": RUNTIME_PREFABS,
        "omitted_official_scripts": sorted(omitted_scripts),
        "outputs": outputs,
        "folder_metadata": folder_metadata,
    }
    manifest = json.loads(REFERENCE.read_text(encoding="utf-8")) if REFERENCE.exists() else {}
    manifest["unity_assets"] = unity_assets
    encoded = json.dumps(manifest, ensure_ascii=False, indent=2) + "\n"
    REFERENCE.parent.mkdir(parents=True, exist_ok=True)
    REFERENCE.write_text(encoded, encoding="utf-8", newline="\n")
    print("Prepared", len(outputs), "daily challenge assets with", len(omitted_scripts),
          "omitted official scripts and", unity_assets["cab_dependencies"], "CAB dependencies.")


if __name__ == "__main__":
    main()
