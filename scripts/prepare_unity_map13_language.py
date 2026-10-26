"""Restore original binary localization and complete language culture executables."""
from pathlib import Path
import hashlib
import json
import re
import struct
import xxhash

from official_apk import ROOT, Apk
from prepare_unity_map13_label_core import NAMESPACE, DEST, clean
from prepare_unity_map13_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_water_components import import_meta

OFFLINE = ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeLabelResources"


def method(text, signature):
    start = text.index(signature); brace = text.index("{", start); depth = 1; end = brace + 1
    while depth:
        if text[end] == "{": depth += 1
        if text[end] == "}": depth -= 1
        end += 1
    return text[start:end]


def build():
    apk = Apk.current(); code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    framework = code / "framework-firstpass-source"
    code_manifest = json.loads((code.parent / "code-manifest.json").read_bytes())
    assemblies = []
    for name in ["Framework.FirstPass.Runtime.dll", "Launch.dll", "Assembly-CSharp.dll"]:
        path = code / "managed/decoded" / name
        if sha(path.read_bytes()) != code_manifest["assemblies"][name]["decoded_sha256"]:
            raise ValueError("Original language assembly hash differs: " + name)
        assemblies.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    names = ["ArgBase", "LanguageManager", "LanguageCulture", "LanguageCultureResolver", "NumberFormatHelper", "CultureTextFormatHelper",
             "CultureTimeSpanArg", "CultureNumberArg", "CultureTextArg", "CultureCustomTextArg", "CultureDateTimeArg",
             "ECultureNumberArgFormatType", "ETimeSpanFormatType", "EDateTimeDisplayType", "ICultureDateFormatter", "ICultureTextFormatter", "ICultureNumberFormatter",
             "CultureDateFormatterBase", "CultureTextFormatterBase", "CultureNumberFormatterBase", "ITextReader", "LocalizationBinaryTextService"]
    originals = [framework / "Framework" / (n + ".cs") for n in names]
    originals += sorted((framework / "Framework").glob("*CultureFormatter_*.cs"))
    if len(originals) != len(set(originals)) or len(originals) != 52: raise ValueError("Original language culture coverage differs")
    originals += [framework / "XXHash64.cs", framework / "LocalizationBinaryFileReader.cs",
                  code / "launch-source/LaunchLanguageBinaryTextDataProvider.cs", code / "launch-source/GameLanguageBinaryTextDataProvider.cs"]
    inputs, outputs = [], []
    for original in originals:
        text = clean(original.read_text(encoding="utf8"))
        sub = ".Framework" if original.parent.name == "Framework" else ""
        text = text.replace("namespace Framework;\n", "")
        if original.stem == "LocalizationBinaryTextService":
            text = text.replace("using YooAsset;\n", "")
            text = text.replace("IRawFileLoader", "ILocalRawFileLoader").replace("RawFileHandle", "ILocalRawFileLease")
            text = text.replace("\tprivate readonly ILogger<LocalizationBinaryTextService> mLogger;\n", "")
            text = text.replace("ILogger<LocalizationBinaryTextService> logger, ", "").replace("\t\tmLogger = logger;\n", "")
            text = text.replace("\t\tbool flag = default;\n", "")
            text = re.sub(r"\t+ILogger val = .*?GZLoggerExtensions.Error\([^\n]+;", '\t\t\t\tUnityEngine.Debug.LogError("Original localization key has a null value: " + key + ", language: " + language);', text, flags=re.S)
            text = "using Zjcs.Offline.World.NativeLabelResources;\n" + text
        if original.stem == "LocalizationBinaryFileReader":
            text = re.sub(r"(?:GLoggerErrorInterpolatedStringHandler )?message = new GLoggerErrorInterpolatedStringHandler[^;]+;\n.*?Log.Error\([^\n]+;", 'UnityEngine.Debug.LogError("Original localization binary read error");', text, flags=re.S)
            text = text.replace("\t\tGLoggerErrorInterpolatedStringHandler message;\n", "").replace("\t\tbool enabled;\n", "")
        if any(t in text for t in ["ILogger", "GLogger", "ZLogger", "YooAsset"]): raise ValueError("Unadapted language dependency: " + original.name)
        target = DEST / ("Framework" if sub else "") / original.name
        write(target, ("// Migrated from selected original language code; see Map13 language source receipt.\nnamespace " + NAMESPACE + sub + "\n{\n" + text + "\n}\n").encode())
        import_meta(target, "MonoImporter"); outputs.append(target)
        inputs.append(dict(path=source_path(original), sha256=sha(original.read_bytes())))
    original = framework / "Framework/LocalizationTextService.cs"
    extracted = method(original.read_text(encoding="utf8"), "public static string GetMissingKeyText(")
    target = DEST / "Framework/LocalizationTextService.cs"
    write(target, ("// Only the original CSV service's pure missing-key function is consumed by the binary service.\nnamespace " + NAMESPACE + ".Framework\n{\npublic static class LocalizationTextService\n{\n" + extracted + "\n}\n}\n").encode()); import_meta(target, "MonoImporter"); outputs.append(target)
    inputs.append(dict(path=source_path(original), sha256=sha(original.read_bytes()), extracted_method="GetMissingKeyText"))
    files = []
    bindings = [
        ("launch", "wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile", "Assets/Launch/Config/Binary/Language/zh_CN/text.g.bytes"),
        ("game", "wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile", "Assets/Config/Binary/Language/zh_CN/text.g.bytes")]
    wwise = {r["name"]: r for r in apk.source.manifest("WwiseBnk")["bundles"]}
    all_entries = {}
    for name, bundle, location in bindings:
        record = wwise[bundle]; entry = "assets/yoo/WwiseBnk/" + record["file"]
        if entry in apk.available:
            raw = apk.archive.read(entry); origin = dict(origin="apk_zip", apk_entry=entry, apk_sha256=apk.source.apk_sha256)
        else:
            cached = apk.cloud.read("WwiseBnk", record)
            if cached is None: raise FileNotFoundError("Selected original language rawfile is not cached: " + bundle)
            raw, origin = cached
        if len(raw) != record["size"] or hashlib.md5(raw).hexdigest() != record["hash"]:
            raise ValueError("Selected rawfile does not match its manifest: " + bundle)
        count = struct.unpack_from("<i", raw)[0]; start = 4 + count * 12
        decoded = {}
        for i in range(count):
            key, offset = struct.unpack_from("<Qi", raw, 4 + i * 12); at = start + offset
            length = struct.unpack_from("<H", raw, at)[0]
            if offset < 0 or at + 2 + length > len(raw): raise ValueError("Original language index is outside its file")
            decoded[key] = raw[at + 2:at + 2 + length].decode("utf8")
        for key, value in decoded.items(): all_entries.setdefault(key, value)  # Original first reader wins.
        target = ROOT / "unity/Assets/StreamingAssets/Map13/NativeText" / (name + "-zh_CN.bytes")
        write(target, raw); meta(target); outputs.append(target)
        files.append(dict(name=name, location=location, path=source_path(target), sha256=sha(raw), bytes=len(raw), md5=record["hash"], entries=count, source=origin, bundle=bundle))
    # Independent per-key reference values for the exact original world-label and culture paths.
    keys = set(re.findall(r'"([A-Za-z_][A-Za-z0-9_]*)"', (framework / "Framework/CultureTimeSpanArg.cs").read_text(encoding="utf8")))
    keys = {key for key in keys if key.startswith(("time_", "DayCountdown", "TwoDay", "HourCountdown", "MinutesCountdown", "SecondCountdown"))}
    keys.update("region_13_" + str(i) for i in range(106))
    reference = {key: all_entries[xxhash.xxh64_intdigest(key.encode())] for key in sorted(keys) if xxhash.xxh64_intdigest(key.encode()) in all_entries}
    vectors = ["", "a", "泽之国", "region_13_0", "abcdefghijklmnopqrstuvwx0123456789", "x" * 257]
    samples = [dict(hash=str(k), value=v) for k, v in sorted(all_entries.items(), key=lambda row: len(row[1]), reverse=True)[:3]]
    target = ROOT / "unity/Assets/Official/World/Map/13/Source/native-label-language.json"
    write(target, encode(dict(files=files, texts=[dict(key=k, value=v) for k, v in reference.items()], samples=samples, hashes=[dict(text=s, seed=str(seed), value=str(xxhash.xxh64_intdigest(s.encode(), seed=seed))) for s in vectors for seed in [0, 123, 2**64-1]])))
    meta(target); outputs.append(target)
    for folder in [OFFLINE, ROOT / "unity/Assets/StreamingAssets/Map13", ROOT / "unity/Assets/StreamingAssets/Map13/NativeText"]:
        import_meta(folder, "DefaultImporter", folder=True)
    path = OFFLINE / "Zjcs.Offline.World.NativeLabelResources.asmdef"
    write(path, encode(dict(name="Zjcs.Offline.World.NativeLabelResources", autoReferenced=True)))
    import_meta(path, "AssemblyDefinitionImporter"); outputs.append(path)
    path = OFFLINE / "LocalLanguageFiles.cs"; import_meta(path, "MonoImporter"); outputs.append(path)
    # Update the already migrated text assembly's explicit dependency, retaining its generator as authority.
    from prepare_unity_map13_label_core import build as build_core
    build_core()
    calls = [code / "launch-source/LaunchContainerInstaller.cs", code / "client-source/GameInitializer.cs", code / "launch-source/Common/ConfigConst.cs"]
    for p in calls:
        if not p.exists(): raise ValueError("Original language caller missing: " + str(p))
    write(ROOT / "assets/reference/official-map/map13-language-code-source-manifest.json", encode(dict(
        format=1, mapClassId=13, manifest=apk.version, assemblies=assemblies, inputs=inputs,
        callers=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in calls], rawfiles=files,
        outputs=[dict(path=source_path(p), sha256=sha(p.read_bytes()), meta_sha256=sha(Path(str(p)+".meta").read_bytes())) for p in sorted(outputs)],
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        validation=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13LanguageAcceptance.cs", ROOT / "unity/Assets/Game/Editor/Map13/Zjcs.Editor.Map13.asmdef"]],
        operation="Port original binary localization service/reader, original XXHash64, manager, all ten supported culture number/text/date formatters and culture argument branches. Preserve provider order, duplicate index overwrite, UTF8/length/buffer/seek/disposal, raw missing-key return, replacement and language reload order. Replace logging and raw-file handle type only; explicit local leases bind byte-identical packaged current rawfiles to original locations. Extract the pure original GetMissingKeyText function used by the binary service; CSV loading is outside this binary path.",
        limitations=["Formal map label/world services and global settings persistence remain unbound. This payload binds the selected official Chinese release files; other-language files are not claimed packaged."])))
    apk.archive.close()
    print("Original language:", len(inputs), "managed inputs;", [(f["name"], f["entries"], f["bytes"]) for f in files])


if __name__ == "__main__": build()
