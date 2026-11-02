"""Bind the selected original character application/configuration to native drawing.

Reads original application code and serialized configuration only, never SDK code.
"""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

import yaml
from current_component_schemas import CurrentSchemas
from official_apk import Apk
from prepare_battle_scene_appearance_runtime import ROOT, SOURCE, common, identity, meta, write_changed
from prepare_unity_profession_character_resources import guid
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header


def character(source):
    text = common(source)
    text = text.replace("using Common;\n", "")
    text = "using System;\n" + text
    start = text.index("\t[Inject]")
    end = text.index("\n\tpublic IAppearanceComponent", start)
    text = text[:start] + '''    private IProfessionAppearanceSceneCharacterBindings Bindings;
    public void Configure(IProfessionAppearanceSceneCharacterBindings bindings, AppearancePartHandler partHandler)
    {
        Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        mPartHandler = partHandler;
    }
''' + text[end:]
    text = text.replace("AppearanceAssetManager.TryGetPlayerSkeletonAssets", "Bindings.TryGetPlayerSkeletonAssets")
    text = text.replace("AppearanceConfig.Value", "Bindings.Config")
    start = text.index("\t\t\tGLoggerErrorInterpolatedStringHandler")
    end = text.index("\n\t\t}\n\t\telse", start)
    text = text[:start] + '            Debug.LogError("AppearanceCharacter.LoadAsset 主角骨架未预热. sex:" + sex);' + text[end:]
    start = text.index("\t\t\tGLoggerErrorInterpolatedStringHandler")
    end = text.index("\n\t\t\treturn false;", start)
    text = text[:start] + '            Debug.LogError("AppearanceComponent invalid");' + text[end:]
    assert all(value not in text for value in ("[Inject]", "GLogger", "using Spine", "Framework", "Deconstruct(ref"))
    return text


def main():
    apk = Apk.current()
    assert apk.version == "884_156692"
    code_receipt = ROOT / ".tools/source-packages/current/884_156692/code-manifest.json"
    code = json.loads(code_receipt.read_bytes())
    assembly = code_receipt.parent / "official-code/managed/decoded/Assembly-CSharp.dll"
    assert identity(assembly)["sha256"] == code["assemblies"][assembly.name]["decoded_sha256"]
    original = SOURCE / "AppearanceSystem.Runtime/AppearanceCharacter.cs"
    runtime = ROOT / "unity/Assets/Game/Runtime/Official/CombatAppearance/AppearanceCharacter.cs"
    write_changed(runtime, character(original)); meta(runtime)
    source = apk.asset("defaultpackage_assets_scriptableconfig_modules_appearance.bundle", "/AppearanceConfig.asset")
    values = CurrentSchemas().read(source)
    old = json.loads((ROOT / "assets/reference/profession-ui/appearance-config-source-manifest.json").read_bytes())

    def only_values(value, name=""):
        if isinstance(value, dict):
            if "m_FileID" in value and "m_PathID" in value:
                assert name == "m_Script" or not any(value.values()), (name, value)
            else:
                for key, item in value.items(): only_values(item, key)
        elif isinstance(value, list):
            for item in value: only_values(item, name)
    only_values(values)
    ports = []
    for prior in old["ports"]:
        name = Path(prior["source"]["path"]).name
        current = SOURCE.parent / "common-source" / name
        assert identity(current)["sha256"] == prior["source"]["sha256"], "Review changed current configuration type: " + name
        migrated = identity(ROOT / prior["runtime"]["path"])
        raw = (ROOT / migrated["path"]).read_bytes()
        assert migrated["sha256"] == prior["runtime"]["sha256"] or hashlib.sha256(raw.replace(b"\r\n", b"\n")).hexdigest() == prior["runtime"]["sha256"]
        ports.append(dict(current=current.relative_to(ROOT).as_posix(), source=identity(current), runtime=migrated,
                          historical_source=prior["source"]))
    destination = ROOT / "unity/Assets/Official/Combat/SceneCharacterAppearance"
    for folder in (destination, destination / "Resources", destination / "Resources/Combat", destination / "Resources/Combat/SceneAppearance"):
        folder.mkdir(exist_ok=True); meta(folder, folder=True)
    config = destination / "Resources/Combat/SceneAppearance/AppearanceConfig.asset"
    native = source_header(); native.update(fields(dict(values, m_Script=dict(m_FileID=0, m_PathID=0))))
    script = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI/AppearanceConfig.cs"
    native.update(m_Script=dict(fileID=11500000, guid=guid(script), type=3), m_EditorClassIdentifier="")
    body = yaml.dump({"MonoBehaviour": native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
    write_changed(config, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n" + body)
    meta(config)
    write_changed(config.with_name(config.name + ".meta"), "fileFormatVersion: 2\nguid: " + guid(config)
                  + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n")
    paths = [runtime, config, config.with_name(config.name + ".meta")]
    paths += [ROOT / path for path in (
        "unity/Assets/Game/Runtime/Official/CombatAppearance/ProfessionAppearanceSceneCharacterBindings.cs",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSceneAppearance.cs",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionSceneAppearanceBindings.cs",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/IProfessionNativeSceneWeaponPoseProducer.cs",
        "unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SceneCharacterAppearance.cs")]
    for path in paths:
        if path.suffix == ".cs": meta(path)
    proof = dict(manifest=apk.version, code_receipt=identity(code_receipt), assembly=identity(assembly),
        original_character=identity(original), source=apk.provenance(source),
        payload_sha256=hashlib.sha256(source.get_raw_data()).hexdigest(), original_fields=values,
        shared_configuration_types=ports, outputs=[identity(path) for path in paths], generator=identity(Path(__file__).resolve()),
        dependencies=[identity(ROOT / path) for path in (
            "assets/reference/battle-animation/scene-appearance-runtime-source-manifest.json",
            "assets/reference/battle-animation/scene-texture-source-manifest.json",
            "assets/reference/battle-animation/scene-native-renderer-source-manifest.json")],
        binding=dict(configuration="Combat/SceneAppearance/AppearanceConfig",
            catalog="Combat/SceneCharacters/appearance-resources", models=["Combat/SceneCharacters/male", "Combat/SceneCharacters/female"],
            caller="Zjcs.Official.CombatAppearance.AppearanceCharacter.SetRuntimeData",
            native="ProfessionNativeSceneAppearance / OfflineProfessionSceneAppearanceBindings",
            pose="Explicit external completed-pose callback; weapon Play/Apply retain the producer clock"),
        operation="Copy selected original AppearanceCharacter and all current AppearanceConfig fields; retain preheated asset lookup, part filtering, append skins, dye-copy order and release branches. Reuse only byte-verified original configuration types. Bind original AppearanceRenderer lifecycle to actual native geometry, Low textures, independent material pool and separately completed poses.",
        limits=["Native character assets contain all exported animations, so no SDK split-loader object is fabricated. Native pose completion is an explicit caller dependency, not Mecanim/AnimationState/physics/mixing/cache implementation.",
                "Each current binding is verified with a fixed-sex actor. Rebinding an existing combined native skin to another sex's skeleton layout remains unimplemented.",
                "This stage does not restore the full original character prefab or bind production battle/Windows Player appearance."])
    target = ROOT / "assets/reference/battle-animation/scene-character-appearance-source-manifest.json"
    write_changed(target, json.dumps(proof, ensure_ascii=False, indent=2) + "\n")
    print("Restored current character application and complete selected appearance configuration without SDK implementation.")


if __name__ == "__main__":
    main()
