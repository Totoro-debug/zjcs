"""Restore full-binary partners' exact original Unity Animator asset closure."""
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from prepare_battle_game_animators import GUID, documents, verify_graph
from prepare_unity_profession_character_resources import asset, guid, meta


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def verify_partner_motion(original, state, converted, native, native_names):
    trees = state["m_BlendTreeConstantArray"]
    if len(trees) != 1:
        raise ValueError("Review additional original partner motion trees")
    nodes = [row["data"] for row in trees[0]["data"]["m_NodeArray"]]
    strings = dict(original.read_typetree()["m_TOS"])

    def verify(index, pointer):
        node = nodes[index]
        if not node["m_ChildIndices"]:
            clip = original.read().m_AnimationClips[node["m_ClipID"]].read()
            if native_names[pointer["guid"]] != clip.m_Name:
                raise ValueError("Original partner state clip differs")
            return
        target = native[pointer["fileID"]]
        if (node["m_BlendType"] != 0 or target["m_BlendType"] != 0
                or strings[node["m_BlendEventID"]] != target["m_BlendParameter"]
                or node["m_BlendEventYID"] != 4294967295 or target["m_BlendParameterY"]
                or len(node["m_ChildIndices"]) != len(target["m_Childs"])):
            raise ValueError("Original partner 1D blend tree differs")
        thresholds = node["m_Blend1dData"]["data"]["m_ChildThresholdArray"]
        if len(thresholds) != len(target["m_Childs"]):
            raise ValueError("Original partner blend threshold count differs")
        for child_index, threshold, child in zip(node["m_ChildIndices"], thresholds, target["m_Childs"]):
            source = nodes[child_index]
            if (abs(threshold - child["m_Threshold"]) > .0000001
                    or source["m_Duration"] != 1 or child["m_TimeScale"] != 1
                    or source["m_CycleOffset"] != child["m_CycleOffset"]
                    or source["m_Mirror"] != child["m_Mirror"]):
                raise ValueError("Original partner blend child threshold/time/offset/mirror differs")
            verify(child_index, child["m_Motion"])
    verify(0, converted["m_Motion"])


def key_times(value):
    if isinstance(value, dict):
        if "time" in value:
            yield value["time"]
        for child in value.values():
            yield from key_times(child)
    elif isinstance(value, list):
        for child in value:
            yield from key_times(child)


def verify_constant_timelines(value):
    if isinstance(value, dict):
        if "time" in value:
            raise ValueError("Unbound standalone motion key")
        for child in value.values():
            verify_constant_timelines(child)
    elif isinstance(value, list) and value:
        if isinstance(value[0], dict):
            first = value[0]
            if first.get("time", 0) != 0:
                raise ValueError("Original static timeline starts after zero")
            fields = lambda row: {key: child for key, child in row.items() if key not in ("time", "curve")}
            for previous, following in zip(value, value[1:]):
                if previous.get("curve") not in (None, "stepped") or fields(first) != fields(following):
                    raise ValueError("Original zero-length carrier has a changing skeleton pose")
            verify_constant_timelines(fields(first))
        else:
            for child in value:
                verify_constant_timelines(child)


def build():
    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Review the partner Animator source selection")
    payload_root = ROOT / "unity/Assets/Official/Combat/NativePartners"
    payload_catalog = payload_root / "catalog.json"
    fighters = json.loads(payload_catalog.read_bytes())["fighters"]
    cache = ROOT / ".tools/source-packages/current" / apk.version / "native-conversion"
    receipt_path = cache / "native-export-all-source-manifest.json"
    receipt = json.loads(receipt_path.read_bytes())
    if (not receipt["completed"] or receipt["manifest"] != apk.version
            or sha((cache / "source-manifest.json").read_bytes()) != receipt["input_manifest_sha256"]):
        raise ValueError("Native conversion receipt changed")
    export = cache / receipt["export_directory"]
    checked = {row["path"]: row["sha256"] for row in receipt["outputs"]}
    npc_prefix = "ExportedProject/Assets/AppearanceAssets/Npc/"
    native_paths = [export / path for path in checked if path.startswith(npc_prefix)
                    and path.endswith((".controller", ".anim"))]
    by_guid, by_controller = {}, {}
    for path in native_paths:
        metadata = Path(str(path) + ".meta")
        raw = metadata.read_bytes()
        if sha(raw) != checked[metadata.relative_to(export).as_posix()]:
            raise ValueError("Converted Animator metadata changed: " + str(path))
        key = GUID.search(raw.decode("utf-8"))[1]
        if key in by_guid:
            raise ValueError("Duplicate converted NPC Animator GUID")
        by_guid[key] = path
        if path.suffix == ".controller":
            by_controller.setdefault(path.name, []).append(path)

    destination = ROOT / "unity/Assets/Official/Combat/NativePartnerAnimators/Resources/Combat/NativePartnerAnimators"
    plans, records, graphs, entries, duration_differences = {}, {}, {}, [], []
    for fighter in fighters:
        npc_id = fighter["npcId"]
        tree_path = payload_root / fighter["controller"]
        original_tree = json.loads(tree_path.read_bytes())
        animations = json.loads((payload_root / fighter["skeleton"]).read_bytes())["animations"]
        source, = by_controller[original_tree["m_Name"] + ".controller"]
        queue, closure = [source], set()
        while queue:
            path = queue.pop()
            if path in closure:
                continue
            closure.add(path)
            raw = path.read_bytes()
            if sha(raw) != checked[path.relative_to(export).as_posix()]:
                raise ValueError("Converted Animator asset changed: " + str(path))
            queue.extend(by_guid[key] for key in GUID.findall(raw.decode("utf-8")))
        originals = {}
        objects = apk.objects(f"defaultpackage_assets_prefabs_ecentity_fight_fightnpc_{fighter['entityId']}.bundle")
        pointers = [obj.read().m_Controller for obj in objects if obj.type.name == "Animator"]
        controllers = [pointer.deref() for pointer in pointers if pointer.path_id]
        controller, = [obj for obj in controllers if obj.read().m_Name == original_tree["m_Name"]]
        if json.loads(json.dumps(controller.read_typetree())) != original_tree:
            raise ValueError("Archived original partner controller differs: " + str(npc_id))
        originals["AnimatorController", original_tree["m_Name"]] = controller
        for pointer in controller.read().m_AnimationClips:
            obj = pointer.deref()
            key = obj.type.name, obj.read().m_Name
            if key in originals and originals[key].get_raw_data() != obj.get_raw_data():
                raise ValueError("Ambiguous original partner motion")
            originals[key] = obj
        native_documents = {path: documents(path.read_bytes()) for path in closure}
        native_names = {GUID.search(Path(str(path) + ".meta").read_text("utf-8"))[1]:
                        next(iter(native_documents[path].values()))["m_Name"] for path in closure}
        graphs[str(npc_id)] = verify_graph(controller, native_documents[source], native_names,
            verify_partner_motion if npc_id in (11004, 11014) else None)
        for path in closure:
            relative = path.relative_to(export / "ExportedProject/Assets/AppearanceAssets/Npc").as_posix()
            fields = next(iter(native_documents[path].values()))
            kind = "AnimatorController" if path.suffix == ".controller" else "AnimationClip"
            original = originals[kind, fields["m_Name"]]
            if kind == "AnimationClip":
                settings = fields["m_AnimationClipSettings"]
                value = original.read()
                if abs(settings["m_StopTime"] - value.m_MuscleClip.m_StopTime) > .000001:
                    raise ValueError("Original partner motion duration differs: " + relative)
                if fields["m_Name"] not in fighter["animationNames"]:
                    raise ValueError("Original motion missing from partner skeleton: " + relative)
                duration = max(key_times(animations[fields["m_Name"]]), default=0)
                if abs(settings["m_StopTime"] - duration) > 1 / 30 + .00001:
                    if (npc_id, fields["m_Name"], settings["m_StopTime"], duration) != (11033, "200/die_02", 0, .3333333432674408):
                        raise ValueError("Review additional original clip/skeleton duration difference: " + relative)
                    animation = animations[fields["m_Name"]]
                    if "events" in animation:
                        raise ValueError("Review original zero-duration motion events")
                    verify_constant_timelines(animation)
                    duration_differences.append(dict(npc_id=npc_id, animation=fields["m_Name"],
                        original_clip_stop=settings["m_StopTime"], original_skeleton_duration=duration,
                        constant_pose=True, runtime_clock="Original zero-length carrier samples the unchanged skeleton pose at zero."))
            remap = {key: sha(("zjcs:partner-battle-animator:" + target.relative_to(
                        export / "ExportedProject/Assets/AppearanceAssets/Npc").as_posix()).encode())[:32]
                     for key, target in by_guid.items() if target in closure}
            target = destination / relative
            outputs = []
            for first, last in ((path, target), (Path(str(path) + ".meta"), Path(str(target) + ".meta"))):
                raw = GUID.sub(lambda match: "guid: " + remap[match[1]], first.read_text("utf-8")).encode()
                if last in plans and plans[last] != raw:
                    raise ValueError("Inconsistent shared partner Animator asset")
                if last.exists() and last.read_bytes() != raw:
                    raise ValueError("Preserve changed partner Animator asset: " + str(last))
                plans[last] = raw
                outputs.append(dict(path=last.relative_to(ROOT).as_posix(), sha256=sha(raw)))
            record = dict(native=identity(path), original=dict(apk.provenance(original), type=kind,
                name=fields["m_Name"], sha256=sha(original.get_raw_data())), outputs=outputs)
            if relative in records and records[relative] != record:
                raise ValueError("Original shared partner Animator identity differs")
            records[relative] = record
        entries.append(dict(NpcId=npc_id, Controller=dict(fileID=9100000,
            guid=sha(("zjcs:partner-battle-animator:" + source.relative_to(
                export / "ExportedProject/Assets/AppearanceAssets/Npc").as_posix()).encode())[:32], type=2)))
        print("Verified original partner Animator", npc_id, "assets", len(closure), flush=True)
    folders = set()
    for path, raw in plans.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        parent = path.parent
        while parent.is_relative_to(ROOT / "unity/Assets/Official/Combat/NativePartnerAnimators"):
            metadata = Path(str(parent) + ".meta")
            if not metadata.exists():
                digest = sha(("zjcs:partner-battle-animator-folder:" + parent.relative_to(ROOT).as_posix()).encode())[:32]
                metadata.write_text("fileFormatVersion: 2\nguid: " + digest +
                    "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n", encoding="utf-8", newline="\n")
            folders.add(metadata)
            parent = parent.parent
    runtime = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/PartnerNativeBattleAnimatorCatalog.cs"
    meta(runtime)
    catalog = destination / "catalog.asset"
    asset(catalog, "Current original partner battle Animators", runtime, dict(entries=entries))
    outputs = [runtime, Path(str(runtime) + ".meta"), catalog, Path(str(catalog) + ".meta")]
    validation = ROOT / "unity/Assets/Game/Editor/ProfessionUI/OfficialPartnerNativeBattleAnimatorAcceptance.cs"
    if not Path(str(validation) + ".meta").exists():
        meta(validation)
    inputs = [validation, Path(str(validation) + ".meta"),
        ROOT / "scripts/prepare_partner_native_battle_validation.py",
        ROOT / "unity/Assets/Game/Editor/ProfessionUI/OfficialPartnerNativeBattleDataValidation.cs",
        ROOT / "unity/Assets/Game/Editor/ProfessionUI/OfficialPartnerNativeBattlePoseAcceptance.cs",
        ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeAnimatorPose.cs"]
    proof = dict(manifest=apk.version, native_receipt=identity(receipt_path), payload_catalog=identity(payload_catalog),
        generator=identity(Path(__file__).resolve()), verifier=identity(ROOT / "scripts/prepare_battle_game_animators.py"),
        graphs=graphs, original_duration_differences=duration_differences,
        assets=[records[key] for key in sorted(records)], folder_metadata=[identity(path) for path in sorted(folders)],
        outputs=[identity(path) for path in outputs], inputs=[identity(path) for path in inputs],
        operation="Copy exact converted original controllers and clip closure. Remap external asset GUIDs and metadata GUIDs only. Verify original parameter order/types/defaults, layers, default states, state speed/offset/mirror/write-defaults/IK, state motions, ordered transition conditions/targets/timings/interruption flags and clip durations against the compiled official assets.",
        binding="Combat/NativePartnerAnimators/catalog: explicit NPC ID to original native RuntimeAnimatorController.",
        limitations=["Assets require the independent native skeleton/Mecanim pose consumer. This catalog alone does not enable formal combat candidates.",
            "Original 11033 200/die_02 carrier clip has zero duration while its skeleton keys end at 0.3333333432674408 seconds. Every channel is constant; preserve both source assets and the zero carrier clock. Formal battle playback remains unvalidated."])
    path = ROOT / "assets/reference/partner-native-battle-animators-source-manifest.json"
    path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Recovered", len(entries), "partner controllers and", len(records) - len(entries), "original clips.")


if __name__ == "__main__":
    build()
