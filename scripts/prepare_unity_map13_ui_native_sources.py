"""Decode every original Map13 UI component, including particles and sound references."""
import json
from pathlib import Path
from collections import Counter
from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas, fields_tree
from map13_entity_native_schemas import install
from home_particle_schema import load as load_particle_schema
from map11_support_ui import Map11SupportUi
from map13_ui_native_schemas import install_input_field
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from prepare_unity_map13_story_playables import serializable


def build():
    source = ROOT / "unity/Assets/Official/World/Map/13/Source/ui-prefabs-source-manifest.json"
    receipt = json.loads(source.read_bytes())
    apk = Apk.current()
    if receipt["manifest"] != apk.version or apk.version != "871_156598":
        raise ValueError("Unexpected Map13 native UI source")
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    schemas = NativeSchemas(code)
    proof = install(schemas, code)
    load_particle_schema(schemas.default)
    ui_schemas = Map11SupportUi(apk, schemas.default, code_root=code).ui_schemas
    ui_schemas.generator.load_dll((ROOT / "unity/Library/ScriptAssemblies/Unity.TextMeshPro.dll").read_bytes())
    ui_schemas.generator.load_dll((ROOT / "unity/Library/ScriptAssemblies/spine-unity.dll").read_bytes())
    proof["tmp_input_field"] = install_input_field(ui_schemas)
    # AssetsTools follows the nonserializable SafeAction/public-property graph.
    # Preserve only the fields serialized by the two original client classes.
    original_tree = schemas.default.tree
    def ui_tree(assembly, name):
        original_fields = {
            "AutoAlignForSingleMultiLineText": [("mTxt", "PPtr<MonoBehaviour>"), ("mTmp", "PPtr<MonoBehaviour>"),
                ("mSingleLineAlignment", "int"), ("mMultiLineAlignment", "int")],
            "AutoAlignForSingleMultiLineTextContent": [("mTxt", "PPtr<MonoBehaviour>"),
                ("mSingleLineAlignment", "int"), ("mMultiLineAlignment", "int")],
        }
        if name not in original_fields:
            return original_tree(assembly, name)
        result = fields_tree(schemas.default, original_fields[name])
        fields = [dict(level=n.m_Level, type=n.m_Type, name=n.m_Name, flags=n.m_MetaFlag or 0) for n in result.traverse()]
        schemas.default.cache[assembly + ":" + name] = fields
        schemas.default.records[assembly + ":" + name] = fields
        return result
    schemas.default.tree = ui_tree
    objects, roots = {}, []
    def capture(obj):
        identity = obj.assets_file.name.lower() + ":" + str(obj.path_id)
        if identity not in objects:
            if obj.type.name == "MonoBehaviour":
                script = obj.read(check_read=False).m_Script.read()
                kind = (script.m_Namespace + "." if script.m_Namespace else "") + script.m_ClassName
                if kind == "AutoAlignForSingleMultiLineText" and len(obj.get_raw_data()) != 64:
                    raise ValueError("Original AutoAlign four-field extent differs")
                try:
                    if kind == "TMPro.TMP_InputField":
                        fields = ui_schemas.read(obj)
                    else:
                        try:
                            fields, _ = schemas.read(obj)
                        except (ValueError, EOFError, AssertionError):
                            # Engine layout types need real Core/UI assemblies.
                            # Both readers enforce full-byte consumption.
                            fields = ui_schemas.read(obj)
                except Exception as error:
                    raise ValueError("Native Map13 UI component " + kind + " (" + identity + ")") from error
            else:
                kind, fields = obj.type.name, obj.read_typetree()
            objects[identity] = dict(identity=identity, type=kind, source=apk.provenance(obj),
                object_sha256=sha(obj.get_raw_data()), fields=serializable(fields))
        return identity
    def hierarchy(obj):
        components, children = [], []
        for record in obj.read().m_Component:
            component = record.component.deref()
            components.append(capture(component))
            if component.type.name in ("Transform", "RectTransform"):
                children = [hierarchy(ref.read().m_GameObject.deref()) for ref in component.read().m_Children]
        return dict(object=capture(obj), components=components, children=children)
    for key, record in receipt["prefabs"].items():
        candidates = [obj for obj in apk.objects(record["bundle"]) if str(obj.path_id) == str(record["path_id"]) and obj.type.name == "GameObject"]
        if len(candidates) != 1 or sha(candidates[0].get_raw_data()) != sha(apk.asset(record["bundle"], "/" + key + ".prefab").get_raw_data()):
            raise ValueError("Resolve original native UI root: " + key)
        roots.append(dict(key=key, hierarchy=hierarchy(candidates[0])))
        print("Decoded native Map13 UI:", key, len(objects), flush=True)
    output = ROOT / "unity/Assets/Official/World/Map/13/Source/native-ui-components.json"
    raw = encode(dict(manifest=apk.version, mapClassId=13, prefabs=roots, objects=list(objects.values()), schema_proof=proof, schemas=schemas.default.records, native_ui_schemas=ui_schemas.records))
    write(output, raw); meta(output)
    result = dict(manifest=apk.version, mapClassId=13,
        operation="Strictly read original UI GameObjects, original Transform/RectTransform hierarchy and every component field including native ParticleSystem/Renderer, Coffee UIParticle and Wwise sound references. This source capture does not imply Windows component binding or particle/audio playback.",
        input=dict(path=source_path(source), sha256=sha(source.read_bytes())),
        output=dict(path=source_path(output), sha256=sha(raw)),
        types=dict(Counter(obj["type"] for obj in objects.values())), prefabs=len(roots), objects=len(objects),
        tools=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in
            [Path(__file__).resolve(), ROOT / "scripts/map13_ui_native_schemas.py", ROOT / "scripts/official_unity_schema.py",
             ROOT / "scripts/map13_native_schemas.py", ROOT / "scripts/map13_entity_native_schemas.py"]],
        client_fields=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in
            [code / "client-source/AutoAlignForSingleMultiLineText.cs", code / "client-source/AutoAlignForSingleMultiLineTextContent.cs"]])
    write(ROOT / "assets/reference/official-map/map13-native-ui-components-source-manifest.json", encode(result))
    print("Map13 native UI component source:", result["prefabs"], result["objects"], result["types"], flush=True)


if __name__ == "__main__":
    build()
