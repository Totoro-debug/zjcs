"""Verify falling wind's original rules, animation graph and sound sources."""
import argparse
import copy
import json
import re
from pathlib import Path

from official_apk import Apk
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "assets/reference/battle-rules/falling-wind-source-manifest.json"


def sound_time(prefab, sound):
    guid = re.search(r"^guid: (\w+)$", Path(str(sound) + ".meta").read_text(), re.M)[1]
    text = prefab.read_text(encoding="utf-8-sig")
    component, = [re.match(r" &(\d+)", block)[1] for block in text.split("--- !u!114")
                  if "\n  SoundEvent:" in block and "guid: " + guid in block]
    times = [float(value) for value in re.findall(
        r"- myTime: ([\d.]+)\s+myPlayable: \{fileID: " + component + r"\}", text)]
    if times != [0.2]:
        raise ValueError("The original falling wind sound timing changed")
    return times


def build():
    entities = {}
    combat = ROOT / "unity/Assets/Official/Combat"
    for name in ("skill-rules.json", "battle-rules.json"):
        for key, components in json.loads((combat / name).read_bytes())["entities"].items():
            entities.setdefault(key, {}).update(components)
    definitions = {key: copy.deepcopy(entities[key]) for key in ("10590", "10591", "10592")}
    skill = definitions["10590"]["FightSkillComponent"]
    hit = definitions["10591"]["FightHitFixedComponent"]
    damage = definitions["10592"]["FightDamageComponent"]
    link, = skill["HitList"]
    scope = {(row["Pos"]["X"], row["Pos"]["Y"]): row["Delay"] for row in hit["Scope"]}
    if (skill["SkillTargetType"] != 0 or skill["TargetType"] != 1 or skill["ElementType"] != 100
            or skill["RangeType"] != 1 or len(skill["Range"]) != 41
            or link["ClassId"] != 10591 or abs(link["Delay"] - .45) > .00001
            or hit["DamageId"] != 10592 or hit["DamageType"] != 1 or hit["IsTrueDamage"]
            or set(scope) != {(0,0), (1,0), (-1,0), (0,1), (0,-1)}
            or scope[(0,0)] != 0 or any(abs(delay - .1) > .00001
                for point, delay in scope.items() if point != (0,0))
            or damage["StatusList"] or damage["MoveCfg"]["MoveBehaviourId"] != 0
            or damage["ChildSkillCfg"]["SkillId"] != 0):
        raise ValueError("Review the original falling wind range, timed hits and effects")
    normalized = []
    if "ExcludeCharacterTypes" not in damage["DisperseStatus"]:
        damage["DisperseStatus"]["ExcludeCharacterTypes"] = []
        normalized.append("10592/DisperseStatus/ExcludeCharacterTypes")
    rules = json.loads((combat / "rules.json").read_bytes())
    item, = (row for row in rules["skill"] if row["ClassId"] == 10043)
    if item["EcEntityId"] != 10590:
        raise ValueError("The original falling wind item binding changed")
    configuration = verify_selected_components(definitions, [item])
    with Apk.current() as apk:
        version = apk.version
    code = ROOT / ".tools/source-packages/current" / version / "official-code"
    originals = [identity(code / "common-source/Common" / (name + ".cs")) for name in (
        "FightSkillComponentInfo", "FightSkillComponentImplement", "FightHitFixedComponentInfo",
        "FightDamageComponentImplement", "FightDamageComponentInfo", "HitScopCfg")]
    originals.extend(identity(code / "client-source" / (name + ".cs")) for name in (
        "MyFightHitBase", "MyFightHitFixed", "MyFightActionSkill", "MyPlayableSound"))
    graph_path = ROOT / "assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json"
    graph = json.loads(graph_path.read_bytes())
    selected = lambda row: "/10590/" in row["path"]
    native = [entry for entry in graph["native_inputs"] if selected(entry)]
    outputs = [entry for entry in graph["outputs"] if selected(entry)]
    if len(native) != 10 or len(outputs) != 10:
        raise ValueError("The original falling wind graph is incomplete")
    for entry in native + outputs:
        if identity(ROOT / entry["path"]) != entry:
            raise ValueError("The original graph identity differs: " + entry["path"])
    sound_path = ROOT / "assets/reference/battle-animation/native-sound-sources-source-manifest.json"
    event, = (row for row in json.loads(sound_path.read_bytes())["events"] if row["name"] == "Skill_10043_1")
    if event["id"] != 2749055671 or event["banks"] != ["Battle_SFX_02"]:
        raise ValueError("The original falling wind sound event changed")
    runtime_sound = ROOT / event["source"]["path"]
    if identity(runtime_sound) != {key: event["source"][key] for key in ("path", "sha256")}:
        raise ValueError("The original sound asset differs")
    original_graph, = (ROOT / row["path"] for row in native if row["path"].endswith("/playable10590.prefab"))
    runtime_graph, = (ROOT / row["path"] for row in outputs if row["path"].endswith("/playable10590.prefab"))
    original_sound = original_graph.parents[5] / "Wwise/ScriptableObjects/Event" / (event["guid"] + ".asset")
    text = original_sound.read_text(encoding="utf8")
    if (re.search(r"\n  id: (\d+)", text)[1] != str(event["id"])
            or re.search(r"\n  objectName: (\S+)", text)[1] != event["name"]
            or re.search(r"\n  guid: (\S+)", text)[1] != event["guid"]):
        raise ValueError("The original sound identity differs")
    if sound_time(original_graph, original_sound) != sound_time(runtime_graph, runtime_sound):
        raise ValueError("The runtime sound differs from the official prefab")
    catalog = ROOT / "unity/Assets/Official/Combat/NativeSound/Resources/Combat/Wwise/catalog.json"
    if {"id": event["id"], "name": event["name"]} not in json.loads(catalog.read_bytes())["events"]:
        raise ValueError("The original sound is absent from the runtime catalog")
    bindings = ROOT / "unity/Assets/Game/Resources/Combat/skill-playables.json"
    cue, = (row for row in json.loads(bindings.read_bytes())["skills"] if row["skill_entity_id"] == 10590)
    if not any(row["kind"] == 1 and row["animation"] == "100/skill_11" for row in cue["playables"]):
        raise ValueError("The original character motion is not bound")
    return dict(manifest=version, configuration=configuration, explicit_empty_dispel_defaults=normalized,
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
            raise ValueError("The falling wind source receipt differs")
    else:
        OUTPUT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print("Verified official falling wind, native graph, character motion and sound.")
