"""Recover the official quick-chat function-entry Animator and serialized bindings."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from official_animation import mecanim_tracks
from official_unity_schema import ScriptSchemas
from refresh_current_ui_graphics import object_path


def sha(data):
    return hashlib.sha256(data).hexdigest()


def build():
    source_path = ROOT / "assets/reference/battle-hud/source-manifest.json"
    runtime_path = ROOT / "unity/Assets/Official/Combat/HUD/panels-runtime.json"
    source = json.loads(source_path.read_bytes())["prefabs"]["main"]
    nodes = next(p["nodes"] for p in json.loads(runtime_path.read_bytes())["prefabs"] if p["key"] == "main")
    apk, schemas = Apk(), ScriptSchemas()
    inputs = {}

    def record(obj):
        info = apk.provenance(obj)
        info["object_sha256"] = sha(obj.get_raw_data())
        inputs[(info["bundle"], info["path_id"])] = info
        return obj

    def reference(owner, data):
        # Serialized UI references here are local to their containing CAB.
        if data["m_FileID"] != 0:
            raise ValueError("Unexpected external UI field reference")
        return record(owner.assets_file.objects[data["m_PathID"]])

    def path(obj):
        basic = obj.read(check_read=False)
        go = obj if obj.type.name == "GameObject" else basic.m_GameObject.deref()
        value, root = object_path(go)
        if root != str(source["path_id"]):
            raise ValueError("Function entry reference escaped the official HUD prefab")
        return value

    entries = []
    for obj in apk.objects(source["bundle"]):
        if obj.type.name != "MonoBehaviour":
            continue
        basic = obj.read(check_read=False)
        if not basic.m_Script.path_id or basic.m_Script.read().m_ClassName != "ChatQuickMsgEntryPanel":
            continue
        record(obj)
        data = schemas.read(obj)
        owner = path(obj)
        tree_nodes = [dict(n, children=[]) for n in nodes if n["path"] == owner or n["path"].startswith(owner + "/")]
        by_path = {n["path"]: n for n in tree_nodes}
        for node in tree_nodes[1:]:
            by_path[node["path"].rsplit("/", 1)[0]]["children"].append(node)
        refs = data["references"]["RefIds"]
        state_refs = data["StateSwitcher"]["mStateConfigs"]
        if len(state_refs) != 1:
            raise ValueError("Unsupported official state config count")
        config = next(r for r in refs if r["rid"] == state_refs[0]["rid"])
        if config["type"]["class"] != "StateAnimatorConfig":
            raise ValueError("Unsupported official state config")
        state = config["data"]["mStateAnimatorData"]
        animator_obj = reference(obj, state["Animator"])
        animator = animator_obj.read()
        if path(animator_obj) != owner:
            raise ValueError("Animator must be attached to the official entry root")
        controller_obj = record(animator.m_Controller.deref())
        controller = controller_obj.read_typetree()
        names = dict(controller["m_TOS"])
        constants = controller["m_Controller"]
        values = constants["m_Values"]["data"]["m_ValueArray"]
        if len(values) != 1 or values[0]["m_Type"] != 3 or len(constants["m_LayerArray"]) != 1:
            raise ValueError("Expected the original single-layer integer Fold controller")
        parameter = names[values[0]["m_ID"]]
        defaults = constants["m_DefaultValues"]["data"]
        default_int = defaults["m_IntValues"][values[0]["m_Index"]]
        states_to_int = {p["State"]["mValue"]: p["Parameter"]["IntValue"] for p in state["Parameters"]}
        if any(p["Parameter"]["ParameterType"] != 3 or p["Parameter"]["ParameterName"] != parameter for p in state["Parameters"]):
            raise ValueError("Unexpected StateSwitcher parameter")
        clips, absent = [], []
        for pointer in controller_obj.read().m_AnimationClips:
            clip_obj = record(pointer.deref())
            clip = clip_obj.read()
            bindings = clip_obj.read_typetree()["m_ClipBindingConstant"]["genericBindings"]
            if any(b["isIntCurve"] or b["isPPtrCurve"] or b["typeID"] not in (1, 4) for b in bindings):
                raise ValueError("Unexpected native curve binding kind")
            tracks, duration, loop, unknown = mecanim_tracks(clip_obj, tree_nodes[0])
            # The shared Fold clip also animates three optional transforms absent
            # from this prefab. Preserve their hashes as evidence, never invent paths.
            if any(u["reason"] != "target_absent" for u in unknown):
                raise ValueError("Unresolved official animation property")
            absent.extend({"clip": clip.m_Name, **u} for u in unknown)
            clips.append({"name": clip.m_Name, "duration": duration, "loop": loop, "frameRate": clip.m_SampleRate,
                          "tracks": [{"path": t["path"][len(owner) + 1:], "property": t["property"],
                                      "keys": [dict(zip(("time", "value", "inTangent", "outTangent"), k)) for k in t["keys"]]} for t in tracks],
                          "events": [{"functionName": e.functionName, "time": e.time,
                                      "data": e.data, "floatParameter": e.floatParameter,
                                      "intParameter": e.intParameter, "messageOptions": e.messageOptions} for e in clip.m_Events]})
        machines = constants["m_StateMachineArray"]
        if len(machines) != 1:
            raise ValueError("Unexpected number of controller state machines")
        machine = machines[0]["data"]
        if machine["m_AnyStateTransitionConstantArray"]:
            raise ValueError("Unexpected AnyState transition")
        states = []
        for wrapped in machine["m_StateConstantArray"]:
            row = wrapped["data"]
            leaves = [n["data"] for t in row["m_BlendTreeConstantArray"] for n in t["data"]["m_NodeArray"]]
            if len(leaves) != 1 or leaves[0]["m_ChildIndices"]:
                raise ValueError("Unexpected blend tree")
            transitions = []
            for item in row["m_TransitionConstantArray"]:
                t = item["data"]
                transitions.append({"destination": t["m_DestinationState"], "duration": t["m_TransitionDuration"],
                                    "fixedDuration": t["m_HasFixedDuration"], "exitTime": t["m_ExitTime"],
                                    "hasExitTime": t["m_HasExitTime"], "offset": t["m_TransitionOffset"],
                                    "canTransitionToSelf": t["m_CanTransitionToSelf"],
                                    "interruptionSource": t["m_InterruptionSource"], "orderedInterruption": t["m_OrderedInterruption"],
                                    "conditions": [{"mode": c["data"]["m_ConditionMode"], "parameter": names[c["data"]["m_EventID"]],
                                                    "threshold": c["data"]["m_EventThreshold"]} for c in t["m_ConditionConstantArray"]]})
            states.append({"name": names[row["m_NameID"]], "clip": leaves[0]["m_ClipID"],
                           "speed": row["m_Speed"], "writeDefaultValues": row["m_WriteDefaultValues"],
                           "transitions": transitions})
        entries.append({"owner": owner, "emojiPanel": path(reference(obj, data["EmojiPanel"])),
                        "entryButtons": [path(reference(obj, p)) for p in data["EntryBtns"]],
                        "selfRects": [path(reference(obj, p)) for p in data["SelfRect"]],
                        "ignoreWindows": bool(data["mIsInogreWindows"]), "controllerName": controller["m_Name"],
                        "parameter": parameter, "defaultInt": default_int, "foldedInt": states_to_int[0],
                        "expandedInt": states_to_int[1], "defaultState": machine["m_DefaultState"],
                        "clips": clips, "states": states, "absentTargets": absent})
    if len(entries) != 1:
        raise ValueError("Expected exactly one battle quick-chat entry")
    payload = (json.dumps({"manifest": "854_156383", "entries": entries}, ensure_ascii=False, separators=(",", ":")) + "\n").encode()
    output = ROOT / "unity/Assets/Game/Resources/Combat/battle-function-entry.json"
    receipt = ROOT / "assets/reference/battle-hud/function-entry-source-manifest.json"
    if output.exists() and sha(output.read_bytes()) != json.loads(receipt.read_bytes())["output_sha256"]:
        raise ValueError("Refusing to overwrite modified function-entry bindings")
    output.write_bytes(payload)
    code = ["client-source/FunctionEntrysPanelBase.cs", "client-source/ChatQuickMsgEntryPanel.cs", "client-source/UIUtils.cs",
            "framework-source/StateSwitcher.cs", "framework-source/StateAnimatorConfig.cs", "framework-source/StateAnimatorData.cs"]
    metadata = {"manifest": "854_156383", "output": output.relative_to(ROOT).as_posix(), "output_sha256": sha(payload),
                "generator_sha256": sha(Path(__file__).read_bytes()), "animation_decoder_sha256": sha((ROOT / "scripts/official_animation.py").read_bytes()),
                "prefab_manifest_sha256": sha(source_path.read_bytes()), "runtime_prefab_sha256": sha(runtime_path.read_bytes()),
                "inputs": list(inputs.values()), "official_code": {p: sha((ROOT / ".tools/source-packages/official-code" / p).read_bytes()) for p in code},
                "operation": "Restore the pinned prefab's state bindings and original Animator states, parameters, transitions, curves and events as Unity native assets.",
                "absent_targets": entries[0]["absentTargets"]}
    receipt.write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return len(entries)


if __name__ == "__main__":
    print("Recovered battle function entries:", build())
