"""Recover current player appearance inputs and bind the original player lifecycle.

Only game application/configuration sources are read; no animation SDK is used.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re

import yaml
from current_component_schemas import CurrentSchemas
from official_apk import Apk
from official_tables import Constructors
from prepare_battle_scene_appearance_runtime import ROOT, identity, meta, write_changed
from prepare_unity_profession_appearance_resolver import adapt
from prepare_unity_profession_player_appearance import binding, logs, strip
from prepare_unity_profession_character_resources import guid
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header


class CurrentConstructors(Constructors):
    def __init__(self, code):
        super().__init__(code)
        self.code = code
        self.paths = {}

    def source(self, name):
        if name not in self.sources:
            # Application vectors share their names with the generated Luban
            # records. Only the latter contain the serialized constructors.
            folders = ("Common.LubanGen",) if name in ("Pos2d", "Vector2", "Vector3") else ("Common", "Common.LubanGen", "")
            candidates = [self.code / "common-source" / folder / (name + ".cs") for folder in folders]
            path = next((p for p in candidates if p.is_file()), None)
            self.sources[name] = path.read_text(encoding="utf-8-sig") if path else ""
            if path: self.paths[name] = path
        return self.sources[name]


def current_path(prior):
    path = ROOT / prior.replace("871_156598", "884_156692")
    if not path.is_file(): path = Path(str(path).replace("common-source\\Common\\", "common-source\\").replace("common-source/Common/", "common-source/"))
    assert path.is_file(), path
    return path


def projection(tables):
    def pairs(mapping):
        return None if mapping is None else [dict(Sex=int(sex), Skins=[dict(Type=int(t), Id=v) for t, v in skins.items()]) for sex, skins in mapping.items()]
    return dict(Spines=tables["spine_skin"], Characters=tables["character_skin"],
        Dyes=tables["character_skin_dye"], Colors=tables["character_skin_color"],
        Groups=[dict(GroupId=row["GroupId"], Ranges=[dict(Values=v) for v in row["NestedIdList"]]) for row in tables["character_skin_id_group"]],
        Professions=[dict(Profession=row["Profession"], PrePro=row["PrePro"], DefaultSkins=pairs(row["DefaultSkins"]),
            AppendAppearanceRewards=pairs(row["AppendAppearanceRewards"])) for row in tables["profession_base"]],
        Appended=[dict(ClassId=row["ClassId"], Skins=None if row["SkinDict"] is None else [dict(Sex=int(s), Id=i) for s, i in row["SkinDict"].items()]) for row in tables["append_appearance"]])


def main():
    apk = Apk.current()
    assert apk.version == "884_156692"
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    code_receipt = code.parent / "code-manifest.json"
    original = json.loads(code_receipt.read_bytes())
    assemblies = [code / "managed/decoded" / name for name in ("Assembly-CSharp.dll", "Console.dll")]
    for path in assemblies: assert identity(path)["sha256"] == original["assemblies"][path.name]["decoded_sha256"]
    decoder = CurrentConstructors(code)
    mapping = {name.rsplit("/", 1)[-1]: kind for kind, name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', decoder.source("Tables"))}
    objects = {obj.read().m_Name.rsplit("/", 1)[-1]: obj for obj in apk.objects("defaultpackage_assets_config_luban.bundle") if obj.type.name == "TextAsset"}
    tables, inputs = {}, []
    for name in ("profession_base", "spine_skin", "character_skin", "character_skin_id_group", "character_skin_dye", "character_skin_color", "append_appearance", "game_settings", "equip_type"):
        obj = objects[name + ".g"]
        raw = obj.read().m_Script.encode("utf-8", "surrogateescape")
        tables[name] = decoder.table(raw, mapping[name])
        inputs.append(dict(name=name, constructor=mapping[name], source=apk.provenance(obj),
            object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(), payload_sha256=hashlib.sha256(raw).hexdigest(),
            consumed_bytes=len(raw), **({"fields": len(tables[name])} if isinstance(tables[name], dict) else {"rows": len(tables[name])})))
        print(name, len(tables[name]), "fields" if isinstance(tables[name], dict) else "rows", "fully decoded", flush=True)
    shared = []
    for name in ("appearance-resolver", "player-appearance"):
        prior = json.loads((ROOT / "assets/reference/profession-ui" / (name + "-source-manifest.json")).read_bytes())
        for row in prior["ports"]:
            path = current_path(row["source"]["path"])
            if path.name == "CharacterSkinInfoParser.cs":
                # The migrated parser never included random selection. Current
                # PostParse/key/lookup bodies are identical after the recorded
                # C# dependency adaptations; an unused historical field is absent.
                text = adapt(path.read_text(encoding="utf-8-sig"))
                text = text[:text.index("\n\tpublic int GetRandomSkinId(")] + "\n}\n"
                text = text.replace(" : LubanCsvParser<CharacterSkinInfo>", "")
                text = text.replace("public override void PostParse()", "public void PostParse(ProfessionAppearanceTables tables)").replace("\t\t((ParserBase)this).PostParse();\n", "")
                for parser in ("SpineSkinInfoParser", "CharacterSkinIdGroupInfoParser", "ProfessionBaseInfoParser"):
                    text = text.replace("((ParserBase)this).ConfigManager.GetParser<" + parser + ">()", "tables." + parser)
                text = text.replace("ProfessionBaseInfo", "ProfessionAppearanceBaseInfo").replace("tables.ProfessionAppearanceBaseInfoParser", "tables.ProfessionBaseInfoParser")
                text = text.replace("CharacterSkinInfo[] array = configs;", "CharacterSkinInfo[] array = Rows;")
                text = text.replace("public Dictionary<int, CharacterSkinInfo> SkinDict", "public CharacterSkinInfo[] Rows;\n\n\tpublic Dictionary<int, CharacterSkinInfo> SkinDict")
                text = text.replace("return CollectionExtensions.GetValueOrDefault<int, CharacterSkinInfo>((IReadOnlyDictionary<int, CharacterSkinInfo>)SkinDict, key);", "SkinDict.TryGetValue(key, out var value); return value;").replace("(int)(gender + 1)", "((int)gender + 1)")
                runtime = (ROOT / row["runtime"]["path"]).read_text(encoding="utf-8-sig")
                runtime = re.sub(r"\n\tprivate static List<int> sWeightList = new List<int>\(\);\n", "", runtime)
                expected = "namespace Zjcs.Official.ProfessionUI\n{\nusing Object = UnityEngine.Object;\n" + text + "\n}\n"
                assert runtime.replace("\n\n\n", "\n\n") == expected.replace("\n\n\n", "\n\n"), "Current retained character parser methods changed"
                comparison = "Exact retained PostParse/GetSkinInfo/GetKey after explicit dependency/C# adaptations; unused historical sWeightList excluded. Random selection remains outside this parser port."
            else:
                assert identity(path)["sha256"] == row["source"]["sha256"], path
                comparison = "Entire original source byte identity"
            shared.append(dict(current=identity(path), historical=row["source"], runtime=identity(ROOT / row["runtime"]["path"]), comparison=comparison))
    defaults_prior = json.loads((ROOT / "assets/reference/profession-ui/skin-defaults-source-manifest.json").read_bytes())
    for row in defaults_prior["application"]:
        path = current_path(row["path"])
        assert identity(path)["sha256"] == row["sha256"], path
        shared.append(dict(current=identity(path), historical=row, comparison="Entire original source byte identity"))
    destination = ROOT / "unity/Assets/Official/Combat/SceneCharacterAppearance/Resources/Combat/SceneAppearance"
    assert destination.is_dir()
    outputs = []
    payloads = {"appearance-tables": projection(tables), "skin-defaults": {key: tables["game_settings"][key] for key in ("InitialWeaponSwordSkinId", "InitialWeaponWandSkinId", "ProfessionSkinTypeList")}}
    payloads["skin-defaults"]["EquipTypes"] = tables["equip_type"]
    for name, value in payloads.items():
        path = destination / (name + ".json")
        write_changed(path, json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n"); meta(path)
        outputs.extend((path, path.with_name(path.name + ".meta")))
    obj = apk.asset("defaultpackage_assets_scriptableconfig_modules_appearance.bundle", "/AppearanceAnimNameCombineRule.asset")
    values = CurrentSchemas().read(obj)
    def check_references(value, key=""):
        if isinstance(value, dict):
            if "m_PathID" in value and key != "m_Script": assert value["m_PathID"] == 0, (key, value)
            for key, child in value.items(): check_references(child, key)
        elif isinstance(value, list):
            for child in value: check_references(child)
    check_references(values)
    native = source_header(); native.update(fields(dict(values, m_Script=dict(m_FileID=0, m_PathID=0))))
    native.update(m_Script=dict(fileID=11500000, guid=guid(ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI/AppearanceAnimNameCombineRule.cs"), type=3), m_EditorClassIdentifier="")
    rules = destination / "AppearanceAnimNameCombineRule.asset"
    write_changed(rules, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n" + yaml.dump({"MonoBehaviour": native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)); meta(rules)
    write_changed(rules.with_name(rules.name + ".meta"), "fileFormatVersion: 2\nguid: " + guid(rules) + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n")
    outputs.extend((rules, rules.with_name(rules.name + ".meta")))
    source = code / "client-source/AppearanceSystem/PlayerAppearance.cs"
    text = strip(source.read_text(encoding="utf-8-sig"))
    text = binding(text, "\t[Inject]", "\tpublic AppearanceRuntimeData AppearanceRuntimeData", "")
    text = logs(text.replace("AppearanceConfig.Value", "PlayerBindings.Config").replace("AppearanceUtils.BuildAppearanceRuntimeData", "PlayerBindings.BuildAppearanceRuntimeData"))
    text = text.replace("public class PlayerAppearance : PlayerAppearanceInfo\n{", "public class PlayerAppearance : PlayerAppearanceInfo\n{\n    public void ConfigureAppearance(AppearanceCharacter character, params APlayerAppearanceInfo[] infos)\n    {\n        AppearanceCharacter = character ? character : throw new ArgumentNullException(nameof(character));\n        mAppearanceInfoList = new List<APlayerAppearanceInfo>(infos ?? throw new ArgumentNullException(nameof(infos)));\n    }\n")
    text = "namespace Zjcs.Official.CombatAppearance\n{\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.ProfessionUI;\n" + text + "\n}\n"
    assert all(token not in text for token in ("[Inject]", "GLogger", "Framework", "Spine."))
    runtime = ROOT / "unity/Assets/Game/Runtime/Official/CombatAppearance/PlayerAppearance.cs"
    write_changed(runtime, text); meta(runtime); outputs.extend((runtime, runtime.with_name(runtime.name + ".meta")))
    tests = [ROOT / "unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAppearance.cs"]
    for path in tests:
        if path.exists(): meta(path); outputs.extend((path, path.with_name(path.name + ".meta")))
    proof = dict(manifest=apk.version, code_receipt=identity(code_receipt), assemblies=[identity(p) for p in assemblies], metadata=identity(code / "global-metadata.dat"),
        configuration=inputs, constructors=[identity(path) for path in decoder.paths.values()],
        decoder=identity(ROOT / "scripts/official_tables.py"), shared_application=shared, original_player=identity(source),
        animation_rules=dict(source=apk.provenance(obj), payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(), fields=values),
        outputs=[identity(path) for path in outputs], generator=identity(Path(__file__).resolve()),
        dependencies=[identity(ROOT / path) for path in ("assets/reference/battle-animation/scene-character-appearance-source-manifest.json", "unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionAppearanceBindings.cs", "unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionAppearanceSkinDefaults.cs")],
        binding=dict(tables="Combat/SceneAppearance/appearance-tables", defaults="Combat/SceneAppearance/skin-defaults", animation_rules="Combat/SceneAppearance/AppearanceAnimNameCombineRule", player="Zjcs.Official.CombatAppearance.PlayerAppearance", append="AppendSkinType.Game from current AppearanceConfig"),
        operation="Fully decode nine selected current original tables, retain appearance-consumed fields and collection order, recover current animation formatting rules, reuse byte-verified application logic and bind the original player appearance lifecycle to the current scene character. Explicit configuration replaces only prefab field injection; refresh/override/event/clear ordering remains original.",
        limits=["A native completed-pose producer is still required; this player appearance path does not implement Mecanim, full physics, animation mixing or the complete original prefab.", "Production battle input/save flow and Windows Player integration remain required."])
    write_changed(ROOT / "assets/reference/battle-animation/scene-player-appearance-source-manifest.json", json.dumps(proof, ensure_ascii=False, indent=2) + "\n")
    print("Current player lifecycle and selected ordered appearance inputs recovered.")
    print("Initial weapon skin IDs:", payloads["skin-defaults"]["InitialWeaponSwordSkinId"], payloads["skin-defaults"]["InitialWeaponWandSkinId"])
    for row in tables["profession_base"]:
        if row["Profession"] in (1,10): print("Base profession defaults:", row["Profession"], row["DefaultSkins"])


if __name__ == "__main__": main()
