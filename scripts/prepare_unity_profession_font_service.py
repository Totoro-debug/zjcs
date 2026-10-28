"""Port the original font service and package its verified source TTF files."""
import hashlib
import json
from pathlib import Path
import re
import shutil
import yaml
from prepare_unity_profession_native_inputs import ROOT, CACHE
from official_apk import Apk
from prepare_unity_profession_reward_sprites import Dumper, source_header


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def meta(path, seed, importer=""):
    target = path.with_name(path.name + ".meta")
    target.write_text("fileFormatVersion: 2\nguid: " + hashlib.md5(seed.encode()).hexdigest() + "\n" + importer, encoding="utf-8", newline="\n")
    return target


def build():
    code = ROOT / ".tools/source-packages/current/871_156598/official-code/framework-firstpass-source"
    runtime = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI"
    ports = []
    for name in ("IFontAssetFontService", "FontAssetFontConfig", "FontAssetFontService"):
        source = code / (name + ".cs")
        text = source.read_text(encoding="utf-8")
        if name == "FontAssetFontService":
            text = re.sub(r"^using (Framework|GFrameCore.*|Microsoft.*|Newtonsoft.*|ZLogger);\n", "", text, flags=re.M)
            text = re.sub(r"^\s*//IL_[^\n]*\n", "\n", text, flags=re.M)
            text = text.replace("IResourceLoader", "IProfessionImageResourceLoader")
            text = text.replace("AssetOperationHandle<", "IProfessionAssetOperation<")
            text = text.replace("protected readonly ILogger<FontAssetFontService> mLogger;", "private readonly Func<string, FontAssetFontConfig> mDeserialize;")
            text = text.replace("ILogger<FontAssetFontService> logger", "Func<string, FontAssetFontConfig> deserialize")
            text = text.replace("mLogger = logger;", "mDeserialize = deserialize;")
            text = text.replace("JsonConvert.DeserializeObject<FontAssetFontConfig>(text)", "mDeserialize(text)")
            text = text.replace(".Release(tryUnloadAsset: true)", ".Dispose()")
            start = text.index("\t\tbool flag = default;")
            end = text.index("\t\tIProfessionAssetOperation<Font> assetOperationHandle", start)
            text = text[:start] + '\t\tif (!Config.FontAssetInfoDict.TryGetValue(name, out var value))\n\t\t{\n\t\t\tDebug.LogError("未找到字体资源(" + name + ")的配置");\n\t\t\treturn;\n\t\t}\n' + text[end:]
            start = text.index("\t\tval = (ILogger)")
            end = text.index("\n\t}\n", start)
            text = text[:start] + '\t\tDebug.Log(name + "使用加载进内存的字体: " + assetOperationHandle.Result);' + text[end:]
        text = "namespace Zjcs.Official.ProfessionUI\n{\nusing Object = UnityEngine.Object;\n" + text + "\n}\n"
        path = runtime / (name + ".cs")
        path.write_text(text, encoding="utf-8", newline="\n")
        ports.append(dict(source=record(source), runtime=record(path), meta=record(meta(path, "zjcs-profession-ui-" + name))))
    apk = Apk.current()
    obj = apk.asset("textmesh_pro_rawfonts_fontassetfontconfig", "FontAssetFontConfig.json")
    raw = obj.read().m_Script.encode("utf-8", "surrogateescape")
    config = json.loads(raw)
    fonts = json.loads((ROOT / "assets/reference/profession-ui/fonts-source-manifest.json").read_bytes())
    directory = ROOT / "unity/Assets/Official/ProfessionUI/Fonts"
    directory.mkdir(parents=True, exist_ok=True)
    folder_meta = meta(directory, "zjcs-profession-fonts", "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n")
    outputs, entries = [], []
    config_file = directory / "FontAssetFontConfig.json"
    config_file.write_bytes(raw)
    outputs.extend((record(config_file), record(meta(config_file, "zjcs-profession-font-config")), record(folder_meta)))
    for font in fonts["fonts"]:
        source = ROOT / font["output"]
        if record(source)["sha256"] != font["sha256"]:
            raise ValueError("Verified original TTF changed")
        target = directory / source.name
        shutil.copyfile(source, target)
        # Import the original dynamic TTF; the TMP asset retains all original
        # face, glyph, material, atlas and rendering parameters.
        importer = "TrueTypeFontImporter:\n  externalObjects: {}\n  serializedVersion: 4\n  fontSize: 16\n  forceTextureCase: -2\n  characterSpacing: 0\n  characterPadding: 1\n  includeFontData: 1\n  fontNames: []\n  fallbackFontReferences: []\n  customCharacters:\n  fontRenderingMode: 0\n  ascentCalculationMode: 1\n  useLegacyBoundsCalculation: 0\n  shouldRoundAdvanceValue: 1\n"
        outputs.extend((record(target), record(meta(target, "zjcs-profession-font-" + source.name, importer))))
        entries.append(dict(name=font["name"], font_path=target.relative_to(ROOT / "unity").as_posix(), info=font["original_config"]))
    fixture = dict(config_path=config_file.relative_to(ROOT / "unity").as_posix(),
        default_font_asset=config["DefaultFontAsset"], entries=entries)
    offline = ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionFontConfig.cs"
    offline_meta = meta(offline, "zjcs-offline-profession-font-config")
    def asset_guid(path):
        return re.search(r"(?m)^guid: ([0-9a-f]{32})$", path.with_name(path.name + ".meta").read_text(encoding="utf-8"))[1]
    def asset(name, script, data):
        target = directory / (name + ".asset")
        native = source_header()
        native.update(m_Script=dict(fileID=11500000, guid=asset_guid(script), type=3), m_Name=name, m_EditorClassIdentifier="")
        native.update(data)
        body = yaml.dump({"MonoBehaviour": native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
        target.write_text("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n" + body, encoding="utf-8", newline="\n")
        outputs.extend((record(target), record(meta(target, "zjcs-profession-font-" + name, "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"))))
        return target.relative_to(ROOT / "unity").as_posix()
    fixture["config_asset"] = asset("ProfessionFontConfig", offline, dict(
        OriginalJson=dict(fileID=4900000, guid=asset_guid(config_file), type=3), DefaultFontAsset=config["DefaultFontAsset"],
        Entries=[dict(Name=name, **info) for name, info in config["FontAssetInfoDict"].items()]))
    resource_entries = [dict(Path="Assets/TextMesh Pro/RawFonts/FontAssetFontConfig.json", AssetGuid=asset_guid(config_file),
        Asset=dict(fileID=4900000, guid=asset_guid(config_file), type=3))]
    for entry in entries:
        target = ROOT / "unity" / entry["font_path"]
        resource_entries.append(dict(Path=entry["info"]["FontPath"], AssetGuid=asset_guid(target), Asset=dict(fileID=12800000, guid=asset_guid(target), type=3)))
    font = CACHE / "typed/export/ExportedProject/Assets/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic.asset"
    resource_entries.append(dict(Path=config["DefaultFontAsset"], AssetGuid=asset_guid(font), Asset=dict(fileID=11400000, guid=asset_guid(font), type=2)))
    fixture["resource_catalog"] = asset("ProfessionFontResources", offline.parent / "OfflineProfessionImageCatalog.cs", dict(Entries=resource_entries))
    (CACHE / "typed/font-service-fixture.json").write_text(json.dumps(fixture, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    result = dict(format=1, manifest=apk.version, ports=ports, config_source=apk.provenance(obj), config=record(config_file),
        original_config=config, outputs=outputs, bindings=entries, offline_config=[record(offline), record(offline_meta)], generator=record(Path(__file__)),
        operation="Port original FontAssetFontService initialization, lazy config load, source-font check, per-font retained handles, reflection assignment and release order. Replace logging and resource handle boundary; inject config deserialization (original JSON dictionary predecoded at packaging). Package exact verified official raw TTF bytes as Windows dynamic Font objects. Keep original TMP glyph/atlas/material/rendering parameters.",
        limitations=["This is the original in-memory FontAssetFontService path. Android streaming-file loading and the complete multi-language service are not claimed here."])
    (ROOT / "assets/reference/profession-ui/font-service-source-manifest.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Ported original font service and packaged two source TTF files")


if __name__ == "__main__":
    build()
