"""Recover original profession fonts and zero-size dynamic atlas native assets."""
import copy
import hashlib
import json
from pathlib import Path
import re
import sys
from prepare_unity_profession_native_inputs import ROOT, CACHE
sys.path.insert(0, str(ROOT / ".tools/unitypy-work"))
from official_apk import Apk
from UnityPy.classes import PPtr
import yaml

FONT_BUNDLE = "defaultpackage_assets_textmesh_pro_assets_fonts_&_materials.bundle"
FONT_NAMES = ("FZY4K SDF - dynamic", "MPLUSRounded1c-Medium SDF")


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def build():
    apk = Apk.current()
    receipt_path = CACHE / "typed/inputs/source-manifest.json"
    receipt = json.loads(receipt_path.read_bytes())
    if receipt["manifest"] != apk.version or apk.version != "871_156598":
        raise ValueError("Profession font source version differs")
    source_objects = {(obj.assets_file.name, str(obj.path_id)): obj for obj in apk.objects(FONT_BUNDLE)}
    config_obj = apk.asset("textmesh_pro_rawfonts_fontassetfontconfig", "FontAssetFontConfig.json")
    config_bytes = config_obj.read().m_Script.encode("utf8", "surrogateescape")
    config = json.loads(config_bytes)
    output = CACHE / "typed/fonts"
    output.mkdir(exist_ok=True)
    fonts, textures, assets = [], [], []
    export = CACHE / "typed/export/ExportedProject/Assets"
    exported_guids = {}
    for meta in export.rglob("*.meta"):
        match = re.search(r"(?m)^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf8"))
        if match: exported_guids[match[1]] = meta.with_suffix("")
    for name in FONT_NAMES:
        record, = [row for row in receipt["components"] if row["type"] == "TMPro.TMP_FontAsset" and row["fields"]["m_Name"] == name]
        obj = source_objects[record["cab"], record["path_id"]]
        if digest(obj.get_raw_data()) != record["payload_sha256"]:
            raise ValueError("Original profession font component changed")
        fields = record["fields"]
        info = config["FontAssetInfoDict"][name]
        raw_path = info["RawFontPath"]
        raw_obj = apk.asset("textmesh_pro_rawfonts_" + Path(raw_path).stem.lower(), Path(raw_path).name)
        raw = raw_obj.read().m_Script.encode("utf8", "surrogateescape")
        if len(raw) != info["Length"] or hashlib.md5(raw).hexdigest() != info["Hash"]:
            raise ValueError("Original streaming font length/hash differs: " + name)
        target = output / Path(info["FontPath"]).name
        target.write_bytes(raw)
        fonts.append(dict(name=name, original_font=apk.provenance(obj), original_payload_sha256=record["payload_sha256"],
            original_config=info, source=apk.provenance(raw_obj), sha256=digest(raw),
            output=target.relative_to(ROOT).as_posix()))
        exported_font = export / "TextMesh Pro/Assets/Fonts & Materials" / (name + ".asset")
        font_text = exported_font.read_text(encoding="utf8")
        atlas_match = re.search(r"(?m)^  m_AtlasTextures:\n  - \{fileID: 2800000, guid: ([0-9a-f]{32}), type: 3\}", font_text)
        pointers = fields["m_AtlasTextures"]
        if not pointers:
            if atlas_match:
                raise ValueError("Export invented a dynamic font atlas")
            continue
        if len(pointers) != 1 or not atlas_match:
            raise ValueError("Inspect original profession font atlas list")
        atlas = PPtr(**pointers[0], assetsfile=obj.assets_file).deref()
        original = atlas.read_typetree()
        if (original["m_Width"], original["m_Height"], original["m_CompleteImageSize"],
            len(original["image data"]), original["m_StreamData"]["size"]) != (0, 0, 0, 0, 0):
            existing = exported_guids.get(atlas_match[1])
            if existing is None or not existing.is_file():
                raise ValueError("A nonempty original font atlas has not been converted")
            fonts[-1]["existing_atlas"] = dict(source=apk.provenance(atlas),
                source_payload_sha256=digest(atlas.get_raw_data()), guid=atlas_match[1],
                path=existing.relative_to(ROOT).as_posix(), sha256=digest(existing.read_bytes()))
            continue
        if fields["m_AtlasPopulationMode"] != 1:
            raise ValueError("An empty atlas belongs to a non-dynamic font")
        native = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject={"fileID": 0},
            m_PrefabInstance={"fileID": 0}, m_PrefabAsset={"fileID": 0})
        native.update(copy.deepcopy(original))
        native["serializedVersion"] = 2
        native["m_TextureSettings"] = dict(serializedVersion=2, **native["m_TextureSettings"])
        native["m_PlatformBlob"] = ""
        native["image data"] = 0
        native["_typelessdata"] = ""
        for key, value in native.items():
            if isinstance(value, bool): native[key] = int(value)
        body = yaml.safe_dump({"Texture2D": native}, sort_keys=False, allow_unicode=True, width=120)
        raw_asset = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!28 &2800000\n" + body).encode("utf8")
        rel = "TextMesh Pro/Assets/Fonts & Materials/" + original["m_Name"] + ".asset"
        path = output / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw_asset)
        meta = path.with_name(path.name + ".meta")
        meta.write_text("fileFormatVersion: 2\nguid: " + atlas_match[1] + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf8")
        textures.append(dict(source=apk.provenance(atlas), original_payload_sha256=digest(atlas.get_raw_data()),
            original_fields={key: value for key, value in original.items() if key != "image data"}, image_bytes=0,
            output=path.relative_to(ROOT).as_posix(), sha256=digest(raw_asset), guid=atlas_match[1],
            meta_sha256=digest(meta.read_bytes()), native_relative_path=rel))
        assets.append(dict(path=path.relative_to(ROOT).as_posix(), native_relative_path=rel,
                           guid=atlas_match[1], sha256=digest(raw_asset), meta_sha256=digest(meta.read_bytes())))
    result = dict(format=1, manifest=apk.version, fonts=fonts, textures=textures, assets=assets,
        font_config=dict(source=apk.provenance(config_obj), sha256=digest(config_bytes)),
        component_cache=dict(path=receipt_path.relative_to(ROOT).as_posix(), sha256=digest(receipt_path.read_bytes())),
        tool=dict(path=Path(__file__).resolve().relative_to(ROOT).as_posix(), sha256=digest(Path(__file__).read_bytes())),
        operation="Recover official raw TTF bytes using the original streaming-font length/MD5 configuration. Preserve original zero-size Alpha8 dynamic atlas native fields and GUID instead of drawing or substituting an image.",
        limitations=["Conversion cache only. Unity native import, original font service binding and rendered glyph validation remain required."])
    proof = ROOT / "assets/reference/profession-ui/fonts-source-manifest.json"
    proof.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    print("Recovered", len(fonts), "official TTF fonts and", len(textures), "original zero-size native atlases")
    return result


if __name__ == "__main__":
    build()
