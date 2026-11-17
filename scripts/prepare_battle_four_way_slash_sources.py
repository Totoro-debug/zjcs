"""Verify original four-way slash rules, native playables and sound against official inputs."""
import argparse
import copy
import json
import re
from pathlib import Path

from official_apk import Apk
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "assets/reference/battle-rules/four-way-slash-source-manifest.json"


def sound_cues(prefab, asset):
    guid = re.search(r"^guid: (\w+)$", Path(str(asset) + ".meta").read_text(), re.M)[1]
    text = prefab.read_text(encoding="utf-8-sig")
    components = [re.match(r" &(\d+)", block)[1] for block in text.split("--- !u!114")
                  if "\n  SoundEvent:" in block and "guid: " + guid in block]
    component, = components
    times = [float(value) for value in re.findall(
        r"- myTime: ([\d.]+)\s+myPlayable: \{fileID: " + component + r"\}", text)]
    if times != [0]:
        raise ValueError("The original four-way slash sound binding or timing changed")
    return times


def build():
    entities = {}
    combat = ROOT / "unity/Assets/Official/Combat"
    for name in ("skill-rules.json", "battle-rules.json"):
        for key, components in json.loads((combat / name).read_bytes())["entities"].items():
            entities.setdefault(key, {}).update(components)
    definitions = {key: copy.deepcopy(entities[key]) for key in ("10650", "10651", "10022")}
    skill = definitions["10650"]["FightSkillComponent"]
    hit = definitions["10651"]["FightHitFixedComponent"]
    damage = definitions["10022"]["FightDamageComponent"]
    offsets = {(x["Pos"]["X"], x["Pos"]["Y"]) for x in hit["Scope"]}
    if (skill["SkillTargetType"] != 0 or skill["TargetType"] != 1 or skill["ElementType"] != 0
            or len(skill["HitList"]) != 1 or skill["HitList"][0]["ClassId"] != 10651
            or hit["DamageId"] != 10022 or hit["DamageType"] != 1 or hit["IsTrueDamage"]
            or len(hit["Scope"]) != 4 or offsets != {(0, 1), (0, -1), (1, 0), (-1, 0)}
            or any(x["Delay"] != 0 for x in hit["Scope"]) or damage["MoveCfg"]["MoveBehaviourId"] != 0):
        raise ValueError("Review the original four-way slash skill and area-hit configuration")
    normalized = []
    if "ExcludeCharacterTypes" not in damage["DisperseStatus"]:
        damage["DisperseStatus"]["ExcludeCharacterTypes"] = []
        normalized.append("10022/DisperseStatus/ExcludeCharacterTypes")
    rules = json.loads((combat / "rules.json").read_bytes())
    item, = (row for row in rules["skill"] if row["ClassId"] == 10013)
    if item["EcEntityId"] != 10650:
        raise ValueError("The original four-way slash item binding changed")
    configuration = verify_selected_components(definitions, [item])
    with Apk.current() as apk:
        version = apk.version
    code = ROOT / ".tools/source-packages/current" / version / "official-code"
    originals = [identity(code / "common-source/Common" / (name + ".cs")) for name in (
        "FightSkillComponentInfo", "FightSkillComponentImplement", "FightHitFixedComponentInfo",
        "FightDamageComponentImplement", "HitScopCfg")]
    originals.extend(identity(code / "client-source" / (name + ".cs")) for name in (
        "MyFightHitBase", "MyFightHitFixed", "MyFightActionSkill", "MyPlayableSound"))
    graph_path = ROOT / "assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json"
    graph = json.loads(graph_path.read_bytes())
    selected = lambda row: "/10650/" in row["path"] or "/Global/10022.prefab" in row["path"]
    native = [entry for entry in graph["native_inputs"] if selected(entry)]
    outputs = [entry for entry in graph["outputs"] if selected(entry)]
    if len(native) != 8 or len(outputs) != 8:
        raise ValueError("The original four-way slash graph is incomplete")
    for entry in native + outputs:
        if identity(ROOT / entry["path"]) != entry:
            raise ValueError("The original four-way slash graph identity differs: " + entry["path"])
    sound_path = ROOT / "assets/reference/battle-animation/native-sound-sources-source-manifest.json"
    event, = (row for row in json.loads(sound_path.read_bytes())["events"] if row["name"] == "Skill_10013")
    if event["id"] != 3744235472 or event["banks"] != ["Battle_SFX_02"]:
        raise ValueError("The original four-way slash sound event changed")
    runtime_sound = ROOT / event["source"]["path"]
    if identity(runtime_sound) != {key: event["source"][key] for key in ("path", "sha256")}:
        raise ValueError("The original four-way slash sound asset differs")
    original_graph, = (ROOT / row["path"] for row in native if row["path"].endswith("/playable10650.prefab"))
    runtime_graph, = (ROOT / row["path"] for row in outputs if row["path"].endswith("/playable10650.prefab"))
    original_sound = original_graph.parents[5] / "Wwise/ScriptableObjects/Event" / (event["guid"] + ".asset")
    original_text = original_sound.read_text(encoding="utf8")
    if (re.search(r"\n  id: (\d+)", original_text)[1] != str(event["id"])
            or re.search(r"\n  objectName: (\S+)", original_text)[1] != event["name"]
            or re.search(r"\n  guid: (\S+)", original_text)[1] != event["guid"]):
        raise ValueError("The original four-way slash sound identity differs")
    if sound_cues(original_graph, original_sound) != sound_cues(runtime_graph, runtime_sound):
        raise ValueError("The runtime four-way slash sound differs from its official prefab")
    catalog = ROOT / "unity/Assets/Official/Combat/NativeSound/Resources/Combat/Wwise/catalog.json"
    if {"id": event["id"], "name": event["name"]} not in json.loads(catalog.read_bytes())["events"]:
        raise ValueError("The original four-way slash sound is absent from the runtime catalog")
    bindings = ROOT / "unity/Assets/Game/Resources/Combat/skill-playables.json"
    cue, = (row for row in json.loads(bindings.read_bytes())["skills"] if row["skill_entity_id"] == 10650)
    if not any(row["kind"] == 1 and row["animation"] == "100/skill_05" for row in cue["playables"]):
        raise ValueError("The original four-way slash character motion is not bound")
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
            raise ValueError("The four-way slash source receipt differs")
    else:
        OUTPUT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print("Verified official four-way slash, native graph, character motion and sound.")
