"""Migrate original entity display selectors and the current EC prefab index."""
from pathlib import Path
import json
import re

from official_apk import Apk, ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_water_components import import_meta
from read_official_native import NativeMethods


OFFICIAL = ROOT / "unity/Assets/Game/Runtime/Official/World/Map13Entities"
DATA = ROOT / "unity/Assets/Game/Resources/Map13/entity-presentation.json"
RECEIPT = ROOT / "assets/reference/official-map/map13-entity-presentation-source-manifest.json"
NAMESPACE = "Zjcs.Official.World.Map13Entities"


def wrap(text):
    text = "\n".join(line.rstrip() for line in text.splitlines())
    return "// Migrated from the selected official code; see Map13 entity presentation source receipt.\n" + text.replace(
        "namespace Common;", "namespace " + NAMESPACE + "\n{", 1).rstrip() + "\n}\n"


def method(text, signature):
    start = text.index(signature)
    brace = text.index("{", start)
    depth = 1
    end = brace + 1
    while depth:
        depth += (text[end] == "{") - (text[end] == "}")
        end += 1
    return text[start:end]


def build():
    apk = Apk.current()
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    source_receipt = ROOT / "assets/reference/official-map/map13-source-manifest.json"
    source = json.loads(source_receipt.read_bytes())
    code_manifest = code.parent / "code-manifest.json"
    assemblies = json.loads(code_manifest.read_bytes())
    if assemblies["manifest"] != apk.version:
        raise ValueError("Current code version differs")
    assembly_records = []
    for name in ["Console.dll", "Assembly-CSharp.dll"]:
        path = code / "managed/decoded" / name
        if sha(path.read_bytes()) != assemblies["assemblies"][name]["decoded_sha256"]:
            raise ValueError("Current official selector assembly differs")
        assembly_records.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    for entry in source["outputs"]:
        if sha((ROOT / entry["path"]).read_bytes()) != entry["sha256"]:
            raise ValueError("Original decoded map source changed: " + entry["path"])
    tables_path = ROOT / "unity/Assets/Official/World/Map/13/Source/tables.json"
    tables = json.loads(tables_path.read_bytes())
    index_obj = apk.asset("config_ec", "index.g.txt")
    index = json.loads(index_obj.read().m_Script)
    data = dict(manifest=apk.version, mapClassId=13,
        Displays=tables["Map/entity_map_display"], UiDisplays=tables["Map/entity_map_ui_display"],
        Actions=tables["Map/entity_map_display_action"],
        Directories=[dict(Directory=key, Ids=value) for key, value in index.items()],
        Areas=[dict(Area=row["Area"], AreaTag=row["AreaTag"]) for row in tables["Map/map_area"] if row["ClassId"] == 13])
    map_path = ROOT / "unity/Assets/Official/World/Map/13/map13.json"
    map_data = json.loads(map_path.read_bytes())
    data["MapSourceInputsSha256"] = map_data["SourceManifestSha256"]
    class_ids = {row["EntityId"] for row in map_data["Entities"]}
    indexed = {value for ids in index.values() for value in ids}
    if not class_ids.issubset(indexed):
        raise ValueError("Current original EC index lacks map entities: " + str(class_ids - indexed))
    inputs, outputs = [], []
    parser_base = code / "common-source/Common/LubanCsvParser.cs"
    inputs.append(dict(path=source_path(parser_base), sha256=sha(parser_base.read_bytes())))
    native_path = ROOT / ".tools/source-packages/commerce-native/libil2cpp-restored.bin"
    metadata_path = ROOT / ".tools/source-packages/official-code/global-metadata.dat"
    native = NativeMethods(native_path, metadata_path)
    address, method_index = native.by_name["GConfig.ParserBase.PostParse"]
    body = native.image[address:address + 4]
    if body != bytes.fromhex("c0035fd6"):
        raise ValueError("Original ParserBase.PostParse is not the verified empty native method")
    parser_base_proof = dict(method="GConfig.ParserBase.PostParse", method_index=method_index, address=address,
        bytes=body.hex(), instruction="ret",
        image=dict(path=source_path(native_path), sha256=sha(native_path.read_bytes())),
        metadata=dict(path=source_path(metadata_path), sha256=sha(metadata_path.read_bytes())))

    def install(path, raw):
        write(path, raw)
        if path.suffix in (".cs", ".asmdef"):
            import_meta(path, "MonoImporter" if path.suffix == ".cs" else "AssemblyDefinitionImporter")
        else:
            meta(path)
        outputs.append(dict(path=source_path(path), sha256=sha(raw)))

    for name in ["GroundType", "EntityMapDisplayInfo", "EntityMapUIDisplayInfo", "EntityMapDisplayActionInfo",
                 "EntityMapDisplayInfoParser", "EntityMapUIDisplayInfoParser", "EntityMapDisplayActionInfoParser"]:
        path = code / "common-source/Common" / (name + ".cs")
        raw = path.read_bytes(); text = raw.decode("utf-8-sig")
        inputs.append(dict(path=source_path(path), sha256=sha(raw)))
        if name.endswith("Parser"):
            info = name.removesuffix("Parser")
            text = text.replace("using System.Linq;\n", "").replace("using Luban;\n", "").replace("using GConfig;\n", "")
            # read_text normalizes line endings; source hashes use original bytes.
            text = text.replace("\r\n", "\n")
            text = text.replace("using System.Linq;\n", "").replace("using Luban;\n", "").replace("using GConfig;\n", "")
            text = text.replace(" : LubanCsvParser<" + info + ">", "")
            if name == "EntityMapUIDisplayInfoParser":
                text = text.replace("public override void PostParse()", "public void Load(" + info + "[] configs)")
                text = text.replace("\t\t((ParserBase)this).PostParse();\n", "")
            else:
                text = text.replace("protected override void PostParse(" + info + "[] configs)", "public void Load(" + info + "[] configs)")
                text = text.replace("\t\tbase.PostParse(configs);\n", "")
            loader = method(text, "protected override " + info + "[] LoadLubanTable")
            text = text.replace(loader, "")
        elif name != "GroundType":
            text = text.replace("\r\n", "\n").replace("using GConfig;\nusing Luban;", "using System;")
            text = re.sub(r"\[Config\([^\n]+\)\]", "[Serializable]", text)
            text = text.replace(" : BeanBase, IConfig", "").replace("public readonly ", "public ")
            for signature in ["public " + name + "(ByteBuf", "public static " + name + " Deserialize" + name,
                              "public void ResolveRef(Tables"]:
                text = text.replace(method(text, signature), "")
            text = text.replace("public override int GetTypeId()", "public int GetTypeId()")
        install(OFFICIAL / (name + ".cs"), wrap(text.replace("\r\n", "\n")).encode())
    # Preserve the original path methods and dictionary/cache lifetime. Only the
    # background-service/JSON/resource boundary and logger are local adapters.
    path = code / "client-source/Common/MyEntityIndexHandler.cs"
    raw = path.read_bytes(); text = raw.decode("utf-8-sig").replace("\r\n", "\n")
    inputs.append(dict(path=source_path(path), sha256=sha(raw)))
    methods = "\n\n".join(method(text, signature) for signature in ["public string GetDir", "public int[] GetIdsByDir", "public string GetEntityPath"])
    port = '''using System;
using System.Collections.Generic;
using UnityEngine;
namespace Common;
public sealed class MyEntityIndexHandler : IDisposable
{
    private static Dictionary<int, string> sEntityPathCache = new Dictionary<int, string>(2048);
    private Dictionary<int, string> mIdDirDict;
    private Dictionary<string, int[]> mDirIdDict;
    public MyEntityIndexHandler(Dictionary<string, int[]> directories)
    {
        mDirIdDict = directories;
        mIdDirDict = new Dictionary<int, string>();
        foreach (KeyValuePair<string, int[]> item in mDirIdDict)
        {
            int[] value = item.Value;
            foreach (int num in value)
                if (!mIdDirDict.TryAdd(num, item.Key))
                    Debug.LogError("MyEntityManager添加物件路径信息错误，检查是不是有重名Entity，EntityClassId:" + num + ", Path:" + item.Key);
        }
    }
    public void Dispose() { mIdDirDict?.Clear(); mDirIdDict?.Clear(); }
''' + methods + "\n}\n"
    install(OFFICIAL / "MyEntityIndexHandler.cs", wrap(port).encode())
    install(OFFICIAL / "Zjcs.Official.World.Map13Entities.asmdef", encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE, references=[])))
    import_meta(OFFICIAL, "DefaultImporter", folder=True)
    for folder in [ROOT / "unity/Assets/Game/Runtime/Offline/World/Map13Entities", ROOT / "unity/Assets/Game/Editor/Map13Entities"]:
        if not folder.is_dir():
            continue
        for path in sorted(folder.iterdir()):
            if path.suffix in (".cs", ".asmdef"):
                install(path, path.read_bytes())
        import_meta(folder, "DefaultImporter", folder=True)
    install(DATA, encode(data))
    manifest = dict(manifest=apk.version, mapClassId=13, path=source_path(DATA), sha256=sha(DATA.read_bytes()))
    install(DATA.with_name("entity-presentation-source.json"), encode(manifest))
    # Independent data projection for current placement contexts. Each level
    # narrows the candidate rows immediately; a deeper miss does not retry a
    # previously rejected broader parent, matching the original parser chain.
    def select(rows, keys):
        selected = list(enumerate(rows))
        for key, value, fallback in keys:
            desired = value if any(row[key] == value for _, row in selected) else fallback
            selected = [(index, row) for index, row in selected if row[key] == desired]
        if len(selected) > 1:
            raise ValueError("Original display selector did not resolve a unique row")
        return selected[0][0] if selected else -1
    areas = {row["Area"]: row["AreaTag"] for row in data["Areas"]}
    directories = {}
    for name, ids in index.items():
        for value in ids:
            directories.setdefault(value, name)
    cases = []
    for entity in map_data["Entities"]:
        x, y = entity["Grid"]
        area, ground = map_data["Navigation"]["Rows"][y]["Cells"][x]["RawValues"]
        area_tag = areas[area]
        keys = [("EntityClassId", entity["EntityId"], None), ("GroupId", 13, 0), ("MapClassId", 13, 0),
                ("AreaTag", area_tag, "IgnoreKey")]
        common = [("Tag", "", "")]
        cases.append(dict(PlacementId=entity["Id"], EntityClassId=entity["EntityId"], X=x, Y=y, Area=area,
            BaseGroundType=ground, AreaTag=area_tag,
            Display=select(data["Displays"], keys + [("GroundType", ground, 8888)] + common),
            Ui=select(data["UiDisplays"], keys + common),
            Action=select(data["Actions"], keys + [("GroundType", ground, 8888)] + common + [("State", 0, -1)]),
            EntityPath="Assets/Prefabs/EcEntity/" + directories[entity["EntityId"]] + "/" + str(entity["EntityId"]) + ".prefab"))
    install(DATA.with_name("entity-presentation-reference.json"), encode(dict(manifest=apk.version,
        mapSha256=sha(map_path.read_bytes()), Cases=cases)))
    counts = dict(displays=len(data["Displays"]), ui_displays=len(data["UiDisplays"]), actions=len(data["Actions"]),
                  directories=len(index), indexed_ids=sum(map(len, index.values())), map_entity_classes=len(class_ids), areas=len(data["Areas"]))
    result = dict(format=1, manifest=apk.version, mapClassId=13,
        code_manifest=dict(path=source_path(code_manifest), sha256=sha(code_manifest.read_bytes())), assemblies=assembly_records,
        parser_base_native=parser_base_proof,
        source_receipt=dict(path=source_path(source_receipt), sha256=sha(source_receipt.read_bytes())),
        tables=dict(path=source_path(tables_path), sha256=sha(tables_path.read_bytes())),
        map=dict(path=source_path(map_path), sha256=sha(map_path.read_bytes())),
        index=dict(source=apk.provenance(index_obj), object_sha256=sha(index_obj.get_raw_data()),
                   text_sha256=sha(index_obj.read().m_Script.encode())),
        official_code=inputs, outputs=outputs, counts=counts,
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Copy original three selector dictionaries, prefix processing, lookup order and branches. Adapt namespace and explicit local Load boundary instead of Luban parsing; retain original data fields, constants, ToString and GetTypeId. Make ten action DTO fields serializable instead of readonly. Copy original EC directory/path methods, static cache and duplicate-first-wins initialization; replace resource-service/JSON/logger boundary with explicit local input.",
        limitations=["Selector and index migration only; native entity runtime, prefab binding, interaction, saves and Windows release remain required."])
    write(RECEIPT, encode(result))
    print("Original entity presentation selectors prepared:", counts, flush=True)


if __name__ == "__main__":
    build()
