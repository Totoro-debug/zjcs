"""Bind the verified original stele prefabs to local native view services.

This converts assets and verifies references without starting Unity. The map
host and interactive/rendering acceptance are separate requirements.
"""
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from UnityPy.classes import PPtr
from official_native_yaml_verification import documents
from prepare_unity_map12_stele_entity_inputs import BUNDLE, OUTPUT, RECEIPT as INPUTS
from prepare_unity_map12_stele_entity_export import PROOF
from prepare_unity_map13_sources import encode, meta, sha, source_path, write

BASE = ROOT / "unity/Assets/Official/World/Map/12"
DESTINATION = BASE / "NativeSteleEntities"
CATALOG = ROOT / "unity/Assets/Game/Resources/Map12/Native/stele-entities-catalog.asset"
RECEIPT = ROOT / "assets/reference/official-map/map12-native-stele-entities-source-manifest.json"
RUNTIME = ROOT / "unity/Assets/Game/Runtime"
VIEWS = RUNTIME / "Official/SteleEntities"
HOST = RUNTIME / "Offline/World/NativeMap12Steles"
SPRITE_HOST = RUNTIME / "Offline/World/NativeMap13EntityAssets/NativeMap13EntitySpriteBinding.cs"
MATERIALS = ROOT / "unity/Assets/Official/World/Map/13/Source/native-entity-material-bindings.json"
PLAYABLES = ROOT / "assets/reference/official-map/map12-native-stele-playables-source-manifest.json"
SPRITES = ROOT / "assets/reference/official-map/map12-native-stele-sprites-source-manifest.json"
GUID = re.compile(r"(?m)^guid: ([a-f0-9]{32})$")
REFERENCE = re.compile(r"guid: ([a-f0-9]{32})")
TYPES = {"ECAttachAgentView", "ECGridTransformView", "ECPeriodicityInteractionStateView",
         "ECWorldStageView", "ECWorldStageCfgNormalProvider", "ECWorldStageInteractionOperationWindow"}


def record(path):
    return dict(path=source_path(path), sha256=sha(path.read_bytes()))


def guid(path):
    return GUID.search(Path(str(path) + ".meta").read_text(encoding="utf8"))[1]


def metadata(path, importer):
    identity = source_path(path).removeprefix("unity/")
    value = hashlib.md5(("zjcs-official:" + identity).encode()).hexdigest()
    raw = ("fileFormatVersion: 2\nguid: " + value + "\n" + importer +
           "\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode()
    target = Path(str(path) + ".meta")
    if target.exists() and target.read_bytes().replace(b"\r\n", b"\n") != raw:
        raise ValueError("Refusing to replace unrelated native stele metadata")
    write(target, raw)


def build():
    guarded, outputs = {}, []

    def watch(path):
        guarded[path] = sha(path.read_bytes())
        return record(path)

    def load(path):
        watch(path)
        return json.loads(path.read_bytes())

    def check(entry):
        path = ROOT / entry["path"]
        if sha(path.read_bytes()) != entry["sha256"]:
            raise ValueError("Original native stele dependency differs: " + entry["path"])
        watch(path)

    source, inputs, proof = load(OUTPUT), load(INPUTS), load(PROOF)
    playables, sprites, materials = load(PLAYABLES), load(SPRITES), load(MATERIALS)
    if any(v["manifest"] != "871_156598" for v in (source, inputs, proof, playables, sprites, materials)):
        raise ValueError("Original stele source version differs")
    for entry in inputs["outputs"] + proof["outputs"] + playables["outputs"] + sprites["outputs"]:
        check(entry)
    check(proof["input_receipt"])
    check(proof["entry"])
    if proof["counts"]["reference_records"] != 12 or proof["hierarchy_verification"]["counts"] != dict(
            pointers=46, values=166, runtime_tag_to_editor_name=8, GameObject=8, Transform=8, SpriteRenderer=2):
        raise ValueError("Original entity field/hierarchy verification is incomplete")
    selection = load(ROOT / "assets/reference/official-map/map12-source-manifest.json")
    selected = {v["path"]: v for v in selection["outputs"]}
    tables, templates = [], []
    for name in ("tables.json", "templates.json"):
        path = BASE / "Source" / name
        check(selected[source_path(path)])
        if name == "tables.json": tables = load(path)
        else: templates = load(path)
    entities = load(BASE / "map12-entities.json")["entities"]
    placements = [v for v in entities if v["entityClassId"] in (6650, 6655)]
    if sorted(v["placementId"] for v in placements) != [1813, 2530, 3013, 5519, 6406, 6614, 8320]:
        raise ValueError("Original seven stele placements differ")
    periodic = {v["PeriodicityGroupId"]: v for v in tables["Map/map_periodicity_interaction"]}
    placement_configs = []
    for entity in placements:
        if (entity["typeId"] != 218 or entity["state"] != 0 or not entity["active"]
                or len(entity["playerPeriodicityGroupIds"]) != 1
                or entity["playerPeriodicityGroupIds"] != entity["periodicityGroupIds"]):
            raise ValueError("Original stele initial state/group differs")
        group, = entity["playerPeriodicityGroupIds"]
        rule = periodic[group]
        if rule != dict(DateType=0, InteractionType=0, IsIgnoreOwner=False, IsTeamInteraction=True,
                        PeriodicityGroupId=group, PeriodicityMaxCount=1, PeriodicityTime=-1):
            raise ValueError("Original stele periodicity rule differs")
        placement_configs.append(dict(placementId=entity["placementId"], entityClassId=entity["entityClassId"],
            periodicityGroupId=group, periodicityMaxCount=rule["PeriodicityMaxCount"],
            x=entity["grid"][0], y=entity["grid"][1], direction=entity["direction"]))

    apk = Apk.current()
    originals = {str(v.path_id): v for v in apk.objects(BUNDLE)}
    native_material, = [v for v in materials["entries"] if v["cab"].casefold() ==
        "cab-224fe4f56699f8da48a113ddcb86cb30" and v["path_id"] == "-2465160282747821122"]
    if native_material["payload_sha256"] != "5af8555b6c7fb4d4be1762f256ae81418b459e0da3023284efa6e674c87904bc":
        raise ValueError("Original stele material bytes differ")
    for entry in (native_material["output"], native_material["shader"]):
        check(entry)
        watch(Path(str(ROOT / entry["path"]) + ".meta"))
    if any(v["state"] != "null" for v in native_material["textures"]):
        raise ValueError("Original stele material now requires explicit textures")
    material_path = ROOT / native_material["output"]["path"]
    if sha(Path(str(material_path) + ".meta").read_bytes()) != native_material["output"]["meta_sha256"]:
        raise ValueError("Original material metadata differs")
    for folder in (VIEWS, HOST, ROOT / "unity/Assets/Game/Editor/Map12SteleEntities",
                   RUNTIME / "Official/StelePlayables", RUNTIME / "Official/StoryAttachment",
                   RUNTIME / "Official/StoryPlayables", RUNTIME / "Official/StoryPlayableManager"):
        for path in sorted(folder.iterdir()):
            if path.is_file(): watch(path)
        watch(Path(str(folder) + ".meta"))
    watch(SPRITE_HOST); watch(Path(str(SPRITE_HOST) + ".meta"))
    watch(Path(__file__).resolve())
    for name in ("official_native_yaml_verification.py", "prepare_unity_map13_sources.py"):
        watch(ROOT / "scripts" / name)
    code = ROOT / ".tools/source-packages/current/871_156598/official-code"
    original_code = []
    for name in sorted(TYPES | {"ECViewBase", "ECViewDriver", "IECView", "WorldStageStateCfg", "ZSetUtils",
            "ECWorldStageStateCfgBaseProviderItemData", "ECWorldStageInteractionOperationItemData",
            "WorldStageInteractionOperationWindowClickData"}):
        path = code / "client-source" / (name + ".cs")
        original_code.append(watch(path))
    for name in ("Direction.cs", "EnumUtils.cs", "GridTransformComponent.cs", "GridGroundComponentInfo.cs", "EntityHandler.cs"):
        original_code.append(watch(code / "common-source/Common" / name))

    display_configs, bindings = [], []
    for prefab in source["prefabs"]:
        identity = prefab["entityClassId"]
        original_driver, = [v for v in source["components"] if v["path_id"] == prefab["driver_path_id"]]
        refs = original_driver["fields"]["references"]["RefIds"]
        if {v["type"]["class"] for v in refs} != TYPES or len(refs) != 6:
            raise ValueError("Original native stele view closure differs")
        native, = [v for v in proof["outputs"] if Path(v["path"]).stem == prefab["name"]]
        restored = (ROOT / native["path"]).read_text(encoding="utf8")
        parsed = documents(restored)
        root_id, = [i for i, (kind, fields) in parsed.items() if kind == "GameObject" and fields["m_Name"] == prefab["name"]]
        driver_id, = [i for i, (kind, fields) in parsed.items() if kind == "MonoBehaviour"]
        renderer_id, = [i for i, (kind, fields) in parsed.items() if kind == "SpriteRenderer"]
        renderer = parsed[renderer_id][1]
        owner_id = renderer["m_GameObject"]["fileID"]
        owner = parsed[owner_id][1]
        # Resolve by the original driver attachment rather than a display name:
        attach, = [v["data"] for v in refs if v["type"]["class"] == "ECAttachAgentView"]
        statue_transform = next(v["Bone"]["m_PathID"] for v in attach["Configs"]["Values"] if v["BoneName"] == "Statues")
        statue_go = originals[str(statue_transform)].read().m_GameObject.path_id
        source_renderer, = [v for v in source["objects"] if v["type"] == "SpriteRenderer"
                           and v["fields"]["m_GameObject"]["m_PathID"] == statue_go]
        renderer_key = prefab["cab"].casefold() + ":" + source_renderer["path_id"]
        if not any(v["sourceKey"] == renderer_key for v in sprites["renderer_bindings"]):
            raise ValueError("Original stele renderer has no exact native Sprite binding")
        material = PPtr(**source_renderer["fields"]["m_Materials"][0], assetsfile=originals[source_renderer["path_id"]].assets_file).deref()
        if material.assets_file.name.casefold() != native_material["cab"].casefold() or str(material.path_id) != native_material["path_id"] or sha(material.get_raw_data()) != native_material["payload_sha256"]:
            raise ValueError("Original renderer material source identity differs")
        patches = []
        result = restored
        def replace(old, new, count=1):
            nonlocal result
            if result.count(old) != count:
                raise ValueError("Ambiguous native stele binding replacement: " + old[:100])
            result = result.replace(old, new)
            patches.append((old, new, count))
        replace("guid: " + parsed[driver_id][1]["m_Script"]["guid"], "guid: " + guid(VIEWS / "ECViewDriver.cs"))
        replace("ns:\n        asm: Assembly-CSharp", "ns: Zjcs.Official.SteleEntities\n        asm: Zjcs.Official.SteleEntities", 6)
        periodic_view, = [v["data"] for v in refs if v["type"]["class"] == "ECPeriodicityInteractionStateView"]
        for field in ("mCanInteractionPlayable", "mEndInteractionPlayable"):
            playable, = [v for v in playables["prefabs"] if v["entityClassId"] == identity and v["field"] == field]
            original = PPtr(**periodic_view[field], assetsfile=originals[prefab["driver_path_id"]].assets_file).deref()
            if (original.assets_file.name.casefold(), str(original.path_id), sha(original.get_raw_data())) != (playable["cab"].casefold(), playable["path_id"], playable["object_raw_sha256"]):
                raise ValueError("Original stele playable source identity differs")
            target = ROOT / playable["nativePath"]
            watch(Path(str(target) + ".meta"))
            if guid(target) != playable["guid"]: raise ValueError("Original playable native GUID differs")
            native_pointer = next(v["data"][field] for v in parsed[driver_id][1]["references"]["RefIds"] if v["type"]["class"] == "ECPeriodicityInteractionStateView")
            replace(field + ": {fileID: " + str(native_pointer["fileID"]) + ", guid: " + native_pointer["guid"] + ", type: 2}",
                    field + ": {fileID: " + str(playable["fileId"]) + ", guid: " + playable["guid"] + ", type: 3}")
        replace("guid: " + renderer["m_Materials"][0]["guid"], "guid: " + guid(material_path))
        pointer = renderer["m_Sprite"]
        replace("m_Sprite: {fileID: " + str(pointer["fileID"]) + ", guid: " + pointer["guid"] + ", type: 2}", "m_Sprite: {fileID: 0}")
        binding_id = 114000000000000666
        if binding_id in parsed: raise ValueError("Native stele binding fileID collision")
        component = "  - component: {fileID: " + str(renderer_id) + "}\n"
        replace(component, component + "  - component: {fileID: " + str(binding_id) + "}\n")
        extension = ("--- !u!114 &" + str(binding_id) + "\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            "  m_GameObject: {fileID: " + str(owner_id) + "}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            "  m_Script: {fileID: 11500000, guid: " + guid(SPRITE_HOST) + ", type: 3}\n  m_Name:\n"
            "  m_EditorClassIdentifier:\n  SourceKey: " + renderer_key + "\n  Renderer: {fileID: " + str(renderer_id) + "}\n  Mask: {fileID: 0}\n")
        reverse = result
        for old, new, count in reversed(patches):
            if reverse.count(new) != count: raise ValueError("Native stele reversible patch is ambiguous")
            reverse = reverse.replace(new, old)
        if reverse != restored: raise ValueError("Original stele data was modified outside explicit bindings")
        result += extension
        final = documents(result)
        if len(final) != len(parsed) + 1 or len(final[owner_id][1]["m_Component"]) != len(owner["m_Component"]) + 1:
            raise ValueError("Native stele extension changed the original hierarchy")
        expected_guids = {guid(VIEWS / "ECViewDriver.cs"), guid(SPRITE_HOST), guid(material_path)}
        expected_guids.update(v["guid"] for v in playables["prefabs"] if v["entityClassId"] == identity)
        if set(REFERENCE.findall(result)) != expected_guids:
            raise ValueError("Native stele still references an unbound exported asset")
        final_refs = final[driver_id][1]["references"]["RefIds"]
        if len(final_refs) != 6 or any(v["type"]["ns"] != "Zjcs.Official.SteleEntities"
                or v["type"]["asm"] != "Zjcs.Official.SteleEntities" for v in final_refs):
            raise ValueError("Native stele managed-reference assembly binding differs")
        target = DESTINATION / "Prefabs" / (prefab["name"] + ".prefab")
        write(target, result.encode()); metadata(target, "PrefabImporter:")
        outputs.extend((record(target), record(Path(str(target) + ".meta"))))
        template = templates[str(identity)]
        grid = template["GridTransformComponent"]
        if "MoveAgentComponent" in template or grid["RealCenter"] != [0.0, 0.0] or grid["BodyRangeInfos"] != [dict(GroundType=2, IgnoreClick=False, OverrideGroundType=False, Pos=dict(X=0, Y=0))]:
            raise ValueError("Native stele selected grid service boundary differs")
        world = template["WorldStageComponent"]
        if world["AutoNextState"] or world["DestroyOnEnd"] or len(world["StateInfos"]) != 1 or world["StateInfos"][0]["SubStateList"] != [
                dict(Behaviour=dict(CompIndex=0, CompType=13), Conditions=[dict(CompIndex=0, CompType=110)], Results=[], Select=7)]:
            raise ValueError("Native stele selected state service boundary differs")
        display_configs.append(dict(entityClassId=identity, prefab=dict(fileID=root_id, guid=guid(target), type=3),
            realCenter=dict(x=grid["RealCenter"][0], y=grid["RealCenter"][1]), bodyRange=[v["Pos"] for v in grid["BodyRangeInfos"]]))
        bindings.append(dict(entityClassId=identity, input=native, output=record(target), renderer=renderer_key,
                             native_root=root_id, native_renderer=renderer_id, explicit_replacements=len(patches)))

    header = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
        "  m_Script: {fileID: 11500000, guid: " + guid(HOST / "NativeMap12SteleCatalog.cs") + ", type: 3}\n"
        "  m_Name: stele-entities-catalog\n  m_EditorClassIdentifier:\n  manifest: 871_156598\n  displays:\n")
    for display in display_configs:
        p = display["prefab"]
        header += ("  - entityClassId: " + str(display["entityClassId"]) + "\n    prefab: {fileID: " + str(p["fileID"]) + ", guid: " + p["guid"] + ", type: 3}\n"
            "    realCenter: {x: 0, y: 0}\n    bodyRange:\n    - {X: 0, Y: 0}\n")
    header += "  placements:\n"
    for placement in sorted(placement_configs, key=lambda v: v["placementId"]):
        header += "  - " + "\n    ".join(key + ": " + str(value) for key, value in placement.items()) + "\n"
    write(CATALOG, header.encode()); metadata(CATALOG, "NativeFormatImporter:\n  mainObjectFileID: 11400000")
    outputs.extend((record(CATALOG), record(Path(str(CATALOG) + ".meta"))))
    for folder in (DESTINATION, DESTINATION / "Prefabs"):
        meta(folder, folder=True); outputs.append(record(Path(str(folder) + ".meta")))
    for path, expected in guarded.items():
        if sha(path.read_bytes()) != expected: raise ValueError("Native stele input changed during conversion: " + source_path(path))
    receipt = dict(format=1, manifest="871_156598", mapClassId=12, inputs=[dict(path=source_path(p), sha256=h) for p, h in guarded.items()],
        original_code=original_code, source_assemblies=inputs["assemblies"], displays=display_configs,
        placements=placement_configs, periodicity_rules=[periodic[v] for v in sorted({p["periodicityGroupId"] for p in placement_configs})],
        material=native_material, bindings=bindings, outputs=outputs, resource="Map12/Native/stele-entities-catalog",
        operation="Reuse verified AssetRipper hierarchies/reference registries without reserializing original YAML. Reverse every explicit script/assembly/playable/material/Sprite pointer replacement and verify exact source text equality. Add one explicit original-source Sprite binding component per Statues node. Preserve all original nodes, component/child/RID order, float text, transforms, sorting and renderer data. Match every playable/material CAB, PathID and original object SHA before binding shared native assets.",
        adaptations=["Selected four-view EC lifecycle uses an explicit local entity/event service in place of original network/DI transport; it is not the full EC framework.",
            "The original periodicity state implementation delegates to the existing migrated core. Original AttachAgent bones, grid depth/direction and state provider fields are retained.",
            "Map-owned original playable manager/center/FIFO pools inject the center before activation and dispose only their own instances; no global resolver is replaced.",
            "The original window operation delegates to the map operation context; dedicated window hosting and map-wide input/visibility wiring remain separate."],
        limitations=["No Unity import, Editor validation, Play Mode input/render/save acceptance, Windows Player or publishing was run. Automatic runtime acceptance remains paused.",
            "The generic Map12 entity factory and dedicated stele UI/NPC team flow are not connected by this asset conversion.", "New assets have not entered a complete asset snapshot."])
    write(RECEIPT, encode(receipt))
    print("Native stele entities verified:", len(bindings), "prefabs;", len(placement_configs), "placements; 12 references; 4 playable and 2 Sprite bindings", flush=True)


if __name__ == "__main__":
    build()
