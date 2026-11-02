"""Bind the original common player/camera and a configured map's native labels."""
import argparse
import json
import re
from pathlib import Path
import xxhash

from official_apk import ROOT, Apk
from official_unity_schema import ScriptSchemas
from prepare_unity_map13_sources import encode, write
from prepare_unity_map13_water_components import import_meta
from prepare_mainline_map15_configurations import record, sha
from refresh_current_localization import language


def build(map_id=15):
    apk = Apk.current()
    sources = []

    def child(root, name):
        transform = next(c.component.read() for c in root.read().m_Component if c.component.type.name == "Transform")
        return next(ref.read().m_GameObject.deref() for ref in transform.m_Children if ref.read().m_GameObject.read().m_Name == name)

    def component(root, kind):
        return next(c.component.deref() for c in root.read().m_Component if c.component.type.name == kind)

    def provenance(obj):
        sources.append(dict(apk.provenance(obj), object_sha256=sha(obj.get_raw_data())))

    player = apk.asset("defaultpackage_assets_prefabs_ecentity_mapworld_worldmapcharacter.bundle", "/3001.prefab")
    model = player
    for name in ("3001_view", "Model", "Offset", "Player_Model_Map"):
        model = child(model, name)
    transform, renderer = component(model, "Transform"), component(model, "MeshRenderer")
    offset = transform.read().m_Father.deref()
    view = dict(modelScale={k: getattr(transform.read().m_LocalScale, k) for k in "xyz"},
        modelOffset={k: getattr(offset.read().m_LocalPosition, k) for k in "xyz"},
        sortingOrder=renderer.read().m_SortingLayer * 1000 + renderer.read().m_SortingOrder,
        sortingLayerId=renderer.read().m_SortingLayerID, nativeSortingOrder=renderer.read().m_SortingOrder)
    root = apk.asset("defaultpackage_assets_prefabs_map_common_entityui.bundle", "/WorldCamera.prefab")
    controller = child(root, "controller")
    adaptor = next(c.component.deref() for c in controller.read().m_Component if c.component.type.name == "MonoBehaviour"
        and c.component.deref().read(check_read=False).m_Script.read().m_ClassName == "OrthographicCameraResolutionAdaptor")
    camera = component(child(root, "Main Camera"), "Camera")
    schemas = ScriptSchemas(code_root=ROOT / ".tools/source-packages/official-code")
    fields = schemas.read(adaptor)
    follow = child(controller, "followVCam")
    follow_controller = next(c.component.deref() for c in follow.read().m_Component if c.component.type.name == "MonoBehaviour"
        and c.component.deref().read(check_read=False).m_Script.read().m_ClassName == "FollowCameraController")
    follow_fields = schemas.read(follow_controller)
    view.update(cameraSafeExtents=fields["mySafeArea"]["m_Extent"], cameraAdaptPriority=fields["mAdapterPriority"],
        cameraDefaultSize=camera.read().orthographic_size, cameraNear=camera.read().near_clip_plane,
        cameraFar=camera.read().far_clip_plane, cameraZ=follow_fields["mZOffset"])
    for obj in (player, model, transform, offset, renderer, root, camera, adaptor, follow_controller):
        provenance(obj)
    prepared_path = ROOT / ".tools/source-packages/prepared-code-source-manifest.json"
    prepared = json.loads(prepared_path.read_bytes())
    expected = {row["path"]: row["sha256"] for row in prepared["outputs"]}
    assembly = record(ROOT / ".tools/source-packages/official-code/managed/decoded/Assembly-CSharp.dll")
    if prepared["manifest"] != apk.version or expected.get(assembly["path"]) != assembly["sha256"]:
        raise ValueError("Selected original camera format assembly differs")

    definition_path = ROOT / f"unity/Assets/Official/World/Map/{map_id}/map{map_id}.json"
    definition = json.loads(definition_path.read_bytes())
    if definition["ClassId"] != map_id:
        raise ValueError("Original map definition identity differs")
    regions = [json.loads(c["Json"]) for c in definition["Configs"] if c["TableName"] == "TbMapRegionInfo"]
    # These shared serialized UI/rank values retain their own historical provenance.
    shared_path = ROOT / "unity/Assets/Official/World/Map/14/Resources/Map14/Native/region-label-data.json"
    shared = json.loads(shared_path.read_bytes())
    ui_path = ROOT / "unity/Assets/Game/Resources/Map12/UI/catalog.json"
    ui = json.loads(ui_path.read_bytes())
    for region in regions:
        region["RecommendCombatRatingIndex"] = ui["recommendCombatRatingIndex"]
        region["HasCombatRatingLevelValues"] = region["CombatRatingLevelValues"] is not None
    native = ROOT / f"unity/Assets/Official/World/Map/{map_id}/NativeCurrent"
    if not (native / f"Resources/Map{map_id}/Native/current-ground.prefab").is_file():
        raise ValueError(f"Bind the complete selected original Map{map_id} native ground first")
    required = {f"region_{map_id}_" + str(r["Region"]) for r in regions}
    time_formatter = ROOT / 'unity/Assets/Game/Runtime/Official/World/NativeLabelUI/Framework/CultureTimeSpanArg.cs'
    if any(r['LimitServerRank'] or r['ReleaseTimeId'] for r in regions):
        required.update(re.findall(r'"((?:TwoDay|Day|Hour|Second|Minutes)Countdown[^"\n]*|time_[a-z_]+)"', time_formatter.read_text('utf8')))
    prefab_inputs = []
    for prefab in sorted(native.rglob("*.prefab")):
        text = prefab.read_text(encoding="utf8")
        keys = {k.strip().strip("\"'") for k in re.findall(r"(?m)^  myKey: (.+)$", text) if k.strip().strip("\"'")}
        if keys:
            required.update(keys)
            prefab_inputs.append(record(prefab))
    texts, language_inputs = language(ROOT / ".tools/source-packages/WwiseBnk")
    missing = [k for k in required if xxhash.xxh64_intdigest(k.encode()) not in texts]
    if missing:
        raise ValueError(f"Unresolved original Map{map_id} label keys: " + str(missing))
    nav = definition["Navigation"]
    area_to_region = {area: r["Region"] for r in regions for area in r["Areas"] or []}
    maxima = {r["Region"]: r["Value"] for r in definition["MaxRegionProgress"]}
    if set(maxima) != set(range(len(regions))) or set(maxima) != {r["Region"] for r in regions}:
        raise ValueError("Original label region IDs are not unique contiguous indexes")
    data = dict(manifest=apk.version, mapClassId=map_id, width=nav["Width"], height=nav["Height"],
        origin=[nav["Origin"][k] for k in ("X", "Y", "Z")], regions=regions,
        regionGrid=[area_to_region.get(c["RawValues"][0], -1) for row in nav["Rows"] for c in row["Cells"]],
        maxProgress=[maxima[region] for region in range(len(regions))],
        texts=[dict(key=k, value=texts[xxhash.xxh64_intdigest(k.encode())]) for k in sorted(required)],
        **{k: shared[k] for k in ("suppressUnlockRank", "subRankToRank", "combatRatingColors")})
    outputs = []
    for name, value in (("BaseGround/player-view", view), ("Native/region-label-data", data)):
        target = ROOT / f"unity/Assets/Game/Resources/Map{map_id}" / (name + ".json")
        write(target, encode(value))
        import_meta(target, "TextScriptImporter")
        import_meta(target.parent, "DefaultImporter", folder=True)
        outputs.append(record(target))
    proof = dict(format=1, manifest=apk.version, mapClassId=map_id, player_camera_objects=sources,
        language_inputs=language_inputs, native_prefab_inputs=prefab_inputs,
        schema_assembly=assembly, prepared_code=record(prepared_path), schema_types=sorted(schemas.records),
        configuration_input=record(definition_path), historical_shared_inputs=[record(shared_path), record(ui_path)],
        tool=record(Path(__file__)), outputs=outputs,
        time_formatter=record(time_formatter),
        operation=f"Read original common 3001 player model/offset/sorting and WorldCamera projection/adaptor. Bind original Map{map_id} region rows, native prefab language keys and verified selected language. Index progress maxima by region ID while retaining original row order. Shared historical label themes/rank mapping retain explicit input identities; no full-version equivalence is asserted.")
    write(ROOT / f"assets/reference/official-map/map{map_id}-world-view-source-manifest.json", encode(proof))
    apk.archive.close()
    print(f"Map{map_id} original player/camera and native label data:", len(required), "language keys")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map-id", type=int, default=15)
    build(parser.parse_args().map_id)
