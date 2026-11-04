"""Recover current full-binary partner fighters for the native geometry backend.

Writes original source data only into the caller's isolated validation workspace.
Renderer installation must retain original materials, animation controllers and
constraints; this export alone does not make a partner playable.
"""
import hashlib
import io
import json
import struct
import gzip
import argparse
from collections import Counter

from convert_official_skeletons import reader_type, WHEEL_SHA256
from official_apk import Apk, ROOT
from official_battle_spine_component import read_component
from official_unity_schema import ScriptSchemas
from prepare_character_creation_native_data import read_aligned_string
from prepare_native_character_geometry import atlas_regions
from validation_workspace import validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def package(npc_id, scale, skeleton, atlas):
    def block(raw):
        compressed = gzip.compress(raw, mtime=0)
        return struct.pack("<I", len(raw)) + hashlib.sha256(raw).digest() + struct.pack("<I", len(compressed)) + compressed
    manifest = b"884_156692"
    return (b"ZJCSPNBT" + struct.pack("<II", 1, len(manifest)) + manifest
            + struct.pack("<if", npc_id, scale) + block(skeleton) + block(atlas))


def build(work, install=False):
    selected = json.loads((ROOT / "official-source.json").read_text(encoding="utf-8"))
    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Review partner battle source selection before conversion")
    rules_path = ROOT / "unity/Assets/Game/Resources/DailyChallenges/npc-assistance-rules.json"
    rules_raw = rules_path.read_bytes()
    rules = json.loads(rules_raw)
    pairs = sorted({(int(row["FightEntityId"]), int(row["Id"])) for row in rules["npc"]
                    if int(row.get("NpcType", 0)) == 4 and int(row.get("FightEntityId", 0)) > 0})
    reader_class, _ = reader_type()
    schemas = ScriptSchemas()
    destination = work / "native-battle"
    destination.mkdir(exist_ok=True)
    sources, outputs, fighters = {}, {}, []

    def record(obj):
        key = obj.assets_file.name + ":" + str(obj.path_id)
        sources[key] = dict(source=apk.provenance(obj), payload_sha256=sha(obj.get_raw_data()))
        return obj

    def at(owner, offset):
        from UnityPy.classes.PPtr import PPtr
        file_id, path_id = struct.unpack_from("<iq", owner.get_raw_data(), offset)
        return record(PPtr(m_FileID=file_id, m_PathID=path_id, assetsfile=owner.assets_file).deref())

    def pointer(owner, value):
        from UnityPy.classes.PPtr import PPtr
        return record(PPtr(**value, assetsfile=owner.assets_file).deref())

    def write(relative, raw):
        path = destination / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        outputs[relative] = dict(bytes=len(raw), sha256=sha(raw))
        return relative

    def document(relative, value):
        return write(relative, json.dumps(value, ensure_ascii=False, separators=(",", ":"),
                                          allow_nan=False).encode("utf-8"))

    def text(obj):
        return record(obj).read(check_read=False).m_Script.encode("utf-8", "surrogateescape")

    for entity_id, npc_id in pairs:
        bundle = f"defaultpackage_assets_prefabs_ecentity_fight_fightnpc_{entity_id}.bundle"
        objects = apk.objects(bundle)
        components = {}
        for obj in objects:
            if obj.type.name == "MonoBehaviour":
                value = obj.read(check_read=False)
                name = value.m_Script.read().m_ClassName
                if name in ("SkeletonMecanim", "SpineLazyAnimLoad", "AppearanceCombineSkin"):
                    components[name] = obj
        if "SpineLazyAnimLoad" in components:
            continue
        renderer_obj = record(components["SkeletonMecanim"])
        renderer = read_component(renderer_obj.get_raw_data(), "SkeletonMecanim")
        skeleton_asset = pointer(renderer_obj, renderer["skeletonDataAsset"])
        raw = skeleton_asset.get_raw_data()
        _, offset = read_aligned_string(raw, 28)
        count = struct.unpack_from("<i", raw, offset)[0]
        if count != 1:
            raise ValueError("Review full-binary fighter atlas count: " + str(npc_id))
        atlas_asset = at(skeleton_asset, offset + 4)
        scale = struct.unpack_from("<f", raw, offset + 16)[0]
        binary = at(skeleton_asset, offset + 20)
        binary_raw = text(binary)
        reader = reader_class()
        skeleton = reader.convert(binary_raw)
        if reader.inp.index != len(binary_raw):
            raise ValueError("Full partner fighter binary was not completely consumed: " + str(npc_id))
        atlas_raw = atlas_asset.get_raw_data()
        _, atlas_offset = read_aligned_string(atlas_raw, 28)
        atlas_file = at(atlas_asset, atlas_offset + 16)
        atlas_bytes = text(atlas_file)
        regions = atlas_regions(atlas_bytes.decode("utf-8"))
        skins = (schemas.read(record(components["AppearanceCombineSkin"]))["CombineSkins"]
                 if "AppearanceCombineSkin" in components else [renderer["initialSkinName"] or "default"])
        game_object = record(renderer_obj.read(check_read=False).m_GameObject.deref())
        transforms = []
        transform_obj = game_object.read().m_Component[0].component.deref()
        while True:
            transform = record(transform_obj).read()
            transforms.append(dict(name=record(transform.m_GameObject.deref()).read().m_Name,
                position={axis: getattr(transform.m_LocalPosition, axis) for axis in "xyz"},
                rotation={axis: getattr(transform.m_LocalRotation, axis) for axis in "xyzw"},
                scale={axis: getattr(transform.m_LocalScale, axis) for axis in "xyz"}))
            if not transform.m_Father.path_id:
                break
            transform_obj = transform.m_Father.deref()
        transforms.reverse()
        animator = next(record(obj) for obj in objects if obj.type.name == "Animator"
                        and obj.read().m_GameObject.path_id == game_object.path_id)
        controller = record(animator.read().m_Controller.deref())
        controller_tree = controller.read_typetree()
        clips = [record(clip.deref()).read().m_Name for clip in controller.read().m_AnimationClips]
        strings = dict(controller_tree["m_TOS"])
        aliases = {}
        for machine in controller_tree["m_Controller"]["m_StateMachineArray"]:
            for item in machine["data"]["m_StateConstantArray"]:
                state = item["data"]
                state_name = strings[state["m_NameID"]]
                for tree in state["m_BlendTreeConstantArray"]:
                    for node in tree["data"]["m_NodeArray"]:
                        clip_id = node["data"]["m_ClipID"]
                        if clip_id < len(clips) and clips[clip_id] in skeleton["animations"]:
                            aliases[state_name] = clips[clip_id]
        for alias, state in (("idle_01", "Idle"), ("run_01", "Run"), ("die_01", "Die"),
                             ("hit_01", "Damage"), ("skill_01", "Skill"), ("attack_01", "Skill")):
            if state in aliases:
                aliases[alias] = aliases[state]
        materials = []
        material_count = struct.unpack_from("<i", atlas_raw, atlas_offset + 28)[0]
        for index in range(material_count):
            material = at(atlas_asset, atlas_offset + 32 + index * 12)
            value = material.read()
            textures = []
            for property_name, environment in value.m_SavedProperties.m_TexEnvs:
                if not environment.m_Texture.path_id:
                    continue
                texture_obj = record(environment.m_Texture.deref())
                texture = texture_obj.read()
                pixels = texture.image.convert("RGBA")
                buffer = io.BytesIO()
                pixels.save(buffer, format="PNG")
                textures.append(dict(property=property_name, name=texture.m_Name,
                    source=texture_obj.assets_file.name + ":" + str(texture_obj.path_id),
                    encoded_sha256=sha(texture.get_image_data()), rgba_sha256=sha(pixels.tobytes()),
                    width=texture.m_Width, height=texture.m_Height, format=int(texture.m_TextureFormat),
                    mip_count=texture.m_MipCount, color_space=texture.m_ColorSpace,
                    file=write("textures/" + sha(buffer.getvalue()) + ".png", buffer.getvalue())))
            shader = record(value.m_Shader.deref())
            materials.append(dict(fields=material.read_typetree(), shader=shader.read().m_ParsedForm.m_Name,
                                  textures=textures))
        prefix = str(npc_id) + "/"
        fighter = dict(npcId=npc_id, entityId=entity_id, scale=scale, renderer=renderer, skins=skins,
            transforms=transforms, controller=document(prefix + "controller.json", controller_tree),
            skeleton=document(prefix + "skeleton.json", skeleton),
            binary=write(prefix + "original.skel.bytes", binary_raw),
            atlas=write(prefix + "original.atlas.txt", atlas_bytes),
            regions=document(prefix + "regions.json", regions),
            materials=document(prefix + "materials.json", materials),
            package=write(prefix + "battle.bytes", package(npc_id, scale,
                json.dumps(skeleton, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode(),
                json.dumps(regions, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode())),
            aliases=aliases,
            counts={name: len(skeleton.get(name, [])) for name in
                    ("bones", "slots", "skins", "animations", "ik", "transform", "path", "physics")},
            animationNames=list(skeleton.get("animations", {})),
            attachmentTypes=dict(Counter(attachment.get("type", "region") for skin in skeleton["skins"]
                for entries in skin.get("attachments", {}).values() for attachment in entries.values())))
        fighters.append(fighter)
        print("Recovered original fighter", npc_id, fighter["counts"], flush=True)
    inputs = ["official-source.json", "scripts/prepare_partner_native_battle.py",
        "scripts/convert_official_skeletons.py", "scripts/prepare_native_character_geometry.py",
        "scripts/official_battle_spine_component.py", "scripts/official_unity_schema.py",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeSkeletonData.PartnerBattle.cs",
        "unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeConstraintPose.cs",
        "unity/Assets/Game/Editor/ProfessionUI/OfficialPartnerNativeBattleDataValidation.cs"]
    proof = dict(manifest=apk.version, selection=selected, rules_sha256=sha(rules_raw), fighters=fighters,
        decoder=dict(name="SkelToJson", version="1.0.1", license="MIT", wheel_sha256=WHEEL_SHA256),
        generator_sha256=sha((ROOT / "scripts/prepare_partner_native_battle.py").read_bytes()),
        inputs=[dict(path=path, sha256=sha((ROOT / path).read_bytes())) for path in inputs],
        sources=sources, outputs=outputs)
    (work / "partner-native-battle-source-manifest.json").write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    if install:
        from prepare_character_creation_opening import write_with_meta
        target = ROOT / "unity/Assets/Official/Combat/NativePartners"
        installed = []
        for relative in outputs:
            installed.append(write_with_meta(target / relative, (destination / relative).read_bytes()))
        installed.append(write_with_meta(target / "catalog.json", json.dumps(dict(manifest=apk.version,
            fighters=fighters), ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode()))
        proof["installed"] = installed
        reference = ROOT / "assets/reference/partner-native-battle-source-manifest.json"
        reference.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Recovered", len(fighters), "full-binary fighters; native runtime binding remains required.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--install", action="store_true", help="Install source payloads, without enabling combat candidates")
    build(validation_directory(), parser.parse_args().install)
