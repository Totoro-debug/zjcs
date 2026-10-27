"""Decode Map14's hand-in UI through the established official UI source pipeline."""
from __future__ import annotations
import hashlib
import json
import struct
import xxhash
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
KEY = "DialogHandInItemDialog"
BUNDLE = "defaultpackage_assets_prefabs_ui_itemdetail.bundle"


def extract(output: Path):
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from map11_support_ui import Map11SupportUi
    from prepare_unity_map11_ui import extract_panels
    apk = Apk.current()
    if apk.version != "871_156598":
        raise ValueError("Map14 UI requires the pinned current source")
    schemas = ScriptSchemas()
    exporter = Map11SupportUi(apk, schemas, output / "Sprites")
    from UnityPy.classes.PPtr import PPtr
    icon_config = apk.asset("scriptableconfig_modules_itemicon", "/ItemIconManagerConfig.asset")
    icon_fields = schemas.read(icon_config)
    icon = PPtr(**icon_fields["DefPrefab"], assetsfile=icon_config.assets_file).deref()
    keys_bundles = {KEY: BUNDLE, icon.read().m_Name: apk.provenance(icon)["bundle"]}
    source, manifest = {}, {"prefabs": {}}
    for key, bundle in keys_bundles.items():
        raw, _ = exporter.object_tree(key, bundle)
        nodes = raw["nodes"]
        for node in nodes:
            node["children"] = []
        for node in nodes[1:]:
            nodes[node["parentIndex"]]["children"].append(node)
        source[key] = nodes[0]
        manifest["prefabs"][key] = dict(raw["source"], bundle=bundle)
    panels, components, sprites, shaders = extract_panels(
        apk, schemas, source, manifest, tuple(source), source, exporter.ui_schemas,
        exporter, b"", output_root=output)
    # Preserve additional original component fields even where the shared UI adapter
    # does not consume them yet; every pointer resolves to an original hierarchy path.
    extra = []
    animators,button_animation=[],None
    for key, bundle in keys_bundles.items():
        paths, objects, native_animators = {}, [], []
        def visit(obj, prefix=""):
            go = obj.read()
            path = prefix + "/" + go.m_Name
            paths[(obj.assets_file.name, obj.path_id)] = path
            for pair in go.m_Component:
                child = pair.component.deref()
                paths[(child.assets_file.name, child.path_id)] = path
                if child.type.name in ("RectTransform", "Transform"):
                    for ref in child.read().m_Children:
                        visit(ref.read().m_GameObject.deref(), path)
                elif child.type.name == "Animator":
                    native_animators.append((path,child))
                elif child.type.name == "MonoBehaviour":
                    objects.append(child)
        visit(apk.asset(bundle, "/" + key + ".prefab"))
        from prepare_unity_map11_ui import animation_record
        def find_tree(node,wanted):
            if node["path"]==wanted:return node
            for child in node.get("children",[]):
                match=find_tree(child,wanted)
                if match:return match
        for location,obj in native_animators:
            fields=obj.read_typetree()
            controller=obj.read().m_Controller
            settings={k:v for k,v in fields.items() if k not in ("m_GameObject","m_Avatar","m_Controller")}
            animators.append({"panel":key,"path":location,"settings":settings,"source":dict(apk.provenance(obj),objectSha256=hashlib.sha256(obj.get_raw_data()).hexdigest()),
                              "controller":controller.read().m_Name if controller.path_id else None})
            if not controller.path_id:continue
            ctrl=controller.deref()
            if ctrl.read().m_Name!="Btn_Base_Click":raise ValueError("Unrecognized original hand-in button controller")
            if button_animation is None:
                tree=find_tree(source[key],location)
                clips=[animation_record(apk,ref.deref(),tree) for ref in ctrl.read().m_AnimationClips if ref.path_id]
                if {c["name"] for c in clips}!={"Normal","Highlighted","Pressed","Selected","Disabled"} or any(c["unknown"] for c in clips):
                    raise ValueError("Original button controller needs complete clip conversion")
                for ref,clip in zip((r for r in ctrl.read().m_AnimationClips if r.path_id),clips):
                    clip["objectSha256"]=hashlib.sha256(ref.deref().get_raw_data()).hexdigest()
                    clip["frameRate"]=ref.read().m_SampleRate
                button_animation={"root":location,"source":dict(apk.provenance(ctrl),objectSha256=hashlib.sha256(ctrl.get_raw_data()).hexdigest()),
                                  "native":ctrl.read_typetree(),"clips":clips}
        def resolve(value, owner):
            if isinstance(value,dict):
                if "m_FileID" in value and "m_PathID" in value:
                    if not value["m_PathID"]: return None
                    obj=PPtr(**value, assetsfile=owner.assets_file).deref()
                    result=paths.get((obj.assets_file.name,obj.path_id))
                    if result is not None:return result
                    result={"source":apk.provenance(obj),"type":obj.type.name}
                    if obj.type.name=="MonoBehaviour" and obj.read(check_read=False).m_Script.read().m_ClassName=="WwiseEventReference":
                        result["event"]=schemas.read(obj)
                    return result
                return {k:resolve(v,owner) for k,v in value.items()}
            if isinstance(value,list): return [resolve(v,owner) for v in value]
            return value
        for obj in objects:
            cls=obj.read(check_read=False).m_Script.read().m_ClassName
            if cls in ("AwardPanelLayoutElement", "MaintainGlobalScaleUI", "ButtonSound", "PopupWindowSound", "UIBloomGroup", "BgDialogElement", "CountRedDotImage", "PreviewTool"):
                extra.append({"panel":key,"path":paths[(obj.assets_file.name,obj.path_id)],
                    "type":cls,"fields":resolve(schemas.read(obj),obj),"source":apk.provenance(obj)})
    for component in components:
        if component["type"]!="PopupWindow":continue
        for record in component["fields"]["clips"]:
            native=next(o for o in apk.objects(record["source"]["bundle"]) if o.path_id==int(record["source"]["path_id"]))
            record["frameRate"]=native.read().m_SampleRate
            record["objectSha256"]=hashlib.sha256(native.get_raw_data()).hexdigest()
    components.extend(extra)
    language_manifest=ROOT/"assets/reference/official-map/localization-source-manifest.json"
    language_inputs=json.loads(language_manifest.read_text(encoding="utf-8"))["inputs"]
    wanted_keys={n["localization_key"] for p in panels for n in p["nodes"] if n["localization_key"]}
    wanted_keys.update(("task_handinitem_succeed_tip", "ui_ItemIcon_count_key", "ui_common_count_plus"))
    wanted={xxhash.xxh64_intdigest(k.encode()):k for k in wanted_keys}
    texts={}
    for record in language_inputs.values():
        raw_language=apk.archive.read(record["source"]["path"])
        if hashlib.sha256(raw_language).hexdigest()!=record["sha256"]:
            raise ValueError("Pinned APK language hash mismatch")
        count=struct.unpack_from("<i",raw_language)[0];start=4+count*12
        for i in range(count):
            identity,offset=struct.unpack_from("<Qi",raw_language,4+i*12)
            if identity not in wanted: continue
            pos=start+offset;size=struct.unpack_from("<H",raw_language,pos)[0]
            texts[wanted[identity]]=raw_language[pos+2:pos+2+size].decode("utf-8")
    for k in wanted_keys-set(texts):
        if k in ("{0}","0","1"):texts[k]=k
        else:raise ValueError("Unresolved original UI language key: "+k)
    for p in panels:
        for n in p["nodes"]:
            if n["localization_key"]:n["text_value"]=texts[n["localization_key"]]
    records = []
    for uri, sprite in sorted(sprites.items()):
        path = ROOT / uri.removeprefix("res://")
        records.append({"uri": uri, "unityAssetPath": path.relative_to(ROOT / "unity").as_posix()
                        if path.is_relative_to(ROOT / "unity") else str(path),
                        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                        "width": sprite["width"], "height": sprite["height"],
                        "border": sprite["border"], "pivot": sprite["pivot"],
                        "pixelsPerUnit": sprite["ppu"]})
    payload = {"manifest": apk.version, "mapClassId": 14, "panels": panels,
               "components": components, "sprites": records, "shaderPrograms": shaders,
               "texts":[{"key":k,"value":v} for k,v in sorted(texts.items())],
               "languageInputs":language_inputs,"iconConfig":apk.provenance(icon_config),
               "source": apk.provenance(apk.asset(BUNDLE, "/" + KEY + ".prefab")),
               "resources": exporter.resources, "schemas": schemas.records,
               "uiSchemas": exporter.ui_schemas.records,"animators":animators,"buttonAnimation":button_animation}
    colors=schemas.read(apk.asset("scriptableconfig_modules_systemsetting", "/GlobalColors.asset"))
    theme=next(r for r in colors["TextColorConfig"]["TextColorLists"] if r["ThemeColorType"]==1)
    lack=theme["TextColors"][theme["TypeList"].index(3)]
    if lack["EnableGradient"]:raise ValueError("Item progress needs original gradient renderer")
    payload["itemProgressEnoughColor"]=[colors["Green"][c] for c in "rgba"]
    payload["itemProgressLackColor"]=[lack["Color"][c] for c in "rgba"]
    payload["globalColorsSource"]=apk.provenance(apk.asset("scriptableconfig_modules_systemsetting", "/GlobalColors.asset"))
    from prepare_battle_hud_materials import shader_text
    ui_config=apk.asset("scriptableconfig_modules_systemsetting", "/GlobalUIConfig.asset")
    ui_fields=schemas.read(ui_config)
    material=PPtr(**ui_fields["GrayMat"],assetsfile=ui_config.assets_file).deref()
    gray=material.read();shader_obj=gray.m_Shader.deref()
    shader_name,native_shader,variants=shader_text(shader_obj.read(),gray.m_ValidKeywords)
    if shader_name!="Gray" or len(variants)!=4:raise ValueError("Unexpected official gray UI shader")
    native_shader=native_shader.replace('Shader "Zjcs/Combat/HudGray"','Shader "Zjcs/Official/UI/Gray"',1)
    shader_path=output/"UIGray.shader";shader_path.write_bytes(native_shader.encode("utf-8"))
    payload["grayMaterial"]={"configSource":apk.provenance(ui_config),"source":apk.provenance(material),
        "shaderSource":apk.provenance(shader_obj),"shaderSha256":hashlib.sha256(native_shader.encode()).hexdigest(),
        "shaderName":"Zjcs/Official/UI/Gray","shaderPath":str(shader_path),
        "floats":dict(gray.m_SavedProperties.m_Floats),
        "colors":{k:[getattr(v,c) for c in "rgba"] for k,v in gray.m_SavedProperties.m_Colors},
        "keywords":gray.m_ValidKeywords,"variantKeywords":[v["keywords"] for v in variants]}
    # Reuse already converted official audio only after proving the entire bank
    # payloads still match the active version, including APK-first provenance.
    from official_source import OfficialSource
    audio_receipt_path=ROOT/"assets/reference/commerce-components/audio-source-manifest.json"
    audio_receipt=json.loads(audio_receipt_path.read_text(encoding="utf-8"))
    selected=OfficialSource();current_banks={r["name"]:r for r in selected.manifest("WwiseBnk")["bundles"]}
    index_dir=ROOT/".tools/source-packages/WwiseBnk"
    bank_receipts=json.loads((index_dir/"bundle-sources.json").read_text(encoding="utf-8"))
    audio_banks=[]
    for key,previous in sorted(audio_receipt["packages"].items()):
        package=index_dir/key;raw=package.read_bytes();current=current_banks[key]
        if hashlib.sha256(raw).hexdigest()!=previous["sha256"] or hashlib.md5(raw).hexdigest()!=current["hash"]:
            raise ValueError("Current UI audio bank needs conversion: "+key)
        entry="assets/yoo/WwiseBnk/"+current["file"]
        packaged=entry in apk.archive.namelist()
        if packaged and apk.archive.read(entry)!=raw:raise ValueError("APK-first UI audio package mismatch")
        audio_banks.append({"apkEntry":entry if packaged else None,"path":package.relative_to(ROOT).as_posix(),"sha256":previous["sha256"],
                           "currentManifest":current,"source":bank_receipts[key]})
    payload["audioBanks"]=audio_banks
    payload["audioReceipt"]={"path":audio_receipt_path.relative_to(ROOT).as_posix(),
        "sha256":hashlib.sha256(audio_receipt_path.read_bytes()).hexdigest()}
    payload["audio"]={}
    for uri,record in audio_receipt["resources"].items():
        if record["event"] not in ("Main_Cursor_Normal","Main_Cursor_Back"):continue
        original=ROOT/uri.removeprefix("res://")
        native=ROOT/"unity/Assets/Official/Commerce/Art/audio"/original.name
        if hashlib.sha256(original.read_bytes()).hexdigest()!=record["sha256"] or native.read_bytes()!=original.read_bytes():
            raise ValueError("Verified official UI audio changed: "+uri)
        payload["audio"][record["event"]]=dict(record,uri=uri,unityAssetPath=native.relative_to(ROOT/"unity").as_posix())
    if len(payload["audio"])!=2:raise ValueError("Both original hand-in sound events are required")
    target = output / "handin-ui.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"nodes": len(panels[0]["nodes"]), "components":len(components),
                      "sprites":len(records), "output": str(target)}, ensure_ascii=False))
    return payload


if __name__ == "__main__":
    import sys
    extract(Path(sys.argv[1]) if len(sys.argv)>1 else ROOT/".tools/map14-handin-ui-verified")
