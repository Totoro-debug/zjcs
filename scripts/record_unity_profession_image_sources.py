"""Record official image proxies, resource lifetime source and Unity validation scope."""
import hashlib
import json
from pathlib import Path
from official_apk import ROOT
from read_official_native import NativeMethods


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build():
    code = ROOT / ".tools/source-packages/current/871_156598/official-code/framework-firstpass-source"
    runtime = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI"
    names = ("AssetGuidProxy", "PreviewableProxyBase", "LoaderProxyBase", "ImageLoaderProxy", "ImageProxy")
    ports = [dict(source=record(code / (name + ".cs")), runtime=record(runtime / (name + ".cs")),
        meta=record(runtime / (name + ".cs.meta"))) for name in names]
    image = ROOT / ".tools/source-packages/commerce-native/libil2cpp-restored.bin"
    metadata = ROOT / ".tools/source-packages/official-code/global-metadata.dat"
    if record(image)["sha256"] != "8e49906e085ba3819c4b47fd3bd12dcdd594ff0fcc75d418a87ac35a4566305b":
        raise ValueError("Original IL2CPP image changed")
    if record(metadata)["sha256"] != "58884b94b56ef35665f44d3f186fce8b8c854cf3bab0a74e4733a18b6a9175b4":
        raise ValueError("Original metadata changed")
    native = NativeMethods(image, metadata)
    methods = []
    for name in ("R3.DisposableBag.Add", "R3.DisposableBag.Clear", "R3.DisposableBag.Dispose"):
        address, index = native.by_name[name]
        end = min(value for value in native.by_address if value > address)
        methods.append(dict(name=name, metadata_index=index, address=hex(address), bytes=end-address,
            sha256=hashlib.sha256(native.image[address:end]).hexdigest()))
    result = dict(format=1, manifest="871_156598", ports=ports,
        resource_contract_sources=[record(code / "Framework" / (name + ".cs")) for name in
            ("IResourceLoader", "AssetOperationHandle")], native_inputs=[record(image), record(metadata)],
        lifetime_methods=methods,
        adapter=record(runtime / "ProfessionImageResourceBridge.cs"),
        adapter_meta=record(runtime / "ProfessionImageResourceBridge.cs.meta"),
        validation=record(ROOT / "unity/Assets/Game/Editor/ProfessionUI/ProfessionImageLoaderValidation.cs"),
        original_reward_assets=record(ROOT / "assets/reference/profession-ui/dynamic-resources-source-manifest.json"),
        recorder=record(Path(__file__)),
        operation="Retain original nested serialized fields, Image delegation, native size/layout/hit tests, image path prefix, synchronous/GUID/async dispatch, newest-operation guard, failure result and Clear/Dispose ordering. Replace scene-container lookup and YooAsset operation types with a consumed local interface. Express original R3 bag Add/Clear/Dispose traversal and disposed state in a scoped local owner; retain additions while disposing. Correct decompiler enum type names to Unity Image.Type/Image.FillMethod.",
        limitations=["The local image resource provider is not yet connected to the complete native ItemIcon prefab.",
            "Unity validation uses imported original reward Sprites and controlled completion ordering; this does not cover profession GUI mouse input or rendered ItemIcon layout.",
            "No resource or UI is drawn or substituted by these image proxy ports."])
    path = ROOT / "assets/reference/profession-ui/image-resource-source-manifest.json"
    path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Recorded five original image proxy ports, resource adapter and native lifetime evidence")
    return result


if __name__ == "__main__":
    build()
