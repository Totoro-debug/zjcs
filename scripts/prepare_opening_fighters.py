"""Recover the opening NPCs' fight skins and lazy animation bytes from APK 854_156383.

The existing offline Spine repacker copies the original skin/timeline bytes; it
does not bake poses or create a replacement animation runtime.
"""
import hashlib
import json
import struct
import subprocess
from pathlib import Path

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from official_battle_spine_component import read_component
from official_manifest import read_asset_manifest
from prepare_character_creation_native_data import read_aligned_string, parse_split_binary_object
from prepare_character_creation_opening import json_bytes, write_with_meta, REFERENCE
from UnityPy.classes.PPtr import PPtr

VERSION = "854_156383"
OUT = ROOT / "unity/Assets/Official/Opening/Fighters"
CACHE = ROOT / ".tools/source-packages/opening-fighters"


def main():
    apk, schemas = Apk(), ScriptSchemas()
    CACHE.mkdir(parents=True, exist_ok=True)
    inputs, outputs, jobs, fighters, page_sources = {}, [], {}, [], {}
    assets = [row for name in apk.archive.namelist()
              if name.startswith("assets/yoo/") and name.endswith("_854_156383.v3.bytes")
              for row in read_asset_manifest(apk.archive.read(name))]
    guids = {row["guid"]: row for row in assets if row["guid"]}
    animation_catalog = json.loads((ROOT / "assets/official/battle-animation/catalog.json").read_text(encoding="utf-8"))
    effects_path = ROOT / "unity/Assets/StreamingAssets/Opening/skill-effects.json"
    effects_bytes = effects_path.read_bytes()
    effects = json.loads(effects_bytes)["effects"]

    def record(obj):
        key = obj.assets_file.name + ":" + str(obj.path_id)
        inputs[key] = dict(apk.provenance(obj), sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())
        return obj

    def ptr(value, owner):
        return record(PPtr(**value, assetsfile=owner.assets_file).deref())

    def pointer_at(raw, offset, owner):
        file_id, path_id = struct.unpack_from("<iq", raw, offset)
        return ptr(dict(m_FileID=file_id, m_PathID=path_id), owner)

    def text(obj):
        return record(obj).read().m_Script.encode("utf-8", "surrogateescape")

    def cache_text(obj, suffix):
        target = CACHE / (hashlib.sha256((obj.assets_file.name + str(obj.path_id)).encode()).hexdigest()[:24] + suffix)
        target.write_bytes(text(obj))
        return target

    for class_id, npc_id in ((4201, 8401), (4202, 8402)):
        bundle = f"defaultpackage_assets_prefabs_ecentity_fight_fightnpc_{class_id}.bundle"
        components = {}
        for obj in apk.objects(bundle):
            if obj.type.name == "MonoBehaviour":
                component = obj.read(check_read=False)
                name = component.m_Script.read().m_ClassName
                if name in ("AppearanceCombineSkin", "SpineLazyAnimLoad", "SkeletonMecanim"):
                    components[name] = record(obj)
        renderer_obj = components["SkeletonMecanim"]
        renderer = read_component(renderer_obj.get_raw_data(), "SkeletonMecanim")
        skeleton = ptr(renderer["skeletonDataAsset"], renderer_obj)
        raw = skeleton.get_raw_data()
        _, offset = read_aligned_string(raw, 28)
        count = struct.unpack_from("<i", raw, offset)[0]
        if count != 1:
            raise ValueError("Opening NPC expects one official atlas")
        offset += 4
        atlas = pointer_at(raw, offset, skeleton)
        offset += 12
        scale = struct.unpack_from("<f", raw, offset)[0]
        binary = pointer_at(raw, offset + 4, skeleton)
        atlas_raw = atlas.get_raw_data()
        _, atlas_offset = read_aligned_string(atlas_raw, 28)
        # AtlasAssetBase: LoadingMode + onDemandTextureLoader, then atlasFile.
        atlas_file = pointer_at(atlas_raw, atlas_offset + 16, atlas)
        material_count = struct.unpack_from("<i", atlas_raw, atlas_offset + 28)[0]
        for index in range(material_count):
            mat = pointer_at(atlas_raw, atlas_offset + 32 + index * 12, atlas)
            env = dict(mat.read().m_SavedProperties.m_TexEnvs).get("_MainTex")
            if env is not None and env.m_Texture.path_id:
                tex = record(env.m_Texture.deref())
                page_sources[tex.read().m_Name] = tex
        skin_names = schemas.read(components["AppearanceCombineSkin"])["CombineSkins"]
        lazy = schemas.read(components["SpineLazyAnimLoad"])
        definition = parse_split_binary_object(ptr(lazy["mSplitBinarySo"], components["SpineLazyAnimLoad"]).get_raw_data())
        controller_obj = next(record(obj) for obj in apk.objects(bundle) if obj.type.name == "Animator"
                              and obj.read().m_GameObject.read().m_Name == "PlayerModel_Game")
        controller = record(controller_obj.read().m_Controller.deref())
        controller_key = controller.assets_file.name + ":" + str(controller.path_id)
        cfg = animation_catalog["controllers"][controller_key]
        base = animation_catalog["controllers"][cfg.get("base", controller_key)]
        overrides = cfg.get("overrides", {})
        clips = [overrides.get(clip["name"], clip) for clip in base["clips"]]
        aliases = {}
        for machine in base["graph"]["m_StateMachineArray"]:
            for state_value in machine["data"]["m_StateConstantArray"]:
                state = state_value["data"]
                state_name = base["strings"][str(state["m_NameID"])]
                for tree in state["m_BlendTreeConstantArray"]:
                    for node in tree["data"]["m_NodeArray"]:
                        clip_id = node["data"]["m_ClipID"]
                        if clip_id >= len(clips):
                            continue
                        clip = clips[clip_id]["name"]
                        aliases[state_name] = aliases[state_name.lower()] = clip
                        aliases[clip.rsplit("/", 1)[-1]] = clip
        aliases.update(idle_01=aliases["Idle"], run_01=aliases["Run"], die_01=aliases["Die"],
                       hit_01=aliases["Damage"], skill_01=aliases["Skill"], attack_01=aliases["Skill"])
        # MyPlayableOverrideAnimator replaces Skill on each cast, beyond the NPC's
        # default controller clips. Recover every pinned opening override for its rig.
        prefix = aliases["Idle"].split("/", 1)[0] + "/"
        for event in effects:
            clip = event.get("source_event", {}).get("animation", "")
            if event["kind"] == "override" and clip.startswith(prefix):
                aliases[clip] = aliases[clip.rsplit("/", 1)[-1]] = clip
        required = set(aliases.values())
        split = {}
        for row in definition["animations"]:
            if row["name"] not in required:
                continue
            location = guids[row["asset_guid"]]
            obj = apk.asset(location["bundle"], "/" + location["path"].rsplit("/", 1)[-1])
            split[row["name"]] = str(cache_text(obj, ".bytes"))
        missing = required - set(split) - set(definition["no_split_animations"])
        # Unity's BoolTemplate / EntrySkill placeholders contain no Spine timeline.
        if missing - {"BoolTemplate", "EntrySkill", "LoopSkill", "EscapeSkill"}:
            raise ValueError("Official NPC animation missing: " + str(missing))
        aliases = {name: clip for name, clip in aliases.items() if clip not in missing}
        stem = "npc-fight-" + str(npc_id)
        jobs[stem] = dict(skeleton=str(cache_text(binary, ".skel.bytes")),
                          atlas=str(cache_text(atlas_file, ".atlas.txt")),
                          split_animations=split, string_table=definition["strings"], keep_skins=skin_names,
                          output=str(OUT / (stem + ".skel.bytes")), output_atlas=str(OUT / (stem + ".atlas.txt")))
        fighters.append(dict(classId=npc_id, entityClassId=class_id, stem=stem, scale=scale, skins=skin_names,
                             renderer=renderer, controller=controller_key,
                             animations=[dict(name=name, spineName=clip) for name, clip in sorted(aliases.items())]))
    OUT.mkdir(parents=True, exist_ok=True)
    job_path = CACHE / "jobs.json"
    job_path.write_bytes(json_bytes(jobs))
    results = CACHE / "results.json"
    subprocess.run(["node", str(ROOT / "scripts/repack_battle_spine.mjs"), str(job_path), str(results)], check=True)
    repacked = json.loads(results.read_text(encoding="utf-8"))
    for fighter in fighters:
        fighter["pages"] = []
        stem = fighter["stem"]
        for name in repacked[stem]["pages"]:
            texture_name = name.removesuffix(".png")
            matches = [row for row in assets if row["path"] ==
                       "Assets/AppearanceAssets/Npc/npc_middle_model/Atlas/High/" + name]
            if len(matches) != 1:
                raise ValueError("Official NPC atlas page is ambiguous: " + name)
            source = matches[0]
            obj = record(next(pointer.deref() for path, pointer in apk.container(source["bundle"])
                              if path.lower() == source["path"].lower() and pointer.type.name == "Texture2D"))
            import io
            buffer = io.BytesIO()
            obj.read().image.save(buffer, format="PNG")
            payload = buffer.getvalue()
            path = OUT / (texture_name + ".png")
            outputs.append(write_with_meta(path, payload))
            fighter["pages"].append(dict(name=name, file=path.name))
        for suffix in (".skel.bytes", ".atlas.txt"):
            path = OUT / (stem + suffix)
            outputs.append(write_with_meta(path, path.read_bytes()))
    outputs.append(write_with_meta(OUT / "fighters.json", json_bytes(dict(manifest=VERSION, fighters=fighters))))
    receipt = json.loads(REFERENCE.read_text(encoding="utf-8"))
    receipt["fight_npc_appearance"] = dict(inputs=list(inputs.values()), reassembly=repacked,
        generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), fighters=fighters,
        override_events_sha256=hashlib.sha256(effects_bytes).hexdigest())
    paths = {row["path"] for row in outputs}
    receipt["outputs"] = [row for row in receipt["outputs"] if row["path"] not in paths] + outputs
    REFERENCE.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps([dict(npc=f["classId"], skins=f["skins"], pages=f["pages"], aliases=len(f["animations"])) for f in fighters]))


if __name__ == "__main__":
    main()
