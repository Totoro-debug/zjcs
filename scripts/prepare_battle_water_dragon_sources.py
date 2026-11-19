"""Verify the original water-dragon rules, graph, animation and sound bindings."""
import argparse
import copy
import hashlib
import json
import re
import subprocess
from pathlib import Path

from official_apk import Apk
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "assets/reference/battle-rules/water-dragon-source-manifest.json"


def local_identity(path, index):
    if not index:
        return identity(path)
    relative = path.relative_to(ROOT).as_posix()
    data = subprocess.check_output(["git", "show", ":" + relative], cwd=ROOT)
    return {"path": relative, "sha256": hashlib.sha256(data).hexdigest()}


def sound_cues(prefab, asset):
    guid = re.search(r"^guid: (\w+)$", Path(str(asset) + ".meta").read_text(encoding="utf-8-sig"), re.M)[1]
    text = prefab.read_text(encoding="utf-8-sig")
    components = [re.match(r" &(\d+)", block)[1] for block in text.split("--- !u!114")
                  if "\n  SoundEvent:" in block and "guid: " + guid in block]
    return sorted(float(value) for component in components for value in re.findall(
        r"- myTime: ([\d.]+)\s+myPlayable: \{fileID: " + component + r"\}", text))


def verify_effect_rows(prefab):
    text = prefab.read_text(encoding="utf-8-sig")
    blocks = re.split(r"(?=^--- !u!)", text, flags=re.M)
    names = {}
    scripts = {}
    for block in blocks:
        header = re.match(r"--- !u!(\d+) &(\d+)", block)
        if not header:
            continue
        kind, file_id = header.groups()
        if kind == "1":
            name = re.search(r"^  m_Name: (.*)$", block, re.M)
            if name:
                names[file_id] = name[1]
        elif kind == "114":
            scripts[file_id] = block
    sequence, = (block for block in scripts.values() if "myPlayables:" in block)
    rows = {}
    for time, file_id in re.findall(r"- myTime: ([\d.]+)\s+myPlayable: \{fileID: (\d+)\}", sequence):
        block = scripts[file_id]
        game_object = re.search(r"m_GameObject: \{fileID: (\d+)", block)[1]
        name = names.get(game_object, "")
        if name not in {f"effect ({index})" for index in range(2, 6)}:
            continue
        wave = int(name[-2]) - 2
        offset = re.search(r"myOffset: \{x: ([\d.-]+), y: ([\d.-]+), z: ([\d.-]+)\}", block)
        if (not offset or tuple(map(float, offset.groups())) != (0.0, float(wave + 1), 0.0)
                or not re.search(r"AttachTo: 2\s+myBoneName: body", block)):
            raise ValueError("The original water-dragon effect attachment changed")
        rows[wave] = float(time)
    if rows != {0: .65, 1: .75, 2: .85, 3: .95}:
        raise ValueError("The four original water-dragon effects changed")


def build(use_index):
    entities = {}
    combat = ROOT / "unity/Assets/Official/Combat"
    for name in ("skill-rules.json", "battle-rules.json"):
        for key, components in json.loads((combat / name).read_bytes())["entities"].items():
            entities.setdefault(key, {}).update(components)
    definitions = {key: copy.deepcopy(entities[key]) for key in
                   ("10550", "10551", "10552", "10553", "10554", "10555", "24103")}
    skill = definitions["10550"]["FightSkillComponent"]
    roots = skill["HitList"]
    if (skill["TargetType"] != 1 or len(roots) != 4
            or [root["ClassId"] for root in roots] != [10551, 10552, 10553, 10554]
            or any(abs(root["Delay"] - (.75 + index * .1)) > .00001 for index, root in enumerate(roots))):
        raise ValueError("The original water-dragon skill roots changed")
    for index, root in enumerate(roots):
        hit = definitions[str(root["ClassId"])]["FightHitFixedComponent"]
        cells = {(entry["Pos"]["X"], entry["Pos"]["Y"]) for entry in hit["Scope"]}
        if (root["ActOnSouce"] != 1 or hit["DamageId"] != 10555 or len(hit["Scope"]) != 3
                or cells != {(x, index + 1) for x in (-1, 0, 1)}
                or any(entry["Delay"] != 0 for entry in hit["Scope"])):
            raise ValueError("The original water-dragon hit row changed")
    damage = definitions["10555"]["FightDamageComponent"]
    normalized = []
    if "ExcludeCharacterTypes" not in damage["DisperseStatus"]:
        damage["DisperseStatus"]["ExcludeCharacterTypes"] = []
        normalized.append("10555/DisperseStatus/ExcludeCharacterTypes")
    falloff = definitions["24103"]["FightStatusDamageFalloffComponent"]
    if (not any(entry["TargetType"] == 2 and entry["StatusId"] == 24103
                and entry["BasePercent"] == 1 for entry in damage["StatusList"])
            or falloff["FalloffPercent"] != 2500 or falloff["FalloffTarget"] != 1
            or falloff["NumOfStart"] != 1 or falloff["RootSkillIds"] != [10550]
            or not falloff["RemoveOnSourceSkillStart"]):
        raise ValueError("The original repeated-hit falloff changed")
    rules = json.loads((combat / "rules.json").read_bytes())
    item, = (row for row in rules["skill"] if row["ClassId"] == 10003)
    if item["EcEntityId"] != 10550:
        raise ValueError("The original water-dragon item binding changed")
    configuration = verify_selected_components(definitions, [item])
    with Apk.current() as apk:
        version = apk.version
    code = ROOT / ".tools/source-packages/current" / version / "official-code"
    originals = [identity(code / "common-source/Common" / (name + ".cs")) for name in (
        "FightSkillComponentInfo", "FightHitFixedComponentInfo", "FightDamageComponentInfo",
        "FightStatusDamageFalloffComponent", "FightStatusDamageFalloffComponentInfo", "HitScopCfg")]
    originals.extend(identity(code / "client-source" / (name + ".cs")) for name in (
        "MyFightHitBase", "MyFightHitFixed", "MyFightStatusDamageFalloff", "MyPlayableSound"))
    graph_path = ROOT / "assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json"
    graph_data = (subprocess.check_output(["git", "show", ":" + graph_path.relative_to(ROOT).as_posix()], cwd=ROOT)
                  if use_index else graph_path.read_bytes())
    graph = json.loads(graph_data)
    native = [row for row in graph["native_inputs"] if "/10550/" in row["path"]]
    outputs = [row for row in graph["outputs"] if "/10550/" in row["path"]]
    if len(native) != 14 or len(outputs) != 14:
        raise ValueError("The original water-dragon graph is incomplete")
    for row in native + outputs:
        if identity(ROOT / row["path"]) != row:
            raise ValueError("The original water-dragon graph differs: " + row["path"])
    original_graph, = (ROOT / row["path"] for row in native if row["path"].endswith("/playable10550.prefab"))
    runtime_graph, = (ROOT / row["path"] for row in outputs if row["path"].endswith("/playable10550.prefab"))
    verify_effect_rows(original_graph)
    verify_effect_rows(runtime_graph)
    sound_path = ROOT / "assets/reference/battle-animation/native-sound-sources-source-manifest.json"
    sound = json.loads(sound_path.read_bytes())
    sound_inputs = []
    for name, expected in (("Skill_10003_1", [0.0]), ("Skill_10003_2", [.65]),
                           ("Skill_10003_3", [.75, .85, .95])):
        event, = (row for row in sound["events"] if row["name"] == name)
        runtime = ROOT / event["source"]["path"]
        original = original_graph.parents[5] / "Wwise/ScriptableObjects/Event" / (event["guid"] + ".asset")
        if sound_cues(original_graph, original) != expected or sound_cues(runtime_graph, runtime) != expected:
            raise ValueError("The original water-dragon sound cues changed: " + name)
        sound_inputs.extend(identity(path) for path in (original, Path(str(original) + ".meta"),
                                                        runtime, Path(str(runtime) + ".meta")))
    bindings = ROOT / "unity/Assets/Game/Resources/Combat/skill-playables.json"
    cue, = (row for row in json.loads(bindings.read_bytes())["skills"] if row["skill_entity_id"] == 10550)
    if not any(row["kind"] == 1 and row["animation"] == "200/skill_07" for row in cue["playables"]):
        raise ValueError("The original water-dragon character motion is not bound")
    local = [ROOT / path for path in (
        "unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs",
        "unity/Assets/Game/Runtime/Official/Combat/FightDamageFalloff.cs",
        "unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleLiveImpactSession.cs",
        "unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMountainWaveImpact.cs",
        "unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleHitPlaybackAcceptance.cs",
        "scripts/fixtures/battle_water_dragon_harness.cs", "scripts/test_battle_water_dragon.ps1")]
    return dict(manifest=version, configuration=configuration, explicit_empty_dispel_defaults=normalized, original_code=originals,
                native_source_manifest=local_identity(graph_path, use_index), native_inputs=native, native_outputs=outputs,
                sound_source_manifest=local_identity(sound_path, use_index), sound_inputs=sound_inputs,
                character_binding=identity(bindings), migration=[local_identity(path, use_index) for path in local],
                generator=local_identity(Path(__file__), use_index))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--index", action="store_true", help="Use staged local bindings and source manifests")
    args = parser.parse_args()
    proof = build(args.index)
    if args.check:
        if not OUTPUT.exists() or json.loads(OUTPUT.read_bytes()) != proof:
            raise ValueError("The water-dragon source receipt differs")
    else:
        OUTPUT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Verified official water-dragon rules, graph, character motion and sound.")
