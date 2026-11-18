"""Verify original mountain smash rules, native playables and sound against official inputs."""
import argparse
import copy
import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk
from official_tables import Constructors
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "assets/reference/battle-rules/mountain-smash-source-manifest.json"


def sound_cues(prefab, asset):
    guid = re.search(r"^guid: (\w+)$", Path(str(asset) + ".meta").read_text(), re.M)[1]
    text = prefab.read_text(encoding="utf-8-sig")
    components = [re.match(r" &(\d+)", block)[1] for block in text.split("--- !u!114")
                  if "\n  SoundEvent:" in block and "guid: " + guid in block]
    component, = components
    times = [float(value) for value in re.findall(
        r"- myTime: ([\d.]+)\s+myPlayable: \{fileID: " + component + r"\}", text)]
    if times != [0.24]:
        raise ValueError("The original mountain smash sound binding or timing changed")
    return times


def build():
    entities = {}
    combat = ROOT / "unity/Assets/Official/Combat"
    for name in ("skill-rules.json", "battle-rules.json"):
        for key, components in json.loads((combat / name).read_bytes())["entities"].items():
            entities.setdefault(key, {}).update(components)
    definitions = {key: copy.deepcopy(entities[key]) for key in ("11120", "11121", "11122", "11123", "10100", "10101", "10006")}
    skill = definitions["11120"]["FightSkillComponent"]
    hit = definitions["11121"]["FightHitFixedComponent"]
    damage = definitions["11122"]["FightDamageComponent"]
    action = definitions["11123"]["FightActionSkillMoveComponent"]
    link, = skill["HitList"]
    move = damage["MoveCfg"]
    offsets = {(row["Pos"]["X"], row["Pos"]["Y"]) for row in hit["Scope"]}
    if (skill["SkillTargetType"] != 0 or skill["TargetType"] != 1 or skill["ElementType"] != 0
            or skill["RangeType"] != 0 or skill["Range"] != [dict(X=0, Y=0)]
            or link["ClassId"] != 11121 or abs(link["Delay"] - .235) > .00001 or link["ActOnSouce"] != 1
            or hit["DamageId"] != 11122 or hit["DamageType"] != 1 or hit["IsTrueDamage"]
            or offsets != {(x,y) for x in range(-2,3) for y in range(-2,3) if 0 < x*x+y*y <= 4}
            or any(abs(row["Delay"] - (.045 if abs(row["Pos"]["X"]) + abs(row["Pos"]["Y"]) == 1 else .165)) > .00001 for row in hit["Scope"])
            or hit["AirCombo"] != dict(AirComboStatusId=24269, BasePercent=1.0, AffectedByProp=False)
            or move["MoveBehaviourId"] != 11123 or move["Distance"] != 1
            or move["SkillMoveDirection"] != 1 or move["TowardsTargetPos"]
            or move["Delay"] != 0 or abs(move["BasePercent"] - .6) > .00001 or not move["AffectedByProp"]
            or action["MoveDuration"] != -1 or abs(action["MoveSpeed"] - 2.5) > .00001
            or abs(action["BlockedLen"] - .8) > .00001 or action["BlockedSkillId"] != 10100
            or definitions["10100"]["FightSkillComponent"]["HitList"][0]["ClassId"] != 10101
            or definitions["10101"]["FightHitFixedComponent"]["DamageId"] != 10006):
        raise ValueError("Review the original mountain smash area, launch, knockback and blocked child")
    normalized = []
    for key in ("11122", "10006"):
        dispel = definitions[key]["FightDamageComponent"]["DisperseStatus"]
        if "ExcludeCharacterTypes" not in dispel:
            dispel["ExcludeCharacterTypes"] = []
            normalized.append(key + "/DisperseStatus/ExcludeCharacterTypes")
    rules = json.loads((combat / "rules.json").read_bytes())
    item, = (row for row in rules["skill"] if row["ClassId"] == 10052)
    if item["EcEntityId"] != 11120:
        raise ValueError("The original mountain smash item binding changed")
    configuration = verify_selected_components(definitions, [item])
    with Apk.current() as apk:
        version = apk.version
        code = ROOT / ".tools/source-packages/current" / version / "official-code"
        settings_asset = apk.asset("config_luban", "/game_settings.g.bytes")
        settings_raw = settings_asset.read().m_Script.encode("utf8", "surrogateescape")
        settings = Constructors(code).table(settings_raw, "TbGameSettings")
        excluded = settings["Battle"]["CanNotApplyBlockedSkillCharacterTypes"]
        if excluded != rules["game_settings"]["Battle"]["CanNotApplyBlockedSkillCharacterTypes"]:
            raise ValueError("Runtime blocked-skill character types differ from the selected official settings")
        collision_settings = dict(source=apk.provenance(settings_asset),
            payload_sha256=hashlib.sha256(settings_raw).hexdigest(), excluded_character_types=excluded)
    code = ROOT / ".tools/source-packages/current" / version / "official-code"
    originals = [identity(code / "common-source/Common" / (name + ".cs")) for name in (
        "FightSkillComponentInfo", "FightSkillComponentImplement", "FightHitFixedComponentInfo",
        "FightDamageComponentImplement", "FightDamageComponentInfo", "FightSKillMoveCfg",
        "FightActionSkillMoveComponent", "FightActionSkillMoveComponentInfo", "HitScopCfg")]
    originals.extend(identity(code / "client-source" / (name + ".cs")) for name in (
        "MyFightHitBase", "MyFightHitFixed", "MyFightActionSkill", "MyFightActionSkillMove", "MyPlayableSound"))
    graph_path = ROOT / "assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json"
    graph = json.loads(graph_path.read_bytes())
    selected = lambda row: ("/11120/" in row["path"] or "/10100/" in row["path"]
        or "/GlobalPlayable/playable_move_blocked" in row["path"])
    native = [entry for entry in graph["native_inputs"] if selected(entry)]
    outputs = [entry for entry in graph["outputs"] if selected(entry)]
    if len(native) != 24 or len(outputs) != 24:
        raise ValueError("The original mountain smash graph is incomplete")
    for entry in native + outputs:
        if identity(ROOT / entry["path"]) != entry:
            raise ValueError("The original mountain smash graph identity differs: " + entry["path"])
    sound_path = ROOT / "assets/reference/battle-animation/native-sound-sources-source-manifest.json"
    event, = (row for row in json.loads(sound_path.read_bytes())["events"] if row["name"] == "Skill_10052_1")
    if event["id"] != 275207001 or event["banks"] != ["Battle_SFX_02"]:
        raise ValueError("The original mountain smash sound event changed")
    runtime_sound = ROOT / event["source"]["path"]
    if identity(runtime_sound) != {key: event["source"][key] for key in ("path", "sha256")}:
        raise ValueError("The original mountain smash sound asset differs")
    original_graph, = (ROOT / row["path"] for row in native if row["path"].endswith("/playable11120.prefab"))
    runtime_graph, = (ROOT / row["path"] for row in outputs if row["path"].endswith("/playable11120.prefab"))
    original_sound = original_graph.parents[5] / "Wwise/ScriptableObjects/Event" / (event["guid"] + ".asset")
    original_text = original_sound.read_text(encoding="utf8")
    if (re.search(r"\n  id: (\d+)", original_text)[1] != str(event["id"])
            or re.search(r"\n  objectName: (\S+)", original_text)[1] != event["name"]
            or re.search(r"\n  guid: (\S+)", original_text)[1] != event["guid"]):
        raise ValueError("The original mountain smash sound identity differs")
    if sound_cues(original_graph, original_sound) != sound_cues(runtime_graph, runtime_sound):
        raise ValueError("The runtime mountain smash sound differs from its official prefab")
    catalog = ROOT / "unity/Assets/Official/Combat/NativeSound/Resources/Combat/Wwise/catalog.json"
    if {"id": event["id"], "name": event["name"]} not in json.loads(catalog.read_bytes())["events"]:
        raise ValueError("The original mountain smash sound is absent from the runtime catalog")
    bindings = ROOT / "unity/Assets/Game/Resources/Combat/skill-playables.json"
    cue, = (row for row in json.loads(bindings.read_bytes())["skills"] if row["skill_entity_id"] == 11120)
    if not any(row["kind"] == 1 and row["animation"] == "100/skill_22" for row in cue["playables"]):
        raise ValueError("The original mountain smash character motion is not bound")
    return dict(manifest=version, configuration=configuration, explicit_empty_dispel_defaults=normalized,
        original_code=originals, collision_settings=collision_settings,
        airborne_source_manifest=identity(ROOT / "assets/reference/battle-rules/air-combo-source-manifest.json"),
        native_source_manifest=identity(graph_path), native_inputs=native,
        native_outputs=outputs, sound_source_manifest=identity(sound_path),
        sound_inputs=[identity(path) for path in (original_sound, Path(str(original_sound) + ".meta"),
            runtime_sound, Path(str(runtime_sound) + ".meta"), catalog)],
        character_binding=identity(bindings), generator=identity(Path(__file__)))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    proof = build()
    if args.check:
        if not OUTPUT.exists() or json.loads(OUTPUT.read_bytes()) != proof:
            raise ValueError("The mountain smash source receipt differs")
    else:
        OUTPUT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print("Verified official mountain smash, native graph, character motion and sound.")
