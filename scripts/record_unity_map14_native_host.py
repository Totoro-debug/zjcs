"""Record the native Map14 host's verified resource and code bindings."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / "assets/reference/official-map/map14-native-host-source-manifest.json"
RUNTIME = ROOT / "unity/Assets/Game/Runtime/Presentation/World/NativeMap14"
EDITOR = ROOT / "unity/Assets/Game/Editor/Map14/WorldHost"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def record(path, line_endings=None):
    data = path.read_bytes()
    if line_endings == "LF":
        data = data.replace(b"\r\n", b"\n")
    result = dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(data))
    if line_endings:
        result["hashLineEndings"] = line_endings
    return result


def build():
    inputs = [record(ROOT / "unity/ProjectSettings/TagManager.asset", "LF")]
    for name in ("terrain", "scene", "environment-scene", "region-labels"):
        path = ROOT / ("assets/reference/official-map/map14-native-" + name + "-binding-source-manifest.json")
        source = json.loads(path.read_bytes())
        if source["mapClassId"] != 14 or source["manifest"] != "871_156598":
            raise ValueError("Map14 native directory source version differs: " + name)
        inputs.append(record(path))
        catalog = source["catalog"]
        asset = ROOT / catalog["path"]
        for file, expected in ((asset, catalog["sha256"]), (Path(str(asset) + ".meta"), catalog["meta_sha256"])):
            actual = record(file)
            if actual["sha256"] != expected:
                raise ValueError("Map14 source directory changed: " + actual["path"])
            inputs.append(actual)
    path = ROOT / "unity/Assets/Official/World/Map/14/Resources/Map14/Native/region-label-data.json"
    data = json.loads(path.read_bytes())
    if data["manifest"] != "871_156598" or len(data["origin"]) != 3 or len(data["regions"]) != 111:
        raise ValueError("Map14 native label data differs from the original geometry")
    inputs.append(record(path))
    inputs.append(record(Path(str(path) + ".meta")))
    shared = ROOT / "unity/Assets/Game/Resources/Map12/Native/world-assets.asset"
    inputs.extend((record(shared), record(Path(str(shared) + ".meta"))))
    for name in ("map14-label-components-code-source-manifest.json", "map13-native-text-font-source-manifest.json"):
        inputs.append(record(ROOT / "assets/reference/official-map" / name))
    for name in ("config", "serialization", "root", "ec-runtime"):
        path = ROOT / ("assets/reference/official-map/map14-shared-" + name + "-source-manifest.json")
        source = json.loads(path.read_bytes())
        if source["mapClassId"] != 14 or source["manifest"] != "871_156598":
            raise ValueError("Map14 shared root source version differs: " + name)
        for output in source["outputs"]:
            if record(ROOT / output["path"])["sha256"] != output["sha256"]:
                raise ValueError("Map14 shared root binding changed: " + output["path"])
        inputs.append(record(path))
    outputs = []
    for folder in (RUNTIME, EDITOR):
        outputs.append(record(Path(str(folder) + ".meta"), "LF"))
        for path in sorted(folder.iterdir()):
            if path.is_file():
                outputs.append(record(path, "LF"))
    receipt = dict(format=1, mapClassId=14, manifest="871_156598",
                   operation="Bind original Map14 terrain, scene, environment, world labels and the original shared nine-component root to one runtime host. Load unchanged local shared map records, complete synchronous parser jobs before each yield, start the original root, and restore shared bindings during normal or interrupted disposal. The runtime host does not enable the mainline map entry.",
                   inputs=inputs, outputs=outputs, generator=record(Path(__file__).resolve(), "LF"),
                   counts=dict(regions=111, terrainCatalog=241, sceneCatalog=242, environmentCatalog=389, labelCatalog=97, sharedRootComponents=9),
                   verificationMethod="Zjcs.Editor.Map14.OfficialMap14NativeHostAcceptance.Run",
                   verificationScope=["original default-entry rendering", "original shared root startup", "live fixture region state refresh", "camera movement and partition loading", "dispose and remount", "interrupted shared map loading", "shared singleton, loader, resolver and configuration lifetime restoration", "pipeline and added camera component cleanup"],
                   remaining=["mainline entry", "real map state and saves", "map entities", "pointer interaction", "Windows Player release"])
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Recorded Map14 native host: " + str(len(inputs)) + " inputs and " + str(len(outputs)) + " code/metadata outputs")
    return receipt


if __name__ == "__main__":
    build()
