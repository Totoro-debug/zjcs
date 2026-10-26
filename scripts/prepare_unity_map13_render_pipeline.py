"""Restore the APK Light2D recession extensions on the local Unity URP baseline."""
from pathlib import Path
import argparse
import json
import struct

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write


BASE = Path("D:/Tool/Unity/Editor/Data/Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.universal")
CACHE = ROOT / ".tools/map13-native-cache-871_156598"
PACKAGE = "com.unity.render-pipelines.universal"


def replace_once(text, old, new):
    if text.count(old) != 1: raise ValueError("Unity URP baseline changed: " + old[:100])
    return text.replace(old, new)


def native_evidence():
    image = ROOT / ".tools/source-packages/commerce-native/libil2cpp-restored.bin"
    metadata = ROOT / ".tools/source-packages/official-code/global-metadata.dat"
    raw_image, raw_metadata = image.read_bytes(), metadata.read_bytes()
    if sha(raw_image) != "8e49906e085ba3819c4b47fd3bd12dcdd594ff0fcc75d418a87ac35a4566305b" or sha(raw_metadata) != "58884b94b56ef35665f44d3f186fce8b8c854cf3bab0a74e4733a18b6a9175b4":
        raise ValueError("Pinned APK-derived native image or metadata changed")
    header = struct.unpack_from("<64I", raw_metadata)
    if header[:2] != (0xfab11baf, 31): raise ValueError("Original metadata version changed")
    def string(index):
        start = header[6] + index
        return raw_metadata[start:raw_metadata.index(0, start)].decode()
    module_name = b"Unity.RenderPipelines.Universal.Runtime.dll\0"
    name_address = raw_image.find(module_name)
    reference = raw_image.find(struct.pack("<Q", name_address), 0x11528210)
    if name_address < 0 or reference < 0 or reference % 8: raise ValueError("Original native URP module is missing")
    module = struct.unpack_from("<18Q", raw_image, reference)
    pointers = struct.unpack_from("<" + str(module[1]) + "Q", raw_image, module[2])
    by_name = {}
    for at in range(header[42], header[42] + header[43], 40):
        entry = struct.unpack_from("<10I", raw_metadata, at)
        if string(entry[0]) + "\0" != module_name.decode(): continue
        for ti in range(entry[2], entry[2] + entry[3]):
            info = struct.unpack_from("<16i8H2I", raw_metadata, header[40] + 88 * ti)
            for mi in range(info[9], info[9] + info[16]):
                method = struct.unpack_from("<IiiiiiIHHHH", raw_metadata, header[12] + 36 * mi)
                by_name[string(info[1]) + "." + string(info[0]) + "." + string(method[0])] = (pointers[(method[6] & 0xffffff) - 1], mi)
    methods = {
        "Light2D..ctor": (0x11166050, 0x15c),
        "RendererLighting.GetLightMaterialIndex": (0x11170aa0, 0x150),
        "RendererLighting.CreateLightMaterial": (0x11170bf0, 0x318),
        "RendererLighting.RenderLightSet": (0x1116f6a4, 0x818),
        "RendererLighting.ApplyRecession": (0x11170524, 0xc0),
    }
    records = []
    for suffix, (address, length) in methods.items():
        name = "UnityEngine.Rendering.Universal." + suffix
        actual, index = by_name[name]
        if actual != address: raise ValueError("Original native method registration changed: " + name)
        raw = raw_image[address:address+length]
        records.append(dict(method=name, metadata_index=index, address=hex(address), length=length,
                            sha256=sha(raw)))
    # Instruction guards for the original material-key bit and command order.
    from capstone import Cs, CS_ARCH_ARM64, CS_MODE_LITTLE_ENDIAN
    decoded = {}
    for record in records:
        address, length = int(record["address"], 16), record["length"]
        decoded[record["method"]] = [(i.mnemonic, i.op_str) for i in Cs(CS_ARCH_ARM64,CS_MODE_LITTLE_ENDIAN).disasm(raw_image[address:address+length],address)]
    index_code = decoded["UnityEngine.Rendering.Universal.RendererLighting.GetLightMaterialIndex"]
    if ("ldrb", "w12, [x19, #0xe1]") not in index_code or ("orr", "w0, w8, w12, lsl #7") not in index_code:
        raise ValueError("Original UseRecession material-key bit is not verified")
    apply_code = decoded["UnityEngine.Rendering.Universal.RendererLighting.ApplyRecession"]
    if [i for i in apply_code if i[0] in ("bl", "b") and i[1] in ("#0x1139dd68", "#0x1139dc6c")] != [
            ("bl", "#0x1139dd68"), ("bl", "#0x1139dc6c"), ("b", "#0x1139dc6c")]:
        raise ValueError("Original recession command order changed")
    if ("b", "#0x1139dc6c") not in apply_code:
        raise ValueError("Original final recession float command missing")
    return dict(image=dict(path=source_path(image), sha256=sha(image.read_bytes())),
        metadata=dict(path=source_path(metadata), sha256=sha(metadata.read_bytes())), methods=records)


def build(project):
    evidence = native_evidence()
    info = json.loads((BASE / "package.json").read_text())
    if info["name"] != PACKAGE or info["version"] != "14.0.11" or info["unity"] != "2022.3":
        raise ValueError("Use the installed and verified Unity 2022.3 URP 14.0.11 baseline")
    destination = project / "Packages" / PACKAGE
    inputs, outputs = [], []
    omit = {"Documentation~", "Samples~", "Tests", "Tests.meta"}
    for original in sorted(BASE.rglob("*")):
        if not original.is_file(): continue
        relative = original.relative_to(BASE)
        if relative.parts[0] in omit: continue
        raw = original.read_bytes()
        inputs.append(dict(path=relative.as_posix(), sha256=sha(raw)))
        if relative.as_posix() == "Runtime/2D/Light2D.cs":
            text = raw.decode("utf8")
            text = replace_once(text, "        Bounds m_LocalBounds;", """        Bounds m_LocalBounds;

        // Original APK IL2CPP Light2D fields, retained in native serialization order.
        public bool UseRecession;
        public Vector3 RecessionCenter;
        public float RecessionRadius;
        public float RecessionBlurEdge;""")
            raw = text.encode()
        elif relative.as_posix() == "Runtime/2D/Passes/Utility/RendererLighting.cs":
            text = raw.decode("utf8")
            text = replace_once(text,
                "return fastQualityBit | pointCookieBit | spriteBit | additiveBit | shapeBit | volumeBit | useNormalMap;",
                "return fastQualityBit | pointCookieBit | spriteBit | additiveBit | shapeBit | volumeBit | useNormalMap | (light.UseRecession ? 1u << 7 : 0u);")
            text = replace_once(text, "            if (light.lightType == Light2D.LightType.Sprite)\n                material.EnableKeyword(k_SpriteLightKeyword);",
                "            if (light.lightType == Light2D.LightType.Sprite)\n                material.EnableKeyword(k_SpriteLightKeyword);\n            else if (light.lightType == Light2D.LightType.Freeform && light.UseRecession)\n                material.EnableKeyword(\"USE_RECESSION\");")
            text = replace_once(text, "                            cmd.DrawMesh(lightMesh, light.transform.localToWorldMatrix, lightMaterial);",
                "                            if (light.UseRecession) ApplyRecession(cmd, light);\n                            cmd.DrawMesh(lightMesh, light.transform.localToWorldMatrix, lightMaterial);")
            marker = "        private static float GetNormalizedInnerRadius(Light2D light)"
            text = replace_once(text, marker, """        // Reconstructed from original ApplyRecession at 0x11170524, including w=0.
        private static void ApplyRecession(CommandBuffer cmd, Light2D light)
        {
            cmd.SetGlobalVector(Shader.PropertyToID("_RecessionCenter"), new Vector4(light.RecessionCenter.x, light.RecessionCenter.y, light.RecessionCenter.z, 0));
            cmd.SetGlobalFloat(Shader.PropertyToID("_RecessionRadius"), light.RecessionRadius);
            cmd.SetGlobalFloat(Shader.PropertyToID("_RecessionBlurEdge"), light.RecessionBlurEdge);
        }

""" + marker)
            raw = text.encode()
        elif relative.as_posix() == "package.json":
            package_info = dict(info)
            package_info.pop("samples", None)
            package_info["zjcsOriginalApkExtensions"] = "Light2D/RendererLighting recession fields and commands; see Map13 pipeline source receipt."
            raw = encode(package_info)
        target = destination / relative
        write(target, raw)
        outputs.append(dict(path=source_path(target), sha256=sha(raw)))
    settings = project / "Packages/manifest.json"
    document = json.loads(settings.read_text()) if settings.exists() else {"dependencies": {}}
    document["dependencies"][PACKAGE] = "file:" + PACKAGE
    write(settings, encode(document))
    receipt = CACHE / "urp-source-manifest.json" if project != ROOT / "unity" else ROOT / "assets/reference/official-map/map13-render-pipeline-source-manifest.json"
    write(receipt, encode(dict(format=1, mapClassId=13, manifest="871_156598", baseline=dict(
        source="Installed Unity 2022.3.57f1c2 package", path=BASE.as_posix(), package=PACKAGE,
        version=info["version"], files=inputs), native=evidence, outputs=outputs,
        omitted=["Unity package documentation, samples and package tests; retain all runtime, editor, shader, texture, licensing and metadata files."],
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Restore the four original serialized Light2D fields, UseRecession material key bit 7, Freeform-only keyword, and original center/radius/blur commands immediately before shape DrawMesh. Zero constructor defaults match native code. Unity URP is a dependency baseline, not claimed to be the full game's decompiled managed implementation.",
        limitations=["Import/compile, original pipeline/renderer asset binding, original shape shader and actual rendering must be verified separately."])))
    print("Prepared original Light2D extension on local URP baseline:", len(outputs), "files")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", type=Path, required=True)
    arguments = parser.parse_args()
    build(arguments.project.resolve())
