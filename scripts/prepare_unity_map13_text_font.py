"""Restore the official dynamic Chinese font, original atlas and material."""
from pathlib import Path
import hashlib
import json
import re
import struct

from official_apk import Apk, ROOT
from UnityPy.classes.PPtr import PPtr
from prepare_unity_map13_native_terrain import CACHE, EXPORTED, GUID, closure
from prepare_unity_map13_native_scene import native_texture
from prepare_unity_map13_sources import encode, sha, source_path, write, meta
from prepare_unity_map13_water_components import import_meta

DEST = ROOT / "unity/Assets/Official/World/Map/13/NativeText"


def build():
    apk = Apk.current(); bundle = "textmesh_pro_assets_fonts_&_materials"
    font = apk.asset(bundle, "/FZY4K SDF - dynamic.asset")
    file_id, path_id = struct.unpack_from("<iq", font.get_raw_data(), 56)
    mat = PPtr(m_FileID=file_id, m_PathID=path_id, assetsfile=font.assets_file).deref()
    atlas = next(v.m_Texture.deref() for k, v in mat.read().m_SavedProperties.m_TexEnvs if k == "_MainTex")
    original_font = apk.asset("textmesh_pro_fonts_fzy4k", "/FZY4K.TTF")
    raw_font = apk.asset("textmesh_pro_rawfonts_fzy4k", "/FZY4K.bytes")
    config = apk.asset("textmesh_pro_rawfonts_fontassetfontconfig", "/FontAssetFontConfig.json")
    language_config = apk.asset(bundle, "/MultiLanguageFontConfig.json")
    data = bytes(original_font.read().m_FontData)
    raw_data = raw_font.read().m_Script.encode("utf8", errors="surrogateescape")
    item = json.loads(config.read().m_Script)["FontAssetInfoDict"]["FZY4K SDF - dynamic"]
    if data != raw_data or len(data) != item["Length"] or hashlib.md5(data).hexdigest() != item["Hash"]:
        raise ValueError("Original raw/font/config bytes differ")
    _, _, index = closure()
    exported_font = EXPORTED / "TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic.asset"
    font_text = exported_font.read_text(encoding="utf8")
    mat_guid = re.search(r"(?m)^  material: \{fileID: 2100000, guid: (\w+)", font_text)[1]
    atlas_guid = re.search(r"(?m)^  - \{fileID: 2800000, guid: (\w+)", font_text)[1]
    exported_mat = index[mat_guid]; exported_atlas = index[atlas_guid]
    mat_text = exported_mat.read_text(encoding="utf8")
    shader_guid = re.search(r"m_Shader: \{fileID: 4800000, guid: (\w+)", mat_text)[1]
    script_guid = re.search(r"m_Script: \{fileID: 11500000, guid: (\w+)", font_text)[1]
    package_meta = ROOT / "unity/Library/PackageCache/com.unity.textmeshpro@3.0.6/Scripts/Runtime/TMP_FontAsset.cs.meta"
    shader = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeWater/Shaders/WorldText.shader"
    paths = {"font": DEST / "FZY4K-Dynamic.asset", "material": DEST / "FZY4K-Dynamic.mat",
             "atlas": DEST / "FZY4K-Dynamic-Atlas.texture.asset", "source": DEST / "FZY4K.ttf"}
    native_guids = {k: hashlib.md5(("zjcs-map13-original-text:" + source_path(v)).encode()).hexdigest() for k, v in paths.items()}
    redirects = {mat_guid: native_guids["material"], atlas_guid: native_guids["atlas"],
                 shader_guid: GUID.search(Path(str(shader) + ".meta").read_text(encoding="utf8"))[1],
                 script_guid: GUID.search(package_meta.read_text(encoding="utf8"))[1]}
    def remap(text): return GUID.sub(lambda m: "guid: " + redirects.get(m[1], m[1]), text)
    font_text = remap(font_text).replace("  m_SourceFontFileGUID:\n", "  m_SourceFontFileGUID: " + native_guids["source"] + "\n")
    font_text = font_text.replace("  m_SourceFontFile: {fileID: 0}", "  m_SourceFontFile: {fileID: 12800000, guid: " + native_guids["source"] + ", type: 3}")
    font_text = font_text.replace("guid: " + native_guids["atlas"] + ", type: 3", "guid: " + native_guids["atlas"] + ", type: 2")
    texture = atlas.read(); pixels = texture.get_image_data()
    if texture.m_TextureFormat != 1 or texture.m_MipCount != 1 or len(pixels) != texture.m_Width * texture.m_Height:
        raise ValueError("Restore original dynamic font atlas format explicitly")
    template = CACHE / "native-texture-template.asset"
    texture_text = native_texture(template.read_text(encoding="utf8"), texture, pixels).decode()
    texture_text, count = re.subn(r"(?m)^  m_TextureFormat: 4$", "  m_TextureFormat: 1", texture_text)
    if count != 1: raise ValueError("Native atlas format field differs")
    payloads = {"font": font_text.encode(), "material": remap(mat_text).replace("guid: " + native_guids["atlas"] + ", type: 3", "guid: " + native_guids["atlas"] + ", type: 2").encode(),
                "atlas": texture_text.encode(), "source": data}
    for key, target in paths.items():
        write(target, payloads[key])
        head = "fileFormatVersion: 2\nguid: " + native_guids[key] + "\n"
        if key == "source":
            metadata = head + "TrueTypeFontImporter:\n  externalObjects: {}\n  serializedVersion: 4\n  fontSize: 16\n  forceTextureCase: -2\n  characterSpacing: 0\n  characterPadding: 1\n  includeFontData: 1\n  fontNames:\n  - FZCuYuan-M03\n  fallbackFontReferences: []\n  customCharacters:\n  fontRenderingMode: 0\n  ascentCalculationMode: 1\n  useLegacyBoundsCalculation: 0\n  shouldRoundAdvanceValue: 1\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
        else:
            metadata = head + "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: " + str({"font": 11400000, "material": 2100000, "atlas": 2800000}[key]) + "\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
        write(Path(str(target) + ".meta"), metadata.encode())
    import_meta(DEST, "DefaultImporter", folder=True)
    source_root = ROOT / "unity/Assets/Official/World/Map/13/Source/Text"
    configs = []
    for obj in [config, language_config]:
        target = source_root / (obj.read().m_Name + ".json")
        write(target, obj.read().m_Script.encode()); meta(target)
        configs.append(dict(path=source_path(target), sha256=sha(target.read_bytes())))
    meta(source_root, folder=True)
    code = ROOT / ".tools/source-packages/current/871_156598/official-code/framework-firstpass-source/FontAssetFontService.cs"
    write(ROOT / "assets/reference/official-map/map13-native-text-font-source-manifest.json", encode(dict(
        format=1, mapClassId=13, manifest=apk.version,
        inputs=[dict(source=apk.provenance(x), object_sha256=sha(x.get_raw_data())) for x in [font, mat, atlas, original_font, raw_font, config, language_config]],
        exported_inputs=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [exported_font, exported_mat, exported_atlas]],
        outputs=[dict(path=source_path(p), sha256=sha(p.read_bytes()), meta_sha256=sha(Path(str(p) + ".meta").read_bytes())) for p in paths.values()],
        configs=configs, source_font=dict(length=len(data), md5=hashlib.md5(data).hexdigest(), sha256=sha(data)),
        atlas=dict(width=texture.m_Width, height=texture.m_Height, format=1, mip_count=1, byte_count=len(pixels), sha256=sha(pixels), readable=texture.m_IsReadable),
        font_service=dict(path=source_path(code), sha256=sha(code.read_bytes()), method="SetupFont(TMP_FontAsset, Action<AssetOperationHandle<Font>>)",
            adaptation="Bind the original config-selected Font directly to m_SourceFontFile before text activation; original FontAssetFontService returns immediately when this field is already bound."),
        dependencies=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [shader, package_meta, template]],
        engine_baseline=dict(package="com.unity.textmeshpro@3.0.6", execution_equivalence_verified=False,
            files=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [package_meta.with_suffix(""),
                package_meta.parent / "TMP_Text.cs", package_meta.parent / "TextMeshPro.cs", package_meta.parent / "TMPro_Private.cs"]]),
        converters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [Path(__file__), ROOT / "scripts/prepare_unity_map13_native_scene.py"]],
        validation=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [
            ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13TextTools.cs",
            ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13TextAcceptance.cs",
            ROOT / "unity/Assets/Game/Editor/Map13/Zjcs.Editor.Map13.asmdef"]],
        operation="Restore original font settings, initial glyph/character tables, original Alpha8 atlas bytes and custom TMP material. Recover identical original TTF and raw font bytes; verify against the original selected-version font config MD5/length. Bind native font/material/atlas/shader explicitly. Keep original custom psd2UguiSetting in the source YAML; stock TMP does not expose this extra serialized field.",
        limitations=["Native text effect code, complete custom TMP runtime compatibility, original region labels and their interactions are not supplied by this font asset conversion."])))
    print("Restored original dynamic Chinese font, material, Alpha8 atlas and identical official TTF/raw font:", len(data), "bytes")


if __name__ == "__main__": build()
