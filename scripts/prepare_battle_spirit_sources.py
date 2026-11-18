"""Verify original battle spirit rules, native playables and sound against official inputs."""
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
OUTPUT = ROOT / "assets/reference/battle-rules/battle-spirit-source-manifest.json"


def sound_cues(prefab, asset):
    guid = re.search(r"^guid: (\w+)$", Path(str(asset) + ".meta").read_text(), re.M)[1]
    text = prefab.read_text(encoding="utf-8-sig")
    components = [re.match(r" &(\d+)", block)[1] for block in text.split("--- !u!114")
                  if "\n  SoundEvent:" in block and "guid: " + guid in block]
    component, = components
    times = [float(value) for value in re.findall(
        r"- myTime: ([\d.]+)\s+myPlayable: \{fileID: " + component + r"\}", text)]
    if times != [0.2]:
        raise ValueError("The original battle spirit sound binding or timing changed")
    return times


def build():
    entities = {}
    combat = ROOT / "unity/Assets/Official/Combat"
    for name in ("skill-rules.json", "battle-rules.json"):
        for key, components in json.loads((combat / name).read_bytes())["entities"].items():
            entities.setdefault(key, {}).update(components)
    definitions = {key: copy.deepcopy(entities[key]) for key in ("11260", "11261", "11262", "24214")}
    skill = definitions["11260"]["FightSkillComponent"]
    hit = definitions["11261"]["FightHitFixedComponent"]
    damage = definitions["11262"]["FightDamageComponent"]
    status = definitions["24214"]["FightStatusComponent"]
    link, = skill["HitList"]
    application, = damage["StatusList"]
    if (skill["TargetType"] != 0 or link["ClassId"] != 11261 or abs(link["Delay"] - .2) > .00001
            or hit["DamageId"] != 11262 or hit["DamageProp"] != 58
            or application["StatusId"] != 24214 or application["TargetType"] != 2
            or application["BasePercent"] != 1 or application["AffectedByProp"]
            or status["DurationRound"] != 4 or status["RoundTarget"] != 2 or status["RoundUpdateTiming"] != 1):
        raise ValueError("Review original battle spirit self-hit and status configuration")
    normalized = []
    if "ExcludeCharacterTypes" not in damage["DisperseStatus"]:
        damage["DisperseStatus"]["ExcludeCharacterTypes"] = []
        normalized.append("11262/DisperseStatus/ExcludeCharacterTypes")
    rules = json.loads((combat / "rules.json").read_bytes())
    item, = (row for row in rules["skill"] if row["ClassId"] == 10051)
    if item["EcEntityId"] != 11260:
        raise ValueError("The original battle spirit item binding changed")
    configuration = verify_selected_components(definitions, [item])
    skill_data = json.loads((combat / "skill-rules.json").read_bytes())
    props = [row for row in skill_data["tables"]["entity_prop"] if row["EntityId"] in (11260, 24214)]
    if len(props) != 2 or next(row for row in props if row["EntityId"] == 24214)["PropFactors"] != {"58": 5000, "109": 5000}:
        raise ValueError("The original battle spirit critical bonuses changed")
    with Apk.current() as apk:
        version = apk.version
        code = ROOT / ".tools/source-packages/current" / version / "official-code"
        constructors = Constructors(code)
        mapping = {name: cls for cls, name in re.findall(
            r'new (Tb\w+)\(loader\("([^\"]+)"\)\)', constructors.source('Tables'))}
        obj = apk.asset('config_luban', '/entity_prop.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        rows = json.loads(json.dumps(constructors.table(raw, mapping['entity_prop'])))
        selected = [row for row in rows if row['EntityId'] in (11260, 24214)]
        if len(selected) != 2 or any(not all(next(row for row in selected if row['EntityId'] == prop['EntityId']).get(key) == value
                                             for key, value in prop.items()) for prop in props):
            raise ValueError('Runtime battle spirit properties differ from the selected official table')
        property_source = dict(source=apk.provenance(obj), payload_sha256=hashlib.sha256(raw).hexdigest(),
                               rows=selected, runtime_rows=props)
        obj = apk.asset('config_luban', '/entity_prop_group_level.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        group_rows = json.loads(json.dumps(constructors.table(raw, mapping['entity_prop_group_level'])))
        selected_groups = [row for row in group_rows if row['GroupId'] == 1]
        runtime_groups = [row for row in skill_data['tables']['entity_prop_group_level'] if row['GroupId'] == 1]
        if (len(selected_groups) != len(runtime_groups)
                or any(not any(all(original.get(key) == value for key, value in row.items())
                               for original in selected_groups) for row in runtime_groups)):
            raise ValueError('Runtime battle spirit level groups differ from the selected official table')
        group_source = dict(source=apk.provenance(obj), payload_sha256=hashlib.sha256(raw).hexdigest(),
                            rows=selected_groups, runtime_rows=runtime_groups)
    code = ROOT / ".tools/source-packages/current" / version / "official-code"
    originals = [identity(code / "common-source/Common" / (name + ".cs")) for name in (
        "FightSkillComponentInfo", "FightSkillComponentImplement", "FightHitFixedComponentInfo",
        "FightDamageComponentImplement", "HitScopCfg")]
    originals.extend(identity(code / "client-source" / (name + ".cs")) for name in (
        "MyFightHitBase", "MyFightHitFixed", "MyFightActionSkill", "MyPlayableSound"))
    graph_path = ROOT / "assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json"
    graph = json.loads(graph_path.read_bytes())
    selected = lambda row: "/11260/" in row["path"]
    native = [entry for entry in graph["native_inputs"] if selected(entry)]
    outputs = [entry for entry in graph["outputs"] if selected(entry)]
    if len(native) != 8 or len(outputs) != 8:
        raise ValueError("The original battle spirit graph is incomplete")
    for entry in native + outputs:
        if identity(ROOT / entry["path"]) != entry:
            raise ValueError("The original battle spirit graph identity differs: " + entry["path"])
    status_path = ROOT / "assets/reference/battle-animation/first-promotion-status-assets-source-manifest.json"
    status_graph = json.loads(status_path.read_bytes())
    select_status = lambda row: "/24214/" in row["path"]
    status_native = [entry for entry in status_graph["native_inputs"] if select_status(entry)]
    status_outputs = [entry for entry in status_graph["outputs"] if select_status(entry)]
    if len(status_native) != 4 or len(status_outputs) != 4:
        raise ValueError("The original battle spirit status graph is incomplete")
    for entry in status_native + status_outputs:
        if identity(ROOT / entry["path"]) != entry:
            raise ValueError("Original battle spirit status asset identity differs: " + entry["path"])
    sound_path = ROOT / "assets/reference/battle-animation/native-sound-sources-source-manifest.json"
    event, = (row for row in json.loads(sound_path.read_bytes())["events"] if row["name"] == "Skill_10051")
    if event["id"] != 3811345950 or event["banks"] != ["Battle_SFX_02"]:
        raise ValueError("The original battle spirit sound event changed")
    runtime_sound = ROOT / event["source"]["path"]
    if identity(runtime_sound) != {key: event["source"][key] for key in ("path", "sha256")}:
        raise ValueError("The original battle spirit sound asset differs")
    original_graph, = (ROOT / row["path"] for row in native if row["path"].endswith("/playable11260.prefab"))
    runtime_graph, = (ROOT / row["path"] for row in outputs if row["path"].endswith("/playable11260.prefab"))
    original_sound = original_graph.parents[5] / "Wwise/ScriptableObjects/Event" / (event["guid"] + ".asset")
    original_text = original_sound.read_text(encoding="utf8")
    if (re.search(r"\n  id: (\d+)", original_text)[1] != str(event["id"])
            or re.search(r"\n  objectName: (\S+)", original_text)[1] != event["name"]
            or re.search(r"\n  guid: (\S+)", original_text)[1] != event["guid"]):
        raise ValueError("The original battle spirit sound identity differs")
    if sound_cues(original_graph, original_sound) != sound_cues(runtime_graph, runtime_sound):
        raise ValueError("The runtime battle spirit sound differs from its official prefab")
    catalog = ROOT / "unity/Assets/Official/Combat/NativeSound/Resources/Combat/Wwise/catalog.json"
    if {"id": event["id"], "name": event["name"]} not in json.loads(catalog.read_bytes())["events"]:
        raise ValueError("The original battle spirit sound is absent from the runtime catalog")
    bindings = ROOT / "unity/Assets/Game/Resources/Combat/skill-playables.json"
    cue, = (row for row in json.loads(bindings.read_bytes())["skills"] if row["skill_entity_id"] == 11260)
    if not any(row["kind"] == 1 and row["animation"] == "100/skill_14" for row in cue["playables"]):
        raise ValueError("The original battle spirit character motion is not bound")
    return dict(manifest=version, configuration=configuration, explicit_empty_dispel_defaults=normalized,
        original_code=originals, property_source=property_source, group_source=group_source,
        status_source_manifest=identity(status_path), status_inputs=status_native, status_outputs=status_outputs,
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
            raise ValueError("The battle spirit source receipt differs")
    else:
        OUTPUT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print("Verified official battle spirit, native graph, character motion and sound.")
