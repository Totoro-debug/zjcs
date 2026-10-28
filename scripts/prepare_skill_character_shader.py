"""Reuse the verified original character shader for the desktop skill preview."""
import hashlib
import json
import re
import uuid
from official_apk import ROOT


def restore(reader):
    receipt_path = ROOT / "assets/reference/official-map/map13-entity-shaders-source-manifest.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    name = "Universal Render Pipeline/Spine/SkeletonDye-2D"
    entry = next(row for row in receipt["inputs"] if row["original"]["name"] == name)
    original = entry["original"]
    obj = reader.object(original["input"], original["path_id"])
    sha = lambda data: hashlib.sha256(data).hexdigest()
    assert sha(obj.get_raw_data()) == original["payload_sha256"]
    output = next(row for row in receipt["outputs"] if row.get("original_shader") == name)
    raw = (ROOT / output["path"]).read_bytes()
    assert sha(raw) == output["sha256"]
    text, count = re.subn(r'^Shader "[^"]+"', 'Shader "Zjcs/Official/Character/SkillPreviewDye"', raw.decode())
    assert count == 1
    # The desktop preview uses the built-in camera; select the same original
    # Universal2D pass while keeping every GPU program and render state intact.
    text, pipeline_count = re.subn(r' "RenderPipeline"="UniversalPipeline"', '', text)
    text, pass_count = re.subn(r'"LIGHTMODE"="Universal2D"', '"LIGHTMODE"="Always"', text)
    assert pipeline_count > 0 and pass_count == 1
    target = ROOT / "unity/Assets/Game/Runtime/Official/Character/NativeSkillPreviewDye.shader"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding="utf-8")
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs-official-skills/native-character-dye").hex
    target.with_name(target.name + ".meta").write_text("fileFormatVersion: 2\nguid: " + guid
        + "\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")
    folder_meta = target.parent.with_name(target.parent.name + ".meta")
    if not folder_meta.exists():
        folder_meta.write_text("fileFormatVersion: 2\nguid: "
            + uuid.uuid5(uuid.NAMESPACE_URL, "zjcs-official-character-code").hex
            + "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")
    # The skill entrance keeps the native Home sky alive. Reuse its Windows
    # program only after checking the pinned APK object, independently of Map13.
    home_name = "Universal Render Pipeline/2D/Sprite-Lit-ColorShift"
    home_original = next(row["original"] for row in receipt["inputs"] if row["original"]["name"] == home_name)
    home_obj = reader.object(home_original["input"], home_original["path_id"])
    assert sha(home_obj.get_raw_data()) == home_original["payload_sha256"]
    home_output = next(row for row in receipt["outputs"] if row.get("original_shader") == home_name)
    assert sha((ROOT / home_output["path"]).read_bytes()) == home_output["sha256"]
    return {"source": reader.apk.provenance(obj), "object_sha256": sha(obj.get_raw_data()),
            "conversion_reference": receipt_path.relative_to(ROOT).as_posix(), "verified_conversion": output,
            "urp_output": output,
            "home_sky_dependency": {"source": reader.apk.provenance(home_obj),
                "object_sha256": sha(home_obj.get_raw_data()), "verified_conversion": home_output},
            "output": {"path": target.relative_to(ROOT).as_posix(), "sha256": sha(target.read_bytes())},
            "adaptation": "Select the original Universal2D pass with the built-in desktop preview camera. Preserve all original GPU variants, alpha unpremultiplication, HSV arithmetic, saturation clamp, alpha discard, blending and depth/stencil state."}
