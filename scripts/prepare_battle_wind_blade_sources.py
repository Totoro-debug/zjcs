"""Verify original wind-blade rules, native playables and sound against official inputs."""
import argparse
import copy
import json
import re
from pathlib import Path

from official_apk import Apk
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "assets/reference/battle-rules/wind-blade-source-manifest.json"


def sound_cues(prefab, asset):
    guid = re.search(r"^guid: (\w+)$", Path(str(asset) + ".meta").read_text(), re.M)[1]
    text = prefab.read_text(encoding="utf-8-sig")
    matching = []
    for block in text.split("--- !u!114"):
        if "\n  SoundEvent:" in block and "guid: " + guid in block:
            matching.append(re.match(r" &(\d+)", block)[1])
    component, = matching
    times = [float(value) for value in re.findall(
        r"- myTime: ([\d.]+)\s+myPlayable: \{fileID: " + component + r"\}", text)]
    if times != [.15]:
        raise ValueError("The original wind-blade sound binding or timing changed")
    return times


def build():
    entities = {}
    combat = ROOT / "unity/Assets/Official/Combat"
    for name in ("skill-rules.json", "battle-rules.json"):
        for key, components in json.loads((combat / name).read_bytes())["entities"].items():
            entities.setdefault(key, {}).update(components)
    definitions = {key: copy.deepcopy(entities[key]) for key in ("10640", "10641", "10642", "10643")}
    skill = definitions["10640"]["FightSkillComponent"]
    hit = definitions["10641"]["FightHitFixedComponent"]
    damage = definitions["10642"]["FightDamageComponent"]
    move = damage["MoveCfg"]
    if (skill["SkillTargetType"] != 0 or skill["TargetType"] != 1 or skill["ElementType"] != 100
            or len(skill["HitList"]) != 1 or skill["HitList"][0]["ClassId"] != 10641
            or hit["DamageId"] != 10642 or hit["IsTrueDamage"]
            or [(x["Pos"]["X"], x["Pos"]["Y"]) for x in hit["Scope"]] != [(0, 1), (0, 2), (0, 3), (0, 4)]
            or move["MoveBehaviourId"] != 10643 or move["Distance"] != 1 or move["SkillMoveDirection"] != 0
            or move["BasePercent"] != .5 or not move["AffectedByProp"] or move["TowardsTargetPos"]):
        raise ValueError("Review the original wind-blade skill, hit and knockback configuration")
    # The selected APK adds an explicitly empty field to this older decoded payload.
    dispel = damage["DisperseStatus"]
    normalized = []
    if "ExcludeCharacterTypes" not in dispel:
        dispel["ExcludeCharacterTypes"] = []
        normalized.append("10642/DisperseStatus/ExcludeCharacterTypes")
    rules = json.loads((combat / "rules.json").read_bytes())
    item, = (row for row in rules["skill"] if row["ClassId"] == 10012)
    if item["EcEntityId"] != 10640:
        raise ValueError("The original wind-blade item binding changed")
    configuration = verify_selected_components(definitions, [item])
    with Apk.current() as apk:
        version = apk.version
    code = ROOT / ".tools/source-packages/current" / version / "official-code"
    originals = [identity(code / "common-source/Common" / (name + ".cs")) for name in (
        "FightSkillComponentInfo", "FightSkillComponentImplement", "FightHitFixedComponentInfo",
        "FightDamageComponentImplement", "FightSKillMoveCfg", "FightActionSkillMoveComponentInfo", "HitScopCfg")]
    originals.extend(identity(code / "client-source" / (name + ".cs")) for name in (
        "MyFightHitFixed", "MyFightActionSkillMove", "MyPlayableSound"))
    graph_path = ROOT / "assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json"
    graph = json.loads(graph_path.read_bytes())
    native = [entry for entry in graph["native_inputs"] if "/10640/" in entry["path"]]
    outputs = [entry for entry in graph["outputs"] if "/10640/" in entry["path"]]
    if len(native) != 14 or len(outputs) != 14:
        raise ValueError("The original wind-blade graph is incomplete")
    for entry in native + outputs:
        if identity(ROOT / entry["path"]) != entry:
            raise ValueError("The original wind-blade graph identity differs: " + entry["path"])
    sound_path = ROOT / "assets/reference/battle-animation/native-sound-sources-source-manifest.json"
    event, = (row for row in json.loads(sound_path.read_bytes())["events"] if row["name"] == "Skill_10012")
    if event["id"] != 3744235473 or event["banks"] != ["Battle_SFX_02"]:
        raise ValueError("The original wind-blade sound event changed")
    runtime_sound = ROOT / event["source"]["path"]
    if identity(runtime_sound) != {key: event["source"][key] for key in ("path", "sha256")}:
        raise ValueError("The original wind-blade sound asset differs")
    original_graph, = (ROOT / row["path"] for row in native if row["path"].endswith("/playable10640.prefab"))
    runtime_graph, = (ROOT / row["path"] for row in outputs if row["path"].endswith("/playable10640.prefab"))
    original_sound = original_graph.parents[5] / "Wwise/ScriptableObjects/Event" / (event["guid"] + ".asset")
    original_text = original_sound.read_text(encoding="utf8")
    if (re.search(r"\n  id: (\d+)", original_text)[1] != str(event["id"])
            or re.search(r"\n  objectName: (\S+)", original_text)[1] != event["name"]
            or re.search(r"\n  guid: (\S+)", original_text)[1] != event["guid"]):
        raise ValueError("The original wind-blade sound identity differs")
    if sound_cues(original_graph, original_sound) != sound_cues(runtime_graph, runtime_sound):
        raise ValueError("The runtime wind-blade sound differs from its official prefab")
    catalog = ROOT / "unity/Assets/Official/Combat/NativeSound/Resources/Combat/Wwise/catalog.json"
    if {"id": event["id"], "name": event["name"]} not in json.loads(catalog.read_bytes())["events"]:
        raise ValueError("The original wind-blade sound is absent from the runtime catalog")
    bindings = ROOT / "unity/Assets/Game/Resources/Combat/skill-playables.json"
    cue, = (row for row in json.loads(bindings.read_bytes())["skills"] if row["skill_entity_id"] == 10640)
    if not any(row["kind"] == 1 and row["animation"] == "100/skill_03" for row in cue["playables"]):
        raise ValueError("The original wind-blade character motion is not bound")
    return dict(manifest=version, configuration=configuration,
        explicit_empty_dispel_defaults=normalized,
        original_code=originals, native_source_manifest=identity(graph_path), native_inputs=native,
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
            raise ValueError("The wind-blade source receipt differs")
    else:
        OUTPUT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print("Verified official wind blade, knockback, native graph, character motion and sound.")
