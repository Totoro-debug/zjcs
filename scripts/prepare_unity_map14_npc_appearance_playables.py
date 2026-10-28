"""Restore the four original NPC appear/disappear prefabs and native FX dependencies.

Resolve material/texture/shader identities from verified original CAB/PathID
records. Never import AssetRipper script stubs or select a substitute shader.
"""
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import struct
import UnityPy
import astc_encoder
import texture2ddecoder

from official_apk import ROOT
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_map13_native_scene import native_texture
from prepare_unity_map14_entity_textures import decode
from prepare_unity_map14_entity_materials import pointers
from prepare_unity_map14_entity_materials_native import parse_material, comparable, clean
from prepare_unity_map14_story_playable_inputs import CACHE, RECEIPT as INPUT_RECEIPT

EXPORTED = CACHE / "export/ExportedProject/Assets"
DESTINATION = ROOT / "unity/Assets/Official/World/Map/14/NativeNpcAppearancePlayables"
RECEIPT = ROOT / "assets/reference/official-map/map14-native-npc-appearance-playables-source-manifest.json"
PREFIX = "assets/reference/official-map/"
NAMES = ("playable_npc_appear", "playable_npc_disappear", "playable_FeiLe_appear", "playable_FeiLe_disappear")
GUID = re.compile(r"(?m)^guid: ([a-f0-9]{32})$")
REFERENCE = re.compile(r"guid: ([a-f0-9]{32})")


from prepare_unity_map14_suimo_performance import Originals, record, guid, native_meta, same_scalars


def build():
    original = Originals(); outputs=[];dependencies=[record(INPUT_RECEIPT)]
    proof_path = ROOT/(PREFIX+"map14-story-playable-hierarchy-source-manifest.json")
    proof = json.loads(proof_path.read_bytes());dependencies.append(record(proof_path))
    source_path_ = ROOT/"unity/Assets/Official/World/Map/14/Source/story-playables.json"
    source = json.loads(source_path_.read_bytes());dependencies.append(record(source_path_))
    objects = {r["identity"]:r for r in source["objects"]}
    prefabs = [p for p in source["prefabs"] if Path(p["path"]).stem in NAMES]
    if len(prefabs)!=4:raise ValueError("Original NPC appearance prefab inventory differs")
    prefab_paths = [EXPORTED/p["path"].removeprefix("Assets/") for p in prefabs]
    expected = {r["path"]:r["sha256"] for r in proof["outputs"]}
    for path in prefab_paths:
        if sha(path.read_bytes())!=expected[source_path(path)]:raise ValueError("Verified original NPC prefab changed")
    bindings = [r for r in proof["bindings"] if (ROOT/r["path"]) in prefab_paths]
    if not bindings:raise ValueError("Original NPC appearance hierarchy is unbound")
    wanted = set()
    for b in bindings:
        if b["type"] not in ("ParticleSystemRenderer","TrailRenderer"):continue
        owner = objects[b["source"]]
        obj = original.reader(*b["source"].split(":"))
        for link in original.links(obj,owner["fields"]):
            if link["field"].startswith(".m_Materials["):wanted.add((link["cab"].casefold(),link["path_id"]))
    if len(wanted)!=24:raise ValueError("Original NPC appearance material inventory changed")
    def receipt(name):
        path = ROOT/(PREFIX+name);dependencies.append(record(path));return json.loads(path.read_bytes())
    material_proof = receipt("map14-native-entity-materials-native-source-manifest.json")
    texture_proof = receipt("map14-native-entity-textures-source-manifest.json")
    shader_proof = receipt("map14-entity-shaders-source-manifest.json")
    shader_binding = receipt("map14-entity-shaders-binding-source-manifest.json")
    materials = {(m["cab"].casefold(),m["path_id"]):m for m in material_proof["materials"]}
    suimo_proof = receipt("map14-suimo-native-performance-source-manifest.json")
    for entry in suimo_proof["materials"]:
        if not entry["reused"]:materials[(entry["cab"].casefold(),entry["path_id"])]=entry
    textures = {(m["cab"].casefold(),m["path_id"]):m for m in texture_proof["textures"]}
    for entry in suimo_proof["textures"]:
        if not entry["reused"]:
            key=(entry["cab"].casefold(),entry["path_id"])
            textures[key]=dict(path=entry["output"]["path"])
            texture_proof["outputs"].append(entry["output"])
    shader_sources = {(r["original"]["cab"].casefold(),r["original"]["path_id"]):(r,o)
                      for r,o in zip(shader_proof["inputs"],shader_proof["outputs"],strict=True)}
    shader_bindings = {r["path"]:r for r in shader_binding["bindings"] if not r["engine"]}
    template_path = ROOT/texture_proof["textures"][0]["path"]
    template = template_path.read_text(encoding="utf8").replace("--- !u!28 &2800000\n","--- !u!28 &1\n")
    dependencies.append(record(template_path))
    export_index = {}
    for path in EXPORTED.rglob("*.meta"):
        match=GUID.search(path.read_text(encoding="utf8"))
        if match:
            if match[1] in export_index:raise ValueError("Duplicate original export GUID")
            export_index[match[1]]=path.with_suffix("")
    replacements={};material_records=[];texture_records=[];used_templates=[]
    def verify_output(output):
        path=ROOT/output["path"]
        if sha(path.read_bytes())!=output["sha256"]:raise ValueError("Previously restored output changed: "+str(path))
        if "meta_sha256" in output and sha(Path(str(path)+".meta").read_bytes())!=output["meta_sha256"]:raise ValueError("Previously restored metadata changed")
        return path
    for cab,path_id in sorted(wanted):
        obj=original.reader(cab,path_id);fields=obj.read_typetree(check_read=True);raw_payload=obj.get_raw_data()
        fields=json.loads(encode(fields));obj.save_typetree(fields)
        if obj.get_raw_data()!=raw_payload:raise ValueError("Material JSON does not preserve original bytes")
        links=original.links(obj,fields)
        candidates=[]
        for path in EXPORTED.rglob("*.mat"):
            raw,converted=parse_material(path)
            if converted["m_Name"]!=fields["m_Name"]:continue
            expected=comparable(fields);projected=clean({k:v for k,v in converted.items() if k in expected})
            if same_scalars(expected,projected):candidates.append((path,raw))
        if len(candidates)!=1:raise ValueError("Original NPC appearance material template is ambiguous: "+fields["m_Name"])
        exported,raw=candidates[0];used_templates.append(record(exported))
        if (cab,path_id) in materials:
            prior=materials[cab,path_id]
            if prior["payload_sha256"]!=sha(raw_payload):raise ValueError("Reused original material identity changed")
            target=verify_output(prior["output"]);dependencies.extend([record(target),record(Path(str(target)+".meta"))])
            replacements[guid(exported)]=(guid(target),2100000)
            material_records.append(dict(cab=cab,path_id=path_id,name=fields["m_Name"],reused=True,output=record(target)))
            continue
        shader_link=next(p for p in links if p["field"]==".m_Shader")
        shader_obj=original.reader(shader_link["cab"],shader_link["path_id"])
        shader_source,shader_output=shader_sources[shader_link["cab"].casefold(),shader_link["path_id"]]
        if sha(shader_obj.get_raw_data())!=shader_source["original"]["payload_sha256"]:raise ValueError("Original NPC appearance shader payload differs")
        shader_target=verify_output(shader_output);binding=shader_bindings[source_path(shader_target)]
        if guid(shader_target)!=binding["guid"]:raise ValueError("Converted original shader GUID changed")
        raw,count=re.subn(r"(?m)^(  m_Shader: )\{[^\n]+\}$",lambda m:m[1]+"{fileID: "+str(binding["file_id"])+", guid: "+binding["guid"]+", type: 3}",raw)
        if count!=1:raise ValueError("Original shader pointer is not unique")
        for link in links:
            match=re.fullmatch(r"\.m_SavedProperties\.m_TexEnvs\[(\d+)\]\[1\]\.m_Texture",link["field"])
            if not match:
                if link["field"]!=".m_Shader":raise ValueError("Inspect additional original material pointer")
                continue
            key=(link["cab"].casefold(),link["path_id"])
            if key in textures:
                prior=textures[key];target=ROOT/prior["path"]
                expected=next(o for o in texture_proof["outputs"] if o["path"]==prior["path"]);verify_output(expected)
                dependencies.extend([record(target),record(Path(str(target)+".meta"))])
                texture_record=dict(cab=key[0],path_id=key[1],reused=True,output=record(target))
            else:
                texture_obj=original.reader(*key);texture=texture_obj.read(check_read=True)
                texture_fields=texture_obj.read_typetree(check_read=True)
                if texture.m_PlatformBlob:raise ValueError("Inspect original platform-specific texture layout")
                rgba,mips,profile,native_format=decode(texture)
                if native_format!=4:raise ValueError("Inspect native NPC appearance texture channel format")
                text=native_texture(template,texture,rgba).decode("utf8")
                values=dict(m_TextureFormat=native_format,m_Name=json.dumps(texture.m_Name,ensure_ascii=False),
                    m_ForcedFallbackFormat=texture.m_ForcedFallbackFormat,m_DownscaleFallback=int(texture.m_DownscaleFallback),
                    m_IsAlphaChannelOptional=int(texture.m_IsAlphaChannelOptional))
                for name,value in values.items():
                    text,n=re.subn(r"(?m)^(  "+name+r":)[^\n]*",lambda m:m[1]+" "+str(value),text)
                    if n!=1:raise ValueError("Native texture schema field differs")
                target=DESTINATION/"Textures"/(hashlib.md5(key[0].encode()).hexdigest()+"_"+key[1]+".texture.asset")
                write(target,text.encode());native_meta(target,2800000,hashlib.md5(("zjcs-map14-story-texture:"+":".join(key)).encode()).hexdigest())
                outputs.extend([target,Path(str(target)+".meta")])
                texture_record=dict(cab=key[0],path_id=key[1],reused=False,object_sha256=sha(texture_obj.get_raw_data()),
                    fields={k:v for k,v in texture_fields.items() if k!="image data"},raw_sha256=sha(rgba),mips=mips,profile=profile,output=record(target))
                textures[key]=dict(path=source_path(target));texture_proof["outputs"].append(dict(record(target),meta_sha256=sha(Path(str(target)+".meta").read_bytes())))
            texture_records.append(texture_record)
            prop=fields["m_SavedProperties"]["m_TexEnvs"][int(match[1])][0]
            pattern=re.compile(r"(?ms)^(      "+re.escape(prop)+r":\n)(.*?)(?=^      \S|\Z)")
            def bind_texture(m):
                body,n=re.subn(r"(?m)^(        m_Texture: )\{[^\n]+\}$",lambda p:p[1]+"{fileID: 2800000, guid: "+guid(target)+", type: 2}",m[2])
                if n!=1:raise ValueError("Original texture pointer is not unique")
                return m[1]+body
            raw,n=pattern.subn(bind_texture,raw)
            if n!=1:raise ValueError("Original texture property is not unique")
        target=DESTINATION/"Materials"/(hashlib.md5(cab.encode()).hexdigest()+"_"+path_id+".mat")
        write(target,raw.encode());native_meta(target,2100000,hashlib.md5(("zjcs-map14-story-material:"+cab+":"+path_id).encode()).hexdigest())
        outputs.extend([target,Path(str(target)+".meta")]);replacements[guid(exported)]=(guid(target),2100000)
        material_records.append(dict(cab=cab,path_id=path_id,name=fields["m_Name"],payload_sha256=sha(raw_payload),fields=fields,links=links,reused=False,output=record(target)))
    # Follow only this original prefab's dependency closure. Script stubs stop
    # traversal and are replaced by the migrated code or installed Unity package.
    queue=list(prefab_paths);seen=set();copies=[];scripts=[]
    script_roots=[ROOT/"unity/Assets/Game/Runtime/Official/StoryPlayables",ROOT/"unity/Assets/Game/Runtime/Official/StoryAnimation",ROOT/"unity/Assets/Game/Runtime/Official/StoryAttachment",ROOT/"unity/Assets/Game/Runtime/Official/StoryFollowBone",ROOT/"unity/Assets/Game/Runtime/Official/StoryNodes",ROOT/"unity/Assets/Game/Runtime/Official/StoryAnimatorTimeline",
                  ROOT/"unity/Assets/Game/Runtime/Official/StorySounds",ROOT/"unity/Assets/Game/Runtime/Official/ProfessionUI",
                  ROOT/"unity/Packages/com.unity.timeline",ROOT/"unity/Packages/com.unity.cinemachine"]
    while queue:
        path=queue.pop()
        if path in seen:continue
        seen.add(path)
        if guid(path) in replacements:continue
        if path.suffix==".cs":
            matches=[p for folder in script_roots for p in folder.rglob(path.name)]
            if len(matches)!=1:raise ValueError("Original runtime script is not uniquely migrated: "+path.stem)
            target=matches[0];replacements[guid(path)]=(guid(target),11500000);scripts.append(dict(original=record(path),target=record(target),meta=record(Path(str(target)+".meta"))))
            continue
        if path.suffix not in (".prefab",".asset",".playable",".anim"):raise ValueError("Unbound original performance asset: "+str(path))
        raw=path.read_text(encoding="utf8")
        for value in REFERENCE.findall(raw):
            if value in export_index:queue.append(export_index[value])
            elif value not in ("0000000000000000e000000000000000","0000000000000000f000000000000000"):raise ValueError("Unresolved original export reference: "+value)
        target=DESTINATION/path.relative_to(EXPORTED)
        new_guid=hashlib.md5(("zjcs-map14-npc-appearance-original:"+path.relative_to(EXPORTED).as_posix()).encode()).hexdigest()
        replacements[guid(path)]=(new_guid,None);copies.append((path,target,raw))
    for path,target,raw in copies:
        # Preserve object IDs, fields, hierarchy and curves. Remap GUIDs only;
        # native material IDs are fixed by their source-verified asset headers.
        def replace(m):
            value=m[1]
            return "guid: "+replacements[value][0] if value in replacements else m[0]
        raw=REFERENCE.sub(replace,raw);write(target,raw.encode())
        source_meta=Path(str(path)+".meta").read_text(encoding="utf8")
        write(Path(str(target)+".meta"),GUID.sub(lambda m:"guid: "+guid_replacement(path,replacements),source_meta).encode())
        outputs.extend([target,Path(str(target)+".meta")]);used_templates.append(record(path))
    from prepare_unity_map14_sources import meta
    for folder in [DESTINATION,*sorted(p for p in DESTINATION.rglob("*") if p.is_dir())]:
        meta(folder,folder=True);outputs.append(Path(str(folder)+".meta"))
    from verify_unity_map14_npc_appearance_native import verify
    native_counts,native_templates=verify()
    used_templates.extend(record(p) for p in native_templates)
    editor=ROOT/"unity/Assets/Game/Editor/Map14/NpcAppearancePlayables"
    if editor.is_dir():
        outputs.extend(p for p in editor.rglob("*") if p.is_file());outputs.append(Path(str(editor)+".meta"))
    catalog=ROOT/"unity/Assets/Game/Resources/Map14/Native/native-npc-appearance-playables-catalog.asset"
    if catalog.is_file():outputs.extend([catalog,Path(str(catalog)+".meta")])
    tool_paths=[Path(__file__),ROOT/"scripts/verify_unity_map14_npc_appearance_native.py",ROOT/"scripts/prepare_unity_map14_suimo_performance.py",ROOT/"scripts/official_native_yaml_verification.py",
                ROOT/"scripts/prepare_unity_map14_entity_textures.py",ROOT/"scripts/prepare_unity_map13_native_scene.py",
                ROOT/"scripts/prepare_unity_map14_entity_materials_native.py",ROOT/"scripts/prepare_unity_map14_entity_materials.py",
                ROOT/"scripts/prepare_unity_bundle_inputs.py",ROOT/"scripts/verify_unity_map14_story_playable_hierarchy.py"]
    converters=[record(p) for module in (astc_encoder,texture2ddecoder)
                for p in sorted(Path(module.__file__).parent.iterdir()) if p.suffix in (".py",".pyd")]
    write(RECEIPT,encode(dict(manifest="871_156598",mapClassId=14,dependencies=dependencies,inputs=list(original.used.values()),
        templates=used_templates,scripts=scripts,materials=material_records,textures=texture_records,
        nativeVerification=native_counts,tools=[record(p) for p in tool_paths],textureConverters=converters,
        outputs=[record(p) for p in sorted(set(outputs))],prefabs=[dict(guid=p["guid"],officialPath=p["path"],nativePath=source_path(DESTINATION/p["path"].removeprefix("Assets/"))) for p in prefabs],
        operation="Restore all four original NPC appear/disappear prefabs and dependency closure using migrated original application scripts. Reuse source-verified native materials/textures; recover additional materials byte-exactly, require every exported material scalar field to match, bind the identical original shader payload and original CAB/PathID textures with all mips. Preserve exported native hierarchy, curves, particles, trails and IDs, remapping GUIDs only.",
        limitations=["Four lightmap fields absent from native editor YAML retain original default descriptors; runtime sentinels/tiling require validation.",
                      "Live validation and its exact scope are recorded in map-14.md. Player model/animation binding, sound-bank playback, full story/map/save and Windows Player remain pending."])))
    print("Original NPC appearance native closure:",len(copies),"native files;",len(scripts),"script bindings;",len(material_records),"materials;",sum(not m["reused"] for m in material_records),"new materials.")


def guid_replacement(path,replacements):return replacements[guid(path)][0]


if __name__=="__main__":build()
