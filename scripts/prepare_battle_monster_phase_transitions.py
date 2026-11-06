#!/usr/bin/env python3
"""Install the original 3803 phase Animator and bind it for runtime loading."""

from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
VERSION = "884_156692"
SOURCE_ASSETS = ROOT / ".tools/source-packages/current" / VERSION / "native-conversion/native-export-all/ExportedProject/Assets"
TARGET_ASSETS = ROOT / "unity/Assets/Official/Battle/Monsters/3803"
CATALOG_ASSET = ROOT / "unity/Assets/Game/Resources/Combat/NativeMonsters/phase-transition-catalog.asset"
CATALOG_META = CATALOG_ASSET.with_suffix(CATALOG_ASSET.suffix + ".meta")
SCRIPT_META = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterPhaseTransitionCatalog.cs.meta"
MANIFEST = ROOT / "assets/reference/battle-animation/monster-3803-phase-transition-source-manifest.json"
CATALOG_ASSET_GUID = "74b7843441f37af1231d00485d242897"

INPUTS = {
    "controller": "AnimatorController/3803tostage2.controller",
    "controller_meta": "AnimatorController/3803tostage2.controller.meta",
    "clip": "AnimationClip/3803tostage2.anim",
    "clip_meta": "AnimationClip/3803tostage2.anim.meta",
    "prefab": "Prefabs/EcEntity/Fight/FightMonster/3803.prefab",
}
OUTPUTS = {
    "controller": TARGET_ASSETS / "3803tostage2.controller",
    "controller_meta": TARGET_ASSETS / "3803tostage2.controller.meta",
    "clip": TARGET_ASSETS / "3803tostage2.anim",
    "clip_meta": TARGET_ASSETS / "3803tostage2.anim.meta",
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def guid_from_meta(path: Path) -> str:
    match = re.search(r"^guid:\s*([0-9a-f]{32})\s*$", path.read_text(encoding="utf-8"), re.MULTILINE)
    if not match:
        raise ValueError(f"Unity GUID is missing from {path}")
    return match.group(1)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def main() -> None:
    sources = {key: SOURCE_ASSETS / relative for key, relative in INPUTS.items()}
    for source in sources.values():
        require(source.is_file(), f"Official 3803 phase source is missing: {source}")

    controller_guid = guid_from_meta(sources["controller_meta"])
    clip_guid = guid_from_meta(sources["clip_meta"])
    controller_text = sources["controller"].read_text(encoding="utf-8")
    clip_text = sources["clip"].read_text(encoding="utf-8")
    prefab_text = sources["prefab"].read_text(encoding="utf-8")
    controller_guids = re.findall(r"guid:\s*([0-9a-f]{32})", controller_text)
    require(controller_guids == [clip_guid], "3803 phase controller dependency closure differs from the verified clip.")
    require("m_Name: stage2" in controller_text and "m_ConditionEvent: stage2" in controller_text,
            "3803 phase controller no longer contains the original stage2 Trigger transition.")
    require("attribute: m_IsActive\n    path: MonsterModel_3803/Stage1" in clip_text,
            "3803 phase clip no longer targets the original Stage1 object.")
    require("attribute: m_IsActive\n    path: MonsterModel_3803/Stage2" in clip_text,
            "3803 phase clip no longer targets the original Stage2 object.")
    require("m_PPtrCurves: []" in clip_text, "Unexpected external object references were added to the 3803 phase clip.")

    animator_blocks = re.findall(r"(?ms)^--- !u!95 &(\d+)\nAnimator:\n(.*?)(?=^--- !u!|\Z)", prefab_text)
    prefab_animator = next((block for _, block in animator_blocks
                            if re.search(rf"^  m_Controller: \{{fileID: 9100000, guid: {controller_guid}, type: 2\}}$",
                                         block, re.MULTILINE)), None)
    require(prefab_animator is not None, "3803 prefab no longer attaches the phase controller to an Animator.")
    game_object_match = re.search(r"^  m_GameObject: \{fileID: (\d+)\}$", prefab_animator, re.MULTILINE)
    require(game_object_match is not None, "3803 phase Animator has no prefab GameObject.")
    game_object_id = game_object_match.group(1)
    game_object_header = re.search(rf"(?m)^--- !u!1 &{game_object_id}$", prefab_text)
    require(game_object_header is not None, "3803 phase Animator GameObject is missing from its prefab.")
    game_object = re.search(rf"(?ms)^--- !u!1 &{game_object_id}\nGameObject:\n(.*?)(?=^--- !u!|\Z)",
                            prefab_text).group(1)
    require(re.search(r"^  m_Name: body$", game_object, re.MULTILINE) is not None,
            "3803 phase Animator is no longer attached to the original body object.")
    for field, value in (("m_Enabled", "1"), ("m_CullingMode", "0"), ("m_UpdateMode", "0"),
                         ("m_ApplyRootMotion", "0"), ("m_KeepAnimatorStateOnDisable", "0"),
                         ("m_WriteDefaultValuesOnDisable", "0")):
        require(re.search(rf"^  {field}: {value}$", prefab_animator, re.MULTILINE) is not None,
                f"3803 prefab body Animator field {field} differs from the verified original.")

    TARGET_ASSETS.mkdir(parents=True, exist_ok=True)
    for key, destination in OUTPUTS.items():
        destination.write_bytes(sources[key].read_bytes())

    script_guid = guid_from_meta(SCRIPT_META)
    CATALOG_ASSET.parent.mkdir(parents=True, exist_ok=True)
    catalog_text = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name: phase-transition-catalog
  m_EditorClassIdentifier:
  bindings:
  - entityId: 3803
    controller: {{fileID: 9100000, guid: {controller_guid}, type: 2}}
"""
    CATALOG_ASSET.write_text(catalog_text, encoding="utf-8", newline="\n")
    CATALOG_META.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {CATALOG_ASSET_GUID}\n"
        "NativeFormatImporter:\n"
        "  externalObjects: {}\n"
        "  mainObjectFileID: 11400000\n"
        "  userData:\n"
        "  assetBundleName:\n"
        "  assetBundleVariant:\n",
        encoding="utf-8",
        newline="\n",
    )

    input_records = []
    for key, relative in INPUTS.items():
        source = sources[key]
        input_records.append({"role": key, "path": str(source.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(source)})
    output_paths = [*OUTPUTS.values(), CATALOG_ASSET, CATALOG_META]
    output_records = [{"path": str(path.relative_to(ROOT)).replace("\\", "/"), "sha256": sha256(path)}
                      for path in output_paths]
    manifest = {
        "sourceVersion": VERSION,
        "nativeExport": "native-export-all",
        "generator": "scripts/prepare_battle_monster_phase_transitions.py",
        "controllerGuid": controller_guid,
        "clipGuid": clip_guid,
        "runtimeBinding": {
            "entityId": 3803,
            "animatorPath": "0template3x3_view/body",
            "trigger": "stage2",
            "prefabAnimator": {"object": "body", "enabled": True, "cullingMode": 0,
                               "updateMode": 0, "applyRootMotion": False,
                               "keepAnimatorStateOnDisable": False,
                               "writeDefaultValuesOnDisable": False},
            "clipTargets": ["MonsterModel_3803/Stage1", "MonsterModel_3803/Stage2"],
            "resourceCatalog": "Combat/NativeMonsters/phase-transition-catalog",
        },
        "inputs": input_records,
        "outputs": output_records,
        "generatorSha256": sha256(Path(__file__).resolve()),
    }
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Installed 3803 phase Animator from {VERSION}: {controller_guid} -> {clip_guid}")
    print(f"Manifest: {MANIFEST.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
