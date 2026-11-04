"""Bind current verified monster controller states without guessing state names."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_battle_game_animators import GUID, documents
from prepare_unity_profession_character_resources import meta


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build():
    receipt_path = ROOT / "assets/reference/battle-animation/live-monsters-source-manifest.json"
    receipt = json.loads(receipt_path.read_bytes())
    if receipt["manifest"] != "884_156692":
        raise ValueError("Review the original monster source version")
    inputs = {}

    def read(path):
        key = path.relative_to(ROOT).as_posix()
        raw = path.read_bytes()
        if sha(raw) != receipt["outputs"][key]:
            raise ValueError("Verified original monster input differs: " + key)
        inputs[key] = sha(raw)
        return raw

    root = ROOT / "unity/Assets/Official/Combat/NativeMonsters"
    inventory = json.loads(read(root / "inventory.json"))
    clips = {}
    for path in sorted((root / "animators").rglob("*.anim")):
        fields, = documents(read(path)).values()
        guid = GUID.search(read(Path(str(path) + ".meta")).decode("utf-8"))[1]
        clips[guid] = dict(animation=fields["m_Name"], clip=path.relative_to(root).as_posix())
    models = []
    for row in inventory["models"]:
        docs = documents(read(root / row["nativeController"]))
        controller, = [v for v in docs.values() if "m_AnimatorLayers" in v]
        layer, = controller["m_AnimatorLayers"]
        machine = docs[layer["m_StateMachine"]["fileID"]]
        if machine["m_ChildStateMachines"]:
            raise ValueError("Bind original monster nested state paths")
        states = []
        for child in machine["m_ChildStates"]:
            state = docs[child["m_State"]["fileID"]]
            motion = state["m_Motion"]
            binding = dict(state=layer["m_Name"] + "." + state["m_Name"], animation="", clip="")
            if motion["fileID"]:
                if motion["fileID"] != 7400000:
                    raise ValueError("Bind original monster blend-tree motions")
                binding.update(clips[motion["guid"]])
            states.append(binding)
        models.append(dict(entityId=row["entityId"], states=states))
    target = ROOT / "unity/Assets/Game/Resources/Combat/monster-animator-bindings.json"
    payload = json.dumps(dict(manifest=inventory["manifest"], models=models), ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    target.write_bytes(payload)
    if not Path(str(target) + ".meta").exists():
        meta(target)
    death_receipt_path = ROOT / "assets/reference/battle-animation/character-death-assets-source-manifest.json"
    death_receipt = json.loads(death_receipt_path.read_bytes())
    death_key = "unity/Assets/Official/Combat/CharacterDeath/Resources/Combat/CharacterDeath/Prefabs/Playables/playable_die.prefab"
    death_original = next(value for value in death_receipt["outputs"] if value["path"] == death_key)
    death_raw = (ROOT / death_key).read_bytes()
    if sha(death_raw) != death_original["sha256"]:
        raise ValueError("Verified original death playable differs")
    death_leaf, = [value for value in documents(death_raw).values() if value.get("myBoolParam") == "die"]
    if death_leaf["myBoolValue"] != 1 or death_leaf["myEndResumeParam"] != 1:
        raise ValueError("Review original death parameter ownership")
    proof = dict(manifest=inventory["manifest"], source_manifest_sha256=sha(receipt_path.read_bytes()),
        generator_sha256=sha(Path(__file__).read_bytes()), inputs=inputs,
        outputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in (target, Path(str(target) + ".meta"))},
        official_application=".tools/source-packages/current/884_156692/official-code/client-source/MyPlayableOverrideAnimator.cs",
        official_application_sha256=sha((ROOT / ".tools/source-packages/current/884_156692/official-code/client-source/MyPlayableOverrideAnimator.cs").read_bytes()),
        death_parameter_source=dict(manifest_sha256=sha(death_receipt_path.read_bytes()),
            path=death_key, sha256=sha(death_raw), bool_param="die", bool_value=True, end_resume=True),
        operation="Read verified original Unity state-machine paths and exact motion PPtrs. Retain empty states and duplicate motion states without inventing aliases; bind existing current native clips for original Animator playables.")
    (ROOT / "assets/reference/battle-animation/monster-animator-bindings-source-manifest.json").write_text(
        json.dumps(proof, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print("Bound original monster controllers:", len(models), "states:", sum(len(m["states"]) for m in models))


if __name__ == "__main__":
    build()
