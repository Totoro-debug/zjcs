"""Port the original Map13 environment callbacks and intact async light state machine."""
import json
from pathlib import Path
import re

from official_apk import ROOT, Apk
from official_unity_schema import ScriptSchemas
from official_source import OfficialSource
from map13_native_schemas import metadata_types
from official_metadata import Metadata
from prepare_unity_map13_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_water_components import import_meta


NAMESPACE = "Zjcs.Official.World.NativeEnvironment"
DESTINATION = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeEnvironment"
CONTRACT = ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeEnvironment"
TYPES = ("DayNightState", "MapEnvData", "MapEnvObject", "MapGroundRoot", "MapEffect", "MapLight",
         "MapRegionLight", "RegionLightHub", "MapEnvEffect", "FollowSceneActiveSelfController",
         "CullingGroupWrapper", "WorldMapEditorManager")


def build():
    source = OfficialSource(); code = ROOT / ".tools/source-packages/current" / source.version / "official-code"
    manifest = json.loads((code.parent / "code-manifest.json").read_text(encoding="utf8"))
    assembly = code / "managed/decoded/Assembly-CSharp.dll"
    if sha(assembly.read_bytes()) != manifest["assemblies"][assembly.name]["decoded_sha256"]:
        raise ValueError("Official environment assembly changed")
    metadata_types()  # Validates the pinned APK metadata.
    metadata = Metadata((ROOT / ".tools/source-packages/official-code/global-metadata.dat").read_bytes())
    timing = next(t for t in metadata.types if t["name"] == "PlayerLoopTiming")
    timing_values = {f["name"]: metadata.default_int(f["index"]) for f in timing["fields"] if f["index"] in metadata.defaults}
    if timing_values["Update"] != 8: raise ValueError("Original fade scheduler timing changed")
    components = json.loads((ROOT / "unity/Assets/Official/World/Map/13/Source/native-components.json").read_text(encoding="utf8"))["components"]
    culling = [c for c in components if c["type"] == "CullingGroupWrapper"]
    if len(culling) != 117 or any(c["fields"]["SkeletonAnimations"] for c in culling):
        raise ValueError("Restore typed native skeleton culling references before migrating this changed source")
    inputs, outputs = [], []
    for name in TYPES:
        original = code / "client-source" / (name + ".cs")
        text = original.read_text(encoding="utf8")
        for using in ["Framework", "Framework.DependencyInjection", "Framework.Logging", "Framework.Logging.InterpolatedStringHandler", "Spine.Unity"]:
            text = text.replace("using " + using + ";\n", "")
        text = text.replace("using Common;", "using Zjcs.Official.World.NativeTerrain.Common;")
        text = text.replace("[ScriptableObjectConfig]\n", "")
        text, injection_count = re.subn(r"\t\[Inject\]\n\tprivate [^\n]+ \{ get; set; \}\n", "", text)
        if injection_count:
            declaration = "public class " + name + " : "
            at = text.index("{", text.index(declaration)) + 1
            binding = "\n\tprivate IMapEnvironmentServices<MapGroundRoot, MapEnvData> Services;\n\tpublic void Bind(IMapEnvironmentServices<MapGroundRoot, MapEnvData> services)\n\t{\n\t\tServices = services ?? throw new System.ArgumentNullException(nameof(services));\n\t}\n"
            text = text[:at] + binding + text[at:]
        text = text.replace("MapRootManagerProxy.MapGroundRoot", "Services.MapGroundRoot")
        text = text.replace("MapEnvData.Value", "Services.EnvironmentData")
        text = text.replace("MyMapPositionHandler.PosToGrid", "Services.PosToGrid").replace("MyMapPositionHandler.QueryRegion", "Services.QueryRegion")
        text = text.replace("MapMyEntityUtils.GetCurrentMapStatus()", "Services.GetCurrentMapStatus()")
        text = text.replace("MapMyEntityUtils.MyMapRegionAllPosUnlock(MapRootManagerProxy, MeManager.MainMap.Container.ClassId, num)", "Services.UnlockAllRegionPositions(Services.MapClassId, num)")
        text = text.replace("ECRegionLightView lightView", "IMapRegionEntityLight<Light2D> lightView")
        # Original compiled state machine is retained, not approximated by a coroutine.
        text = re.sub(r"\bAwaiter\b", "UniTask.Awaiter", text)
        text = re.sub(r"(?:GLoggerErrorInterpolatedStringHandler )?message = new GLoggerErrorInterpolatedStringHandler[^;]+;\n.*?Log.Error\([^\n]+;",
                      'UnityEngine.Debug.LogError("Map environment registration error");', text, flags=re.S)
        text = text.replace("\t\tGLoggerErrorInterpolatedStringHandler message;\n", "").replace("\t\tbool enabled;\n", "")
        if name == "CullingGroupWrapper":
            # All 117 original Map13 instances have an empty SkeletonAnimations
            # list. Keep the enable/disable loop ready for native Behaviour adapters.
            text = text.replace("SkeletonAnimation", "Behaviour")
            text = text.replace("Behaviours", "SkeletonAnimations")  # Retain serialized field name.
            text = text.replace("ToggleBehaviours", "ToggleSkeletonAnimations")
        if "Framework" in text or "MapMyEntityUtils" in text or "MeManager" in text or "[Inject]" in text:
            raise ValueError("Unbound original environment dependency: " + name)
        prefix = "using Zjcs.Offline.World.NativeEnvironment;\nusing Zjcs.Official.World.NativeTerrain;\nusing Object = UnityEngine.Object;\n"
        raw = ("// Migrated from selected official environment code; see Map13 source receipt.\nnamespace " + NAMESPACE + "\n{\n" + prefix + text + "\n}\n").encode()
        target = DESTINATION / original.name; write(target, raw); import_meta(target, "MonoImporter")
        inputs.append(dict(path=source_path(original), sha256=sha(original.read_bytes())))
        outputs.append(dict(path=source_path(target), sha256=sha(raw)))
    for folder, name, refs in [(CONTRACT, "Zjcs.Offline.World.NativeEnvironment", ["Zjcs.Official.World.NativeTerrain"]),
        (DESTINATION, NAMESPACE, ["Zjcs.Official.World.NativeTerrain", "Zjcs.Offline.World.NativeEnvironment", "Unity.RenderPipelines.Universal.Runtime", "UniTask"])]:
        path = folder / (name + ".asmdef");write(path, encode(dict(name=name, references=refs, autoReferenced=True)))
        import_meta(path, "AssemblyDefinitionImporter"); import_meta(folder, "DefaultImporter", folder=True)
        outputs.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    path = CONTRACT / "EnvironmentServices.cs"; import_meta(path, "MonoImporter")
    outputs.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    apk = Apk.current(); schemas = ScriptSchemas(code_root=code)
    configurations = [o for o in apk.objects("scriptableconfig_modules_mapenv") if o.type.name == "MonoBehaviour"
                      and o.read(check_read=False).m_Script.path_id
                      and o.read(check_read=False).m_Script.read().m_ClassName == "MapEnvData"]
    if len(configurations) != 1: raise ValueError("Original environment configuration is not unique")
    config = configurations[0]; fields = schemas.read(config)
    path = ROOT / "unity/Assets/Official/World/Map/13/Source/native-environment-config.json"
    write(path, encode({k: fields[k] for k in ["DisappearDelayTime", "DisappearTime", "FadeTime"]})); meta(path)
    outputs.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    write(ROOT / "assets/reference/official-map/map13-environment-code-source-manifest.json", encode(dict(
        format=1, mapClassId=13, manifest=source.version, assembly=dict(path=source_path(assembly), sha256=sha(assembly.read_bytes())),
        inputs=inputs, outputs=outputs, player_loop_timing=timing_values,
        original_empty_skeleton_culling_lists=len(culling),
        configuration=dict(source=apk.provenance(config), object_sha256=sha(config.get_raw_data()), fields=fields),
        validation=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in
                    [ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13EnvironmentTools.cs",
                     ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13EnvironmentAcceptance.cs"]],
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Port original environment fields, registration, day/night, scene lifecycle, camera culling and RegionLightHub async state machine. Qualify the original UniTask.Awaiter and preserve all state-machine instructions, cancellation, elapsed/duration comparisons and Update timing. Replace dependency injection with explicit local services and diagnostics with Unity logging. Culling skeleton list accepts native Behaviour adapters; all 117 original Map13 lists are empty.",
        limitations=["Local world-state service, native scene/resource binding, actual rendering and interaction/save validation remain required."])))
    print("Migrated official environment types:", len(TYPES))


if __name__ == "__main__": build()
