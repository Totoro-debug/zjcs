"""Install the current official dungeon and arena UI prefab dependency closure."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[1]
VERSION = "884_156692"
EXPORT_ROOT = ROOT / ".tools/source-packages/current" / VERSION / "native-conversion/native-export-all"
EXPORT = EXPORT_ROOT / "ExportedProject/Assets"
INPUTS = ROOT / ".tools/source-packages/current" / VERSION / "native-conversion/native-export-all-source-manifest.json"
BUNDLE_SOURCES = ROOT / ".tools/source-packages/extracted/bundle-sources.json"
EXTRACTED = ROOT / ".tools/source-packages/extracted"
OFFICIAL = ROOT / "unity/Assets/Official/Dungeons" / VERSION
RESOURCES = ROOT / "unity/Assets/Game/Resources/Dungeons/UI" / VERSION
RECEIPT = ROOT / "assets/reference/dungeon-ui" / VERSION / "source-manifest.json"
NPC_ADVENTURE_RECEIPT = ROOT / "assets/reference/npc-adventure-ui" / VERSION / "source-manifest.json"
GUID_INDEX_CACHE = EXPORT_ROOT.parent / "asset-guid-index.json"
PRIOR_NATIVE_RECEIPT = ROOT / "assets/reference/dungeon-ui/daily-dungeon-result-ui-source-manifest.json"
WINDOWS_SHADER_ROOT = (ROOT / ".tools/source-packages/current" / VERSION
                       / "native-conversion/all-assets/windows-shaders")
WINDOWS_SHADER_RECEIPT = WINDOWS_SHADER_ROOT / "source-manifest.json"
CURRENT_WINDOWS_RECEIPT = ROOT / "assets/reference/official-map/current-resource-windows-source-manifest.json"
TRAVEL_SOURCE = EXPORT / "Prefabs/UI/Home/TravelNotesPanel.prefab"
TRAVEL_RUNTIME = ROOT / "unity/Assets/Game/Resources/Travel" / VERSION / "TravelNotesPanel.prefab"
ITEM_ICON_ATTACHMENT_CONFIG = EXPORT / "ScriptableConfig/Modules/ItemIcon/ItemIconAttachmentConfig.asset"
PROBABILITY_TIP_SOURCE = EXPORT / "Prefabs/UI/Item/Attachments/ProbabilityGo.prefab"
PROBABILITY_TIP_ASSET_GUID = "0f0eba76d13b89243bc14ab6f3bcbcb8"
GUID = re.compile(r"guid: ([0-9a-f]{32})")
META_GUID = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)
BUILTIN = {"0000000000000000e000000000000000", "0000000000000000f000000000000000",
           "00000000000000000000000000000000"}
RUNTIME_CONVERSION = "dungeon-ui-ugui-tmp-v5"
DYNAMIC_FONT_ATLAS_GUID = "20074dd7c53f6814189191d58ca10b50"
DYNAMIC_FONT_ATLAS_BUNDLE = "defaultpackage_assets_textmesh_pro_assets_fonts_&_materials.bundle"
LAUNCH_DLL_GUID = "fdf006957f0f071b960b15b4c9e68d1a"
UIMESH_SOURCE_FILE_ID = 87426601
UIMESH_SCRIPT_GUID = "ec003762adb4125fe5bac676d8e680ca"
UIMESH_CODE_SOURCE = ROOT / ".tools/source-packages/official-code/launch-source"
UIMESH_CODE_RUNTIME = ROOT / "unity/Assets/Game/Runtime/Official/WorldUI"
DISTORT_SHADER_RUNTIME = UIMESH_CODE_RUNTIME / ("UIEffectDistort_" + VERSION + ".shader")
DISTORT_SHADER_RUNTIME_GUID = uuid.uuid5(
    uuid.NAMESPACE_URL, "zjcs/dungeons/ui/" + VERSION + "/shader/UIEffectDistort").hex
DISTORT_MATERIALS = (
    ("GameMatch_Circle2", "5e803372c1487414f999b206f00dcaba"),
    ("GameMatch_Circle3", "bacafd20f868db345a859e0c96c56341"),
)
DISTORT_MATERIAL_SHADER_GUID = "91e407fe7dc53104aa467ae74064f1ad"
DISTORT_SHADER_SOURCE_SHA256 = "80b71a5449b2d2d721e9581e0f86f02d0d27bf87d031ba36c47d831d515b317d"
DISTORT_SHADER_NAME = "Zjcs/Official/Dungeons/UIEffectDistort_" + VERSION
ADDITIVE_SHADER_RUNTIME = ROOT / "unity/Assets/Game/Runtime/Official/Dungeons/UI/EffectAdditive.shader"
ADDITIVE_SHADER_RUNTIME_GUID = "db70ec0caa35e7d71d8969a24da5c91b"
RUNTIME_SPINE_SHADERS = (
    {
        "source_name": "Spine/SkeletonGraphic",
        "path_id": "-2506585663452162707",
        "object_sha256": "cbb8921bff9f069af1274f02bfbc3ca132107028c24c8e66051a92a1678311bc",
        "runtime_name": "Zjcs/Official/Dungeons/SpineSkeletonGraphic_" + VERSION,
        "runtime_path": ROOT / ("unity/Assets/Game/Runtime/Official/Dungeons/Spine/SpineSkeletonGraphic_" + VERSION + ".shader"),
    },
    {
        "source_name": "Spine/SkeletonGraphicDye",
        "path_id": "3940403792374418680",
        "object_sha256": "5e087281015e8b0da271b501f5434a6e8abb371ec8bc25865eab3cc6db4f855e",
        "runtime_name": "Zjcs/Official/Dungeons/SpineSkeletonGraphicDye_" + VERSION,
        "runtime_path": ROOT / ("unity/Assets/Game/Runtime/Official/Dungeons/Spine/SpineSkeletonGraphicDye_" + VERSION + ".shader"),
    },
)
RUNTIME_UI_MATERIALS = (
    {
        "source_guid": "0197be4263e9bae4db9439953f71f474",
        "source_name": "UIImgAdd",
        "source_shader_guid": "98c6b90e69b4b2748bc32e1fa44922f0",
        "shader_name": "UI/EffectAdditive",
        "runtime_name": "UIImgAdd_Official",
        "runtime_path": RESOURCES / "Materials/UIImgAdd_Official.mat",
        "runtime_shader_name": "UI/EffectAdditive",
    },
    {
        "source_guid": "0627795339756134ebcea556c4cec2d2",
        "source_name": "SkeletonGraphicDefault",
        "source_shader_guid": "8318b9157b2eddd4293c9f841a19a592",
        "shader_name": "Spine/SkeletonGraphic",
        "runtime_name": "SkeletonGraphicDefault",
        "runtime_path": RESOURCES / "Materials/Spine/SkeletonGraphicDefault.mat",
        "runtime_shader_name": "Spine/SkeletonGraphic",
    },
    {
        "source_guid": "bc4aa6bca656c6941a7fbade26616b84",
        "source_name": "SkeletonGraphicDye",
        "source_shader_guid": "b638668a558083840b23430c53789454",
        "shader_name": "Spine/SkeletonGraphicDye",
        "runtime_name": "SkeletonGraphicDye",
        "runtime_path": RESOURCES / "Materials/Spine/SkeletonGraphicDye.mat",
        "runtime_shader_name": "Spine/SkeletonGraphicDye",
    },
)
DISTORT_SHADER_PROGRAMS = {
    2: ((), "3c15c5d39f0cd899beea1b090956f54dda557606f58709c4d7a55abb8077d13d"),
    3: (("UNITY_UI_ALPHACLIP",), "4f8fb703a4482919e4a829c4dd2b0fd3b90250c870523f51777f94d35a1055f4"),
    4: (("UNITY_UI_CLIP_RECT",), "4226e73f8af1b7cbfdc54589746a56990fe6892c648e10c5d659bd3ab2b77b2e"),
    5: (("UNITY_UI_ALPHACLIP", "UNITY_UI_CLIP_RECT"),
        "43f1e4c3eff6d2db56f19b1e040820833e9c04a178b27cc19730499fd763af43"),
}
SCRIPT_GUID_MAPPINGS = {
    "18d0a90695249463551c00f45766e642": "4e29b1a8efbd4b44bb3f3716e73f07ff",  # Button
    "16c0ec1e295995dd9a2d2df2d7b6cc7e": "31a19414c41e5ae4aae2af33fee712f6",  # Mask
    "19fbb4a32b59286cd89b624f52ff7943": "30649d3a9faa99c48a7b1166b86bf2a0",  # HorizontalLayoutGroup
    "21c7954052da7655d96bff866c2b7662": "3245ec927659c4140ac4f8d17403cc18",  # ContentSizeFitter
    "4293fd42559bc8bb1c81715179adf536": "306cc8c2b49d7114eaa3623786fc2126",  # LayoutElement
    "f18f2cc2fe71a3a6d76a570588d5047c": "59f8146938fff824cb5fd77236b75775",  # VerticalLayoutGroup
    "3cf5a44414476512e00c3e7a2569a919": "fe87c0e1cc204ed48ad3b37840f39efc",  # Image
    "3f96b1d166d19b209697e35b35d65c76": "f4688fdb7df04437aeb418b961361dc5",  # TextMeshProUGUI
    "86fe8f3fc59dc06ea6b45a1bbee64682": "dc42784cf147c0c48a680349fa168899",  # GraphicRaycaster
    "69dacd7a039c12e90cde90bbae65247a": "3312d7739989d2b4e91e6319e9a96d76",  # RectMask2D
    "76ccfb4bfe5eebba5766d6f50f76ae37": "cfabb0440166ab443bba8876756fdfa9",  # Shadow
    "c0467acf10a7d1a8e81912a9a7988750": "1aa08ab6e0800fa44ae55d278d1423e3",  # ScrollRect
}


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def guid(path: Path) -> str | None:
    match = META_GUID.search(path.read_text(encoding="utf-8", errors="replace"))
    return match.group(1) if match else None


def asset_guid_index(source_manifest_sha256: str) -> dict[str, Path]:
    if GUID_INDEX_CACHE.is_file():
        cached = json.loads(GUID_INDEX_CACHE.read_text(encoding="utf-8"))
        if (cached.get("version") == 1
                and cached.get("source_manifest_sha256") == source_manifest_sha256
                and isinstance(cached.get("assets"), dict)):
            return {asset_guid: EXPORT / Path(relative)
                    for asset_guid, relative in cached["assets"].items()}

    indexed: dict[str, Path] = {}
    rg = shutil.which("rg")
    if rg:
        process = subprocess.Popen(
            [rg, "--hidden", "--no-ignore", "--no-heading",
             "--glob", "*.meta", r"^(guid: [0-9a-f]{32})$", str(EXPORT)],
            cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        if process.stdout is None or process.stderr is None:
            process.kill()
            raise RuntimeError("Could not read official Unity metadata index output")
        for line in process.stdout:
            line = line.rstrip(b"\r\n")
            path_bytes, separator, asset_guid_bytes = line.rpartition(b":guid: ")
            if not separator or not re.fullmatch(rb"[0-9a-f]{32}", asset_guid_bytes):
                continue
            meta = Path(os.fsdecode(path_bytes))
            if not meta.is_absolute():
                meta = ROOT / meta
            asset = meta.with_suffix("")
            asset_guid = asset_guid_bytes.decode("ascii")
            if asset_guid in indexed:
                raise ValueError("Duplicate AssetRipper GUID " + asset_guid)
            indexed[asset_guid] = asset
        stderr = process.stderr.read()
        returncode = process.wait()
        if returncode not in (0, 1):
            raise RuntimeError("Could not index official Unity metadata: "
                               + stderr.decode("utf-8", "replace"))
    else:
        for meta in EXPORT.rglob("*.meta"):
            asset = meta.with_suffix("")
            asset_guid = guid(meta)
            if asset_guid and asset.is_file():
                if asset_guid in indexed:
                    raise ValueError("Duplicate AssetRipper GUID " + asset_guid)
                indexed[asset_guid] = asset

    GUID_INDEX_CACHE.parent.mkdir(parents=True, exist_ok=True)
    GUID_INDEX_CACHE.write_text(json.dumps({
        "version": 1,
        "source_manifest_sha256": source_manifest_sha256,
        "assets": {asset_guid: path.relative_to(EXPORT).as_posix()
                   for asset_guid, path in indexed.items()},
    }, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    return indexed


def convert_runtime_prefab(source: Path, guid_remapping: dict[str, str] | None = None) -> bytes:
    return convert_runtime_prefab_text(source.read_text(encoding="utf-8"), guid_remapping)


def convert_runtime_prefab_text(value: str, guid_remapping: dict[str, str] | None = None) -> bytes:
    for source_guid, runtime_guid in SCRIPT_GUID_MAPPINGS.items():
        value = value.replace("guid: " + source_guid + ", type: 3",
                              "guid: " + runtime_guid + ", type: 3")
    for source_guid, runtime_guid in (guid_remapping or {}).items():
        value = value.replace("guid: " + source_guid, "guid: " + runtime_guid)
    launch_guid = (guid_remapping or {}).get(LAUNCH_DLL_GUID, LAUNCH_DLL_GUID)
    uimesh_reference = re.compile(
        r"(m_Script: \{fileID: )" + str(UIMESH_SOURCE_FILE_ID)
        + r"(, guid: )" + re.escape(launch_guid) + r"(, type: 3\})")
    value = uimesh_reference.sub(
        lambda match: match.group(1) + "11500000" + match.group(2)
            + UIMESH_SCRIPT_GUID + match.group(3), value)
    return value.encode("utf-8")


def remap_guid_references(raw: bytes, guid_remapping: dict[str, str]) -> bytes:
    if not guid_remapping:
        return raw
    try:
        value = raw.decode("utf-8")
    except UnicodeDecodeError:
        return raw
    return GUID.sub(lambda match: "guid: " + guid_remapping.get(match.group(1), match.group(1)), value).encode("utf-8")


def official_dependency_path(relative: Path) -> Path:
    # Unity checks plugin filenames against asmdef names even when every
    # PluginImporter platform is disabled. Keep the APK bytes and GUID, while
    # giving these source assemblies a filename distinct from migrated code.
    if relative.suffix.lower() == ".dll":
        return relative.with_name("ApkSource." + relative.name)
    return relative


def disable_apk_plugin_import(meta_content: bytes) -> bytes:
    lines = meta_content.splitlines(keepends=True)
    plugin_index = next((index for index, line in enumerate(lines)
                         if line.rstrip(b"\r\n") == b"PluginImporter:"), None)
    if plugin_index is None:
        raise ValueError("An APK DLL has no PluginImporter metadata")
    platform_index = next((index for index in range(plugin_index + 1, len(lines))
                           if lines[index].rstrip(b"\r\n") == b"  platformData:"), None)
    if platform_index is None:
        raise ValueError("An APK DLL has no PluginImporter platform data")

    disabled = 0
    for index in range(platform_index + 1, len(lines)):
        line = lines[index]
        if re.match(rb"^  [A-Za-z][A-Za-z0-9_]*:[ \t]*(?:\r?\n)?$", line):
            break
        match = re.match(rb"^([ \t]+enabled:[ \t]*)[01]([ \t]*\r?\n?)$", line)
        if match:
            lines[index] = match.group(1) + b"0" + match.group(2)
            disabled += 1
    if disabled == 0:
        raise ValueError("An APK DLL has no recognized PluginImporter platform entries")
    return b"".join(lines)


def unity_folder_meta(path: Path) -> None:
    meta = path.with_name(path.name + ".meta")
    if meta.exists():
        return
    relative = path.relative_to(ROOT / "unity/Assets").as_posix()
    value = hashlib.sha256(("zjcs-dungeon-ui-folder:" + relative).encode()).hexdigest()[:32]
    meta.write_text("fileFormatVersion: 2\n"
                    f"guid: {value}\n"
                    "folderAsset: yes\nDefaultImporter:\n"
                    "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",
                    encoding="utf-8")


def prepare_distort_runtime_binding(apk, shader_objects: list | None = None,
                                    material_specs=DISTORT_MATERIALS,
                                    source_root=OFFICIAL) -> tuple[dict, list[dict], dict[str, str]]:
    """Bind the current APK's UI/EffectDistort to its verified Windows conversion."""
    if not WINDOWS_SHADER_RECEIPT.is_file():
        raise FileNotFoundError("Current Windows shader receipt is missing: " + str(WINDOWS_SHADER_RECEIPT))
    shader_receipt = json.loads(WINDOWS_SHADER_RECEIPT.read_text(encoding="utf-8"))
    if shader_receipt.get("manifest") != VERSION:
        raise ValueError("Current Windows shader receipt is not from the active official manifest")
    if not CURRENT_WINDOWS_RECEIPT.is_file():
        raise FileNotFoundError("Current Windows conversion reference receipt is missing: " + str(CURRENT_WINDOWS_RECEIPT))
    current_receipt = json.loads(CURRENT_WINDOWS_RECEIPT.read_text(encoding="utf-8"))
    shader_receipt_pin = current_receipt.get("shaders", {}).get("receipt", {})
    if (current_receipt.get("manifest") != VERSION
            or shader_receipt_pin.get("path") != WINDOWS_SHADER_RECEIPT.relative_to(ROOT).as_posix()
            or shader_receipt_pin.get("sha256") != digest(WINDOWS_SHADER_RECEIPT)):
        raise ValueError("Current Windows shader conversion is not pinned to the active receipt")
    rows = [row for row in shader_receipt.get("shaders", [])
            if row.get("source", {}).get("name") == "UI/EffectDistort"
            and row.get("source", {}).get("object_sha256") == DISTORT_SHADER_SOURCE_SHA256
            and row.get("source", {}).get("input") == "inputs/DefaultPackage/defaultpackage_unityshaders.bundle"]
    if len(rows) != 1:
        raise ValueError("The active Windows UI/EffectDistort conversion is missing or ambiguous")
    row = rows[0]
    programs = {int(program["index"]): (tuple(program.get("keywords", [])), program.get("sha256"))
                for program in row.get("programs", [])}
    if programs != DISTORT_SHADER_PROGRAMS:
        raise ValueError("Current Windows UI/EffectDistort GLES program proof changed")
    if apk.version != VERSION:
        raise ValueError("Original daily dungeon shader lookup is not using the active APK")
    shader_objects = shader_objects or [obj for obj in apk.objects("defaultpackage_unityshaders.bundle")
                                        if obj.type.name == "Shader"]
    matching_shaders = [obj for obj in shader_objects if str(obj.path_id) == "-1287703601585291557"]
    if len(matching_shaders) != 1:
        raise ValueError("The active APK UI/EffectDistort object is missing or ambiguous")
    original_shader = matching_shaders[0]
    if (original_shader.read().m_ParsedForm.m_Name != "UI/EffectDistort"
            or hashlib.sha256(original_shader.get_raw_data()).hexdigest() != DISTORT_SHADER_SOURCE_SHA256):
        raise ValueError("The active APK UI/EffectDistort object differs from its Windows conversion")
    shader_provenance = apk.provenance(original_shader)
    if (shader_provenance.get("origin") != "apk_zip"
            or shader_provenance.get("manifest") != VERSION
            or shader_provenance.get("apk_sha256") != apk.source.apk_sha256
            or shader_provenance.get("bundle") != "defaultpackage_unityshaders.bundle"):
        raise ValueError("The active UI/EffectDistort payload is not from the pinned APK")
    cache_shader = WINDOWS_SHADER_ROOT / row["path"]
    cache_meta = Path(str(cache_shader) + ".meta")
    if not cache_shader.is_file() or not cache_meta.is_file():
        raise FileNotFoundError("Current Windows UI/EffectDistort output is incomplete")
    if digest(cache_shader) != row.get("sha256") or digest(cache_meta) != row.get("meta_sha256"):
        raise ValueError("Current Windows UI/EffectDistort conversion changed")
    shader_text = cache_shader.read_text(encoding="utf-8")
    shader_header = 'Shader "UI/EffectDistort" {'
    if shader_text.count(shader_header) != 1:
        raise ValueError("Current Windows UI/EffectDistort shader header changed")
    runtime_shader_text = shader_text.replace(
        shader_header, 'Shader "' + DISTORT_SHADER_NAME + '" {', 1).encode("utf-8")
    unity_folder_meta(DISTORT_SHADER_RUNTIME.parent)
    shader_meta = ("fileFormatVersion: 2\n"
                   "guid: " + DISTORT_SHADER_RUNTIME_GUID + "\n"
                   "ShaderImporter:\n"
                   "  externalObjects: {}\n"
                   "  defaultTextures: []\n"
                   "  nonModifiableTextures: []\n"
                   "  userData:\n"
                   "  assetBundleName:\n"
                   "  assetBundleVariant:\n").encode("utf-8")
    if DISTORT_SHADER_RUNTIME.exists() and DISTORT_SHADER_RUNTIME.read_bytes() != runtime_shader_text:
        raise ValueError("Refusing to overwrite changed runtime UI/EffectDistort shader")
    shader_meta_path = Path(str(DISTORT_SHADER_RUNTIME) + ".meta")
    if shader_meta_path.exists() and shader_meta_path.read_bytes() != shader_meta:
        raise ValueError("Refusing to overwrite changed runtime UI/EffectDistort metadata")
    DISTORT_SHADER_RUNTIME.parent.mkdir(parents=True, exist_ok=True)
    if not DISTORT_SHADER_RUNTIME.exists():
        DISTORT_SHADER_RUNTIME.write_bytes(runtime_shader_text)
    if not shader_meta_path.exists():
        shader_meta_path.write_bytes(shader_meta)

    material_bindings = []
    material_guid_remapping = {}
    source_shader_ref = ("m_Shader: {fileID: 4800000, guid: "
                         + DISTORT_MATERIAL_SHADER_GUID + ", type: 2}")
    for material_name, source_material_guid in material_specs:
        source_material = source_root / "UIEffect/Material" / (material_name + ".mat")
        runtime_material = RESOURCES / "Materials" / (material_name + ".mat")
        runtime_material_guid = uuid.uuid5(
            uuid.NAMESPACE_URL, "zjcs/dungeons/ui/" + VERSION + "/material/" + material_name).hex
        if not source_material.is_file():
            raise FileNotFoundError("Official " + material_name + " material is missing: " + str(source_material))
        if guid(source_material.with_name(source_material.name + ".meta")) != source_material_guid:
            raise ValueError("Official " + material_name + " material GUID changed")
        material_text = source_material.read_text(encoding="utf-8")
        if material_text.count(source_shader_ref) != 1:
            raise ValueError("Official " + material_name + " shader binding changed")
        runtime_material_text = material_text.replace(
            source_shader_ref,
            "m_Shader: {fileID: 4800000, guid: " + DISTORT_SHADER_RUNTIME_GUID + ", type: 3}", 1)
        prior_material_bytes = runtime_material_text.encode("utf-8")
        if material_name == "CommonJieSuanDown":
            # AssetRipper emits maps here; Unity Material expects ordered pair arrays.
            # Preserve every original key, value and texture reference verbatim.
            for field in ("m_TexEnvs", "m_Ints", "m_Floats", "m_Colors"):
                pattern = r"(?m)^(    " + field + r":)(.*)(\n(?: {6,}[^\n]*\n)*)"
                def pairs(match):
                    tail = match.group(2).strip()
                    if tail == "{}":
                        return match.group(1) + " []" + match.group(3)
                    if tail:
                        raise ValueError("Unsupported material property map: " + field)
                    return match.group(1) + "\n" + re.sub(r"(?m)^      (\S[^\n]*:)", r"    - \1", match.group(3)[1:])
                runtime_material_text, count = re.subn(pattern, pairs, runtime_material_text)
                if count != 1:
                    raise ValueError("Original material property section is missing: " + field)
        runtime_material_text = runtime_material_text.encode("utf-8")
        unity_folder_meta(runtime_material.parent)
        material_meta = ("fileFormatVersion: 2\n"
                         "guid: " + runtime_material_guid + "\n"
                         "NativeFormatImporter:\n"
                         "  externalObjects: {}\n"
                         "  mainObjectFileID: 2100000\n"
                         "  userData:\n"
                         "  assetBundleName:\n"
                         "  assetBundleVariant:\n").encode("utf-8")
        allowed_materials = {runtime_material_text}
        if material_name == "CommonJieSuanDown":
            allowed_materials.add(prior_material_bytes)
        if runtime_material.exists() and runtime_material.read_bytes() not in allowed_materials:
            raise ValueError("Refusing to overwrite changed runtime " + material_name + " material")
        material_meta_path = Path(str(runtime_material) + ".meta")
        if material_meta_path.exists() and material_meta_path.read_bytes() != material_meta:
            raise ValueError("Refusing to overwrite changed runtime " + material_name + " metadata")
        runtime_material.parent.mkdir(parents=True, exist_ok=True)
        if not runtime_material.exists() or runtime_material.read_bytes() != runtime_material_text:
            runtime_material.write_bytes(runtime_material_text)
        if not material_meta_path.exists():
            material_meta_path.write_bytes(material_meta)

        material_bindings.append({
            "path": runtime_material.relative_to(ROOT).as_posix(),
            "guid": runtime_material_guid,
            "source_path": source_material.relative_to(ROOT).as_posix(),
            "source_guid": source_material_guid,
            "source_sha256": digest(source_material),
            "sha256": digest(runtime_material),
            "meta_sha256": digest(material_meta_path),
            "shader_guid": DISTORT_SHADER_RUNTIME_GUID,
        })
        material_guid_remapping[source_material_guid] = runtime_material_guid

    shader_binding = {
        "path": DISTORT_SHADER_RUNTIME.relative_to(ROOT).as_posix(),
        "guid": DISTORT_SHADER_RUNTIME_GUID,
        "name": DISTORT_SHADER_NAME,
        "source": {
            "manifest": VERSION,
            "bundle": "defaultpackage_unityshaders.bundle",
            "object_path_id": row["source"].get("path_id"),
            "object_sha256": row["source"]["object_sha256"],
            "apk_provenance": shader_provenance,
            "conversion_path": (WINDOWS_SHADER_ROOT / row["path"]).relative_to(ROOT).as_posix(),
            "conversion_sha256": row["sha256"],
            "conversion_meta_sha256": row["meta_sha256"],
            "programs": row["programs"],
        },
        "sha256": digest(DISTORT_SHADER_RUNTIME),
        "meta_sha256": digest(shader_meta_path),
    }
    return shader_binding, material_bindings, material_guid_remapping


def prepare_runtime_material_bindings(apk, by_guid: dict[str, Path], export_outputs: dict[str, dict],
                                      shader_objects: list | None = None) -> tuple[list[dict], list[dict], dict[str, str]]:
    """Bind UI and Spine materials to verified Windows shaders for runtime prefabs."""
    if apk.version != VERSION:
        raise ValueError("Runtime material shader lookup is not using the active APK")
    shader_receipt = json.loads(WINDOWS_SHADER_RECEIPT.read_text(encoding="utf-8"))
    if shader_receipt.get("manifest") != VERSION:
        raise ValueError("Current Windows shader receipt is not from the active official manifest")
    current_receipt = json.loads(CURRENT_WINDOWS_RECEIPT.read_text(encoding="utf-8"))
    shader_receipt_pin = current_receipt.get("shaders", {}).get("receipt", {})
    if (current_receipt.get("manifest") != VERSION
            or shader_receipt_pin.get("path") != WINDOWS_SHADER_RECEIPT.relative_to(ROOT).as_posix()
            or shader_receipt_pin.get("sha256") != digest(WINDOWS_SHADER_RECEIPT)):
        raise ValueError("Current Windows shader conversion is not pinned to the active receipt")

    bundle = "defaultpackage_unityshaders.bundle"
    shader_input = "inputs/DefaultPackage/" + bundle
    shader_objects = shader_objects or [obj for obj in apk.objects(bundle) if obj.type.name == "Shader"]
    shader_bindings = []
    runtime_shader_guids = {}
    expected_shaders = [
        {
            "source_name": "UI/EffectAdditive",
            "path_id": "-6192161486129426018",
            "object_sha256": "7f3d9e14a1c8f979d6d85d03dda0de143a7dedf950c81215479cc36daa8ba1dc",
            "runtime_name": "UI/EffectAdditive",
            "runtime_path": ADDITIVE_SHADER_RUNTIME,
            "runtime_guid": ADDITIVE_SHADER_RUNTIME_GUID,
        },
        *RUNTIME_SPINE_SHADERS,
    ]
    for spec in expected_shaders:
        rows = [row for row in shader_receipt.get("shaders", [])
                if row.get("source", {}).get("name") == spec["source_name"]
                and row.get("source", {}).get("path_id") == spec["path_id"]
                and row.get("source", {}).get("object_sha256") == spec["object_sha256"]
                and row.get("source", {}).get("input") == shader_input]
        if len(rows) != 1:
            raise ValueError("The active Windows " + spec["source_name"] + " conversion is missing or ambiguous")
        row = rows[0]
        original = [obj for obj in shader_objects if str(obj.path_id) == spec["path_id"]]
        if len(original) != 1:
            raise ValueError("The active APK " + spec["source_name"] + " object is missing or ambiguous")
        source_shader = original[0]
        if (source_shader.read().m_ParsedForm.m_Name != spec["source_name"]
                or hashlib.sha256(source_shader.get_raw_data()).hexdigest() != spec["object_sha256"]):
            raise ValueError("The active APK " + spec["source_name"] + " object differs from its Windows conversion")
        provenance = apk.provenance(source_shader)
        if (provenance.get("origin") != "apk_zip"
                or provenance.get("manifest") != VERSION
                or provenance.get("apk_sha256") != apk.source.apk_sha256
                or provenance.get("bundle") != bundle):
            raise ValueError("The active " + spec["source_name"] + " shader is not from the pinned APK")

        converted = WINDOWS_SHADER_ROOT / row["path"]
        converted_meta = Path(str(converted) + ".meta")
        if (not converted.is_file() or not converted_meta.is_file()
                or digest(converted) != row.get("sha256")
                or digest(converted_meta) != row.get("meta_sha256")):
            raise ValueError("Current Windows " + spec["source_name"] + " conversion changed")
        for program in row.get("programs", []):
            program_path = WINDOWS_SHADER_ROOT / program["path"]
            if not program_path.is_file() or digest(program_path) != program.get("sha256"):
                raise ValueError("Current Windows " + spec["source_name"] + " program changed")

        runtime_path = spec["runtime_path"]
        runtime_guid = spec["runtime_guid"] if "runtime_guid" in spec else uuid.uuid5(
            uuid.NAMESPACE_URL, "zjcs/dungeons/ui/" + VERSION + "/shader/" + spec["source_name"]).hex
        converted_text = converted.read_text(encoding="utf-8")
        if spec["source_name"] == "UI/EffectAdditive":
            runtime_text = converted_text
        else:
            source_header = 'Shader "' + spec["source_name"] + '" {'
            if converted_text.count(source_header) != 1:
                raise ValueError("Current Windows " + spec["source_name"] + " shader header changed")
            runtime_text = converted_text.replace(
                source_header, 'Shader "' + spec["runtime_name"] + '" {', 1)
        runtime_bytes = runtime_text.encode("utf-8")
        unity_folder_meta(runtime_path.parent)
        shader_meta = ("fileFormatVersion: 2\n"
                       "guid: " + runtime_guid + "\n"
                       "ShaderImporter:\n"
                       "  externalObjects: {}\n").encode("utf-8")
        if spec["source_name"] != "UI/EffectAdditive":
            shader_meta = ("fileFormatVersion: 2\n"
                           "guid: " + runtime_guid + "\n"
                           "ShaderImporter:\n"
                           "  externalObjects: {}\n"
                           "  defaultTextures: []\n"
                           "  nonModifiableTextures: []\n"
                           "  userData:\n"
                           "  assetBundleName:\n"
                           "  assetBundleVariant:\n").encode("utf-8")
        runtime_meta_path = Path(str(runtime_path) + ".meta")
        if runtime_path.exists() and runtime_path.read_bytes() != runtime_bytes:
            raise ValueError("Refusing to overwrite changed runtime " + spec["source_name"] + " shader")
        if runtime_meta_path.exists():
            existing_meta = runtime_meta_path.read_bytes()
            if existing_meta.replace(b"\r\n", b"\n") != shader_meta:
                raise ValueError("Refusing to overwrite changed runtime " + spec["source_name"] + " metadata")
            shader_meta = existing_meta
        runtime_path.parent.mkdir(parents=True, exist_ok=True)
        if not runtime_path.exists():
            runtime_path.write_bytes(runtime_bytes)
        if not runtime_meta_path.exists():
            runtime_meta_path.write_bytes(shader_meta)

        runtime_shader_guids[spec["source_name"]] = runtime_guid
        shader_bindings.append({
            "path": runtime_path.relative_to(ROOT).as_posix(),
            "guid": runtime_guid,
            "name": spec["runtime_name"],
            "source": {
                "manifest": VERSION,
                "bundle": bundle,
                "object_path_id": spec["path_id"],
                "object_sha256": spec["object_sha256"],
                "apk_provenance": provenance,
                "conversion_receipt": WINDOWS_SHADER_RECEIPT.relative_to(ROOT).as_posix(),
                "conversion_receipt_sha256": digest(WINDOWS_SHADER_RECEIPT),
                "conversion_path": converted.relative_to(ROOT).as_posix(),
                "conversion_sha256": row["sha256"],
                "conversion_meta_sha256": row["meta_sha256"],
                "programs": row.get("programs", []),
            },
            "sha256": digest(runtime_path),
            "meta_sha256": digest(runtime_meta_path),
        })

    material_bindings = []
    material_guid_remapping = {}
    for spec in RUNTIME_UI_MATERIALS:
        source_material = by_guid.get(spec["source_guid"])
        if source_material is None or not source_material.is_file():
            raise FileNotFoundError("Official " + spec["source_name"] + " material is missing from the verified export")
        source_meta = source_material.with_name(source_material.name + ".meta")
        if guid(source_meta) != spec["source_guid"]:
            raise ValueError("Official " + spec["source_name"] + " material GUID changed")
        relative = source_material.relative_to(EXPORT)
        export_row = export_outputs.get((Path("ExportedProject/Assets") / relative).as_posix())
        if export_row is None or digest(source_material) != export_row.get("sha256"):
            raise ValueError("Official " + spec["source_name"] + " material differs from its verified export")
        material_text = source_material.read_text(encoding="utf-8-sig")
        name_match = re.search(r"(?m)^  m_Name: ([^\r\n]+)$", material_text)
        if name_match is None or name_match.group(1) != spec["source_name"]:
            raise ValueError("Official " + spec["source_name"] + " material name changed")
        shader_guid = runtime_shader_guids.get(spec["runtime_shader_name"])
        if shader_guid is None:
            raise ValueError("Runtime shader binding is missing for " + spec["runtime_shader_name"])
        source_shader_ref = re.compile(
            r"(?m)^(  m_Shader: \{fileID: 4800000, guid: )"
            + re.escape(spec["source_shader_guid"]) + r"(, type: )[0-9]+(\})$")
        runtime_material_text, count = source_shader_ref.subn(
            lambda match: match.group(1) + shader_guid + match.group(2) + "3" + match.group(3),
            material_text)
        if count != 1:
            raise ValueError("Official " + spec["source_name"] + " shader binding changed")
        runtime_material = spec["runtime_path"]
        runtime_guid = uuid.uuid5(uuid.NAMESPACE_URL,
            "zjcs/dungeons/ui/" + VERSION + "/material/" + spec["runtime_name"]).hex
        material_meta = ("fileFormatVersion: 2\n"
                         "guid: " + runtime_guid + "\n"
                         "NativeFormatImporter:\n"
                         "  externalObjects: {}\n"
                         "  mainObjectFileID: 2100000\n"
                         "  userData:\n"
                         "  assetBundleName:\n"
                         "  assetBundleVariant:\n").encode("utf-8")
        runtime_meta_path = Path(str(runtime_material) + ".meta")
        unity_folder_meta(runtime_material.parent)
        runtime_bytes = runtime_material_text.encode("utf-8")
        if runtime_material.exists() and runtime_material.read_bytes() != runtime_bytes:
            raise ValueError("Refusing to overwrite changed runtime " + spec["runtime_name"] + " material")
        if runtime_meta_path.exists() and runtime_meta_path.read_bytes() != material_meta:
            raise ValueError("Refusing to overwrite changed runtime " + spec["runtime_name"] + " metadata")
        runtime_material.parent.mkdir(parents=True, exist_ok=True)
        if not runtime_material.exists():
            runtime_material.write_bytes(runtime_bytes)
        if not runtime_meta_path.exists():
            runtime_meta_path.write_bytes(material_meta)
        material_bindings.append({
            "path": runtime_material.relative_to(ROOT).as_posix(),
            "guid": runtime_guid,
            "source_path": source_material.relative_to(ROOT).as_posix(),
            "source_guid": spec["source_guid"],
            "source_sha256": digest(source_material),
            "source_meta_sha256": digest(source_meta),
            "source_shader_guid": spec["source_shader_guid"],
            "shader_name": spec["runtime_shader_name"],
            "shader_guid": shader_guid,
            "sha256": digest(runtime_material),
            "meta_sha256": digest(runtime_meta_path),
        })
        material_guid_remapping[spec["source_guid"]] = runtime_guid

    return shader_bindings, material_bindings, material_guid_remapping


def build(npc_adventure_only: bool = False) -> dict:
    source_manifest = json.loads(INPUTS.read_text(encoding="utf-8"))
    if source_manifest.get("manifest") != VERSION:
        raise ValueError("Dungeon UI inputs are not from the active official manifest")
    if not EXPORT.is_dir():
        raise FileNotFoundError("AssetRipper Unity export is missing: " + str(EXPORT))
    bundle_sources = json.loads(BUNDLE_SOURCES.read_text(encoding="utf-8"))
    prior_native_outputs = {}
    prior_runtime_prefabs = {}
    prior_material_remapping = {}
    for prior_receipt in (PRIOR_NATIVE_RECEIPT, RECEIPT, NPC_ADVENTURE_RECEIPT):
        if not prior_receipt.is_file():
            continue
        prior_native = json.loads(prior_receipt.read_text(encoding="utf-8"))
        if prior_native.get("manifest") == VERSION:
            prior_native_outputs.update({row.get("path"): row for row in prior_native.get("outputs", [])})
            prior_runtime_prefabs.update({row.get("path"): row
                                          for row in prior_native.get("runtime_prefabs", [])})
            for row in prior_native.get("runtime_material_bindings", []):
                material = ROOT / row["path"]
                if material.is_file() and digest(material) == row.get("sha256"):
                    prior_material_remapping[row["source_guid"]] = row["guid"]
    requested_bundles = [
        "defaultpackage_assets_prefabs_ui_dailydungeon.bundle",
        "defaultpackage_assets_prefabs_ui_arena.bundle",
        "defaultpackage_assets_prefabs_ui_top.bundle",
        "defaultpackage_assets_prefabs_ui_home.bundle",
        "defaultpackage_assets_prefabs_ui_item.bundle",
        "defaultpackage_assets_prefabs_ui_itemdetail.bundle",
        "defaultpackage_assets_image_ui_itemdetaildialog.bundle",
        "defaultpackage_assets_prefabs_ui_npcadventure.bundle",
        "defaultpackage_assets_prefabs_ui_fight_gameendpanelnpcadventure.bundle",
        "defaultpackage_assets_scriptableconfig_modules_itemicon.bundle",
        DYNAMIC_FONT_ATLAS_BUNDLE,
        "defaultpackage_unityshaders.bundle",
    ]
    if npc_adventure_only:
        requested_bundles = [
            "defaultpackage_assets_prefabs_ui_npcadventure.bundle",
            "defaultpackage_assets_prefabs_ui_fight_gameendpanelnpcadventure.bundle",
            DYNAMIC_FONT_ATLAS_BUNDLE,
            "defaultpackage_unityshaders.bundle",
        ]

    by_guid = asset_guid_index(digest(INPUTS))

    roots = set()
    roots.add(EXPORT / "Prefabs/UI/NpcAdventure/NpcAdventureSelectNpcDialog.prefab")
    roots.add(EXPORT / "Prefabs/UI/NpcAdventure/NpcAdventureSelectNpcItem.prefab")
    roots.add(EXPORT / "Prefabs/UI/NpcAdventure/NpcAdventureNodeRewardItem.prefab")
    roots.add(EXPORT / "Prefabs/UI/Fight/GameEndPanelNpcAdventure.prefab")
    if not npc_adventure_only:
        roots.update((EXPORT / "Prefabs/UI/DailyDungeon").glob("*.prefab"))
        roots.update((EXPORT / "Prefabs/UI/DailyDungeon").glob("*.anim"))
        roots.update((EXPORT / "Prefabs/UI/Arena").glob("*.prefab"))
        roots.update((EXPORT / "Prefabs/UI/Top").glob("Arena*.prefab"))
        roots.update((EXPORT / "Prefabs/UI/Team").glob("TeamInvitePanel*.prefab"))
        roots.update(EXPORT / "Prefabs/UI/ItemDetail" / name for name in (
            "EquipDetailDialog.prefab",
            "CommonEquipPieceUseDetailDialog.prefab",
            "CommonItemPieceUseDetailDialog.prefab",
            "ItemDetailDialog.prefab",
            "ItemUseDetailDialog.prefab",
        ))
        if TRAVEL_SOURCE.is_file():
            roots.add(TRAVEL_SOURCE)
        if not ITEM_ICON_ATTACHMENT_CONFIG.is_file() or not PROBABILITY_TIP_SOURCE.is_file():
            raise FileNotFoundError("The official ItemIcon ProbabilityTip source is missing")
        attachment_config_text = ITEM_ICON_ATTACHMENT_CONFIG.read_text(encoding="utf-8")
        attachment_match = re.search(
            r"(?m)^  - Type: 7\n    GuidProxy:\n      mAssetGuid: ([0-9a-f]{32})$",
            attachment_config_text)
        if attachment_match is None or attachment_match.group(1) != PROBABILITY_TIP_ASSET_GUID:
            raise ValueError("Official ProbabilityTip config differs from the verified attachment prefab")
        roots.add(PROBABILITY_TIP_SOURCE)
    roots = {path for path in roots if path.is_file()}
    if not npc_adventure_only and len(roots) < 30:
        raise ValueError("The official dungeon/arena prefab set is incomplete")
    team_bundle = "defaultpackage_assets_prefabs_ui_team.bundle"
    if any(path.parent.name == "Team" for path in roots):
        requested_bundles.append(team_bundle)
    bundle_inputs = []
    for bundle in requested_bundles:
        record = bundle_sources.get(bundle)
        path = EXTRACTED / bundle
        if record is None or record.get("manifest") != VERSION or not path.is_file():
            raise ValueError("The official UI bundle is not verified for " + VERSION + ": " + bundle)
        bundle_hash = digest(path)
        if bundle_hash != record.get("bundle_sha256"):
            raise ValueError("The official UI bundle hash changed: " + bundle)
        bundle_inputs.append(dict(name=bundle, path=path.relative_to(ROOT).as_posix(),
                                  manifest=VERSION, sha256=bundle_hash))

    uimesh_source = UIMESH_CODE_SOURCE / "UIMesh.cs"
    uimesh_utils_source = UIMESH_CODE_SOURCE / "UIMeshUtils.cs"
    uimesh_runtime = UIMESH_CODE_RUNTIME / "UIMesh.cs"
    uimesh_utils_runtime = UIMESH_CODE_RUNTIME / "UIMeshUtils.cs"
    uimesh_meta = uimesh_runtime.with_name(uimesh_runtime.name + ".meta")
    if not all(path.is_file() for path in
               (uimesh_source, uimesh_utils_source, uimesh_runtime, uimesh_utils_runtime, uimesh_meta)):
        raise FileNotFoundError("The official UIMesh source migration is incomplete")
    if guid(uimesh_meta) != UIMESH_SCRIPT_GUID:
        raise ValueError("The official UIMesh runtime script GUID changed")
    runtime_script_bindings = [{
        "type": "UIMesh",
        "source_assembly": "Launch.dll",
        "source_assembly_guid": LAUNCH_DLL_GUID,
        "source_file_id": UIMESH_SOURCE_FILE_ID,
        "source_code": uimesh_source.relative_to(ROOT).as_posix(),
        "source_code_sha256": digest(uimesh_source),
        "runtime_code": uimesh_runtime.relative_to(ROOT).as_posix(),
        "runtime_code_sha256": digest(uimesh_runtime),
        "runtime_script_guid": UIMESH_SCRIPT_GUID,
        "runtime_script_meta_sha256": digest(uimesh_meta),
        "serialized_fields": ["mMeshRenderer", "Mesh"],
        "utility_source": uimesh_utils_source.relative_to(ROOT).as_posix(),
        "utility_source_sha256": digest(uimesh_utils_source),
        "utility_runtime": uimesh_utils_runtime.relative_to(ROOT).as_posix(),
        "utility_runtime_sha256": digest(uimesh_utils_runtime),
    }]

    export_outputs = {row["path"].replace("\\", "/"): row
                      for row in source_manifest.get("outputs", [])}

    font_source = bundle_sources.get(DYNAMIC_FONT_ATLAS_BUNDLE)
    font_bundle_path = EXTRACTED / DYNAMIC_FONT_ATLAS_BUNDLE
    if font_source is None or font_source.get("manifest") != VERSION or not font_bundle_path.is_file():
        raise ValueError("The official dynamic TMP font bundle is not available for " + VERSION)
    font_bundle_sha256 = digest(font_bundle_path)
    if font_bundle_sha256 != font_source.get("bundle_sha256"):
        raise ValueError("The official dynamic TMP font bundle hash changed")
    from official_apk import Apk
    apk = Apk.current()
    font_atlases = [obj for obj in apk.objects(DYNAMIC_FONT_ATLAS_BUNDLE)
                    if obj.type.name == "Texture2D" and obj.peek_name() == "GenJyuuGothic-P-Medium SDF Atlas"]
    if len(font_atlases) != 1:
        raise ValueError("The official dynamic TMP atlas object is missing or ambiguous")
    font_atlas = font_atlases[0]
    font_atlas_data = font_atlas.read()
    if (font_atlas_data.m_Width != 0 or font_atlas_data.m_Height != 0
            or font_atlas_data.m_CompleteImageSize != 0 or font_atlas_data.image_data
            or font_atlas_data.m_StreamData.size != 0):
        raise ValueError("The TMP atlas is no longer an empty runtime-generated texture")
    binding_code = ROOT / "unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonUiBindingModule.cs"
    runtime_generated_bindings = [{
        "guid": DYNAMIC_FONT_ATLAS_GUID,
        "source": {
            "manifest": VERSION,
            "bundle": DYNAMIC_FONT_ATLAS_BUNDLE,
            "bundle_sha256": font_bundle_sha256,
            "apk": font_source.get("apk_sha256"),
            "apk_manifest": font_source.get("apk_manifest"),
            "object_type": font_atlas.type.name,
            "object_name": font_atlas.peek_name(),
            "path_id": str(font_atlas.path_id),
            "raw_sha256": hashlib.sha256(font_atlas.get_raw_data()).hexdigest(),
            "width": font_atlas_data.m_Width,
            "height": font_atlas_data.m_Height,
            "image_bytes": font_atlas_data.m_CompleteImageSize,
        },
        "runtime_binding": {
            "path": binding_code.relative_to(ROOT).as_posix(),
            "sha256": digest(binding_code),
            "reason": "The official Texture2D has no image payload; DailyDungeonUiBindingModule rebinds every TMP_Text to the loaded UI font before display.",
        },
    }]

    selected = set(roots)
    pending = list(roots)
    unresolved: dict[str, set[Path]] = {}
    while pending:
        asset = pending.pop()
        try:
            content = asset.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        for reference in GUID.findall(content):
            if reference in BUILTIN:
                continue
            dependency = by_guid.get(reference)
            if dependency is None or not dependency.is_file():
                unresolved.setdefault(reference, set()).add(asset)
            elif dependency not in selected:
                selected.add(dependency)
                pending.append(dependency)
    unresolved.pop(DYNAMIC_FONT_ATLAS_GUID, None)
    if unresolved:
        sample = next(iter(unresolved.items()))
        raise ValueError("Official UI dependency is unresolved: " + sample[0] + " in "
                         + str(next(iter(sample[1]))))

    external: dict[str, Path] = {}
    official_root = ROOT / "unity/Assets/Official"
    for meta in official_root.rglob("*.meta"):
        asset = meta.with_suffix("")
        asset_guid = guid(meta)
        if asset_guid and asset.is_file() and not asset.is_relative_to(OFFICIAL):
            external[asset_guid] = asset

    guid_remapping = {}
    for source in selected:
        source_guid = guid(source.with_name(source.name + ".meta"))
        existing = external.get(source_guid)
        relative = source.relative_to(EXPORT)
        source_digest = digest(source)
        canonical_target = OFFICIAL / official_dependency_path(relative)
        canonical_meta = canonical_target.with_name(canonical_target.name + ".meta")
        canonical_prior = prior_native_outputs.get(canonical_target.relative_to(ROOT).as_posix())
        canonical_conversion_is_verified = bool(
            canonical_target.is_file() and canonical_meta.is_file() and canonical_prior
            and canonical_prior.get("source") == relative.as_posix()
            and canonical_prior.get("source_guid") == source_guid
            and canonical_prior.get("source_sha256") == source_digest
            and canonical_prior.get("sha256") == digest(canonical_target)
            and canonical_prior.get("meta_sha256") == digest(canonical_meta)
            and guid(canonical_meta) == canonical_prior.get("guid"))
        canonical_target_conflicts = bool(
            canonical_target.is_file()
            and digest(canonical_target) != source_digest
            and not canonical_conversion_is_verified)
        canonical_meta_conflicts = bool(
            canonical_meta.is_file()
            and guid(canonical_meta) != source_guid
            and not canonical_conversion_is_verified)
        external_guid_conflicts = existing and digest(existing) != source_digest
        if external_guid_conflicts or canonical_target_conflicts or canonical_meta_conflicts:
            prior_target = OFFICIAL / "GuidConflicts" / official_dependency_path(relative)
            prior_meta = prior_target.with_name(prior_target.name + ".meta")
            prior = prior_native_outputs.get(prior_target.relative_to(ROOT).as_posix())
            preserved_guid = None
            if prior_target.is_file() and prior_meta.is_file() and prior:
                if (prior.get("source") == relative.as_posix()
                        and prior.get("source_guid") == source_guid
                        and prior.get("source_sha256") == source_digest
                        and guid(prior_meta) == prior.get("guid")):
                    preserved_guid = prior["guid"]
            guid_remapping[source_guid] = preserved_guid or uuid.uuid5(uuid.NAMESPACE_URL,
                "zjcs/dungeons/ui/" + VERSION + "/guid-conflict/" + source_guid).hex

    if npc_adventure_only:
        shader_objects = [obj for obj in apk.objects("defaultpackage_unityshaders.bundle")
                          if obj.type.name == "Shader"]
        shader_binding, runtime_material_bindings, runtime_material_guid_remapping = (
            prepare_distort_runtime_binding(apk, shader_objects,
                material_specs=(("CommonJieSuanDown", "a0f7d565f0cc7724fa2a53ef57ed0942"),),
                source_root=EXPORT))
        runtime_shader_bindings = [shader_binding]
        prefab_shaders, prefab_materials, prefab_remapping = prepare_runtime_material_bindings(
            apk, by_guid, export_outputs, shader_objects)
        runtime_shader_bindings.extend(prefab_shaders)
        runtime_material_bindings.extend(prefab_materials)
        runtime_material_guid_remapping.update(prefab_remapping)
    else:
        shader_objects = [obj for obj in apk.objects("defaultpackage_unityshaders.bundle")
                          if obj.type.name == "Shader"]
        distort_shader_binding, distort_material_bindings, distort_guid_remapping = (
            prepare_distort_runtime_binding(apk, shader_objects))
        prefab_shader_bindings, prefab_material_bindings, prefab_material_guid_remapping = (
            prepare_runtime_material_bindings(apk, by_guid, export_outputs, shader_objects))
        runtime_shader_bindings = [distort_shader_binding, *prefab_shader_bindings]
        runtime_material_bindings = [*distort_material_bindings, *prefab_material_bindings]
        runtime_material_guid_remapping = dict(distort_guid_remapping)
        runtime_material_guid_remapping.update(prefab_material_guid_remapping)
    runtime_guid_remapping = dict(guid_remapping)
    runtime_guid_remapping.update(runtime_material_guid_remapping)

    assets_root = ROOT / "unity/Assets"
    unity_folder_meta(assets_root / "Official/Dungeons")
    unity_folder_meta(RESOURCES.parent)
    unity_folder_meta(RESOURCES)
    output_records = []
    shared_records = []
    resource_records = []
    for source in sorted(selected):
        relative = source.relative_to(EXPORT)
        export_record = export_outputs.get((Path("ExportedProject/Assets") / relative).as_posix())
        if export_record is None or digest(source) != export_record.get("sha256"):
            raise ValueError("Dungeon UI asset differs from its verified AssetRipper export: " + str(source))
        source_meta = source.with_name(source.name + ".meta")
        asset_guid = guid(source_meta)
        if not asset_guid:
            raise ValueError("AssetRipper asset has no Unity GUID: " + str(source))
        existing = external.get(asset_guid)
        if existing and asset_guid not in guid_remapping:
            if digest(existing) != digest(source):
                raise ValueError("Official asset GUID conflicts with different local content: " + asset_guid)
            shared_records.append({"source": relative.as_posix(), "guid": asset_guid,
                                  "shared_asset": existing.relative_to(ROOT).as_posix(),
                                  "sha256": digest(existing)})
            official_target = existing
        else:
            remapped = asset_guid in guid_remapping
            output_relative = official_dependency_path(relative)
            official_target = (OFFICIAL / "GuidConflicts" / output_relative) if remapped else OFFICIAL / output_relative
            official_target.parent.mkdir(parents=True, exist_ok=True)
            folder = OFFICIAL
            target_relative = official_target.relative_to(OFFICIAL)
            for part in target_relative.parent.parts:
                folder = folder / part
                unity_folder_meta(folder)
            copied_content = remap_guid_references(source.read_bytes(), guid_remapping)
            source_digest = digest(source)
            copied_digest = hashlib.sha256(copied_content).hexdigest()
            output_guid = guid_remapping.get(asset_guid, asset_guid)
            official_meta = official_target.with_name(official_target.name + ".meta")
            existing_digest = digest(official_target) if official_target.exists() else None
            prior = prior_native_outputs.get(official_target.relative_to(ROOT).as_posix())
            reused_conversion = bool(official_target.exists() and official_meta.is_file() and prior
                and prior.get("source") == relative.as_posix()
                and prior.get("source_guid") == asset_guid
                and prior.get("guid") == output_guid
                and prior.get("source_sha256") == source_digest
                and prior.get("sha256") == existing_digest
                and prior.get("meta_sha256") == digest(official_meta)
                and guid(official_meta) == output_guid)
            if official_target.exists() and existing_digest not in {source_digest, copied_digest} \
                    and not reused_conversion:
                # A later validation pass may have rewritten a shared official
                # dependency while preserving its pinned source identity. Keep
                # that user-owned payload intact and record the current digest.
                if not (prior and prior.get("source") == relative.as_posix()
                        and prior.get("source_guid") == asset_guid
                        and prior.get("guid") == output_guid
                        and prior.get("source_sha256") == source_digest
                        and official_meta.is_file() and guid(official_meta) == output_guid):
                    raise ValueError("Refusing to overwrite changed dungeon UI asset: " + str(official_target))
                reused_conversion = True
            if not reused_conversion and (not official_target.exists() or existing_digest != copied_digest):
                official_target.write_bytes(copied_content)
            if official_meta.exists() and guid(official_meta) != output_guid:
                raise ValueError("Refusing to overwrite changed Unity metadata: " + str(official_meta))
            meta_content = remap_guid_references(source_meta.read_bytes(), guid_remapping)
            if remapped:
                meta_text = meta_content.decode("utf-8")
                meta_content = META_GUID.sub("guid: " + output_guid, meta_text).encode("utf-8")
            is_apk_plugin = source.suffix.lower() == ".dll"
            if is_apk_plugin:
                meta_content = disable_apk_plugin_import(meta_content)
            if is_apk_plugin and official_meta.exists() and digest(official_meta) != hashlib.sha256(meta_content).hexdigest():
                owns_existing_meta = bool(prior and prior.get("source") == relative.as_posix()
                    and prior.get("source_guid") == asset_guid and prior.get("guid") == output_guid
                    and prior.get("source_sha256") == source_digest
                    and prior.get("meta_sha256") == digest(official_meta))
                if not owns_existing_meta:
                    raise ValueError("Refusing to overwrite changed Unity metadata: " + str(official_meta))
                official_meta.write_bytes(meta_content)
            elif not official_meta.exists():
                official_meta.write_bytes(meta_content)
            external[output_guid] = official_target
            output_record = {"path": official_target.relative_to(ROOT).as_posix(),
                             "source": relative.as_posix(), "source_guid": asset_guid,
                             "guid": output_guid, "guid_remapped": remapped,
                             "bytes": official_target.stat().st_size,
                             "source_sha256": source_digest,
                             "sha256": digest(official_target),
                             "meta_sha256": digest(official_meta),
                             "preserved_existing": reused_conversion}
            if is_apk_plugin:
                output_record["source_meta_sha256"] = digest(source_meta)
                output_record["unity_plugin_import_disabled"] = True
            output_records.append(output_record)

        if source in roots and source.suffix == ".prefab":
            resource_target = TRAVEL_RUNTIME if source == TRAVEL_SOURCE else RESOURCES / source.name
            resource_target.parent.mkdir(parents=True, exist_ok=True)
            unity_folder_meta(resource_target.parent)
            converted = convert_runtime_prefab(source, runtime_guid_remapping)
            converted_digest = hashlib.sha256(converted).hexdigest()
            allowed_existing = {digest(source), converted_digest}
            # Recompute the previous conversion from the verified original and
            # verified material bindings; never accept an arbitrary changed prefab.
            previous_remapping = dict(guid_remapping)
            previous_remapping.update(prior_material_remapping)
            allowed_existing.add(hashlib.sha256(convert_runtime_prefab(source, previous_remapping)).hexdigest())
            prior_runtime = prior_runtime_prefabs.get(resource_target.relative_to(ROOT).as_posix())
            prior_runtime_is_verified = bool(resource_target.is_file() and prior_runtime
                and prior_runtime.get("source") == relative.as_posix()
                and prior_runtime.get("conversion") in (
                    "dungeon-ui-ugui-tmp-v1", "dungeon-ui-ugui-tmp-v2",
                    "dungeon-ui-ugui-tmp-v3", "dungeon-ui-ugui-tmp-v4", RUNTIME_CONVERSION)
                and prior_runtime.get("source_sha256") == digest(source)
                and prior_runtime.get("sha256") == digest(resource_target))
            if prior_runtime_is_verified:
                allowed_existing.add(digest(resource_target))
            if resource_target.exists() and digest(resource_target) not in allowed_existing:
                raise ValueError("Refusing to overwrite changed runtime prefab: " + str(resource_target))
            if not resource_target.exists() or digest(resource_target) != converted_digest:
                resource_target.write_bytes(converted)
            resource_meta = resource_target.with_name(resource_target.name + ".meta")
            runtime_guid = uuid.uuid5(uuid.NAMESPACE_URL,
                "zjcs/dungeons/ui/" + VERSION + "/" + source.name).hex
            if resource_meta.exists() and guid(resource_meta) != runtime_guid:
                raise ValueError("Runtime prefab GUID changed: " + str(resource_meta))
            if not resource_meta.exists():
                resource_meta.write_text("fileFormatVersion: 2\n"
                    f"guid: {runtime_guid}\nPrefabImporter:\n"
                    "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n",
                    encoding="utf-8")
            source_script_pattern = re.compile(
                r"m_Script: \{fileID: " + str(UIMESH_SOURCE_FILE_ID)
                + r", guid: " + re.escape(LAUNCH_DLL_GUID) + r", type: 3\}")
            converted_script_pattern = re.compile(
                r"m_Script: \{fileID: 11500000, guid: " + re.escape(UIMESH_SCRIPT_GUID) + r", type: 3\}")
            source_script_count = len(source_script_pattern.findall(source.read_text(encoding="utf-8")))
            converted_script_count = len(converted_script_pattern.findall(converted.decode("utf-8")))
            if source_script_count != converted_script_count:
                raise ValueError("Official UIMesh script references did not bind completely: " + str(source))
            if source.name == "DailyDungeonEntryPanel.prefab" and source_script_count != 2:
                raise ValueError("DailyDungeonEntryPanel must preserve both original UIMesh components")
            resource_record = {"path": resource_target.relative_to(ROOT).as_posix(),
                               "source": relative.as_posix(), "guid": runtime_guid,
                               "conversion": RUNTIME_CONVERSION,
                               "source_sha256": digest(source),
                               "sha256": digest(resource_target),
                               "official_guid": guid_remapping.get(asset_guid, asset_guid)}
            if source_script_count:
                resource_record["script_bindings"] = [dict(
                    type="UIMesh", count=source_script_count,
                    source_guid=guid_remapping.get(LAUNCH_DLL_GUID, LAUNCH_DLL_GUID),
                    source_file_id=UIMESH_SOURCE_FILE_ID,
                    runtime_guid=UIMESH_SCRIPT_GUID, runtime_file_id=11500000)]
            resource_records.append(resource_record)

    for material_binding in runtime_material_bindings:
        source_material_guid = material_binding["source_guid"]
        runtime_material_guid = material_binding["guid"]
        original_material_references = sum(
            source.read_text(encoding="utf-8").count("guid: " + source_material_guid)
            for source in roots if source.suffix == ".prefab")
        runtime_material_references = 0
        for resource in resource_records:
            prefab_text = (ROOT / resource["path"]).read_text(encoding="utf-8")
            if "guid: " + source_material_guid in prefab_text:
                raise ValueError("A runtime dungeon prefab still refers to the mobile "
                                 + material_binding["source_path"])
            runtime_material_references += prefab_text.count("guid: " + runtime_material_guid)
        if not npc_adventure_only and (original_material_references == 0
                or original_material_references != runtime_material_references):
            raise ValueError("Runtime " + Path(material_binding["path"]).stem
                             + " material references are incomplete")
        material_binding["prefab_reference_count"] = runtime_material_references

    receipt = {
        "manifest": VERSION,
        "input_manifest": INPUTS.relative_to(ROOT).as_posix(),
        "bundle_inputs": bundle_inputs,
        "asset_ripper_manifest_sha256": digest(INPUTS),
        "ripper_export_root": EXPORT_ROOT.relative_to(ROOT).as_posix(),
        "outputs": output_records,
        "shared_bindings": shared_records,
        "runtime_prefabs": resource_records,
        "runtime_shader_bindings": runtime_shader_bindings,
        "runtime_material_bindings": runtime_material_bindings,
        "runtime_script_bindings": runtime_script_bindings,
        "runtime_generated_bindings": runtime_generated_bindings,
        "probability_tip_source": {
            "type": "ProbabilityTip",
            "config": ITEM_ICON_ATTACHMENT_CONFIG.relative_to(EXPORT).as_posix(),
            "config_sha256": digest(ITEM_ICON_ATTACHMENT_CONFIG),
            "asset_guid": PROBABILITY_TIP_ASSET_GUID,
            "unity_guid": guid(PROBABILITY_TIP_SOURCE.with_name(PROBABILITY_TIP_SOURCE.name + ".meta")),
            "prefab": PROBABILITY_TIP_SOURCE.relative_to(EXPORT).as_posix(),
            "prefab_sha256": digest(PROBABILITY_TIP_SOURCE),
        },
        "guid_conflicts": [dict(source_guid=source_guid, runtime_guid=runtime_guid)
                           for source_guid, runtime_guid in sorted(guid_remapping.items())],
        "prefab_roots": sorted(path.relative_to(EXPORT).as_posix() for path in roots),
        "counts": {"prefabs": len(roots), "dependency_assets": len(output_records),
                   "shared_assets": len(shared_records), "runtime_shaders": len(runtime_shader_bindings),
                   "runtime_materials": len(runtime_material_bindings)},
    }
    receipt_path = NPC_ADVENTURE_RECEIPT if npc_adventure_only else RECEIPT
    receipt_path.parent.mkdir(parents=True, exist_ok=True)
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return receipt


if __name__ == "__main__":
    print(json.dumps(build(npc_adventure_only="--npc-adventure-only" in os.sys.argv),
        ensure_ascii=False, indent=2))
