"""Adapt selected official partition execution to explicit local service boundaries."""
from pathlib import Path
import json
import re

from official_apk import ROOT
from official_source import OfficialSource
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_map13_water_components import import_meta


DESTINATION = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeTerrain"
NAMESPACE = "Zjcs.Official.World.NativeTerrain"
TYPES = ("ChunkAssetState", "ChunkAssetType", "ChunkRuntimeState", "ChunkData", "PartitionAsset",
         "PartitionTile", "PartitionScene", "PartitionExtension", "PartitionLoader", "PartitionController")


def replace_once(text, old, new):
    if text.count(old) != 1:
        raise ValueError("Official source changed: " + old[:100])
    return text.replace(old, new)


def diagnostic(match):
    block = match[0]
    literals = re.findall(r'AppendLiteral\("((?:[^"\\]|\\.)*)"\)', block)
    message = "".join(literals) or "Original partition diagnostic"
    level = "LogWarning" if "GZLoggerExtensions.Warn" in block else "LogError"
    return match[1] + "UnityEngine.Debug." + level + "(" + json.dumps(message) + ");\n"


def build():
    source = OfficialSource()
    code = ROOT / ".tools/source-packages/current" / source.version / "official-code"
    manifest_path = code.parent / "code-manifest.json"
    manifest = json.loads(manifest_path.read_text())
    assemblies = []
    for filename in ["Assembly-CSharp.dll", "Console.dll"]:
        assembly = code / "managed/decoded" / filename
        if sha(assembly.read_bytes()) != manifest["assemblies"][assembly.name]["decoded_sha256"]:
            raise ValueError("Selected official partition assembly differs")
        assemblies.append(dict(path=source_path(assembly), sha256=sha(assembly.read_bytes())))
    inputs, outputs, diagnostic_counts = [], [], {}
    for name in TYPES:
        original = code / "client-source" / (name + ".cs")
        text = original.read_text(encoding="utf8")
        if name == "PartitionController":
            # ILSpy could not reconstruct the four original UniTask state machines.
            # Keep the synchronous controller methods verbatim. Their continuations
            # are restored below from those state machines, with the same waits.
            text = "using System;\nusing System.Collections;\nusing System.Collections.Generic;\nusing UnityEngine;\nusing " + NAMESPACE + ".Common;\n\npublic class PartitionController : MonoBehaviour\n{\n" + text[text.index("\tpublic PartitionLoader Loader;"):]
            text = re.sub(r'\t\[AsyncStateMachine\([^\n]+\n\t(?:private|public) UniTask (\w+)\([^\n]*\)\n\t\{.*?\n\t\}\n', "", text, flags=re.S)
            text = re.sub(r'\tprivate void CheckVersion\(\)\n\t\{.*?\n\t\}\n', "", text, flags=re.S)
            text = replace_once(text, "mMapClassId = MeManager.Instance.MapData.MapContainer.ClassId;", "mMapClassId = Context.MapClassId;")
            text = text.replace("\t\tCheckVersion();\n", "")
            text = text.replace("MeManager.MainPlayer.OnMapStatusUpdate", "Context.OnMapStatusUpdate").replace("MeManager.MainPlayer.OnWorldMapProgressUpdate", "Context.OnWorldMapProgressUpdate")
            text = text.replace("if (MeManager.MainPlayer != null)", "if (Context != null)")
            text = text.replace("Pos2d pos = MeManager.Instance.MapData.MapCharacter.GridTransform.Pos;\n\t\t\tint num = MapUtils.GridToRegion(mapClassId, pos);", "int num = Context.CurrentRegion;")
            text = text.replace("MapMyEntityUtils.GetCurrentMapStatus()", "Context.GetCurrentMapStatus()")
            text = text.replace("MapRegionEntityInfoManager.Instance.GetMapRegionGoddessId", "Context.GetMapRegionGoddessId")
            text = text.replace("Formula.Instance.WorldMapRegionCleared", "Context.WorldMapRegionCleared")
            text = text.replace("MeManager.MainPlayer.GetWorldMapRegionProgress", "Context.GetWorldMapRegionProgress")
            text = text.replace("UniTaskExtensions.Forget(DelayRemovePrivateRegion(region, 5f));", "StartCoroutine(DelayRemovePrivateRegion(region, 5f));")
            extra = '''
    public IPartitionWorldContext<MapRegionStatus> Context { get; set; }

    private IEnumerator DelayRemovePrivateRegion(int region, float delayTime)
    {
        yield return new WaitForSecondsRealtime(delayTime);
        if (gameObject != null)
        {
            PartitionData.RegionData regionData = PartitionData.RegionDatas[region];
            RemoveLayer(regionData.GetLayerData(PartitionLayer.Night));
        }
    }

    public IEnumerator LoadByPlayerPos()
    {
        yield return LoadInstantly(Context.PlayerWorldPosition);
    }

    public IEnumerator LoadInstantly(Vector3 worldPos)
    {
        Vector2Int chunkPos = Loader.ConvertWorldPos2ChunkPos(worldPos);
        if (!Loader.HasValidChunkInVicinity(chunkPos))
        {
            Debug.Log("Partition LoadInstantly doesn't HasValidChunkInVicinity");
            yield break;
        }
        Loader.LoadVicinity(chunkPos.x, chunkPos.y);
        int maxTryTime = 120;
        int tryCount = 0;
        while (++tryCount < maxTryTime && !Loader.IsPosReady(worldPos)) yield return null;
        if (tryCount >= maxTryTime)
            Debug.LogError($"Partition LoadInstantly Exception, mMapClassId:{mMapClassId}, chunkPos:{chunkPos}, ErrorInfo:{Loader.GetNearbyAbnormalStateInfo(chunkPos)}");
        else Debug.Log($"Partition LoadInstantly:{tryCount}");
    }
'''
            text = text.rstrip()[:-1] + extra + "}\n"
        else:
            text, count = re.subn(r'(?m)^(\s*\t)ILogger [^\n]+;\n.*?GZLoggerExtensions\.(?:Error|Warn)\([^\n]+;\n', diagnostic, text, flags=re.S)
            diagnostic_counts[name] = count
            if name == "ChunkData":
                text = re.sub(r'GLoggerErrorInterpolatedStringHandler message = [^\n]+;\n.*?Log.Error\([^\n]+;', 'UnityEngine.Debug.LogError("Assets Count is 0");', text, flags=re.S)
            if name == "PartitionAsset":
                text = text.replace("\tprotected ILogger mLogger;\n", "")
                text = re.sub(r'\tpublic void SetLogger\(ILogger logger\)\n\t\{.*?\n\t\}\n', "", text, flags=re.S)
            if name == "PartitionLoader":
                text = text.replace("\tprivate DisposableBag mDisposableBag;\n", "")
                text = text.replace("\t[Inject]\n", "")
                text = text.replace("private IResourceLoader ResourceLoader { get; set; }", "public IPartitionResourceLoader ResourceLoader { get; set; }")
                text = text.replace("private IServiceProvider ServiceProvider { get; set; }", "public IPartitionInstanceFactory ServiceProvider { get; set; }")
                text = text.replace("private IApplicationService ApplicationService { get; set; }", "public Func<bool> IsOtherGameRunning { get; set; }")
                text = text.replace("private ICameraControlManager CameraControlManager { get; set; }", "public Func<bool> IsCameraReady { get; set; }")
                text = text.replace("\tprivate ILogger<PartitionLoader> Logger { get; set; }\n", "")
                text = replace_once(text, "Disposable.AddTo<IDisposable>(ApplicationService.OnLowMemory.Subscribe(ApplicationService_OnLowMemory), ref mDisposableBag);", "Application.lowMemory += ApplicationService_OnLowMemory;")
                text = replace_once(text, "mDisposableBag.Dispose();", "Application.lowMemory -= ApplicationService_OnLowMemory;")
                text = replace_once(text, "CameraControlManager.ControlBrain.CameraController == null", "IsCameraReady == null || !IsCameraReady()")
                text = replace_once(text, "MeManager.Instance.GameData.Game == null", "IsOtherGameRunning != null && !IsOtherGameRunning()")
                text = text.replace("\t\tpartitionAsset.SetLogger((ILogger)(object)Logger);\n", "")
                text = text.replace("TryGetComponent<PartitionElement>(ref partitionElement)", "TryGetComponent<PartitionElement>(out partitionElement)")
                text = text.replace("\t\tbool flag = default;\n\t\tfor (int count = assets.Count;", "\t\tfor (int count = assets.Count;")
            text = text.replace("IResourceLoader", "IPartitionResourceLoader").replace("AssetOperationHandle<", "PartitionAssetHandle<")
            text = text.replace("IServiceProvider", "IPartitionInstanceFactory")
            text = text.replace("ServiceProviderExtensions.Instantiate(mServiceProvider, result, mHostTrans, false)", "mServiceProvider.Instantiate(result, mHostTrans, false)")
        text = re.sub(r'^using (?:Framework(?:\.[^;]+)?|GFrameCore\.[^;]+|Microsoft.Extensions.Logging|R3|ZLogger);\n', "", text, flags=re.M)
        # Object means UnityEngine.Object in the original assembly; disambiguate from System.Object.
        if "using UnityEngine;" in text:
            text = text.replace("using UnityEngine;", "using UnityEngine;\nusing Object = UnityEngine.Object;")
        text = "// Migrated official partition execution; see Map13 partition source receipt.\nnamespace " + NAMESPACE + "\n{\nusing Zjcs.Offline.World.NativePartition;\n" + text + "\n}\n"
        if re.search(r'\b(?:MeManager|UniTask|ILogger|GZLoggerExtensions|ZLogger\w+|DisposableBag|ICameraControlManager)\b', text):
            raise ValueError("Unadapted partition service dependency: " + name)
        path = DESTINATION / original.name
        write(path, text.encode()); import_meta(path, "MonoImporter")
        inputs.append(dict(path=source_path(original), sha256=sha(original.read_bytes())))
        outputs.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    original = code / "common-source/Common/MapRegionStatus.cs"
    text = original.read_text().replace("namespace Common;", "namespace " + NAMESPACE + ".Common\n{") + "\n}\n"
    path = DESTINATION / "Common/MapRegionStatus.cs"
    write(path, text.encode()); import_meta(path, "MonoImporter")
    inputs.append(dict(path=source_path(original), sha256=sha(original.read_bytes())))
    outputs.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    asmdef = DESTINATION / "Zjcs.Official.World.NativeTerrain.asmdef"
    document = json.loads(asmdef.read_text())
    document["references"] = ["Zjcs.Offline.World.NativePartition"]
    write(asmdef, encode(document))
    outputs.append(dict(path=source_path(asmdef), sha256=sha(asmdef.read_bytes())))
    receipt = ROOT / "assets/reference/official-map/map13-partition-code-source-manifest.json"
    adapters = [ROOT / "unity/Assets/Game/Runtime/Offline/World" / path for path in [
        "NativePartition/PartitionServices.cs", "NativePartition/Zjcs.Offline.World.NativePartition.asmdef",
        "NativeMap13/NativeMap13ResourceLoader.cs", "NativeMap13/Zjcs.Offline.World.NativeMap13.asmdef"]]
    write(receipt, encode(dict(format=1, mapClassId=13, manifest=source.version,
        assemblies=assemblies,
        inputs=inputs, outputs=outputs, diagnostic_blocks=diagnostic_counts,
        adapters=[dict(path=source_path(path), sha256=sha(path.read_bytes())) for path in adapters],
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        adaptations=["Keep official partition data, branch order, nine-neighbor load order, tile fill/clear and refcounts, 900 idle frames and unload interval greater than five frames.",
            "Bind resource handles, prefab instantiation, camera readiness, other-game state and world progress through explicit local interfaces; subscribe to actual Unity low-memory event.",
            "Reconstruct official UniTask continuations from their decompiled state machines as Unity coroutines: realtime five-second private-layer removal; preincrement readiness loop with maxTryTime 120; same call order and branches.",
            "Omit GM-only outdated-version broadcast diagnostics; route logger diagnostics to Unity Debug, retaining branch placement and literal text.",
            "Repair decompiled TryGetComponent ref argument to actual Unity out parameter; disambiguate UnityEngine.Object."],
        limitations=["World-context and native scene bindings must be connected and validated; code import is not gameplay verification."])))
    print("Migrated original partition execution types:", len(inputs))


if __name__ == "__main__": build()
