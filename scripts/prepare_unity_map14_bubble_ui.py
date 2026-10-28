"""Recover the original entity-Timeline bubble prefab, not a replacement layout."""
import json
import base64
from pathlib import Path
from official_apk import Apk, ROOT
from UnityPy.classes.PPtr import PPtr
from official_manifest import read_asset_manifest
from official_unity_schema import ScriptSchemas
from map11_support_ui import Map11SupportUi
from prepare_unity_map11_ui import extract_panels
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from prepare_unity_map10_entities import write_texture_meta

OUTPUT=ROOT/"unity/Assets/Official/World/Map/14/BubbleUI"
RECEIPT=ROOT/"assets/reference/official-map/map14-bubble-ui-source-manifest.json"


def record(path):return dict(path=source_path(path),sha256=sha(path.read_bytes()))


def build():
    apk=Apk.current()
    if apk.version!="871_156598":raise ValueError("Unexpected selected original version")
    code=ROOT/".tools/source-packages/current"/apk.version/"official-code"
    proof=json.loads((code.parent/"code-manifest.json").read_bytes())
    for name in ("Assembly-CSharp.dll","Framework.dll"):
        if sha((code/"managed/decoded"/name).read_bytes())!=proof["assemblies"][name]["decoded_sha256"]:
            raise ValueError("Selected original assembly changed: "+name)
    assets=read_asset_manifest(apk.source.manifest_bytes("DefaultPackage",apk.archive))
    matches=[r for r in assets if r["path"].endswith("/BubbleDialog.prefab")]
    if len(matches)!=1:raise ValueError("Expected one selected original BubbleDialog")
    schemas=ScriptSchemas(code_root=code)
    exporter=Map11SupportUi(apk,schemas,OUTPUT/"Sprites",code)
    tmp_schema=ROOT/"unity/Library/ScriptAssemblies/Unity.TextMeshPro.dll"
    exporter.ui_schemas.generator.load_dll(tmp_schema.read_bytes())
    native,_=exporter.object_tree("BubbleDialog",matches[0]["bundle"])
    nodes=native["nodes"]
    for node in nodes:node["children"]=[]
    for node in nodes[1:]:nodes[node["parentIndex"]]["children"].append(node)
    tree=nodes[0]
    manifest=dict(manifest=apk.version,prefabs={"BubbleDialog":dict(native["source"],path=native["source_path"])},resources=exporter.resources)
    panels,components,sprites,shaders=extract_panels(apk,schemas,{"BubbleDialog":tree},manifest,
        ["BubbleDialog"],{"BubbleDialog":tree},exporter.ui_schemas,exporter,encode(tree),
        output_root=OUTPUT,preserve_native_transforms=True)
    # Retain every serialized component, including scripts not handled by the
    # common UI extraction whitelist. Resolve references only against real GOs.
    root=apk.asset(matches[0]["bundle"],"/BubbleDialog.prefab")
    paths={};objects=[];counts={}
    def visit(obj,prefix=""):
        go=obj.read();stem=prefix+"/"+go.m_Name;number=counts.get(stem,0);counts[stem]=number+1
        path=stem+(f" ({number})" if number else "")
        paths[(obj.assets_file.name,obj.path_id)]=path
        for entry in go.m_Component:
            component=entry.component.deref();paths[(component.assets_file.name,component.path_id)]=path
            objects.append(component)
            if component.type.name in ("Transform","RectTransform"):
                for child in component.read().m_Children:visit(child.read().m_GameObject.deref(),path)
    visit(root)
    def resolve(value,owner):
        if isinstance(value,dict):
            if "m_FileID" in value and "m_PathID" in value:
                if not value["m_PathID"]:return None
                obj=PPtr(**value,assetsfile=owner.assets_file).deref()
                return paths.get((obj.assets_file.name,obj.path_id),dict(source=apk.provenance(obj),type=obj.type.name))
            return {k:resolve(v,owner) for k,v in value.items()}
        if isinstance(value,list):return [resolve(v,owner) for v in value]
        return value
    native_components=[]
    for obj in objects:
        kind=obj.type.name
        if kind=="MonoBehaviour":
            script=obj.read(check_read=False).m_Script.read();kind=script.m_ClassName
            reader=exporter.ui_schemas if script.m_AssemblyName.removesuffix(".dll") in ("UnityEngine.UI","Unity.TextMeshPro") else schemas
            fields=reader.read(obj)
        else:fields=obj.read_typetree()
        native_components.append(dict(path=paths[(obj.assets_file.name,obj.path_id)],type=kind,
            fields=resolve(fields,obj),source=apk.provenance(obj),rawSha256=sha(obj.get_raw_data()),
            rawDataBase64=base64.b64encode(obj.get_raw_data()).decode()))
    sprite_records=[]
    for uri,s in sorted(sprites.items()):
        path=ROOT/uri.removeprefix("res://");write_texture_meta(path)
        sprite_records.append(dict(uri=uri,file=s["file"],unityAssetPath=path.relative_to(ROOT/"unity").as_posix(),
            sha256=sha(path.read_bytes()),width=s["width"],height=s["height"],border=s["border"],pivot=s["pivot"],pixelsPerUnit=s["ppu"],source=exporter.resources[uri]))
    payload=OUTPUT/"bubble-ui.json"
    write(payload,encode(dict(manifest=apk.version,mapClassId=14,panels=panels,components=components,
        nativeComponents=native_components,nativeNodes=nodes,sprites=sprite_records,shaderPrograms=shaders,prefab=manifest["prefabs"]["BubbleDialog"])))
    meta(payload);meta(OUTPUT,folder=True);meta(OUTPUT/"Sprites",folder=True)
    outputs=[p for p in OUTPUT.rglob("*") if p.is_file()]+[Path(str(OUTPUT)+".meta")]
    write(RECEIPT,encode(dict(manifest=apk.version,mapClassId=14,operation="Recover original BubbleDialog hierarchy, native transforms, all component fields and pointer bindings, original sprites and native object SHA256. Selected APK-first verified cloud resolution; no replacement layout.",
        prefab=matches[0],source=manifest["prefabs"]["BubbleDialog"],resources=exporter.resources,
        assemblies=[record(code/"managed/decoded"/n) for n in ("Assembly-CSharp.dll","Framework.dll")],
        tools=[record(Path(__file__)),record(ROOT/"scripts/map11_support_ui.py"),record(ROOT/"scripts/prepare_unity_map11_ui.py")],
        schemaDependencies=[record(tmp_schema)],
        dependencies=[record(ROOT/"assets/reference/official-map/map13-native-text-font-source-manifest.json")],
        outputs=[record(p) for p in outputs],limitations=["Native Windows prefab import, original BubbleDialog/Timeline behavior and graphical input validation still required."])))
    print("Original BubbleDialog:",len(nodes),"nodes,",len(native_components),"native components,",len(sprites),"sprites",flush=True)


if __name__=="__main__":build()
