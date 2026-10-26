"""Stripped native Light2D schema, verified against the APK metadata and bytes.

Conversion metadata only; this is not a runtime Light2D implementation. The
client's custom recession fields are retained between Bounds and point fields.
"""
from functools import lru_cache
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from official_metadata import Metadata
from UnityPy.helpers.TypeTreeNode import TypeTreeNode
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from official_unity_schema import ScriptSchemas


LIGHT_FIELDS = (
    ("m_ComponentVersion", "int"), ("m_LightType", "int"), ("m_BlendStyleIndex", "int"),
    ("m_FalloffIntensity", "float"), ("m_Color", "ColorRGBA"), ("m_Intensity", "float"),
    ("m_LightVolumeIntensity", "float"), ("m_LightVolumeIntensityEnabled", "bool"),
    ("m_ApplyToSortingLayers", "int[]"), ("m_LightCookieSprite", "PPtr<Sprite>"),
    ("m_DeprecatedPointLightCookieSprite", "PPtr<Sprite>"), ("m_LightOrder", "int"),
    ("m_AlphaBlendOnOverlap", "bool"), ("m_OverlapOperation", "int"),
    ("m_NormalMapDistance", "float"), ("m_NormalMapQuality", "int"), ("m_UseNormalMap", "bool"),
    ("m_ShadowIntensityEnabled", "bool"), ("m_ShadowIntensity", "float"),
    ("m_ShadowVolumeIntensityEnabled", "bool"), ("m_ShadowVolumeIntensity", "float"),
    ("m_LocalBounds", "AABB"), ("UseRecession", "bool"), ("RecessionCenter", "Vector3f"),
    ("RecessionRadius", "float"), ("RecessionBlurEdge", "float"),
    ("m_PointLightInnerAngle", "float"), ("m_PointLightOuterAngle", "float"),
    ("m_PointLightInnerRadius", "float"), ("m_PointLightOuterRadius", "float"),
    ("m_ShapeLightParametricSides", "int"), ("m_ShapeLightParametricAngleOffset", "float"),
    ("m_ShapeLightParametricRadius", "float"), ("m_ShapeLightFalloffSize", "float"),
    ("m_ShapeLightFalloffOffset", "Vector2f"), ("m_ShapePath", "Vector3f[]"),
)


def node(kind, name, level):
    sizes = {"bool": 1, "int": 4, "float": 4, "SInt64": 8}
    value = TypeTreeNode(level, kind, name, sizes.get(kind, -1), 1,
                         m_MetaFlag=0x4000 if kind == "bool" else 0)
    if kind.endswith("[]"):
        value.m_Type = "vector"
        array = node("Array", "Array", level + 1)
        array.m_TypeFlags = 1
        array.m_Children = [node("int", "size", level + 2), node(kind[:-2], "data", level + 2)]
        value.m_Children = [array]
    elif kind in ("ColorRGBA", "Vector2f", "Vector3f"):
        fields = {"ColorRGBA": "rgba", "Vector2f": "xy", "Vector3f": "xyz"}[kind]
        value.m_Children = [node("float", field, level + 1) for field in fields]
        value.m_ByteSize = 4 * len(fields)
    elif kind == "AABB":
        value.m_Children = [node("Vector3f", field, level + 1) for field in ("m_Center", "m_Extent")]
        value.m_ByteSize = 24
    elif kind.startswith("PPtr<"):
        value.m_Children = [node("int", "m_FileID", level + 1), node("SInt64", "m_PathID", level + 1)]
        value.m_ByteSize = 12
    elif kind == "string":
        value.m_Children = [node("Array", "Array", level + 1)]
        value.m_Children[0].m_TypeFlags = 1
        value.m_Children[0].m_MetaFlag = 0x4000
        value.m_Children[0].m_Children = [node("int", "size", level + 2), node("char", "data", level + 2)]
        value.m_Children[0].m_Children[1].m_ByteSize = 1
    return value


@lru_cache(maxsize=1)
def light_metadata():
    path = ROOT / ".tools/source-packages/official-code/global-metadata.dat"
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != "58884b94b56ef35665f44d3f186fce8b8c854cf3bab0a74e4733a18b6a9175b4":
        raise ValueError("Light2D metadata differs from the pinned official APK")
    return next(value for value in Metadata(raw).types
                    if value["name"] == "Light2D" and value["namespace"] == "UnityEngine.Rendering.Universal")


def light_tree(schemas):
    metadata = light_metadata()
    actual = [field["name"] for field in metadata["fields"]]
    selected = [name for name, _ in LIGHT_FIELDS]
    if [name for name in actual if name in selected] != selected:
        raise ValueError("Light2D serialized field order differs from the pinned APK metadata")
    tree = schemas.tree("Assembly-CSharp.dll", "MapGroundRoot")
    tree.m_Children = tree.m_Children[:4] + [node(kind, name, 1) for name, kind in LIGHT_FIELDS]
    for index, child in enumerate(tree.traverse()):
        child.m_Index = index
    return tree


def fields_tree(schemas, fields):
    tree = schemas.tree("Assembly-CSharp.dll", "MapGroundRoot")
    tree.m_Children = tree.m_Children[:4] + [node(kind, name, 1) for name, kind in fields]
    return tree


def structure(kind, name, level, fields):
    value = node(kind, name, level)
    value.m_Children = [node(field_kind, field_name, level + 1) for field_name, field_kind in fields]
    return value


@lru_cache(maxsize=1)
def metadata_types():
    light_metadata()  # Validate the pinned APK metadata hash before reuse.
    return Metadata((ROOT / ".tools/source-packages/official-code/global-metadata.dat").read_bytes()).types


def verify_fields(name, fields):
    actual = next(t for t in metadata_types() if t["name"] == name)
    names = [f["name"] for f in actual["fields"]]
    if [n for n in names if n in fields] != list(fields):
        raise ValueError("Pinned APK field order differs: " + name)


def spine_tree(schemas, name):
    # Serialization descriptions only. No Spine runtime code is reconstructed.
    if name == "SpineAtlasAsset":
        fields = [("textureLoadingMode", "int"), ("onDemandTextureLoader", "PPtr<MonoBehaviour>"),
                  ("atlasFile", "PPtr<TextAsset>"), ("materials", "PPtr<Material>[]")]
        verify_fields("AtlasAssetBase", [n for n, _ in fields[:2]])
        verify_fields(name, [n for n, _ in fields[2:]])
        return fields_tree(schemas, fields)
    if name == "SkeletonMecanim":
        fields = [("skeletonDataAsset", "PPtr<MonoBehaviour>"), ("initialSkinName", "string"),
                  ("initialFlipX", "bool"), ("initialFlipY", "bool"), ("updateWhenInvisible", "int"),
                  ("separatorSlotNames", "string[]"), ("zSpacing", "float")]
        fields += [(n, "bool") for n in ("useClipping", "immutableTriangles", "pmaVertexColors",
            "clearStateOnDisable", "tintBlack", "singleSubmesh", "fixDrawOrder", "addNormals", "calculateTangents")]
        fields += [("maskInteraction", "int"), ("maskMaterials", "SpriteMaskInteractionMaterials"),
                   ("disableRenderingOnOverride", "bool"), ("physicsPositionInheritanceFactor", "Vector2f"),
                   ("physicsRotationInheritanceFactor", "float"), ("physicsMovementRelativeTo", "PPtr<Transform>")]
        verify_fields("SkeletonRenderer", [n for n, _ in fields])
        fields += [("translator", "MecanimTranslator"), ("updateTiming", "int")]
        tree = fields_tree(schemas, fields)
        for i, child in enumerate(tree.m_Children):
            if child.m_Name == "maskMaterials":
                tree.m_Children[i] = structure(child.m_Type, child.m_Name, 1,
                    [(n, "PPtr<Material>[]") for n in ("materialsMaskDisabled", "materialsInsideMask", "materialsOutsideMask")])
            elif child.m_Name == "translator":
                data = [("autoReset", "bool"), ("useCustomMixMode", "bool"), ("layerMixModes", "int[]"), ("layerBlendModes", "int[]")]
                verify_fields("MecanimTranslator", [n for n, _ in data])
                tree.m_Children[i] = structure(child.m_Type, child.m_Name, 1, data)
        return tree
    if name == "SkeletonDataAsset":
        fields = [("atlasAssets", "PPtr<MonoBehaviour>[]"), ("scale", "float"), ("skeletonJSON", "PPtr<TextAsset>"),
            ("isUpgradingBlendModeMaterials", "bool"), ("blendModeMaterials", "BlendModeMaterials"),
            ("skeletonDataModifiers", "PPtr<MonoBehaviour>[]"), ("fromAnimation", "string[]"),
            ("toAnimation", "string[]"), ("duration", "float[]"), ("defaultMix", "float"),
            ("controller", "PPtr<RuntimeAnimatorController>"), ("zSlots", "string[]")]
        verify_fields(name, [n for n, _ in fields])
        tree = fields_tree(schemas, fields)
        blend_fields = [("requiresBlendModeMaterials", "bool"), ("applyAdditiveMaterial", "bool")]
        blend_fields += [(n, "ReplacementMaterial[]") for n in ("additiveMaterials", "multiplyMaterials", "screenMaterials")]
        verify_fields("BlendModeMaterials", [n for n, _ in blend_fields])
        for i, child in enumerate(tree.m_Children):
            if child.m_Name != "blendModeMaterials": continue
            replacement = structure("BlendModeMaterials", child.m_Name, 1, blend_fields)
            for array in replacement.m_Children[2:]:
                array.m_Children[0].m_Children[1] = structure("ReplacementMaterial", "data", 4,
                    [("pageName", "string"), ("material", "PPtr<Material>")])
            tree.m_Children[i] = replacement
        return tree
    raise ValueError("Unsupported native Spine serialization type: " + name)


class NativeSchemas:
    def __init__(self, code):
        self.default = ScriptSchemas(code_root=code)
        self.native = ScriptSchemas.__new__(ScriptSchemas)
        self.native.generator = TypeTreeGenerator("2022.3.57f1", generator="AssetsTools")
        self.native.cache, self.native.records = {}, {}
        editor = Path(json.loads((ROOT / "unity-tools.json").read_text())["editorInstallPath"]) / "Editor/Data"
        self.reference_inputs = []
        for folder in [editor / "NetStandard/ref/2.1.0", editor / "Managed/UnityEngine"]:
            for path in sorted(folder.glob("*.dll")):
                self.native.generator.load_dll(path.read_bytes())
                self.reference_inputs.append(path)
        for name in ["Unity.TextMeshPro", "UnityEngine.UI"]:
            path = ROOT / "unity/Library/ScriptAssemblies" / (name + ".dll")
            self.native.generator.load_dll(path.read_bytes())
            self.reference_inputs.append(path)
        self.native.generator.load_local_dll_folder(str(code / "managed/decoded"))
        key = "Unity.TextMeshPro:TMPro.TMP_FontAsset"
        self.native.tree("Unity.TextMeshPro", "TMPro.TMP_FontAsset")
        fields, skip = [], False
        for field in self.native.cache[key]:
            if field["level"] == 1: skip = field["name"] == "m_SourceFontFile_EditorRef"
            if skip: continue
            if field["level"] == 1 and field["name"] == "m_Version":
                fields.extend([dict(level=1, type="Psd2UguiSetting", name="psd2UguiSetting", flags=0)] +
                    [dict(level=2, type="int", name=n, flags=0) for n in ("maxOutlineSize", "maxShadowDis", "maxShadowBlur")])
            fields.append(field)
        self.native.cache[key] = fields

    def tree(self, assembly, name):
        return self.default.tree(assembly, name)

    def read(self, obj):
        return read(self, obj)


def read(schemas, obj):
    # A restored class can contain managed references. Read its fixed native
    # header first, before the resolver for the serialized registry is installed.
    original = obj.serialized_type.node
    try:
        obj.serialized_type.node = None
        return read_stripped(schemas, obj)
    finally:
        obj.serialized_type.node = original


def read_stripped(schemas, obj):
    default = getattr(schemas, "default", schemas)
    script = obj.read(check_read=False).m_Script.read()
    if script.m_Namespace == "UnityEngine.Rendering.Universal" and script.m_ClassName == "Light2D":
        tree = light_tree(default)
        return obj.read_typetree(nodes=tree), tree
    if script.m_Namespace == "Spine.Unity" and script.m_ClassName in ("SkeletonMecanim", "SkeletonDataAsset", "SpineAtlasAsset"):
        tree = spine_tree(default, script.m_ClassName)
        value = obj.read_typetree(nodes=tree)
        if script.m_ClassName == "SkeletonMecanim":
            from official_battle_spine_component import read_component
            expected = read_component(obj.get_raw_data(), script.m_ClassName)
            if any(value[n] != v for n, v in expected.items() if n != "physicsPositionInheritanceFactor"):
                raise ValueError("Restored Spine fields differ from the strict original component reader")
            if list(value["physicsPositionInheritanceFactor"].values()) != expected["physicsPositionInheritanceFactor"]:
                raise ValueError("Original Spine physics inheritance differs")
        return value, tree
    selected = schemas.native if hasattr(schemas, "native") and script.m_ClassName in (
        "TextMeshPro", "TMP_FontAsset", "TextMeshProColorProxy", "ContentSizeFitter", "GUISkin") else default
    value = selected.read(obj)
    name = (script.m_Namespace + "." if script.m_Namespace else "") + script.m_ClassName
    return value, selected.tree(script.m_AssemblyName, name)
