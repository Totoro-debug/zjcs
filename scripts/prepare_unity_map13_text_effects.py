"""Recover original TMP effect methods, literals and constants from the pinned APK."""
from pathlib import Path
import struct

from official_apk import ROOT
from official_metadata import Metadata
from read_official_native import NativeMethods
from prepare_unity_map13_sources import encode, sha, source_path, write, meta

DEST = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeText"


def build():
    image = ROOT / ".tools/source-packages/commerce-native/libil2cpp-restored.bin"
    metadata = ROOT / ".tools/source-packages/official-code/global-metadata.dat"
    native = NativeMethods(image, metadata); definitions = Metadata(metadata.read_bytes())
    records = []
    prefixes = ("TMPro.TMPEffect.", "TMPro.TMPEffectManager.", "TMPro.TMPEffectMaterial.")
    for name, (address, index) in native.by_name.items():
        if not name.startswith(prefixes): continue
        end = min(p for p in native.by_address if p > address)
        records.append(dict(method=name, metadata_index=index, address=hex(address), byte_count=end-address,
                            sha256=sha(native.image[address:end])))
    if len(records) != 30: raise ValueError("Original TMP effect method coverage differs")
    literals = []
    for slot in range(0x1233e518, 0x1233e5c8, 8):
        pointer = struct.unpack_from("<Q", native.image, slot)[0]
        encoded = struct.unpack_from("<Q", native.image, pointer)[0]
        if encoded >> 29 != 5: raise ValueError("Original TMP effect literal usage differs")
        index = (encoded & 0x1fffffff) >> 1
        length, offset = struct.unpack_from("<II", native.metadata, native.header[2] + index*8)
        literal = native.metadata[native.header[4] + offset:native.header[4] + offset + length].decode()
        literals.append(dict(slot=hex(slot), encoded=hex(encoded), index=index, value=literal))
    color = struct.unpack_from("<4f", native.image, 0x3982c70)
    radians = struct.unpack_from("<f", native.image, 0x3983050)[0]
    if color != (0, 0, 0, 1) or radians != 0.01745329238474369:
        raise ValueError("Original TMP effect constructor/angle constants differ")
    getters = []
    for name, prefix in [("TMPro.TMP_FontAsset.get_atlasPadding", "002841b9c0035fd6"),
                         ("TMPro.TMP_Text.get_enableAutoSizing", "00e04839c0035fd6"),
                         ("TMPro.TMP_Text.get_textInfo", "00b441f9c0035fd6")]:
        address, index = native.by_name[name]
        # Verify actual field reads instead of inferring a field from proximity.
        code = native.image[address:address+8]
        if code.hex() != prefix: raise ValueError("Original TMP field getter changed: " + name)
        getters.append(dict(method=name, address=hex(address), metadata_index=index, bytes=code.hex()))
    schema = [t for t in definitions.types if t["namespace"] == "TMPro" and t["name"] in
              ["TMPEffect", "TMPEffectManager", "TMPEffectMaterial", "TMP_Text", "TMP_FontAsset", "TMPro_EventManager"]]
    name = "TMPro.TextMeshPro.Rebuild"
    address, index = native.by_name[name]; end = min(p for p in native.by_address if p > address)
    material_dirty = dict(method=name, metadata_index=index, address=hex(address), byte_count=end-address,
                          sha256=sha(native.image[address:end]),
                          operation="CanvasUpdate.PreRender calls OnPreRenderObject, reads byte 0x140, calls UpdateMaterial when set, then clears byte 0x140; matches the engine baseline m_isMaterialDirty branch.")
    evidence = ROOT / "unity/Assets/Official/World/Map/13/Source/native-text-effects.json"
    write(evidence, encode(dict(methods=records, literals=literals, constructor_color=color, radians=radians,
        field_getters=getters, material_dirty_field=material_dirty, metadata_types=schema,
        observations=dict(font_material="DoSetup resets fontSharedMaterial to font.material before CanRender and both mesh updates.",
            normalized_effect="pointSize / fontSize / atlasPadding; Clamp01 each original width/distance/blur; X=-cos(angle)*distance, Y=-sin(angle)*distance.",
            font_dirty="Original direct TMP_Text field 0x140 is m_isMaterialDirty, not m_havePropertiesChanged (0x370).",
            material_pool="Shader.GetHashCode -> reference material; CopyPropertiesFromMaterial -> effect callback -> ComputeCRC cast to Int32 -> shader/CRC pool and reference count.",
            release="OnEnable/OnDisable/OnValidate/SetupEffect/SetGray/RealtimeUpdate release first. Last pool reference destroys its effect; last entry also destroys shader reference material.",
            autosize="TEXT_CHANGED_EVENT -> TMP_Text with enableAutoSizing -> effect ID/font size -> outline or underlay -> update tracked size -> release -> virtual DoSetup(true)."))))
    meta(evidence)
    template = ROOT / "scripts/map13_tmp_effect_runtime.cs"
    target = DEST / "TMPEffect.cs"
    write(target, template.read_bytes())
    for folder in [DEST]: meta(folder, folder=True)
    for path in DEST.iterdir():
        if path.is_file() and path.suffix != ".meta": meta(path)
    outputs = [p for p in DEST.iterdir() if p.is_file() and p.suffix != ".meta"]
    write(ROOT / "assets/reference/official-map/map13-text-effects-code-source-manifest.json", encode(dict(
        format=1, mapClassId=13, manifest="871_156598",
        inputs=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [image, metadata]],
        native_methods=records, evidence=dict(path=source_path(evidence), sha256=sha(evidence.read_bytes())),
        templates=[dict(path=source_path(template), sha256=sha(template.read_bytes()))],
        outputs=[dict(path=source_path(p), sha256=sha(p.read_bytes()), meta_sha256=sha(Path(str(p)+".meta").read_bytes())) for p in sorted(outputs)],
        converters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [Path(__file__), ROOT / "scripts/read_official_native.py", ROOT / "scripts/official_metadata.py"]],
        engine_baseline=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [
            ROOT / "unity/Library/PackageCache/com.unity.textmeshpro@3.0.6/Scripts/Runtime/TMP_Text.cs",
            ROOT / "unity/Library/PackageCache/com.unity.textmeshpro@3.0.6/Scripts/Runtime/TextMeshPro.cs"]],
        validation=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [
            ROOT / "unity/Assets/Game/Editor/Map13/OfficialMap13TextEffectAcceptance.cs",
            ROOT / "unity/Assets/Game/Editor/Map13/Zjcs.Editor.Map13.asmdef"]],
        operation="Reconstruct C# from all 30 pinned official TMPEffect/manager/material native methods and original metadata; recover every material property/keyword literal and constructor/angle constant. Preserve original branch/operation/lifecycle order, effect normalization, material CRC pooling, reference counts, color space, autosize callbacks and raw TMP material-dirty field access. The separate native manager calls private/protected effect methods; internal wrappers adapt only managed access visibility. Keep the engine TMP dependency separate and explicitly preserve its reflected field for Player builds.",
        limitations=["Native code reconstruction and engine compatibility require runtime validation; original world-label prefabs and full map state/UI integration remain separate."])))
    print("Recovered original TMP effects:", len(records), "native methods,", len(literals), "keyword/property literals")


if __name__ == "__main__": build()
