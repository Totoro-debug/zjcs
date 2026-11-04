"""Recover current monsters' original entity AnimationController bindings."""
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / ".tools/python-packages"))
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from prepare_battle_live_monsters import entity_prefab_family
from prepare_unity_profession_character_resources import meta
from UnityPy.classes import PPtr


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def build():
    inventory = ROOT / "unity/Assets/Official/Combat/NativeMonsters/inventory.json"
    receipt = ROOT / "assets/reference/battle-animation/live-monsters-source-manifest.json"
    selected = json.loads(inventory.read_bytes())
    if digest(inventory.read_bytes()) != json.loads(receipt.read_bytes())["outputs"][inventory.relative_to(ROOT).as_posix()]:
        raise ValueError("Verified original monster inventory differs")
    apk, schemas = Apk.current(), CurrentSchemas()
    if apk.version != selected["manifest"] or apk.version != "884_156692":
        raise ValueError("Review original monster controller source versions")
    rows, originals = [], []
    try:
        for row in selected["models"]:
            entity = row["entityId"]
            family = entity_prefab_family(apk, entity)
            objects = apk.objects(f"prefabs_ecentity_fight_{family}_{entity}")
            component, = [obj for obj in objects if obj.type.name == "MonoBehaviour"
                and obj.read(check_read=False).m_Script.read().m_ClassName == "AnimationController"]
            fields = schemas.read(component)
            owner = component.read(check_read=False).m_GameObject.deref()
            root_transform, = [ref.component.deref() for ref in owner.read().m_Component
                if ref.component.deref().type.name == "Transform"]
            if root_transform.read().m_Father.path_id or owner.read().m_Name != row["transforms"][0]["name"]:
                raise ValueError("Original monster application controller is not on its entity root")
            animator = PPtr(**fields["myAnimator"], assetsfile=component.assets_file).deref()
            if animator.read_typetree() != row["animator"]:
                raise ValueError("Original monster application controller points to a different Animator")
            node, = [ref.component.deref() for ref in animator.read().m_GameObject.read().m_Component
                if ref.component.deref().type.name == "Transform"]
            path = []
            while node.path_id != root_transform.path_id:
                path.append(node.read().m_GameObject.read().m_Name)
                node = node.read().m_Father.deref()
            path.reverse()
            if path != [value["name"] for value in row["transforms"][1:]]:
                raise ValueError("Original monster application Animator hierarchy differs")
            binding = dict(entityId=entity, animatorPath="/".join(path),
                initOnAwake=bool(fields["InitOnAwake"]), createOverride=bool(fields["CreateOverrideController"]),
                notCreateGraph=bool(fields["NotCreateGraph"]), resetOnDisable=bool(fields["mResetAnimatorInitedOnDisable"]),
                enabled=bool(fields["m_Enabled"]))
            rows.append(binding)
            originals.append(dict(entityId=entity, fields=fields, objects=[dict(source=apk.provenance(obj),
                sha256=digest(obj.get_raw_data())) for obj in (component, animator, root_transform)]))
            print(entity, "original entity AnimationController recovered", flush=True)
    finally:
        apk.archive.close()
    target = ROOT / "unity/Assets/Game/Resources/Combat/monster-animation-controllers.json"
    payload = json.dumps(dict(manifest=apk.version, models=rows), separators=(",", ":")).encode("utf-8")
    proof_path = ROOT / "assets/reference/battle-animation/monster-animation-controllers-source-manifest.json"
    if target.exists() and target.read_bytes() != payload:
        old = json.loads(proof_path.read_bytes())
        if digest(target.read_bytes()) != old["outputs"][target.relative_to(ROOT).as_posix()]:
            raise ValueError("Preserve modified monster application controller bindings")
    target.write_bytes(payload)
    if not Path(str(target) + ".meta").exists():
        meta(target)
    application = ROOT / ".tools/source-packages/current" / apk.version / "official-code/client-source/AnimationController.cs"
    proof = dict(manifest=apk.version, selection_sha256=digest(inventory.read_bytes()),
        source_receipt_sha256=digest(receipt.read_bytes()), originals=originals, schemas=schemas.records,
        application=dict(path=application.relative_to(ROOT).as_posix(), sha256=digest(application.read_bytes())),
        generator_sha256=digest(Path(__file__).read_bytes()),
        outputs={p.relative_to(ROOT).as_posix(): digest(p.read_bytes()) for p in (target, Path(str(target) + ".meta"))},
        bindings={path: digest((ROOT / path).read_bytes()) for path in (
            "unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMonsterAnimationController.cs",
            "unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMonsterAnimationController.cs.meta",
            "unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleLiveMonsterModelView.cs")},
        operation="Decode selected original root AnimationController fields and verify its exact Animator and complete parent chain against the installed verified monster inventory. Preserve serialized flags and source object identity; no art conversion.")
    proof_path.write_bytes(json.dumps(proof, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
    print("Installed original monster application controllers:", len(rows))


if __name__ == "__main__":
    build()
