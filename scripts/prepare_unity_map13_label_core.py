"""Port the selected official world-label text and state components without UI redesign."""
from pathlib import Path
import json
import re
import struct

from official_apk import ROOT
from official_source import OfficialSource
from read_official_native import NativeMethods
from official_metadata import Metadata
from prepare_unity_map13_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_water_components import import_meta

NAMESPACE = "Zjcs.Official.World.NativeLabelUI"
DEST = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeLabelUI"
FILES = {
    "framework-source": ["StateConfigValue", "StateKVMatch", "StateGameObjectData", "IStateConfig", "StateGameObjectConfig", "StateSwitcher"],
    "framework-firstpass-source": ["MonoKeyValuePair", "ColorGradientData", "TextMeshProColorProxy", "MultiLanguageFontAsset", "IMultiLanguageFontService", "IMultiLanguageUIService", "MultiLanguageUIService", "MultiLanguageUI", "MultiLanguageText", "Common/ITextProcessor", "Framework/LanguageType", "Framework/ILanguageTypeService", "Framework/ILocalizationTextService", "Framework/ITextReplacer", "Framework/ILanguageTextDataProvider"],
}


def clean(text):
    text = re.sub(r"^\s*//IL_[^\n]*\n", "\n", text, flags=re.M)
    for name in ["Framework.DependencyInjection", "Framework.Logging", "Framework.Logging.InterpolatedStringHandler", "Microsoft.Extensions.Logging", "GFrameCore.Extensions.ZLogger", "ZLogger"]:
        text = text.replace("using " + name + ";\n", "")
    text = text.replace("\t[Inject]\n", "").replace("[EnableCheckBoxClose]\n", "")
    text = text.replace("using Common;", "using " + NAMESPACE + ".Common;")
    text = text.replace("using Framework;", "using " + NAMESPACE + ".Framework;")
    return text


def build():
    source = OfficialSource()
    code = ROOT / ".tools/source-packages/current" / source.version / "official-code"
    manifest = json.loads((code.parent / "code-manifest.json").read_text(encoding="utf8"))
    assemblies = []
    for name in ["Framework.dll", "Framework.FirstPass.dll"]:
        # Actual output assembly names are recorded by the source preparer.
        candidates = [p for p in (code / "managed/decoded").glob("*.dll") if p.stem in [name[:-4], name[:-4]+".Runtime"]]
        if len(candidates) != 1: raise ValueError("Official label assembly is not unique: " + name)
        assembly = candidates[0]
        if sha(assembly.read_bytes()) != manifest["assemblies"][assembly.name]["decoded_sha256"]:
            raise ValueError("Official label assembly changed: " + assembly.name)
        assemblies.append(dict(path=source_path(assembly), sha256=sha(assembly.read_bytes())))
    inputs, outputs = [], []
    for folder, names in FILES.items():
        for name in names:
            original = code / folder / (name + ".cs")
            text = clean(original.read_text(encoding="utf8"))
            namespace = NAMESPACE
            if "/" in name:
                suffix, _ = name.split("/")
                text = text.replace("namespace " + suffix + ";\n", "")
                namespace += "." + suffix
            if name == "MultiLanguageUI":
                text = re.sub(r"\n\tprotected ILogger<MultiLanguageUI> Logger \{ get; set; \}\n", "\n", text)
                anchor = "\tprotected virtual void Awake()"
                binding = "\t// Explicit replacement for the original dependency injection, before Awake.\n\tpublic void BindLanguageServices(IMultiLanguageUIService uiService, ILanguageTypeService languageService)\n\t{\n\t\tmMultiLanguageUIService = uiService ?? throw new System.ArgumentNullException(nameof(uiService));\n\t\tmLanguageTypeService = languageService ?? throw new System.ArgumentNullException(nameof(languageService));\n\t}\n\n"
                text = text.replace(anchor, binding + anchor)
            if name == "MultiLanguageText":
                start = text.index("\t\t\t\tILogger logger")
                end = text.index("\n\t\t\t}", start)
                text = text[:start] + '\t\t\t\tDebug.LogError($"Language key:{myKey}, args count:{mArgs.Length}\\n{ex}", this);' + text[end:]
            if name == "StateSwitcher":
                start = text.index("\t\t\t\tGLoggerErrorInterpolatedStringHandler")
                end = text.index("\n\t\t\t\treturn;", start)
                text = text[:start] + '\t\t\t\tDebug.LogError("StateSwitcher不支持该类型:T");' + text[end:]
                start = text.index("\t\t\tGLoggerErrorInterpolatedStringHandler")
                end = text.index("\n\t\t}", start)
                text = text[:start] + '\t\t\tDebug.LogError($"{value}\\n{exception}", mOwner);' + text[end:]
            if any(token in text for token in ["[Inject]", "ILogger", "GLogger", "GZLogger", "ZLogger"]):
                raise ValueError("Unadapted official label dependency: " + name)
            raw = ("// Migrated from selected official managed code; see Map13 label-core source receipt.\nnamespace " + namespace + "\n{\nusing Object = UnityEngine.Object;\n" + text + "\n}\n").encode()
            target = DEST / (name + ".cs"); write(target, raw); import_meta(target, "MonoImporter")
            inputs.append(dict(path=source_path(original), sha256=sha(original.read_bytes())))
            outputs.append(target)
    # The optional gradient component is AOT code rather than a managed DLL body.
    image = ROOT / ".tools/source-packages/commerce-native/libil2cpp-restored.bin"
    metadata = ROOT / ".tools/source-packages/official-code/global-metadata.dat"
    native = NativeMethods(image, metadata)
    records = []
    for name, (address, index) in native.by_name.items():
        if not name.startswith("TMPColorGradient."): continue
        end = min(p for p in native.by_address if p > address)
        records.append(dict(method=name, metadata_index=index, address=hex(address), byte_count=end-address, sha256=sha(native.image[address:end])))
    if len(records) != 8: raise ValueError("Original TMP gradient method coverage differs")
    definitions = Metadata(metadata.read_bytes())
    schema = next(t for t in definitions.types if t["name"] == "TMPColorGradient")
    template = ROOT / "scripts/map13_tmp_gradient_runtime.cs"
    path = DEST / "TMPColorGradient.cs"; write(path, template.read_bytes()); import_meta(path, "MonoImporter"); outputs.append(path)
    evidence = ROOT / "unity/Assets/Official/World/Map/13/Source/native-text-gradient.json"
    write(evidence, encode(dict(methods=records, metadata_type=schema,
        angle=struct.unpack("<f", struct.pack("<I", 0x42340000))[0],
        observations=dict(set_colors="List<Color>.Count keys; time i/(Count-1); two alpha=1 keys at times 0 and 1; no mode replacement.",
            update_mesh="Rotate Quaternion.Euler(0,0,Angle); set renderMode=255; ForceMeshUpdate(false,false).",
            render="Each line firstCharacterIndex through lastCharacterIndex inclusive; every character and all four vertices, including invisible characters; rotate the vertex and InverseLerp against original unrotated line min/max; write Color32 from Gradient.Evaluate."))))
    meta(evidence)
    path = DEST / (NAMESPACE + ".asmdef")
    write(path, encode(dict(name=NAMESPACE, references=["Unity.ugui", "Unity.TextMeshPro", "Zjcs.Official.World.NativeText"], autoReferenced=True)))
    import_meta(path, "AssemblyDefinitionImporter"); outputs.append(path)
    for folder in [DEST, DEST / "Common", DEST / "Framework"]: import_meta(folder, "DefaultImporter", folder=True)
    receipt = dict(format=1, mapClassId=13, manifest=source.version, assemblies=assemblies, inputs=inputs,
        native_inputs=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [image, metadata]],
        native_methods=records, evidence=dict(path=source_path(evidence), sha256=sha(evidence.read_bytes())),
        outputs=[dict(path=source_path(p), sha256=sha(p.read_bytes()), meta_sha256=sha(Path(str(p)+".meta").read_bytes())) for p in sorted(outputs)],
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        templates=[dict(path=source_path(template), sha256=sha(template.read_bytes()))],
        validation=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13LabelCoreAcceptance.cs", ROOT / "unity/Assets/Game/Editor/Map13/Zjcs.Editor.Map13.asmdef"]],
        operation="Copy selected official text/service/interfaces, color proxy and state code into an isolated namespace. Replace DI with explicit pre-Awake binding; replace unavailable logging only, preserving catch/return/iteration/order and all branches. Preserve fields, shared GameObject membership and source no-op Clear; reconstruct all eight original AOT gradient methods and metadata fields.",
        limitations=["Original label prefabs, region/world/time/player service binding and full map interaction/save integration remain required."])
    write(ROOT / "assets/reference/official-map/map13-label-core-code-source-manifest.json", encode(receipt))
    print("Official label core:", len(inputs), "managed inputs,", len(records), "native gradient methods")


if __name__ == "__main__": build()
