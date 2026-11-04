"""Recover the selected official Game-phase controllers and their exact clip closure."""
import hashlib
import json
from pathlib import Path
import re

import yaml
from official_apk import Apk, ROOT

GUID = re.compile(r"guid: ([a-f0-9]{32})")
NAMES = ("Male_Zhanshi_Controller.controller", "Male_Fashi_Controller.controller",
         "Female_Zhanshi_Controller.overrideController", "Female_Fashi_Controller.overrideController")
DESTINATION = ROOT / "unity/Assets/Official/Combat/GameAnimators/Resources/Combat/GameAnimators"


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def documents(raw):
    text = raw.decode("utf8")
    ids = re.findall(r"(?m)^--- !u!\d+ &(-?\d+)", text)
    text = re.sub(r"(?m)^%[^\n]*\n", "", text)
    text = re.sub(r"(?m)^--- !u!\d+ &-?\d+", "---", text)
    return {int(key): next(iter(value.values())) for key, value in zip(ids, yaml.safe_load_all(text))}


def verify_graph(original, native, native_names, verify_motion=None):
    tree = original.read_typetree()
    strings = dict(tree["m_TOS"])
    constant = tree["m_Controller"]
    main = native[9100000]
    expected = [(strings[row["m_ID"]], row["m_Type"]) for row in constant["m_Values"]["data"]["m_ValueArray"]]
    if expected != [(row["m_Name"], row["m_Type"]) for row in main["m_AnimatorParameters"]]:
        raise ValueError("Original Animator parameter order/types differ")
    defaults = constant["m_DefaultValues"]["data"]
    for row, parameter in zip(constant["m_Values"]["data"]["m_ValueArray"], main["m_AnimatorParameters"]):
        group, field = {1: ("m_FloatValues", "m_DefaultFloat"), 3: ("m_IntValues", "m_DefaultInt"),
                        4: ("m_BoolValues", "m_DefaultBool"), 9: ("m_BoolValues", "m_DefaultBool")}[row["m_Type"]]
        if defaults[group][row["m_Index"]] != parameter[field]:
            raise ValueError("Original Animator parameter default differs")
    if len(constant["m_LayerArray"]) != len(main["m_AnimatorLayers"]):
        raise ValueError("Original Animator layer count differs")
    result = []
    for layer_constant, layer in zip(constant["m_LayerArray"], main["m_AnimatorLayers"]):
        machine = constant["m_StateMachineArray"][layer_constant["data"]["m_StateMachineIndex"]]["data"]
        state_constants = [row["data"] for row in machine["m_StateConstantArray"]]
        state_names = [strings[row["m_NameID"]] for row in state_constants]
        native_machine = native[layer["m_StateMachine"]["fileID"]]
        states = {native[row["m_State"]["fileID"]]["m_Name"]: native[row["m_State"]["fileID"]] for row in native_machine["m_ChildStates"]}
        if set(states) != set(state_names) or len(states) != len(state_constants):
            raise ValueError("Original Animator state names differ")
        if native[native_machine["m_DefaultState"]["fileID"]]["m_Name"] != state_names[machine["m_DefaultState"]]:
            raise ValueError("Original Animator default state differs")

        def transitions(constants, pointers):
            if len(constants) != len(pointers):
                raise ValueError("Original Animator transition count differs")
            for row, pointer in zip(constants, pointers):
                source, target = row["data"], native[pointer["fileID"]]
                fields = {"m_TransitionDuration": "m_TransitionDuration", "m_TransitionOffset": "m_TransitionOffset",
                          "m_ExitTime": "m_ExitTime", "m_HasExitTime": "m_HasExitTime",
                          "m_HasFixedDuration": "m_HasFixedDuration", "m_InterruptionSource": "m_InterruptionSource",
                          "m_OrderedInterruption": "m_OrderedInterruption", "m_CanTransitionToSelf": "m_CanTransitionToSelf"}
                for first, second in fields.items():
                    equal = (abs(source[first] - target[second]) <= 0.0000001 if isinstance(source[first], float)
                             else source[first] == target[second])
                    if not equal:
                        raise ValueError("Original Animator transition field differs: " + first + ": " + repr((source[first], target[second])))
                if native[target["m_DstState"]["fileID"]]["m_Name"] != state_names[source["m_DestinationState"]]:
                    raise ValueError("Original Animator transition target differs")
                conditions = [(value["data"]["m_ConditionMode"], strings[value["data"]["m_EventID"]], value["data"]["m_EventThreshold"])
                              for value in source["m_ConditionConstantArray"]]
                if conditions != [(value["m_ConditionMode"], value["m_ConditionEvent"], value["m_EventTreshold"]) for value in target["m_Conditions"]]:
                    raise ValueError("Original Animator transition conditions/order differ")

        transitions(machine["m_AnyStateTransitionConstantArray"], native_machine["m_AnyStateTransitions"])
        for row, name in zip(state_constants, state_names):
            for field in ("m_Speed", "m_CycleOffset", "m_IKOnFeet", "m_WriteDefaultValues", "m_Mirror"):
                if row[field] != states[name][field]:
                    raise ValueError("Original Animator state field differs: " + name + "/" + field)
            if verify_motion is not None:
                verify_motion(original, row, states[name], native, native_names)
            else:
                trees = row["m_BlendTreeConstantArray"]
                if len(trees) != 1 or len(trees[0]["data"]["m_NodeArray"]) != 1:
                    raise ValueError("Verify additional original blend tree: " + name)
                node = trees[0]["data"]["m_NodeArray"][0]["data"]
                if node["m_ChildIndices"]:
                    raise ValueError("Verify original blended state motion: " + name)
                clip = original.read().m_AnimationClips[node["m_ClipID"]].read()
                if native_names[states[name]["m_Motion"]["guid"]] != clip.m_Name:
                    raise ValueError("Original Animator state clip differs: " + name)
            transitions(row["m_TransitionConstantArray"], states[name]["m_Transitions"])
        result.append(dict(layer=layer["m_Name"], states=state_names,
            transitions=len(machine["m_AnyStateTransitionConstantArray"]) + sum(len(row["m_TransitionConstantArray"]) for row in state_constants)))
    return dict(parameters=[dict(name=name, type=kind) for name, kind in expected], layers=result)


def build():
    apk = Apk.current()
    cache = ROOT / ".tools/source-packages/current" / apk.version / "native-conversion"
    receipt_path = cache / "native-export-all-source-manifest.json"
    receipt_raw = receipt_path.read_bytes()
    receipt = json.loads(receipt_raw)
    input_receipt = cache / "source-manifest.json"
    if receipt["manifest"] != apk.version or not receipt["completed"] or sha(input_receipt.read_bytes()) != receipt["input_manifest_sha256"]:
        raise ValueError("Native conversion receipt/version changed")
    export = cache / receipt["export_directory"]
    checked = {row["path"]: row["sha256"] for row in receipt["outputs"]}
    source = export / "ExportedProject/Assets/AppearanceAssets/Character/Model"
    by_guid = {}
    for path in source.rglob("*.meta"):
        match = GUID.search(path.read_text("utf8"))
        if match:
            if match[1] in by_guid:
                raise ValueError("Converted Animator GUID collision")
            by_guid[match[1]] = path.with_suffix("")
    queue = [source / "Anim" / name for name in NAMES]
    closure = set()
    while queue:
        path = queue.pop()
        if path in closure:
            continue
        closure.add(path)
        for digest in GUID.findall(path.read_text("utf8")):
            queue.append(by_guid[digest])
    remap = {GUID.search(Path(str(path) + ".meta").read_text("utf8"))[1]:
             hashlib.sha256(("zjcs:game-animators:" + path.relative_to(source).as_posix()).encode()).hexdigest()[:32] for path in closure}
    for path in closure:
        for item in (path, Path(str(path) + ".meta")):
            if sha(item.read_bytes()) != checked[item.relative_to(export).as_posix()]:
                raise ValueError("Converted Animator asset bytes changed: " + str(item))
    native_names = {GUID.search(Path(str(path) + ".meta").read_text("utf8"))[1]:
                    next(iter(documents(path.read_bytes()).values()))["m_Name"] for path in closure}
    bundle = "defaultpackage_assets_appearanceassets_character_model_anim.bundle"
    roots = {Path(path).name: pointer.deref() for path, pointer in apk.container(bundle) if Path(path).name in NAMES}
    if set(roots) != set(NAMES):
        raise ValueError("Original Game-phase controller containers missing")
    originals = {}
    graphs = {}

    def register(obj):
        value = obj.read()
        key = obj.type.name, value.m_Name
        if key in originals and originals[key].get_raw_data() != obj.get_raw_data():
            raise ValueError("Ambiguous original Animator asset: " + repr(key))
        originals[key] = obj
        if obj.type.name == "AnimatorController":
            for pointer in value.m_AnimationClips:
                register(pointer.deref())

    for name, obj in roots.items():
        register(obj)
        if obj.type.name == "AnimatorController":
            graphs[name] = verify_graph(obj, documents((source / "Anim" / name).read_bytes()), native_names)
        else:
            fields = next(iter(documents((source / "Anim" / name).read_bytes()).values()))
            value = obj.read()
            if native_names[fields["m_Controller"]["guid"]] != value.m_Controller.read().m_Name:
                raise ValueError("Original override base controller differs")
            if len(value.m_Clips) != len(fields["m_Clips"]):
                raise ValueError("Original clip override count differs")
            for pair, converted in zip(value.m_Clips, fields["m_Clips"]):
                for field in ("m_OriginalClip", "m_OverrideClip"):
                    pointer = getattr(pair, field)
                    reference = converted[field]
                    if pointer.m_PathID == 0:
                        if reference["fileID"] != 0:
                            raise ValueError("Original null clip override differs")
                    elif native_names[reference["guid"]] != pointer.read().m_Name:
                        raise ValueError("Original clip override differs")
    plan, records = [], []
    for path in sorted(closure):
        fields = next(iter(documents(path.read_bytes()).values()))
        kind = "AnimationClip" if path.suffix == ".anim" else "AnimatorController" if path.suffix == ".controller" else "AnimatorOverrideController"
        original = originals[kind, fields["m_Name"]]
        if kind == "AnimationClip" and abs(fields["m_AnimationClipSettings"]["m_StopTime"] - original.read().m_MuscleClip.m_StopTime) > 0.000001:
            raise ValueError("Original Animator clip duration differs")
        target = DESTINATION / path.relative_to(source)
        outputs = {}
        for first, last in ((path, target), (Path(str(path) + ".meta"), Path(str(target) + ".meta"))):
            raw = GUID.sub(lambda match: "guid: " + remap[match[1]], first.read_text("utf8")).encode("utf8")
            if last.exists() and last.read_bytes() != raw:
                raise ValueError("Refusing changed existing Game Animator asset: " + str(last))
            plan.append((last, raw))
            outputs[last.relative_to(ROOT).as_posix()] = sha(raw)
        records.append(dict(native_source=path.relative_to(ROOT).as_posix(), native_sha256=sha(path.read_bytes()),
            original=dict(apk.provenance(original), type=kind, name=fields["m_Name"], sha256=sha(original.get_raw_data())), outputs=outputs))
    folders = {}
    for path, raw in plan:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        parent = path.parent
        while parent.is_relative_to(ROOT / "unity/Assets/Official/Combat/GameAnimators"):
            metadata = Path(str(parent) + ".meta")
            if not metadata.exists():
                digest = hashlib.sha256(("zjcs:game-animator-folder:" + parent.relative_to(ROOT).as_posix()).encode()).hexdigest()[:32]
                metadata.write_text("fileFormatVersion: 2\nguid: " + digest + "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n", "utf8", newline="\n")
            folders[metadata.relative_to(ROOT).as_posix()] = sha(metadata.read_bytes())
            parent = parent.parent
    evidence = dict(manifest=apk.version, apk_manifest=apk.source.apk_version, apk_sha256=apk.source.apk_sha256,
        native_receipt=dict(path=receipt_path.relative_to(ROOT).as_posix(), sha256=sha(receipt_raw)),
        generator_sha256=sha(Path(__file__).read_bytes()), graphs=graphs, assets=records, folder_metadata=folders,
        application_sources=[dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes())) for path in (
            cache.parent / "official-code/client-source/AppearanceSystem.Runtime/AppearanceAddressUtils.cs",
            cache.parent / "official-code/client-source/AppearanceSystem/PlayerAnimatorAppearance.cs")],
        operation="Copy verified native Game-phase controllers and exact dependency closure; change external GUIDs and asset metadata GUIDs only. Check original parameter order/types/defaults, state names/scalars/defaults/motions, ordered transitions/conditions and override bindings against the compiled official controller. Float comparisons tolerate at most 1e-7 for YAML decimal representation.",
        limitations=["Spine Mecanim name-carrier clips retain their original curves; they require the independent native skeleton consumer. Importing controllers alone does not restore player actions. Existing baked preview controllers and gameplay bindings remain unchanged."])
    destination = ROOT / "assets/reference/battle-animation/game-animators-source-manifest.json"
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", "utf8", newline="\n")
    print("Recovered official Game Animator closure:", len(closure))


if __name__ == "__main__":
    build()
