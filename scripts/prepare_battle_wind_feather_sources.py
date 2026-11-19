"""Verify original wind-feather waves, child skill, falloff and native graphs."""
import argparse
import copy
import hashlib
import json
import subprocess
from pathlib import Path

from official_apk import Apk
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "assets/reference/battle-rules/wind-feather-source-manifest.json"


def local_identity(path, index):
    if not index:
        return identity(path)
    relative = path.relative_to(ROOT).as_posix()
    data = subprocess.check_output(["git", "show", ":" + relative], cwd=ROOT)
    return {"path": relative, "sha256": hashlib.sha256(data).hexdigest()}


def build(index):
    entities = {}
    combat = ROOT / "unity/Assets/Official/Combat"
    for name in ("skill-rules.json", "battle-rules.json"):
        for key, components in json.loads((combat / name).read_bytes())["entities"].items():
            entities.setdefault(key, {}).update(components)
    definitions = {key: copy.deepcopy(entities[key]) for key in
                   ("10910", "10911", "10912", "10913", "11050", "11051", "11052", "24224")}
    skill = definitions["10910"]["FightSkillComponent"]
    wrapper = definitions["10911"]["FightHitRandomTargetComponent"]
    waves = wrapper["HitList"]
    hit = definitions["10912"]["FightHitFixedComponent"]
    child = definitions["10913"]["FightDamageComponent"]["ChildSkillCfg"]
    projectile = definitions["11051"]["FightHitFixedComponent"]
    damage = definitions["11052"]["FightDamageComponent"]
    falloff = definitions["24224"]["FightStatusDamageFalloffComponent"]
    if (len(skill["HitList"]) != 1 or skill["HitList"][0]["ClassId"] != 10911
            or len(waves) != 7 or any(row["ClassId"] != 10912
                or abs(row["Delay"] - i * .1) > .00001 for i, row in enumerate(waves))
            or wrapper["MiniHitTargetCount"] != 7 or len(wrapper["Scope"]) != 3
            or len(hit["Scope"]) != 5 or hit["DamageId"] != 10913
            or child["SkillId"] != 11050 or child["Rate"] != 1
            or projectile["DamageId"] != 11052
            or not any(row["StatusId"] == 24224 and row["TargetType"] == 2 for row in damage["StatusList"])
            or falloff["FalloffPercent"] != 3500 or falloff["RootSkillIds"] != [10910]
            or falloff["NumOfStart"] != 1 or not falloff["RemoveOnSourceSkillStart"]):
        raise ValueError("The original wind-feather waves or falloff changed")
    normalized = []
    for key in ("10913", "11052"):
        dispel = definitions[key]["FightDamageComponent"]["DisperseStatus"]
        if "ExcludeCharacterTypes" not in dispel:
            dispel["ExcludeCharacterTypes"] = []
            normalized.append(key + "/DisperseStatus/ExcludeCharacterTypes")
    rules = json.loads((combat / "rules.json").read_bytes())
    item, = (row for row in rules["skill"] if row["ClassId"] == 10036)
    if item["EcEntityId"] != 10910:
        raise ValueError("The original wind-feather item binding changed")
    configuration = verify_selected_components(definitions, [item])
    with Apk.current() as apk:
        version = apk.version
    code = ROOT / ".tools/source-packages/current" / version / "official-code"
    originals = [identity(code / "common-source/Common" / (name + ".cs")) for name in (
        "FightSkillComponentInfo", "FightHitRandomTargetComponentInfo", "FightHitFixedComponentInfo",
        "FightDamageComponentInfo", "FightStatusDamageFalloffComponentInfo", "HitScopCfg")]
    originals.extend(identity(code / "client-source" / (name + ".cs")) for name in (
        "MyFightHitBase", "MyFightHitRandomTarget", "MyFightHitFixed", "MyFightStatusDamageFalloff"))
    graph_path = ROOT / "assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json"
    graph_data = (subprocess.check_output(["git", "show", ":" + graph_path.relative_to(ROOT).as_posix()], cwd=ROOT)
                  if index else graph_path.read_bytes())
    graph = json.loads(graph_data)
    native = [row for row in graph["native_inputs"] if any("/" + key + "/" in row["path"] for key in ("10910", "11050"))]
    outputs = [row for row in graph["outputs"] if any("/" + key + "/" in row["path"] for key in ("10910", "11050"))]
    if len(native) != 24 or len(outputs) != 24:
        raise ValueError("The original wind-feather graphs are incomplete")
    for row in native + outputs:
        if identity(ROOT / row["path"]) != row:
            raise ValueError("The original wind-feather graph differs: " + row["path"])
    local = [ROOT / path for path in (
        "unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs",
        "unity/Assets/Game/Runtime/Offline/Combat/FightBattleExecutor.StatusOnly.cs",
        "unity/Assets/Game/Runtime/Offline/Combat/FightHitTargetSelectors.cs",
        "scripts/fixtures/battle_wind_feather_harness.cs", "scripts/test_battle_wind_feather.ps1")]
    return dict(manifest=version, configuration=configuration, explicit_empty_dispel_defaults=normalized,
                original_code=originals, native_source_manifest=local_identity(graph_path, index),
                native_inputs=native, native_outputs=outputs,
                migration=[local_identity(path, index) for path in local],
                generator=local_identity(Path(__file__), index))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--index", action="store_true")
    args = parser.parse_args()
    proof = build(args.index)
    if args.check:
        if not OUTPUT.exists() or json.loads(OUTPUT.read_bytes()) != proof:
            raise ValueError("The wind-feather source receipt differs")
    else:
        OUTPUT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Verified original wind-feather configuration and native graphs.")
