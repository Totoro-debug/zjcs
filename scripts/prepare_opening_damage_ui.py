"""Restore the official bitmap damage fonts and UGUI Text bindings omitted by the TMP exporter."""
import hashlib
import io
import json
import struct
from pathlib import Path
from official_apk import Apk, ROOT
from prepare_character_creation_opening import json_bytes, write_with_meta, REFERENCE, PAYLOAD
from UnityPy.classes.PPtr import PPtr
from official_unity_schema import ScriptSchemas

OUT = ROOT / "unity/Assets/Official/Opening/Damage/Resources/OpeningDamage"


def main():
    apk = Apk()
    schemas = ScriptSchemas()
    source = json.loads((ROOT / "assets/reference/battle-hud/source-manifest.json").read_text(encoding="utf-8"))
    fonts, prefabs, inputs, outputs = {}, [], {}, []

    def record(obj):
        inputs[obj.assets_file.name + ":" + str(obj.path_id)] = dict(apk.provenance(obj),
            sha256=hashlib.sha256(obj.get_raw_data()).hexdigest())
        return obj

    def pointer(value, owner):
        return record(PPtr(**value, assetsfile=owner.assets_file).deref())

    def font(obj):
        record(obj)
        data = obj.read_typetree()
        name = data["m_Name"]
        if name in fonts:
            return name
        # Screen-space (_Combine) prefabs bind the bitmap material explicitly.
        # The world-space font copy uses Unity's null default-font material.
        # Screen-space fonts have repacked UVs and proportional advances, so keep
        # their own glyph table along with their own material/atlas as one unit.
        canonical = next(p.deref() for path, p in apk.container("defaultpackage_assets_textmesh_pro_bmpfonts.bundle")
                         if p.type.name == "Font" and p.read().m_Name == name)
        canonical_data = record(canonical).read_typetree()
        obj, data = canonical, canonical_data
        material = pointer(data["m_DefaultMaterial"], obj)
        fields = material.read().m_SavedProperties
        env = dict(fields.m_TexEnvs)["_MainTex"]
        texture = record(env.m_Texture.deref())
        buffer = io.BytesIO()
        texture.read().image.save(buffer, format="PNG")
        outputs.append(write_with_meta(OUT / (name + ".png"), buffer.getvalue()))
        shader = record(material.read().m_Shader.deref())
        native_shader = shader.read().m_ParsedForm.m_Name
        fonts[name] = dict(name=name, glyphs=data["m_CharacterRects"], lineSpacing=data["m_LineSpacing"],
            size=data["m_FontSize"], ascent=data["m_Ascent"], descent=data["m_Descent"],
            materialShader=native_shader, materialFloats=dict(fields.m_Floats),
            materialColors={key:dict(r=value.r, g=value.g, b=value.b, a=value.a) for key,value in fields.m_Colors})
        return name

    def path_of(go):
        names = []
        while go is not None:
            names.insert(0, go.m_Name)
            transform = next(c.component.read() for c in go.m_Component
                             if c.component.type.name in ("Transform", "RectTransform"))
            go = transform.m_Father.read().m_GameObject.read() if transform.m_Father.path_id else None
        return "/" + "/".join(names)

    for key, entry in source["prefabs"].items():
        if not key.startswith("damage_"):
            continue
        texts, images = [], []
        for obj in apk.objects(entry["bundle"]):
            if obj.type.name != "MonoBehaviour":
                continue
            value = obj.read(check_read=False)
            script = value.m_Script.read()
            if script.m_ClassName in ("DamagePartialImage", "DamageSequenceImage"):
                data = schemas.read(record(obj))
                sprite_obj = pointer(data["m_Sprite"], obj)
                sprite = sprite_obj.read()
                buffer = io.BytesIO()
                sprite.image.save(buffer, format="PNG")
                file = sprite.m_Name + "-" + hashlib.sha256(buffer.getvalue()).hexdigest()[:12] + ".png"
                outputs.append(write_with_meta(PAYLOAD / "DamageUI" / file, buffer.getvalue()))
                material = pointer(data["m_Material"], obj).read()
                shader = record(material.m_Shader.deref()).read().m_ParsedForm.m_Name
                if shader != "UI/GameDamage":
                    raise ValueError("Unexpected native damage image shader: " + shader)
                images.append(dict(path=path_of(value.m_GameObject.read()), file=file,
                    color=list(data["m_Color"].values()), type=data["m_Type"], preserveAspect=bool(data["m_PreserveAspect"]),
                    raycastTarget=bool(data["m_RaycastTarget"]), maskable=bool(data["m_Maskable"]),
                    sequence=script.m_ClassName == "DamageSequenceImage", rate=data.get("mRate",0),
                    rows=data.get("mRow",1), columns=data.get("mCol",1)))
                continue
            if script.m_ClassName != "Text" or script.m_Namespace != "UnityEngine.UI":
                continue
            raw = record(obj).get_raw_data()
            # Pinned Unity 2022 UGUI Graphic/MaskableGraphic + FontData serialization.
            # FontData begins at 88; m_Text immediately follows its 56 bytes.
            file_id, path_id = struct.unpack_from("<iq", raw, 88)
            length = struct.unpack_from("<i", raw, 144)[0]
            if length < 0 or (148 + length + 3) & ~3 != len(raw):
                raise ValueError("Unexpected official legacy Text field layout: " + key)
            font_name = font(pointer(dict(m_FileID=file_id, m_PathID=path_id), obj))
            texts.append(dict(path=path_of(value.m_GameObject.read()), font=font_name,
                fontSize=struct.unpack_from("<i", raw, 100)[0], style=struct.unpack_from("<i", raw, 104)[0],
                bestFit=bool(struct.unpack_from("<i", raw, 108)[0]), minSize=struct.unpack_from("<i", raw, 112)[0],
                maxSize=struct.unpack_from("<i", raw, 116)[0], alignment=struct.unpack_from("<i", raw, 120)[0],
                alignByGeometry=bool(struct.unpack_from("<i", raw, 124)[0]), richText=bool(struct.unpack_from("<i", raw, 128)[0]),
                horizontalOverflow=struct.unpack_from("<i", raw, 132)[0], verticalOverflow=struct.unpack_from("<i", raw, 136)[0],
                lineSpacing=struct.unpack_from("<f", raw, 140)[0], color=list(struct.unpack_from("<4f", raw, 44))))
        prefabs.append(dict(key=key, texts=texts, images=images))
    outputs.append(write_with_meta(OUT / "fonts.json", json_bytes(dict(manifest="854_156383", fonts=list(fonts.values())))))
    from prepare_opening_battlefield import DAMAGE_RULES
    rules_bytes = DAMAGE_RULES.read_bytes()
    colors = json.loads(rules_bytes)["damage_settings"]
    outputs.append(write_with_meta(PAYLOAD / "damage-ui.json", json_bytes(dict(manifest="854_156383", prefabs=prefabs, colors=colors))))
    receipt = json.loads(REFERENCE.read_text(encoding="utf-8"))
    receipt["bitmap_damage_ui"] = dict(inputs=list(inputs.values()), rules_sha256=hashlib.sha256(rules_bytes).hexdigest(),
        generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
    paths = {row["path"] for row in outputs}
    receipt["outputs"] = [row for row in receipt["outputs"] if row["path"] not in paths] + outputs
    REFERENCE.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps([dict(name=f["name"], glyphs=len(f["glyphs"]), shader=f["materialShader"]) for f in fonts.values()]))


if __name__ == "__main__":
    main()
