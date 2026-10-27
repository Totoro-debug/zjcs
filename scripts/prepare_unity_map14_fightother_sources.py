"""Decode the current Map14 FightOther models without a Spine runtime.

This prepares source caches and a provenance receipt. It does not install a
renderer, flatten skin-required constraints or claim Windows playback.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from convert_official_skeletons import convert, WHEEL_SHA256
from map13_native_schemas import NativeSchemas
from official_apk import Apk, ROOT
from UnityPy.classes import PPtr

BUNDLE = "defaultpackage_assets_appearanceassets_npc_npc_middle_model.bundle"
SKELETON = "npc_middle_model.skel"
RECEIPT = ROOT / "assets/reference/official-map/map14-fightother-source-manifest.json"
COMPONENTS = {"SkeletonMecanim", "AppearanceCombineSkin", "AppearanceRendererLite",
              "AnimationController", "AppearanceRendererHsvOffset"}


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def encode(value):
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def schema_fields(node):
    fields = {name: getattr(node, name) for name in
              ("m_ByteSize", "m_Index", "m_Level", "m_MetaFlag", "m_Name", "m_RefTypeHash",
               "m_Type", "m_TypeFlags", "m_VariableCount", "m_Version")}
    fields["m_Children"] = [schema_fields(child) for child in node.m_Children]
    return fields


def build():
    apk = Apk.current()
    cache = ROOT / ".tools/source-packages/current" / apk.version / "skeletons/npc_middle_model"
    decoded = convert(apk.version, output=cache, model_bundle=BUNDLE, skeleton_name=SKELETON)
    skeleton_bytes = (cache / "skeleton.json").read_bytes()
    if sha(skeleton_bytes) != decoded["output_sha256"]:
        raise ValueError("Decoded skeleton cache changed")
    skeleton = json.loads(skeleton_bytes)
    skins = {skin["name"]: skin for skin in skeleton["skins"]}
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    schemas = NativeSchemas(code)
    inputs = {}

    def record(obj):
        key = obj.assets_file.name + ":" + str(obj.path_id)
        inputs[key] = dict(apk.provenance(obj), object_sha256=sha(obj.get_raw_data()))
        return obj

    def deref(obj, value):
        return record(PPtr(**value, assetsfile=obj.assets_file).deref())

    models = []
    for entity_id in (4651, 4652):
        components = []
        selected_skins = None
        for obj in apk.objects("defaultpackage_assets_prefabs_ecentity_fight_fightother_"
                               + str(entity_id) + ".bundle"):
            if obj.type.name != "MonoBehaviour":
                continue
            name = obj.read(check_read=False).m_Script.read().m_ClassName
            if name not in COMPONENTS:
                continue
            record(obj)
            fields, tree = schemas.read(obj)
            components.append(dict(type=name, source=apk.provenance(obj), fields=fields,
                                   schema_sha256=sha(encode(schema_fields(tree)))))
            if name == "AppearanceCombineSkin":
                if selected_skins is not None or fields["UseRandomSkin"]:
                    raise ValueError("FightOther skin selection requires another original branch")
                selected_skins = fields["CombineSkins"]
            if name == "SkeletonMecanim":
                data_obj = deref(obj, fields["skeletonDataAsset"])
                data, _ = schemas.read(data_obj)
                binary_obj = deref(data_obj, data["skeletonJSON"])
                if binary_obj.read().m_Name != SKELETON:
                    raise ValueError("FightOther references a different original skeleton")
                components.append(dict(type="SkeletonDataAsset", source=apk.provenance(data_obj), fields=data))
            if name == "AnimationController":
                animator = deref(obj, fields["myAnimator"])
                animator_tree = animator.read_typetree()
                controller = deref(animator, animator_tree["m_Controller"])
                components.append(dict(type="Animator", source=apk.provenance(animator), fields=animator_tree))
                components.append(dict(type="AnimatorController", source=apk.provenance(controller),
                                       fields=controller.read_typetree()))
        if selected_skins is None or any(name not in skins for name in selected_skins):
            raise ValueError("FightOther selected skins are not completely decoded")
        membership = {name: {kind: skins[name].get(kind, []) for kind in
                            ("bones", "ik", "transform", "path", "physics")}
                      for name in selected_skins}
        models.append(dict(entityId=entity_id, selectedSkins=selected_skins,
                           skinMembership=membership, components=components))
    bindings = cache / "map14-fightother-bindings.json"
    bindings.write_bytes(encode(dict(manifest=apk.version, models=models)))
    outputs = []
    for path in (cache / "skeleton.json", cache / "source-manifest.json", bindings):
        outputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes())))
    receipt = dict(format=1, manifest=apk.version, mapClassId=14,
                   operation="Decode original NPC skeleton, every split animation, skin memberships and FightOther renderer/controller bindings into source caches. Native rendering remains unbound.",
                   reader=dict(name="SkelToJson", version="1.0.1", license="MIT", wheel_sha256=WHEEL_SHA256),
                   generators=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes().replace(b"\r\n", b"\n")), hashLineEndings="LF") for p in
                               (Path(__file__).resolve(), ROOT / "scripts/convert_official_skeletons.py",
                                ROOT / "scripts/map13_native_schemas.py", ROOT / "scripts/official_unity_schema.py")],
                   inputs=list(inputs.values()), decodedSource=outputs[1], outputs=outputs,
                   counts=decoded["counts"], models=[dict(entityId=m["entityId"], selectedSkins=m["selectedSkins"],
                   skinMembershipCounts={name: {kind: len(values) for kind, values in fields.items()}
                                         for name, fields in m["skinMembership"].items()}) for m in models])
    RECEIPT.write_bytes(encode(receipt))
    print(json.dumps(dict(counts=receipt["counts"], models=receipt["models"]), ensure_ascii=False))
    return receipt


if __name__ == "__main__":
    build()
