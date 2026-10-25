"""Recover the APK TMP sprite asset, retaining its exact glyph metrics and atlas."""
import io
from pathlib import Path

from UnityPy.classes.PPtr import PPtr
from official_unity_schema import ScriptSchemas
from prepare_unity_map10_entities import ROOT, sha256, write_generated, write_texture_meta


def extract_common(apk, output):
    editor = Path("D:/Tool/Unity/Editor/Data")
    overrides = {
        "Unity.TextMeshPro": ROOT / "unity/Library/ScriptAssemblies/Unity.TextMeshPro.dll",
        "UnityEngine.TextCoreFontEngineModule": editor / "Managed/UnityEngine/UnityEngine.TextCoreFontEngineModule.dll",
        "netstandard": editor / "NetStandard/ref/2.1.0/netstandard.dll",
    }
    schemas = ScriptSchemas(overrides)
    obj = apk.asset("textmesh_pro_assets_sprite_assets_common", "/Common.asset")
    fields = schemas.read(obj)  # Strict schema read must consume all original bytes.
    if fields["m_Name"] != "Common" or fields["m_Version"] != "1.1.0" \
            or len(fields["m_SpriteGlyphTable"]) != 7 or len(fields["m_SpriteCharacterTable"]) != 7:
        raise ValueError("Unexpected pinned Common TMP sprite asset")
    texture = PPtr(**fields["spriteSheet"], assetsfile=obj.assets_file).deref()
    material = PPtr(**fields["material"], assetsfile=obj.assets_file).deref()
    mat = material.read()
    if mat.m_Shader.read().m_ParsedForm.m_Name != "TextMeshPro/Sprite":
        raise ValueError("Unexpected official TMP sprite shader")
    data = io.BytesIO()
    texture.read().image.save(data, format="PNG")
    image = data.getvalue()
    path = output / "Common.png"
    write_generated(path, image)
    write_texture_meta(path)
    payload = {
        "name": fields["m_Name"], "hashCode": fields["hashCode"], "version": fields["m_Version"],
        "texturePath": path.relative_to(ROOT / "unity").as_posix(), "textureSha256": sha256(image),
        "glyphs": [{"index": row["m_Index"], "metrics": list(row["m_Metrics"].values()),
                    "rect": list(row["m_GlyphRect"].values()), "scale": row["m_Scale"],
                    "atlasIndex": row["m_AtlasIndex"]} for row in fields["m_SpriteGlyphTable"]],
        "characters": [{"name": row["m_Name"], "hashCode": row["m_HashCode"],
                        "unicode": row["m_Unicode"], "glyphIndex": row["m_GlyphIndex"],
                        "scale": row["m_Scale"]} for row in fields["m_SpriteCharacterTable"]],
    }
    sources = {"asset": apk.provenance(obj), "texture": apk.provenance(texture),
               "material": dict(apk.provenance(material), fields=material.read_typetree()),
               "shader": apk.provenance(mat.m_Shader.deref()), "schemas": schemas.records,
               "schemaAssemblies": {key: {"path": str(value), "sha256": sha256(value.read_bytes())}
                                    for key, value in overrides.items()},
               "output": {"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(image)}}
    return payload, sources
