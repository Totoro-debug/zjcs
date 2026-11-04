"""Recover current original monster skeletons, renderer bindings and atlas inputs.

The existing native geometry backend consumes full source channels rather than
sampled meshes. APK-first resolution and selected-version provenance stay in Apk.
"""
import argparse
from collections import Counter
import gzip
import hashlib
import json
from pathlib import Path
import struct
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / ".tools/python-packages"))

from official_apk import Apk, ROOT
from convert_official_skeletons import reader_type, WHEEL_SHA256
from current_component_schemas import CurrentSchemas
from official_battle_spine_component import read_component
from official_manifest import read_asset_manifest
from prepare_native_character_geometry import atlas_regions
from validation_workspace import validation_directory
from prepare_battle_game_animators import GUID, documents, verify_graph
from prepare_partner_native_battle_animators import verify_partner_motion


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def encoded(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8")


def verify_monster_motion(original, state, converted, native, native_names):
    if not state["m_BlendTreeConstantArray"]:
        if converted["m_Motion"] != {"fileID": 0}:
            raise ValueError("Original empty monster state has a converted motion")
        return
    verify_partner_motion(original, state, converted, native, native_names)


def package(entity, scale, skeleton, regions):
    def block(raw):
        compressed = gzip.compress(raw, mtime=0)
        return struct.pack("<I", len(raw)) + hashlib.sha256(raw).digest() + struct.pack("<I", len(compressed)) + compressed
    manifest = b"884_156692"
    return (b"ZJCSMNST" + struct.pack("<II", 1, len(manifest)) + manifest
            + struct.pack("<if", entity, scale) + block(encoded(skeleton)) + block(encoded(regions)))


def entity_prefab_family(apk, entity):
    families = [family for family in ("fightmonster", "fightpet")
        if f"defaultpackage_assets_prefabs_ecentity_fight_{family}_{entity}.bundle" in apk.index]
    if len(families) != 1:
        raise ValueError("Bind an unambiguous original summon prefab family: " + str(entity))
    return families[0]


def build(roles, pet_roles=()):
    work = validation_directory()
    if not (work / ".zjcs-validation.json").is_file():
        raise ValueError("An owned monster validation workspace is required")
    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Review the selected monster source version")
    from UnityPy.classes.PPtr import PPtr
    schemas = CurrentSchemas()
    Reader, _ = reader_type()
    sources, outputs, records = {}, {}, []
    destination = work / "live-monsters"
    destination.mkdir(exist_ok=True)
    table = apk.source.manifest_bytes("DefaultPackage", apk.archive)
    locations = read_asset_manifest(table)
    by_guid = {row["guid"]: row for row in locations if row["guid"]}
    cache = ROOT / ".tools/source-packages/current" / apk.version / "native-conversion"
    conversion_path = cache / "native-export-all-source-manifest.json"
    conversion = json.loads(conversion_path.read_bytes())
    if (not conversion["completed"] or conversion["manifest"] != apk.version
            or sha((cache / "source-manifest.json").read_bytes()) != conversion["input_manifest_sha256"]):
        raise ValueError("The original monster native conversion receipt differs")
    exported = cache / conversion["export_directory"]
    verified = {row["path"]: row["sha256"] for row in conversion["outputs"]}
    animator_paths = [exported / path for path in verified if path.endswith((".controller", ".anim"))
        and path.startswith("ExportedProject/Assets/")]
    animator_guids, controllers = {}, {}
    native_sources, native_graphs = {}, {}
    for path in animator_paths:
        sidecar = Path(str(path) + ".meta")
        raw = sidecar.read_bytes()
        if sha(raw) != verified[sidecar.relative_to(exported).as_posix()]:
            raise ValueError("Converted original monster Animator metadata differs")
        key = GUID.search(raw.decode("utf-8"))[1]
        if key in animator_guids:
            raise ValueError("Duplicate converted original monster Animator GUID")
        animator_guids[key] = path
        if path.suffix == ".controller" and path.is_relative_to(exported / "ExportedProject/Assets/AppearanceAssets"):
            controllers.setdefault(path.name, []).append(path)

    def record(obj):
        sources[obj.assets_file.name + ":" + str(obj.path_id)] = dict(
            source=apk.provenance(obj), payload_sha256=sha(obj.get_raw_data()))
        return obj

    def pointer(owner, value):
        return record(PPtr(**value, assetsfile=owner.assets_file).deref())

    def text(obj):
        return record(obj).read(check_read=False).m_Script.encode("utf-8", "surrogateescape")

    def write(relative, raw):
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(raw)
        outputs[relative] = dict(bytes=len(raw), sha256=sha(raw))
        return relative

    def recover_animator(original, clips):
        start, = controllers[original.read().m_Name + ".controller"]
        pending, closure = [start], set()
        while pending:
            path = pending.pop()
            if path in closure:
                continue
            raw = path.read_bytes()
            if sha(raw) != verified[path.relative_to(exported).as_posix()]:
                raise ValueError("Converted original monster Animator differs: " + str(path))
            closure.add(path)
            pending.extend(animator_guids[key] for key in GUID.findall(raw.decode("utf-8")))
        natives = {path: documents(path.read_bytes()) for path in closure}
        names = {GUID.search(Path(str(path) + ".meta").read_text("utf-8"))[1]:
            next(iter(natives[path].values()))["m_Name"] for path in closure}
        native_graphs[original.read().m_Name] = verify_graph(original, natives[start], names, verify_monster_motion)
        originals = {(obj.type.name, obj.read().m_Name): obj for obj in [original, *clips]}
        remap = {key: sha(("zjcs:battle-live-monster:" + path.relative_to(exported).as_posix()).encode())[:32]
            for key, path in animator_guids.items() if path in closure}
        for path in closure:
            fields = next(iter(natives[path].values()))
            source = originals[("AnimatorController" if path.suffix == ".controller" else "AnimationClip", fields["m_Name"])]
            if path.suffix == ".anim" and abs(fields["m_AnimationClipSettings"]["m_StopTime"] - source.read().m_MuscleClip.m_StopTime) > .000001:
                raise ValueError("Original monster clip duration differs: " + str(path))
            native_sources[path.relative_to(exported).as_posix()] = dict(sha256=sha(path.read_bytes()),
                original=source.assets_file.name + ":" + str(source.path_id))
            relative = "animators/" + path.relative_to(exported / "ExportedProject/Assets").as_posix()
            for first, target in ((path, relative), (Path(str(path) + ".meta"), relative + ".meta")):
                raw = GUID.sub(lambda match: "guid: " + remap[match[1]], first.read_text("utf-8")).encode("utf-8")
                write(target, raw)
        return "animators/" + start.relative_to(exported / "ExportedProject/Assets").as_posix()

    config_location, = [row for row in locations if row["path"].endswith("/AppearanceConfig.asset")]
    config_obj = record(apk.asset(config_location["bundle"], config_location["path"]))
    config = schemas.read(config_obj)
    skeleton_paths = {row["SkeletonName"]: row["Path"] for row in config["mAppearanceSkeletonPaths"]}
    if roles is None:
        roles = sorted(int(name.rsplit("_", 1)[1].removesuffix(".bundle")) for name in apk.index
            if "prefabs_ecentity_fight_fightmonster_" in name and name.rsplit("_", 1)[1].removesuffix(".bundle").isdigit())
    pet_roles = set(pet_roles)
    if set(roles) & pet_roles:
        raise ValueError("An entity cannot use both monster and pet prefabs")
    for entity in sorted(set(roles) | pet_roles):
        family = entity_prefab_family(apk, entity)
        if (family == "fightpet") != (entity in pet_roles):
            raise ValueError("Requested summon family differs from original prefab: " + str(entity))
        objects = apk.objects(f"prefabs_ecentity_fight_{family}_{entity}")
        components = {}
        for obj in objects:
            if obj.type.name == "MonoBehaviour":
                kind = obj.read(check_read=False).m_Script.read().m_ClassName
                components.setdefault(kind, []).append(obj)
        renderers = [(kind, obj) for kind in ("SkeletonMecanim", "SkeletonAnimation") for obj in components.get(kind, [])]
        if len(renderers) != 1:
            raise ValueError("Bind each original renderer explicitly: " + str(entity))
        kind, renderer_obj = renderers[0]
        renderer = read_component(record(renderer_obj).get_raw_data(), kind)
        data_obj = pointer(renderer_obj, renderer["skeletonDataAsset"])
        data = schemas.read(data_obj)
        if len(data["atlasAssets"]) != 1 or any(data[key] for key in
                ("skeletonDataModifiers", "fromAnimation", "toAnimation", "duration", "zSlots")):
            raise ValueError("Bind additional original monster skeleton asset fields: " + str(entity))
        binary = text(pointer(data_obj, data["skeletonJSON"]))
        reader = Reader()
        skeleton = reader.convert(binary)
        if reader.inp.index != len(binary):
            raise ValueError("Original monster binary was not fully consumed: " + str(entity))
        atlas_obj = pointer(data_obj, data["atlasAssets"][0])
        atlas = schemas.read(atlas_obj)
        atlas_bytes = text(pointer(atlas_obj, atlas["atlasFile"]))
        regions = atlas_regions(atlas_bytes.decode("utf-8"))
        appearance_obj, = [obj for obj in components.get("AppearanceRendererLite", [])
            if schemas.read(obj)["SkeletonRenderer"] == dict(m_FileID=0, m_PathID=renderer_obj.path_id)]
        appearance = schemas.read(record(appearance_obj))
        resolution = {1: "Low", 2: "High"}[appearance["LevelOfResolution"]]
        skeleton_name = data_obj.read(check_read=False).m_Name.removesuffix("_SkeletonData")
        loader_location, = [row for row in locations if row["path"] == skeleton_paths[skeleton_name]
            + "/" + skeleton_name + "_TextureLoader_" + resolution + ".asset"]
        loader_obj = record(apk.asset(loader_location["bundle"], loader_location["path"]))
        loader = schemas.read(loader_obj)
        mappings = {}
        for mapping in loader["placeholderMap"]:
            placeholder = pointer(loader_obj, mapping["placeholderTexture"])
            key = placeholder.assets_file.name + ":" + str(placeholder.path_id)
            if key in mappings:
                raise ValueError("Duplicate original monster texture placeholder")
            mappings[key] = mapping
        textures, materials = {}, []
        for material_pointer in atlas["materials"]:
            material_obj = pointer(atlas_obj, material_pointer)
            material = material_obj.read()
            bindings = []
            for property_name, environment in material.m_SavedProperties.m_TexEnvs:
                if not environment.m_Texture.path_id:
                    continue
                placeholder = record(environment.m_Texture.deref())
                key = placeholder.assets_file.name + ":" + str(placeholder.path_id)
                selected = [(property_name, placeholder)]
                if property_name == "_MainTex":
                    mapping = mappings[key]
                    selected = []
                    for field, target_property in (("targetTextureReference", "_MainTex"), ("dyeTextureReference", "_DyeTex")):
                        guid = mapping[field]["AssetReference"]["mAssetGuid"]
                        if guid:
                            location = by_guid[guid]
                            selected.append((target_property, record(apk.asset(location["bundle"], location["path"]))))
                for target_property, texture_obj in selected:
                    texture = texture_obj.read()
                    identity = texture_obj.assets_file.name + ":" + str(texture_obj.path_id)
                    textures[identity] = dict(name=texture.m_Name, width=texture.m_Width, height=texture.m_Height,
                        mipCount=texture.m_MipCount, colorSpace=texture.m_ColorSpace, settings=texture_obj.read_typetree()["m_TextureSettings"],
                        imageBytes=write("textures/" + sha(texture.get_image_data()) + ".bytes", texture.get_image_data()),
                        format=int(texture.m_TextureFormat))
                    bindings.append(dict(property=target_property, texture=identity, placeholder=key))
            shader = record(material.m_Shader.deref())
            materials.append(dict(fields=material_obj.read_typetree(), shader=shader.read().m_ParsedForm.m_Name,
                shaderSource=shader.assets_file.name + ":" + str(shader.path_id), textures=bindings))
        go_obj = record(renderer_obj.read(check_read=False).m_GameObject.deref())
        transforms, transform_obj = [], go_obj.read().m_Component[0].component.deref()
        while True:
            transform = record(transform_obj).read()
            node = record(transform.m_GameObject.deref()).read()
            transforms.append(dict(name=node.m_Name, active=node.m_IsActive, layer=node.m_Layer,
                position={k: getattr(transform.m_LocalPosition, k) for k in "xyz"},
                rotation={k: getattr(transform.m_LocalRotation, k) for k in "xyzw"},
                scale={k: getattr(transform.m_LocalScale, k) for k in "xyz"}))
            if not transform.m_Father.path_id:
                break
            transform_obj = transform.m_Father.deref()
        transforms.reverse()
        animator_obj, = [obj for obj in objects if obj.type.name == "Animator" and obj.read().m_GameObject.path_id == go_obj.path_id]
        controller = record(animator_obj.read().m_Controller.deref())
        clips = [record(pointer.deref()) for pointer in controller.read().m_AnimationClips]
        mesh_obj, = [ref.component.deref() for ref in go_obj.read().m_Component if ref.component.deref().type.name == "MeshRenderer"]
        skins = schemas.read(record(components["AppearanceCombineSkin"][0]))["CombineSkins"] if "AppearanceCombineSkin" in components else [renderer["initialSkinName"] or "default"]
        prefix = str(entity) + "/"
        result = dict(entityId=entity, skeletonName=skeleton_name, scale=data["scale"], rendererKind=kind,
            renderer=renderer, skeletonData=data, skins=skins, transforms=transforms,
            meshRenderer=record(mesh_obj).read_typetree(), animator=record(animator_obj).read_typetree(),
            controller=write(prefix + "controller.json", encoded(controller.read_typetree())),
            nativeController=recover_animator(controller, clips),
            clips=[dict(name=obj.read().m_Name, duration=obj.read().m_MuscleClip.m_StopTime,
                source=obj.assets_file.name + ":" + str(obj.path_id)) for obj in clips],
            package=write(prefix + "model.bytes", package(entity, data["scale"], skeleton, regions)),
            skeleton=write(prefix + "skeleton.json", encoded(skeleton)), atlas=write(prefix + "atlas.txt", atlas_bytes),
            regions=write(prefix + "regions.json", encoded(regions)), materials=materials, textures=textures,
            counts={key: len(skeleton.get(key, [])) for key in ("bones", "slots", "skins", "animations", "ik", "transform", "path", "physics")},
            attachmentTypes=dict(Counter(a.get("type", "region") for skin in skeleton["skins"]
                for entries in skin.get("attachments", {}).values() for a in entries.values())))
        records.append(result)
        print(entity, skeleton_name, result["counts"], result["attachmentTypes"], flush=True)
    write("catalog.json", encoded(dict(manifest=apk.version, models=records)))
    proof = dict(manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()),
        assetTableSha256=sha(table), decoder=dict(name="SkelToJson", version="1.0.1", license="MIT", wheel_sha256=WHEEL_SHA256),
        sources=sources, outputs=outputs, schemas=schemas.records, nativeAnimatorSources=native_sources, nativeAnimatorGraphs=native_graphs,
        nativeConversion=dict(path=conversion_path.relative_to(ROOT).as_posix(), sha256=sha(conversion_path.read_bytes())),
        operation="Resolve actual selected-version monster prefabs and dependencies through APK-first source resolution. Strictly decode full skeletons and source schemas; retain original controller, renderer, transform, skin, atlas, material and texture-loader bindings. Raw texture blocks are export inputs, not Windows-ready texture assets.")
    (work / "live-monsters-source-manifest.json").write_bytes(encoded(proof))
    apk.archive.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--roles", help="Comma-separated original monster entity IDs; omitted means all numeric monster prefabs")
    parser.add_argument("--pet-roles", default="", help="Comma-separated original pet entity IDs, recovered from fightpet prefabs")
    args = parser.parse_args()
    build(sorted({int(value) for value in args.roles.split(",")}) if args.roles else None,
        [int(value) for value in args.pet_roles.split(",") if value])
