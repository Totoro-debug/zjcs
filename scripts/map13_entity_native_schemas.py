"""Serialization descriptions from official metadata, never a Spine runtime.

The reference-only schema DLL lacks OnDemandTextureLoader. Recover its original
field visibility and SerializeField constructor from the pinned native type and
attribute registrations, then describe the concrete client generic instance.
"""
import struct
import os
from pathlib import Path

from official_apk import ROOT
from official_metadata import Metadata
from map13_native_schemas import fields_tree, node, spine_tree
from official_managed_reference_conversion import clone, vector
from prepare_unity_map13_sources import sha, source_path
from read_official_native import NativeMethods


def install(schemas, code):
    image = ROOT / ".tools/source-packages/commerce-native/libil2cpp-restored.bin"
    metadata = ROOT / ".tools/source-packages/official-code/global-metadata.dat"
    native = NativeMethods(image, metadata)
    model = Metadata(native.metadata)
    registration = 0x11B6A188
    values = struct.unpack_from("<16Q", native.image, registration)
    if values[10] != len(model.types) or values[12] != len(model.types) or values[7] + values[6] * 8 > len(native.image):
        raise ValueError("Pinned IL2CPP type registration differs")
    table = values[7]
    images = [struct.unpack_from("<10I", native.metadata, at)
              for at in range(native.header[42], native.header[42] + native.header[43], 40)]
    proofs = []

    def describe(name, expected, unsupported=(), type_index=None):
        typ, = [value for value in model.types if value["name"] == name and
                (type_index is None or value["index"] == type_index)]
        img, = [value for value in images if value[2] <= typ["index"] < value[2] + value[3]]
        attributes = dict(struct.unpack_from("<2I", native.metadata, native.header[52] + index * 8)
                          for index in range(img[8], img[8] + img[9]))
        fields, serialized = [], []
        for field in typ["fields"]:
            pointer = struct.unpack_from("<Q", native.image, table + field["type_index"] * 8)[0]
            data, bits = struct.unpack_from("<QI", native.image, pointer)
            flags = bits & 0xffff
            record = dict(field, type_pointer=pointer, type_data=data, type_bits=bits)
            serialize_field = False
            if field["token"] in attributes:
                at = native.header[50] + attributes[field["token"]]
                count = native.metadata[at]
                if count >= 0x80:
                    raise ValueError("Inspect extended native attribute count")
                constructors = []
                for number in range(count):
                    index = struct.unpack_from("<I", native.metadata, at + 1 + number * 4)[0]
                    method = native.methods[index]
                    owner = native.types[method[1]]
                    constructor = native.string(owner[1]) + "." + native.string(owner[0]) + "." + native.string(method[0])
                    constructors.append(dict(method_index=index, name=constructor))
                    serialize_field |= constructor == "UnityEngine.SerializeField..ctor"
                record["attribute_constructors"] = constructors
            if field["name"] in unsupported:
                record["unity_unsupported"] = (unsupported[field["name"]] if isinstance(unsupported, dict) else
                    "Delegate field; Unity does not serialize delegates. Confirmed by the current original client declaration.")
            elif not flags & (0x10 | 0x20 | 0x40 | 0x80) and ((flags & 7) == 6 or serialize_field):
                serialized.append(field["name"])
            fields.append(record)
        if expected is not None and serialized != expected:
            raise ValueError("Official native serialized field order differs: " + name + " " + str(serialized))
        proofs.append(dict(type=name, namespace=typ["namespace"], type_index=typ["index"], fields=fields, serialized=serialized))
        return serialized

    describe("OnDemandTextureLoader", ["atlasAsset", "skeletonDataAsset"])
    describe("AppearanceOnDemandTextureLoader`2", ["maxPlaceholderSize", "unloadAfterSecondsUnused", "placeholderMap"])
    describe("PlaceholderTextureMapping", ["placeholderTexture", "targetTextureReference", "dyeTextureReference"])
    describe("YooAssetTextureReference", ["AssetReference"])
    describe("YooAssetTextureLoader", ["LevelOfResolution", "PlaceholderPath", "TargetTexturePath", "DyeTexturePath"])
    describe("AssetProxyBase`1", ["mAssetGuid"])
    describe("UnsafeAssetProxy`1", [])
    describe("SpineTextureAssetProxy", [])
    describe("SkeletonDataModifierAsset", [])
    describe("LoadAllAnimationsSkeletonDataModifier", [])
    describe("PhysicsPriorityData", ["PhysicsConstraintEndLetter", "Priority"])
    describe("PhysicsConstraintSkeletonDataModifier", ["mSerializedPhysicsPriority", "mPhysicsPriorityDataList"],
             unsupported=("OnPhysicsPriorityChanged",))
    graphic_fields = describe("SkeletonGraphic", None)
    bone_fields = [("skeletonRenderer", "PPtr<MonoBehaviour>"), ("boneName", "string")]
    bone_fields += [(name, "bool") for name in ("followXYPosition", "followZPosition", "followBoneRotation",
        "followSkeletonFlip", "followLocalScale", "followParentWorldScale")]
    bone_fields += [("maintainedAxisOrientation", "int"), ("initializeOnAwake", "bool")]
    describe("BoneFollower", [name for name, _ in bone_fields])
    describe("SkeletonPartsRenderer", [])
    describe("CinemachinePipeline", [])
    animation_fields = [("updateTiming", "int"), ("unscaledTime", "bool"), ("_animationName", "string"),
                        ("loop", "bool"), ("timeScale", "float")]
    describe("SkeletonAnimation", [name for name, _ in animation_fields],
             unsupported={"state": "Spine.AnimationState is a runtime class without the Serializable flag; not serialized by Unity."})
    state, = [typ for typ in model.types if typ["namespace"] == "Spine" and typ["name"] == "AnimationState"]
    if state["flags"] & 0x2000:
        raise ValueError("Inspect original animation state serialization")
    describe("CameraSceneLayerCuller", ["mCamera", "mFullScreenUICullingMask"])
    describe("MyEquipAgent", ["myAttachAgent", "myRenderer", "Weapons", "SecondWeapons"])
    particle_fields = [("m_IsTrail", "bool"), ("m_IgnoreCanvasScaler", "bool"), ("m_AbsoluteMode", "bool"),
        ("m_Scale3D", "Vector3f"), ("m_AnimatableProperties", "AnimatableProperty[]"),
        ("m_Particles", "PPtr<ParticleSystem>[]"), ("m_MeshSharing", "int"), ("m_GroupId", "int"),
        ("m_GroupMaxId", "int"), ("m_PositionMode", "int"), ("m_AutoScaling", "bool"),
        ("m_AutoScalingMode", "int"), ("m_UseCustomView", "bool"), ("m_CustomViewSize", "float")]
    describe("UIParticle", [name for name, _ in particle_fields])
    describe("AnimatableProperty", ["m_Name", "m_Type"])
    describe("AppearancePartHandler", ["IgnoreSkinTypes"])
    appearance_fields = [("mSkeletonRenderer", "PPtr<MonoBehaviour>"), ("mSlot", "PPtr<Transform>"),
                         ("mSortingLayer", "string"), ("mDefaultSortingOrder", "int")]
    describe("AppearanceRendererPartHandler", [name for name, _ in appearance_fields],
             unsupported=("OnGetSharedMaterialDelegate",))
    describe("AppearanceSpine", ["SkinTextureAsset", "mPartHandler"], unsupported=("OnSkinUpdate",))
    describe("AppearanceRenderer", ["mSkeletonRenderer", "mTextureLoader"])
    describe("AppearanceMecanim", [])
    describe("AppearanceUI", ["mSkeletonGraphic", "mTextureLoader"], unsupported=("ReplaceMaterialsAfterLoad",))
    layout_fields = [("m_Spacing", "float"), ("m_SpacingList", "float[]")]
    layout_fields += [(name, "bool") for name in ("m_ChildForceExpandWidth", "m_ChildForceExpandHeight",
        "m_ChildControlWidth", "m_ChildControlHeight", "m_ChildScaleWidth", "m_ChildScaleHeight", "m_ReverseArrangement")]
    describe("HorizontalOrVerticalLayoutGroupJSZX", [name for name, _ in layout_fields])
    describe("VerticalLayoutGroupJSZX", [])
    describe("AkAudioListener", ["isDefaultListener", "listenerId"])
    describe("AkGameObj", ["m_listeners", "isEnvironmentAware", "isStaticObject", "m_positionOffsetData", "m_posOffsetData", "listenerMask"])
    describe("BaseListenerList", [])
    describe("AkGameObjListenerList", ["initialListenerList", "useDefaultListeners"])
    describe("AkGameObjPositionOffsetData", ["KeepMe", "positionOffset"])
    describe("AkGameObjPosOffsetData", ["positionOffset"])
    separator_fields = [("skeletonRenderer", "PPtr<MonoBehaviour>"), ("copyPropertyBlock", "bool"),
        ("copyMeshRendererFlags", "bool"), ("partsRenderers", "PPtr<MonoBehaviour>[]"), ("OptimizeRender", "bool")]
    describe("SkeletonRenderSeparator", [name for name, _ in separator_fields])
    describe("RenderExistingMesh", ["referenceRenderer", "replacementMaterials"])
    describe("MaterialReplacement", ["originalMaterial", "replacementMaterial"], type_index=52352)
    describe("SkeletonRendererCustomMaterials", ["skeletonRenderer", "customSlotMaterials", "customMaterialOverrides"])
    material_arrays = [("customSlotMaterials", "SlotMaterialOverride", 52416,
                       [("overrideDisabled", "bool"), ("slotName", "string"), ("material", "PPtr<Material>")]),
                      ("customMaterialOverrides", "AtlasMaterialOverride", 52417,
                       [("overrideDisabled", "bool"), ("originalMaterial", "PPtr<Material>"), ("replacementMaterial", "PPtr<Material>")])]
    for _, name, index, fields in material_arrays:
        describe(name, [label for label, _ in fields], type_index=index)
    for address, expected in [(315103552, 52416), (315103520, 52417)]:
        generic = struct.unpack_from("<4Q", native.image, address)
        count, arguments = struct.unpack_from("<2Q", native.image, generic[1])
        pointer = struct.unpack_from("<Q", native.image, arguments)[0]
        argument, bits = struct.unpack_from("<QI", native.image, pointer)
        if count != 1 or argument != expected or (bits >> 16) & 255 != 17:
            raise ValueError("Original custom material generic argument differs")
    describe("MeshGenerator", ["settings"])
    mesh_settings = [("useClipping", "bool"), ("zSpacing", "float"), ("useCustomSpacing", "bool"),
                     ("tintBlack", "bool"), ("canvasGroupCompatible", "bool"), ("pmaVertexColors", "bool"),
                     ("addNormals", "bool"), ("calculateTangents", "bool"), ("immutableTriangles", "bool")]
    describe("Settings", [name for name, _ in mesh_settings], type_index=52434)
    graphic_types = dict(skeletonDataAsset="PPtr<MonoBehaviour>", additiveMaterial="PPtr<Material>",
        multiplyMaterial="PPtr<Material>", screenMaterial="PPtr<Material>", m_SkeletonColor="ColorRGBA",
        initialSkinName="string", initialFlipX="bool", initialFlipY="bool", startingAnimation="string",
        startingLoop="bool", timeScale="float", freeze="bool", layoutScaleMode="int", referenceSize="Vector2f",
        pivotOffset="Vector2f", referenceScale="float", layoutScale="float", updateWhenInvisible="int",
        allowMultipleCanvasRenderers="bool", canvasRenderers="PPtr<CanvasRenderer>[]", separatorSlotNames="string[]",
        enableSeparatorSlots="bool", separatorParts="PPtr<Transform>[]", updateSeparatorPartLocation="bool",
        updateSeparatorPartScale="bool", disableMeshAssignmentOnOverride="bool", useCustomDemandLoading="bool",
        physicsPositionInheritanceFactor="Vector2f", physicsRotationInheritanceFactor="float",
        physicsMovementRelativeTo="PPtr<Transform>", meshGenerator="MeshGenerator", updateTiming="int", unscaledTime="bool")
    if set(graphic_fields) != set(graphic_types):
        raise ValueError("Inspect original SkeletonGraphic field types")
    original_tree = schemas.default.tree
    for module in ("Unity.RenderPipelines.Core.Runtime", "Unity.RenderPipelines.Universal.Runtime"):
        path = getattr(schemas, "unity_project", ROOT / "unity") / "Library/ScriptAssemblies" / (module + ".dll")
        schemas.native.generator.load_dll(path.read_bytes())
        schemas.reference_inputs.append(path)
    for module in ("mscorlib", "System.Core"):
        path = Path(os.environ.get("WINDIR", "C:/Windows")) / "Microsoft.NET/Framework64/v4.0.30319" / (module + ".dll")
        schemas.native.generator.load_dll(path.read_bytes())
        schemas.reference_inputs.append(path)

    def cache(assembly, name, result):
        fields = [dict(level=child.m_Level, type=child.m_Type, name=child.m_Name, flags=child.m_MetaFlag or 0)
                  for child in result.traverse()]
        schemas.default.cache[assembly + ":" + name] = fields
        schemas.default.records[assembly + ":" + name] = fields
        return result

    def tree(assembly, name):
        if name == "AkGameObj":
            result = fields_tree(schemas.default, [("m_listeners", "AkGameObjListenerList"), ("isEnvironmentAware", "bool"),
                ("isStaticObject", "bool"), ("m_positionOffsetData", "AkGameObjPositionOffsetData"),
                ("m_posOffsetData", "PPtr<MonoBehaviour>"), ("listenerMask", "int")])
            for child in result.m_Children:
                if child.m_Name == "m_listeners":
                    child.m_Children = [node("PPtr<MonoBehaviour>[]", "initialListenerList", 2), node("bool", "useDefaultListeners", 2)]
                elif child.m_Name == "m_positionOffsetData":
                    child.m_Children = [node("bool", "KeepMe", 2), node("Vector3f", "positionOffset", 2)]
            return cache(assembly, name, result)
        if name == "AkGameObjPosOffsetData":
            return cache(assembly, name, fields_tree(schemas.default, [("positionOffset", "Vector3f")]))
        if name in ("UGUIJszx.VerticalLayoutGroupJSZX", "UGUIJszx.HorizontalLayoutGroupJSZX"):
            result = clone(schemas.native.tree("UnityEngine.UI", "UnityEngine.UI.LayoutGroup"))
            result.m_Children.extend(node(kind, label, 1) for label, kind in layout_fields)
            return cache(assembly, name, result)
        if name == "AkAudioListener":
            return cache(assembly, name, fields_tree(schemas.default, [("isDefaultListener", "bool"), ("listenerId", "int")]))
        if name in ("AppearanceSystem.Runtime.AppearanceMecanim", "AppearanceSystem.Runtime.AppearanceUI"):
            renderer = "mSkeletonGraphic" if name.endswith(".AppearanceUI") else "mSkeletonRenderer"
            return cache(assembly, name, fields_tree(schemas.default,
                [(label, "PPtr<MonoBehaviour>") for label in ("SkinTextureAsset", "mPartHandler", renderer, "mTextureLoader")]))
        if name == "Spine.Unity.SkeletonRenderSeparator":
            return cache(assembly, name, fields_tree(schemas.default, separator_fields))
        if name == "AppearanceRendererPartHandler":
            return cache(assembly, name, fields_tree(schemas.default, [("IgnoreSkinTypes", "int[]")] + appearance_fields))
        if name == "Coffee.UIExtensions.UIParticle":
            result = clone(schemas.native.tree("UnityEngine.UI", "UnityEngine.UI.MaskableGraphic"))
            for label, kind in particle_fields:
                child = node(kind, label, 1)
                if label == "m_AnimatableProperties":
                    data = node("AnimatableProperty", "data", 0)
                    data.m_Children = [node("string", "m_Name", 1), node("int", "m_Type", 1)]
                    child = vector(label, data, 1)
                result.m_Children.append(child)
            return cache(assembly, name, result)
        if name.startswith(("UnityEngine.Rendering.", "UGUIJszx.")) or name in ("MyEquipAgent", "CameraSceneLayerCuller"):
            return cache(assembly, name, schemas.native.tree(assembly, name))
        if name in ("Spine.Unity.SkeletonPartsRenderer", "Cinemachine.CinemachinePipeline"):
            return cache(assembly, name, fields_tree(schemas.default, []))
        if name == "Spine.Unity.BoneFollower":
            return cache(assembly, name, fields_tree(schemas.default, bone_fields))
        if name == "Spine.Unity.SkeletonAnimation":
            result = spine_tree(schemas.default, "SkeletonMecanim")
            result.m_Children = [child for child in result.m_Children if child.m_Name not in ("translator", "updateTiming")]
            result.m_Children.extend(node(kind, label, 1) for label, kind in animation_fields)
            return cache(assembly, name, result)
        if name == "Spine.Unity.SkeletonRendererCustomMaterials":
            result = fields_tree(schemas.default, [("skeletonRenderer", "PPtr<MonoBehaviour>")])
            for label, kind, _, fields in material_arrays:
                data = node(kind, "data", 0)
                data.m_Children = [node(field_type, field_name, 1) for field_name, field_type in fields]
                result.m_Children.append(vector(label, data, 1))
            return cache(assembly, name, result)
        if name == "Spine.Extension.RenderExistingMesh":
            result = fields_tree(schemas.default, [("referenceRenderer", "PPtr<MeshRenderer>")])
            replacement = node("MaterialReplacement", "data", 0)
            replacement.m_Children = [node("PPtr<Material>", field, 1)
                                     for field in ("originalMaterial", "replacementMaterial")]
            result.m_Children.append(vector("replacementMaterials", replacement, 1))
            return cache(assembly, name, result)
        if name.startswith("UnityEngine.UI.") or name == "TMPro.TextMeshProUGUI":
            return cache(assembly, name, schemas.native.tree(assembly, name))
        if name == "Spine.Unity.SkeletonGraphic":
            result = clone(schemas.native.tree("UnityEngine.UI", "UnityEngine.UI.MaskableGraphic"))
            for field in graphic_fields:
                child = node(graphic_types[field], field, 1)
                if field == "meshGenerator":
                    settings = node("Settings", "settings", 2)
                    settings.m_Children = [node(kind, label, 3) for label, kind in mesh_settings]
                    child.m_Children = [settings]
                result.m_Children.append(child)
            return cache(assembly, name, result)
        if name == "AppearanceSystem.SpineExt.LoadAllAnimationsSkeletonDataModifier":
            return cache(assembly, name, fields_tree(schemas.default, []))
        if name == "AppearanceSystem.PhysicsConstraintSkeletonDataModifier":
            result = fields_tree(schemas.default, [("mSerializedPhysicsPriority", "int")])
            data = clone(original_tree("Assembly-CSharp", "AppearanceSystem.PhysicsPriorityData"))
            data.m_Children = [child for child in data.m_Children if child.m_Name not in ("m_GameObject", "m_Enabled", "m_Script", "m_Name")]
            result.m_Children.append(vector("mPhysicsPriorityDataList", data, 1))
            return cache(assembly, name, result)
        if name != "AppearanceSystem.Runtime.YooAssetTextureLoader":
            return original_tree(assembly, name)
        result = fields_tree(schemas.default, [("atlasAsset", "PPtr<MonoBehaviour>"), ("skeletonDataAsset", "PPtr<MonoBehaviour>"),
            ("maxPlaceholderSize", "int"), ("unloadAfterSecondsUnused", "float")])
        mapping = node("PlaceholderTextureMapping", "data", 0)
        mapping.m_Children = [node("PPtr<Texture>", "placeholderTexture", 1)]
        for label in ("targetTextureReference", "dyeTextureReference"):
            reference = node("YooAssetTextureReference", label, 1)
            proxy = node("SpineTextureAssetProxy", "AssetReference", 2)
            proxy.m_Children = [node("string", "mAssetGuid", 3)]
            reference.m_Children = [proxy]
            mapping.m_Children.append(reference)
        result.m_Children.append(vector("placeholderMap", mapping, 1))
        result.m_Children.extend(node(kind, label, 1) for label, kind in [("LevelOfResolution", "int"),
            ("PlaceholderPath", "string"), ("TargetTexturePath", "string"), ("DyeTexturePath", "string")])
        return cache(assembly, name, result)

    schemas.default.tree = tree
    sources = [code / "client-source/AppearanceSystem.Runtime" / (name + ".cs") for name in
               ["AppearanceOnDemandTextureLoader", "YooAssetTextureReference", "YooAssetTextureLoader"]]
    sources.append(code / "framework-source/SpineTextureAssetProxy.cs")
    sources.extend(code / "client-source/AppearanceSystem" / (name + ".cs") for name in
                   ["PhysicsConstraintSkeletonDataModifier", "PhysicsPriorityData"])
    sources.append(code / "client-source/AppearanceSystem.SpineExt/LoadAllAnimationsSkeletonDataModifier.cs")
    sources.extend(code / "client-source" / (name + ".cs") for name in
                   ("MyEquipAgent", "CameraSceneLayerCuller", "AppearancePartHandler", "AppearanceRendererPartHandler"))
    sources.extend(code / "client-source/AppearanceSystem.Runtime" / (name + ".cs") for name in
                   ("AppearanceSpine", "AppearanceRenderer", "AppearanceMecanim", "AppearanceUI"))
    sources.extend(code / "framework-source/UGUIJszx" / (name + ".cs") for name in
                   ("HorizontalOrVerticalLayoutGroupJSZX", "VerticalLayoutGroupJSZX"))
    return dict(image=dict(path=source_path(image), sha256=sha(image.read_bytes())),
                metadata=dict(path=source_path(metadata), sha256=sha(metadata.read_bytes())),
                metadata_registration=registration, type_count=values[6], type_table=table, types=proofs,
                native_reference_inputs=[dict(path=path.as_posix(), sha256=sha(path.read_bytes()))
                                         for path in schemas.reference_inputs],
                client_sources=[dict(path=source_path(path), sha256=sha(path.read_bytes())) for path in sources],
                limitation="Serialization schema only; does not implement appearance texture loading or animation.")
