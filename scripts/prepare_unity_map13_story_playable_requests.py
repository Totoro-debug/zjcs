"""Pin original Map13 request inputs and the local boundary around original execution."""
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / "assets/reference/official-map/map13-story-playable-requests-source-manifest.json"


def record(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(raw).hexdigest())


def build():
    source = ROOT / "unity/Assets/Game/Resources/Map13/stories.json"
    data = json.loads(source.read_bytes())
    dependencies = [ROOT / "assets/reference/official-map" / (name + "-source-manifest.json") for name in (
        "map13-native-npc-story-playables", "map13-native-npc-appearance-bindings")]
    prefabs = {p["guid"] for proof in dependencies for p in json.loads(proof.read_bytes())["prefabs"]}
    if len(prefabs) != 12:
        raise ValueError("Original Map13 native request prefab closure differs")
    requests = []
    counts = dict(requests=0, indexedActors=0, unconnectedIndexedTargets=0, unconnectedGroupedPorts=0,
                  nativeAssets=12, waits=0, stopStageBefore=0)
    for graph in data["stories"] + data["subgraphs"]:
        nodes = {n["id"]: n for n in graph["nodes"]}
        for node in graph["nodes"]:
            if node["type"] != "PlayPlayableNode":
                continue
            fields = json.loads(node["sourceJson"])
            if not (fields["PlayableDura"] == -1 or fields["PlayableDura"] > 0):
                raise ValueError("Original request duration differs")
            guids = [p["mAssetGuid"] for p in fields["PlayablesProxy"]]
            if not set(guids) <= prefabs:
                raise ValueError("Original request native asset is not bound")
            ports = []
            for port in node["ports"]:
                name = port["name"]
                if name in ("SourceEntitys", "TargetEntitys"):
                    if port["connections"]:
                        raise ValueError("Inspect original connected grouped actor port")
                    counts["unconnectedGroupedPorts"] += 1
                elif name.startswith(("SourceEntitys ", "TargetEntitys ")):
                    if not port["connections"]:
                        if name != "TargetEntitys 0":
                            raise ValueError("Inspect original unconnected actor input")
                        counts["unconnectedIndexedTargets"] += 1
                        continue
                    if len(port["connections"]) != 1:
                        raise ValueError("Inspect original multiple indexed actor inputs")
                    actor = nodes[port["connections"][0]["nodeId"]]
                    if actor["type"] not in ("GetEntityNode", "AddMapExclusiveNpcAtPosNode", "AddMapExclusiveNpcAroundPlayerNode"):
                        raise ValueError("Inspect original actor output type")
                    ports.append(dict(name=name, nodeId=actor["id"], type=actor["type"],
                                      sourceSha256=hashlib.sha256(actor["sourceJson"].encode()).hexdigest()))
                    counts["indexedActors"] += 1
            requests.append(dict(graph=graph["key"], nodeId=node["id"], sourceSha256=hashlib.sha256(node["sourceJson"].encode()).hexdigest(),
                                 assetGuids=guids, indexedPorts=ports, duration=fields["PlayableDura"],
                                 flags={k: v for k, v in fields.items() if k.startswith("Is")}))
            counts["requests"] += 1
            counts["waits"] += fields["IsWaitPlayableStop"]
            counts["stopStageBefore"] += fields["IsStopEntitesPlayableSupportBeforeNodeRun"]
    if (counts["requests"], counts["indexedActors"], counts["unconnectedIndexedTargets"],
        counts["unconnectedGroupedPorts"], counts["stopStageBefore"]) != (29, 55, 1, 58, 2):
        raise ValueError("Original request graph coverage differs: " + repr(counts))
    official = ROOT / ".tools/source-packages/current/871_156598/official-code/client-source"
    originals = [official / name for name in ("PlayPlayableNode.cs", "GetEntityBaseNode.cs", "XNode/Node.cs", "XNode/NodePort.cs")]
    bindings = [ROOT / "unity/Assets/Game/Runtime/Official/StoryPlayableRequests" / name for name in (
        "PlayPlayableNode.cs", "StoryPlayableRequestNode.cs", "StoryHandleProxy.cs")]
    outputs = []
    for relative in ("unity/Assets/Game/Runtime/Offline/World/NativeMap13StoryPlayables",
                     "unity/Assets/Game/Editor/Map13StoryPlayableRequests"):
        folder = ROOT / relative
        outputs.extend(p for p in folder.iterdir() if p.is_file())
        outputs.append(Path(str(folder) + ".meta"))
    result = dict(manifest="871_156598", mapClassId=13,
        inputs=[record(p) for p in [source, *dependencies, *originals, *bindings]],
        tools=[record(Path(__file__))], outputs=[record(p) for p in sorted(outputs)], counts=counts, requests=requests,
        operation="Bind all twelve original native NPC prefabs by original GUID. Deserialize every configured asset and numeric flag; retain -1 as native duration. Use the migrated original PlayPlayableNode, manager/center and FIFO pools for actual dispatch, indexed actor inputs, waits, stage stop/restart, skip, resource leases and graph cleanup. Preserve XNode exact grouped-port lookup: unconnected SourceEntitys/TargetEntitys return null, not the indexed dynamic-port contents. Retain the single unconnected target input. No duration-only substitute or invented actor/model.",
        pending=["Formal actor EC view/visibility/stage bindings and full story graph integration", "Original NPC native skeleton/skin/constraint animation", "Actual sound media", "Map mouse/save interaction and Windows Player verification"])
    if RECEIPT.exists():
        previous = json.loads(RECEIPT.read_bytes())
        if previous.get("validation"):
            result["validation"] = previous["validation"]
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_bytes((json.dumps(result, ensure_ascii=False, indent=2) + "\n").encode())
    print("Original Map13 playable request inputs:", counts)


if __name__ == "__main__":
    build()
