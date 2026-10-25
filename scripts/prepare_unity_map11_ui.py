"""Restore the pinned Map11 goddess UI hierarchy and original serialized bindings."""
from __future__ import annotations

import json
import struct
import os
import uuid
import io

from official_apk import Apk
from official_unity_schema import ScriptSchemas
from official_animation import mecanim_tracks
from official_shader_programs import gles_programs
from prepare_unity_map10_ui import flatten_nodes
from prepare_unity_map10_entities import ROOT, VERSION, sha256, json_bytes, write_meta, write_generated
from UnityPy.classes.PPtr import PPtr
import xxhash
from map11_support_ui import load_support_ui
from map11_tmp_sprites import extract_common
from map11_minimap_ui import extract_marks
from official_manifest import read_asset_manifest
from official_tables import Constructors


PANELS = ("GoddessTouchDialog", "LostGoddessTouchDialog")
ICONS = ("6010_11_DialogIcon", "6011_11_DialogIcon", "6620_11_DialogIcon")
OUTPUT = ROOT / "unity/Assets/Official/World/Map/11/UI"


def write_ui_payload(path, content, known):
    # Other isolated editors may hold a read mapping of the previous JSON. Replace
    # the file atomically after the same provenance check instead of truncating it.
    if path.exists():
        previous = path.read_bytes()
        if previous == content:
            return
        if sha256(previous) != known.get(path.resolve()):
            raise ValueError("Refusing to replace an unrecognized UI payload: " + str(path))
    temporary = path.with_name(path.name + "." + uuid.uuid4().hex + ".tmp")
    try:
        temporary.write_bytes(content)
        try:
            os.replace(temporary, path)
        except PermissionError:
            # Windows readers can disallow deletion while allowing a normal write.
            # Recheck the original receipt immediately before using that path.
            if not path.exists() or sha256(path.read_bytes()) != known.get(path.resolve()):
                raise
            path.write_bytes(content)
    finally:
        temporary.unlink(missing_ok=True)


def build():
    source_path = ROOT / "assets/official/map-ui.json"
    manifest_path = ROOT / "assets/reference/official-map/ui-source-manifest.json"
    raw = source_path.read_bytes()
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest["manifest"] != VERSION or manifest["output_sha256"] != sha256(raw):
        raise ValueError("Official map UI differs from the pinned source manifest")
    source = json.loads(raw)
    apk, schemas = Apk(), ScriptSchemas()
    panels, components, sprites, shader_programs = [], [], {}, {}
    extra_trees, extra_sources, extra_resources, item_icons, icon_config_source, quality_sprite, ui_schemas, support_exporter = load_support_ui(apk, schemas)
    source.update(extra_trees)
    manifest["prefabs"].update(extra_sources)
    manifest["resources"].update(extra_resources)
    panel_keys = PANELS + ICONS + ("WorldCanvas",) + tuple(extra_trees)

    for key in panel_keys:
        root = apk.asset(manifest["prefabs"][key]["bundle"], "/" + key + ".prefab")
        paths, behaviours, native_animations = {}, [], []
        sibling_counts = {}

        def visit(obj, prefix=""):
            go = obj.read()
            stem = prefix + "/" + go.m_Name
            duplicate = sibling_counts.get(stem, 0)
            sibling_counts[stem] = duplicate + 1
            path = stem + (f" ({duplicate})" if duplicate else "")
            paths[(obj.assets_file.name, obj.path_id)] = path
            for entry in go.m_Component:
                component = entry.component.deref()
                paths[(component.assets_file.name, component.path_id)] = path
                if component.type.name in ("Transform", "RectTransform"):
                    for child in component.read().m_Children:
                        visit(child.read().m_GameObject.deref(), path)
                elif component.type.name == "MonoBehaviour":
                    behaviours.append(component)
                elif component.type.name in ("Animation", "Animator") and (key in ("TalkDialog", "OptionTalkDialog")
                        or key == "WorldCanvas" and component.type.name == "Animator"
                        and go.m_Name in ("FunctionEntryFunc", "FunctionEntryPhoto", "FunctionEntryChatQuick")):
                    native_animations.append(component)
                elif component.type.name == "Canvas":
                    native = component.read_typetree()
                    components.append({"panel": key, "path": path, "type": "Canvas",
                                       "fields": {k: native[k] for k in ("m_Enabled", "m_RenderMode", "m_PlaneDistance", "m_OverrideSorting", "m_SortingOrder", "m_SortingLayerID", "m_AdditionalShaderChannelsFlag")},
                                       "source": apk.provenance(component)})
                elif component.type.name == "Camera":
                    native = component.read_typetree()
                    components.append({"panel": key, "path": path, "type": "Camera",
                                       "fields": {"nearClip": native["near clip plane"], "farClip": native["far clip plane"],
                                                  "orthographicSize": native["orthographic size"],
                                                  "backgroundColor": native["m_BackGroundColor"], "clearFlags": native["m_ClearFlags"]},
                                       "source": apk.provenance(component)})

        visit(root)

        def animation_record(animation, tree):
            native = animation.read_typetree()
            if native["m_Legacy"]:
                tracks, unknown = [], []
                for curve in native["m_FloatCurves"]:
                    path = tree["path"] + ("/" + curve["path"] if curve["path"] else "")
                    tracks.append({"path": path, "property": curve["attribute"], "keys": [
                        [k["time"], k["value"], k["inSlope"], k["outSlope"]] for k in curve["curve"]["m_Curve"]]})
                for field, prefix, axes in (("m_PositionCurves", "position.", "xyz"), ("m_ScaleCurves", "scale.", "xyz"),
                                             ("m_EulerCurves", "rotation.", "xyz"), ("m_RotationCurves", "quaternion.", "xyzw")):
                    for curve in native[field]:
                        path = tree["path"] + ("/" + curve["path"] if curve["path"] else "")
                        for axis in axes:
                            tracks.append({"path": path, "property": prefix + axis, "keys": [
                                [k["time"], k["value"][axis], k["inSlope"][axis], k["outSlope"][axis]] for k in curve["curve"]["m_Curve"]]})
                if native["m_PPtrCurves"] or native["m_CompressedRotationCurves"]:
                    raise ValueError("Unconverted native dialogue animation channels")
                duration = max((k[0] for t in tracks for k in t["keys"]), default=0)
                loop = native["m_WrapMode"] == 2
            else:
                tracks, duration, loop, unknown = mecanim_tracks(animation, tree)
            for track in tracks:
                track["keyframes"] = [dict(zip(("time", "value", "inTangent", "outTangent"),
                    [values[0], values[1], values[2] or 0, values[3] or 0])) for values in track["keys"]]
            return {"name": animation.read().m_Name, "duration": duration, "loop": loop,
                    "tracks": tracks, "unknown": unknown, "events": native["m_Events"], "source": apk.provenance(animation)}

        flat_source = flatten_nodes(source[key])
        def subtree(node, path):
            if node["path"] == path:
                return node
            return next((result for child in node.get("children", []) if (result := subtree(child, path)) is not None), None)
        for component in native_animations:
            location = paths[(component.assets_file.name, component.path_id)]
            default_name, auto_play = "", False
            initial_state = key == "WorldCanvas"
            controller_source = None
            if component.type.name == "Animation":
                animation_data = component.read()
                refs = animation_data.m_Animations
                auto_play = animation_data.m_PlayAutomatically
                if animation_data.m_Animation.m_PathID:
                    default_name = animation_data.m_Animation.read().m_Name
            else:
                controller = component.read().m_Controller
                if not controller.m_PathID:
                    continue
                refs = controller.read().m_AnimationClips
                if initial_state:
                    controller_data = controller.deref().read_typetree()
                    machines = controller_data["m_Controller"]["m_StateMachineArray"]
                    if len(machines) != 1:
                        raise ValueError("Unexpected function-entry Animator layers")
                    machine = machines[0]["data"]
                    state = machine["m_StateConstantArray"][machine["m_DefaultState"]]["data"]
                    leaves = [node["data"] for tree in state["m_BlendTreeConstantArray"] for node in tree["data"]["m_NodeArray"]]
                    if len(leaves) != 1 or leaves[0]["m_ChildIndices"]:
                        raise ValueError("Unexpected default function-entry blend tree")
                    refs = [refs[leaves[0]["m_ClipID"]]]
                    default_name = refs[0].read().m_Name
                    controller_source = apk.provenance(controller.deref())
            clips = [animation_record(ref.deref(), subtree(source[key], location)) for ref in refs if ref.m_PathID]
            components.append({"panel": key, "path": location, "type": "NativeAnimatorInitialState" if initial_state else "Native" + component.type.name,
                               "fields": {"clips": clips, "defaultClip": default_name, "playAutomatically": auto_play,
                                          "controllerSource": controller_source},
                               "source": apk.provenance(component)})

        def resolve(value, owner):
            if isinstance(value, dict):
                if "m_FileID" in value and "m_PathID" in value:
                    if not int(value["m_PathID"]):
                        return None
                    obj = PPtr(**value, assetsfile=owner.assets_file).deref()
                    return paths.get((obj.assets_file.name, obj.path_id),
                                     {"source": apk.provenance(obj), "type": obj.type.name})
                return {k: resolve(v, owner) for k, v in value.items()}
            if isinstance(value, list):
                return [resolve(v, owner) for v in value]
            return value

        for obj in behaviours:
            script = obj.read(check_read=False).m_Script.read()
            if script.m_ClassName == "RawImage" and key == "MiniMapMaskedUI":
                raw_image = obj.get_raw_data()
                fid, pid = struct.unpack_from("<iq", raw_image, 32)
                material_object = PPtr(m_FileID=fid, m_PathID=pid, assetsfile=obj.assets_file).deref()
                material = material_object.read()
                shader = material.m_Shader.deref()
                mask = dict(material.m_SavedProperties.m_TexEnvs)["_MaskTex"].m_Texture.deref()
                stream = io.BytesIO()
                mask.read().image.save(stream, format="PNG")
                raw_mask = stream.getvalue()
                mask_path = OUTPUT / "Sprites" / ("MiniMapUIMask-" + sha256(raw_mask)[:10] + ".png")
                write_generated(mask_path, raw_mask)
                uri = "res://" + mask_path.relative_to(ROOT).as_posix()
                image = mask.read().image
                sprites[uri] = {"width": image.width, "height": image.height, "ppu": 100, "file": uri}
                manifest["resources"][uri] = dict(apk.provenance(mask), sha256=sha256(raw_mask))
                shader_programs["UI/Masked"] = {"source": apk.provenance(shader), "programs": gles_programs(shader.read(), True)}
                components.append({"panel": key, "path": paths[(obj.assets_file.name, obj.path_id)], "type": "RawImage",
                                   "fields": {"maskTexture": uri, "material": {"name": material.m_Name, "shader": "UI/Masked"}},
                                   "source": apk.provenance(obj)})
            elif script.m_ClassName == "SlicedFilledImage":
                fields = ui_schemas.read(obj)
                sprite = support_exporter.add_sprite(PPtr(**fields["m_Sprite"], assetsfile=obj.assets_file).deref())
                sprites[sprite["file"]] = sprite
                components.append({"panel": key, "path": paths[(obj.assets_file.name, obj.path_id)], "type": "SlicedFilledImage",
                                   "fields": dict(resolve(fields, obj), sprite=sprite["file"]), "source": apk.provenance(obj)})
            elif script.m_ClassName in ("Image", "MirrorImage", "Mask"):
                component_data = obj.get_raw_data()
                if script.m_ClassName == "Mask":
                    if len(component_data) != 36 or component_data[32] not in (0, 1):
                        raise ValueError("Unexpected pinned UI Mask layout")
                    fields = {"showMaskGraphic": bool(component_data[32])}
                else:
                    if len(component_data) < 84 or component_data[60] not in (0, 1) or component_data[80] not in (0, 1):
                        raise ValueError("Unexpected pinned UI Image Graphic layout")
                    fields = {"color": list(struct.unpack_from("<4f", component_data, 44)),
                              "raycastTarget": bool(component_data[60]), "maskable": bool(component_data[80])}
                    if script.m_ClassName == "MirrorImage":
                        mirror = schemas.read(obj)
                        fields.update(mirrorType=mirror["m_mirrorType"], reversed=bool(mirror["mReversed"]))
                    material_file, material_id = struct.unpack_from("<iq", component_data, 32)
                    if material_id:
                        material = PPtr(m_FileID=material_file, m_PathID=material_id, assetsfile=obj.assets_file).deref()
                        mat = material.read()
                        fields["material"] = {"name": mat.m_Name, "shader": mat.m_Shader.read().m_ParsedForm.m_Name,
                                              "source": apk.provenance(material)}
                        shader = mat.m_Shader.deref()
                        if fields["material"]["shader"] == "UI/ColorShift":
                            shader_programs["UI/ColorShift"] = {"source": apk.provenance(shader), "programs": gles_programs(shader.read(), True),
                                                                "material": material.read_typetree()}
                components.append({"panel": key, "path": paths[(obj.assets_file.name, obj.path_id)],
                                   "type": script.m_ClassName, "fields": fields, "source": apk.provenance(obj)})
            elif script.m_ClassName == "PopupWindow":
                window = schemas.read(obj)
                clips = []
                for field in ("mShowClip", "mHideClip", "mLoopClip"):
                    reference = window.get(field) or {}
                    if not reference.get("m_PathID"):
                        continue
                    animation = PPtr(**reference, assetsfile=obj.assets_file).deref()
                    optional_paths = ()
                    if key in ("CostItemConfirmDialog", "PayMoneyChangeDialog"):
                        # These exact shared-clip paths are also present in the
                        # pinned map UI prefab closure, but absent in this dialog.
                        optional_paths = ("BgDialog3", "Content")
                        def contains_path(node, relative, prefix=""):
                            return prefix == relative or any(contains_path(child, relative,
                                prefix + ("/" if prefix else "") + child["name"]) for child in node.get("children", []))
                        original_ui = json.loads(raw)
                        for relative in optional_paths:
                            if not any(contains_path(tree, relative) for tree in original_ui.values()
                                       if isinstance(tree, dict) and "children" in tree):
                                raise ValueError("Unverified optional shared animation path: " + relative)
                    tracks, duration, loop, unknown = mecanim_tracks(animation, source[key], binding_paths=optional_paths)
                    for track in tracks:
                        track["keyframes"] = [dict(zip(("time", "value", "inTangent", "outTangent"), values))
                                              for values in track["keys"]]
                    clips.append({"field": field, "name": animation.read().m_Name, "duration": duration,
                                  "loop": loop, "tracks": tracks, "unknown": unknown,
                                  "optionalTargetPaths": optional_paths, "source": apk.provenance(animation)})
                components.append({"panel": key, "path": paths[(obj.assets_file.name, obj.path_id)], "type": "PopupWindow",
                                   "fields": {"clips": clips}, "source": apk.provenance(obj)})
            elif script.m_ClassName == "CanvasScaler" and key == "WorldCanvas":
                raw_scaler = obj.get_raw_data()
                if len(raw_scaler) != 80:
                    raise ValueError("Unexpected pinned world CanvasScaler layout")
                mode, ppu, factor, width, height, match, ratio = struct.unpack_from("<i4fif", raw_scaler, 32)
                components.append({"panel": key, "path": paths[(obj.assets_file.name, obj.path_id)],
                                   "type": "CanvasScaler", "fields": {"scaleMode": mode, "referencePixelsPerUnit": ppu,
                                   "scaleFactor": factor, "referenceResolution": [width, height],
                                   "screenMatchMode": match, "matchWidthOrHeight": ratio}, "source": apk.provenance(obj)})
            elif script.m_ClassName == "GridLayoutGroupJSZX":
                raw_fields = ui_schemas.read(obj)
                settings_ref = raw_fields["m_gridLayoutSettings"]
                settings = None
                if int(settings_ref["m_PathID"]):
                    settings_obj = PPtr(**settings_ref, assetsfile=obj.assets_file).deref()
                    settings = ui_schemas.read(settings_obj)
                    settings["source"] = apk.provenance(settings_obj)
                fields = resolve(raw_fields, obj)
                fields["gridSettings"] = settings
                components.append({"panel": key, "path": paths[(obj.assets_file.name, obj.path_id)],
                                   "type": script.m_ClassName, "fields": fields,
                                   "source": apk.provenance(obj)})
            elif script.m_ClassName in ("HorizontalLayoutGroupJSZX", "VerticalLayoutGroupJSZX"):
                fields = resolve(ui_schemas.read(obj), obj)
                fields["spacing"] = fields["m_Spacing"]
                components.append({"panel": key, "path": paths[(obj.assets_file.name, obj.path_id)],
                                   "type": script.m_ClassName, "fields": fields,
                                   "source": apk.provenance(obj)})
            elif script.m_ClassName == "WorldStageInteractionPreviewContentLayoutElement":
                components.append({"panel": key, "path": paths[(obj.assets_file.name, obj.path_id)],
                                   "type": script.m_ClassName, "fields": resolve(ui_schemas.read(obj), obj),
                                   "source": apk.provenance(obj)})
            elif script.m_ClassName in (key, "StageDialog", "ConditionItem", "PropTextItem",
                                      "WorldDialogEntityContent", "GoddessDialogIcon", "SafeArea", "WorldPanel",
                                      "GameMatchMiniSwitchPanel", "PlayAsYouDownloadEntry", "WorldExploreBtn",
                                      "ChatQuickMsgEntryPanel", "ChatActionQuickMsgEntryPanel", "WorldMapCharActionEndBtn",
                                      "PlayerCombatRatingText", "PlayerBlessLevelViewController", "TeamMiniPanel", "ShowMapRequire",
                                      "ConfirmDialog", "CostItemConfirmDialog", "PlayerCostMoneyContent", "TopBarHandler", "ItemIcon",
                                      "WorldWitchTouchUI", "StoryBookDialog", "PanelTitle", "PeriodicityInteractionStageUI",
                                      "WorldStageInteractionPreviewDialog", "WorldStageInteractionPreviewContent", "MultiLanguageText",
                                      "WorldStageInteractionPreviewContentLayoutElement",
                                      "WorldStageInteractionResultAwardStatusEffectPreview", "WorldStageInteractionResultAwardStatusEffectPreviewItem",
                                      "WorldStageInteractionResultModifyHpPercentPreview", "WorldStageInteractionResultSpawnEntityPreview",
                                      "WorldStageInteractionAwardPreview", "ItemAwardIcon", "MiniMapMaskedUILoader",
                                      "MiniMapMaskedUI", "AreaInfoUI", "TalkDialog", "OptionTalkDialogPanel",
                                      "TalkDialogCharacter", "TalkDialogContentSwitch", "GraphicCapsuleMeshClipper",
                                      "HeadCharacterPortrait", "PlayerExpSlider", "PlayerHpSlider", "PublicPlayerInfoUI",
                                      "TopBarItem", "MePlayerItemUI", "PinnedTaskPanel", "ShowMapRequireItemCount",
                                      "ShowMapRequireTaskProcess", "ShowMapRequireDemonInvade", "ShowMapArrestProcess",
                                      "TaskListItem", "TaskGoal", "TaskListRecommendCombatRatingItem", "TaskConditionItem",
                                      "MiniMapFullScreenPanel", "MiniMapCameraController", "UITouchInput", "MiniMapMarkUIFilter",
                                      "TeamChallengeIconEntry", "DemonInvadeIconEntry", "VoidFissureIconEntry",
                                      "LimitedTimeGiftIconEntry", "FirstPayIconEntry", "FightDemonBattlefieldAutoPushEndNotifyUI", "TipDotNodeLinker",
                                      "TeamMiniMemberItem", "FunctionEntrysPanel", "MapNpcTipHandler", "PublicMapBranchLineBtn",
                                      "FunctionEntryDialog", "TimeInfoUI", "FunctionEntryBigBtnItem", "MarkUIFilterEntryItem", "MiniMapMarkFilterItem", "TabItem", "TabObjectTransition", "MultiTextsControler", "MiniMapSwitchTabItem", "UIColorByLevel"):
                reader = ui_schemas if script.m_ClassName == "TabItem" else schemas
                try:
                    fields = resolve(reader.read(obj), obj)
                except Exception as error:
                    raise ValueError("Failed native component " + key + ":" + script.m_ClassName) from error
                if script.m_ClassName == "GraphicCapsuleMeshClipper":
                    for flag in ("Enable", "NeedLeftCircle", "NeedRightCircle"):
                        fields["mMeshClipperCapsuleArgs"][flag] = bool(fields["mMeshClipperCapsuleArgs"][flag])
                if script.m_ClassName == "UITouchInput":
                    fields["dragSettings"] = {k: v for k, v in fields["Data"].items() if k != "Target"}
                    for flag in ("Horizontal", "Vertical", "Inertia", "UseMultipleRectangleBorders"):
                        fields["dragSettings"][flag] = bool(fields["dragSettings"][flag])
                components.append({"panel": key, "path": paths[(obj.assets_file.name, obj.path_id)],
                                   "type": script.m_ClassName, "fields": fields,
                                   "source": apk.provenance(obj)})
            elif (key in extra_trees or script.m_ClassName == "Slider") and script.m_AssemblyName.removesuffix(".dll") == "UnityEngine.UI":
                fields = ui_schemas.read(obj)
                components.append({"panel": key, "path": paths[(obj.assets_file.name, obj.path_id)],
                                   "type": script.m_ClassName, "fields": resolve(fields, obj), "source": apk.provenance(obj)})
        nodes = flatten_nodes(source[key])
        node_by_path = {node["path"]: node for node in nodes}
        for component in (c for c in components if c["panel"] == key):
            node, fields = node_by_path[component["path"]], component["fields"]
            if component["type"] in ("Image", "MirrorImage"):
                if any(abs(a - b) > 0.00001 for a, b in zip(node["image_color"], fields["color"])):
                    raise ValueError("Official Image Graphic color layout differs: " + component["path"])
                node.update(image_graphic_flags=True, image_raycast_target=fields["raycastTarget"], image_maskable=fields["maskable"])
            elif component["type"] == "Mask":
                node.update(mask_graphic_flags=True, mask_show_graphic=fields["showMaskGraphic"])
            elif component["type"] == "MultiLanguageText":
                node["localization_key"] = fields["myKey"]
        panels.append({"key": key, "nodes": nodes})
        stack = [source[key]]
        while stack:
            node = stack.pop()
            sprite = (node.get("image") or {}).get("sprite") or {}
            if sprite.get("file"):
                sprites.setdefault(sprite["file"], sprite)
            stack.extend(node.get("children", []))

    # Native world-space player marker used by MiniMapCharacterManager. Its
    # SpriteRenderers and loop clip are independent of the UGUI panel hierarchy.
    marker_root = apk.asset("defaultpackage_assets_prefabs_map_common_minimap.bundle", "/MinimapCharacter.prefab")
    marker_nodes, marker_animations = [], []

    def marker_visit(obj, parent_index=-1, prefix=""):
        game_object = obj.read()
        path = prefix + "/" + game_object.m_Name
        index = len(marker_nodes)
        row = {"name": game_object.m_Name, "path": path, "parentIndex": parent_index,
               "active": bool(game_object.m_IsActive)}
        marker_nodes.append(row)
        children = []
        for entry in game_object.m_Component:
            component = entry.component.deref()
            if component.type.name == "Transform":
                transform = component.read()
                row.update(position={k: getattr(transform.m_LocalPosition, k) for k in "xyz"},
                           rotation={k: getattr(transform.m_LocalRotation, k) for k in "xyzw"},
                           scale={k: getattr(transform.m_LocalScale, k) for k in "xyz"})
                children = transform.m_Children
            elif component.type.name == "SpriteRenderer":
                renderer = component.read()
                materials = [ref.read() for ref in renderer.m_Materials]
                if any(material.m_Shader.read().m_ParsedForm.m_Name != "Sprites/Default" for material in materials):
                    raise ValueError("Unconverted native minimap character material")
                sprite = support_exporter.add_sprite(renderer.m_Sprite.deref())
                sprites[sprite["file"]] = sprite
                row.update(sprite=sprite["file"], color={k: getattr(renderer.m_Color, k) for k in "rgba"}, sortingOrder=renderer.m_SortingLayer * 1000 + renderer.m_SortingOrder,
                           flipX=bool(renderer.m_FlipX), flipY=bool(renderer.m_FlipY))
            elif component.type.name == "Animation":
                marker_animations.append((path, component))
            elif component.type.name == "Camera":
                row["camera"] = component.read_typetree()
        for child in children:
            marker_visit(child.read().m_GameObject.deref(), index, path)

    marker_visit(marker_root)
    for path, component in marker_animations:
        native = component.read_typetree()
        clips = [animation_record(PPtr(**ref, assetsfile=component.assets_file).deref(), {"path": path})
                 for ref in native["m_Animations"] if ref["m_PathID"]]
        components.append({"panel": "MinimapCharacter", "path": path, "type": "NativeAnimation",
                           "fields": {"clips": clips, "playAutomatically": bool(native["m_PlayAutomatically"])},
                           "source": apk.provenance(component)})

    mark_prefabs, mark_bindings, mark_components, mark_sources, mark_inputs = extract_marks(
        apk, schemas, support_exporter, animation_record, sprites)
    components.extend(mark_components)
    marks_object = apk.asset("config_luban", "/minimap_marks.g.bytes")
    mark_rows = Constructors().table(marks_object.read().m_Script.encode("utf-8", "surrogateescape"), "TbMiniMapMarkInfo")
    mark_rows = [row for row in mark_rows if 11 in (row["ShowOnMapClassId"] or []) or 11 in (row["ShowOnMapGroupId"] or [])]
    mark_sprite_objects = {obj.read().m_Name: obj for obj in apk.objects("defaultpackage_assets_image_mapworld_minimap_atlas.bundle")
                           if obj.type.name == "Sprite"}
    minimap_marks = []
    for row in mark_rows:
        sprite = support_exporter.add_sprite(mark_sprite_objects[row["Image"].rsplit("/", 1)[-1]])
        sprites[sprite["file"]] = sprite
        minimap_marks.append({"name": row["Name"], "sprite": sprite["file"], "description": row["Description"],
                              "showOnEntry": row["ShowOnFilterEntry"]})
    for icon in item_icons:
        sprites.setdefault(icon["sprite"], icon.pop("metadata"))
        sprites.setdefault(icon["qualitySprite"], icon.pop("qualityMetadata"))
    sprites.setdefault(quality_sprite["file"], quality_sprite)
    manifest["resources"].update(support_exporter.resources)
    records = []
    for uri, metadata in sorted(sprites.items()):
        original = ROOT / uri.removeprefix("res://")
        raw = original.read_bytes()
        provenance = manifest["resources"][uri]
        if sha256(raw) != provenance["sha256"]:
            raise ValueError("Map11 UI sprite differs from its source manifest: " + uri)
        target = OUTPUT / "Sprites" / original.name
        write_generated(target, raw)
        records.append({"uri": uri, "file": original.name,
                        "unityAssetPath": target.relative_to(ROOT / "unity").as_posix(),
                        "sha256": sha256(raw), "width": metadata["width"], "height": metadata["height"],
                        "border": metadata.get("border", [0, 0, 0, 0]),
                        "pivot": metadata.get("pivot", [0, 0]), "pixelsPerUnit": metadata.get("ppu", 100)})
    def states(component):
        refs = component["fields"]["references"]["RefIds"]
        configs = [r["data"]["mStateGameObjectData"]["GoConfig"] for r in refs
                   if r.get("type", {}).get("class") == "StateGameObjectConfig"]
        if len(configs) != 1:
            raise ValueError("Expected one official StateGameObjectConfig: " + component["type"])
        return [{"state": int(row["State"]["mValue"]), "paths": row["GameObjects"]} for row in configs[0]]

    bindings = []
    for key in PANELS:
        component = next(c for c in components if c["panel"] == key and c["type"] == key)
        f = component["fields"]
        condition = next(c for c in components if c["panel"] == key and c["path"] == f["Condition"])
        stage = next(c for c in components if c["panel"] == key and c["type"] == "StageDialog")
        content = next(c for c in components if c["panel"] == key and c["type"] == "WorldDialogEntityContent")
        bindings.append({"key": key, "touchButton": f["myTouchBtn"], "cannotButton": f["CannotActiveBtn"],
                         "transferButton": f["mTransferBtn"], "title": stage["fields"]["myStageName"],
                         "conditionText": condition["fields"]["mDesc"], "states": states(component),
                         "conditionStates": states(condition), "propTemplate": f.get("mPropPrefab"),
                         "propParent": f.get("mPropPrefabSlot"), "blessingText": f.get("mBlessingLevelTxt"),
                         "treasureParent": f.get("TreasureAwardSlot"),
                         "iconSlot": content["fields"]["mIconSlot"]["mPreviewSlot"],
                         "iconKey": ICONS[0 if key == PANELS[0] else 1]})
    icon_bindings = [{"key": c["panel"], "path": c["path"], "states": states(c)}
                     for c in components if c["type"] == "GoddessDialogIcon"]
    world = next(c["fields"] for c in components if c["type"] == "WorldPanel")
    world_bindings = {
        "withoutCharacter": world["myWithoutCharacterUIs"], "withCharacter": world["myWithCharacterUIs"],
        "leader": world["myLeaderUIs"], "member": world["myMemberUIs"], "hideUi": world["myHideUI"],
        "safeAreas": [{"path": c["path"], "paddingTop": c["fields"]["PaddingTop"],
                       "paddingBottom": c["fields"]["PaddingBottom"], "enabled": bool(c["fields"]["m_Enabled"])}
                      for c in components if c["type"] == "SafeArea" and c["panel"] == "WorldCanvas"],
        "idleMatchContents": [c["fields"]["mContentGo"] for c in components if c["type"] == "GameMatchMiniSwitchPanel"],
        "downloadContents": [c["fields"]["mRoot"] for c in components if c["type"] == "PlayAsYouDownloadEntry"],
        "canvasScaler": next(c["fields"] for c in components if c["type"] == "CanvasScaler"),
        "exploreButtons": [{"path": c["path"], "isOneTime": bool(c["fields"]["mIsExploreOneTimes"]),
                            "states": states(c)} for c in components if c["type"] == "WorldExploreBtn"],
        "chatActionExpanded": [path for c in components if c["type"] == "ChatActionQuickMsgEntryPanel"
                               for path in c["fields"]["mExpendObjs"]],
        "quickMessageMasks": [c["fields"]["OpenBtnCDMask"] for c in components if c["type"] == "ChatQuickMsgEntryPanel"],
        "actionEndButtons": [c["fields"]["mEndBtn"] for c in components if c["type"] == "WorldMapCharActionEndBtn"],
        "teamPanels": [{"path": c["path"], "states": states(c)} for c in components if c["type"] == "TeamMiniPanel"],
    }
    language_manifest = ROOT / "assets/reference/official-map/localization-source-manifest.json"
    language_inputs = json.loads(language_manifest.read_text(encoding="utf-8"))["inputs"]
    keys = ["entity_6010_name", "entity_6011_name", "ui_GoddessTouchDialog_level_text",
            "ui_goddess_region_progress_100_text", "STAGE_CONDITION_REGION_PROGRESS_NOT_FULL",
            "PropType.HangUpExpAddPercent", "PropType.HangUpMoneyAddPercent", "item_1_name", "suit_tip_with_parenthesis"]
    keys.extend(("ui_witch_book_collect", "ui_map_region_rank_suppression_text"))
    keys.extend("ui_witch_result_" + str(i) for i in range(14))
    keys.extend("ui_witch_content_" + str(i) for i in range(14))
    keys.extend("story_book_" + str(i) + "_title" for i in range(1, 15))
    keys.extend("story_book_" + str(i) + "_content" for i in range(1, 15))
    keys.extend(node["localization_key"] for panel in panels for node in panel["nodes"] if node["localization_key"])
    keys.extend(("entity_6620_name", "entity_6620_desc", "map_periodicity_interaction_tips_1",
                 "map_entity_group_explore_desc_2000", "ui_modify_demage_Hp", "ui_get_buff_desc"))
    keys.extend("map_periodicity_random_award_interaction_" + suffix + "_" + str(i)
                for i in range(1, 8) for suffix in ("title", "desc"))
    keys.extend("entry_" + str(i) + "_" + suffix for i in (28500, 28501, 28502, 28503, 28504, 28505,
                28510, 28511, 28512, 28513, 28520) for suffix in ("name", "desc"))
    keys.extend("region_11_" + str(i) for i in range(77))
    keys.extend(("map_pos_info", "Mainland_11"))
    keys.extend(row["description"] for row in minimap_marks)
    keys.extend("task_" + str(i) + suffix for i in range(1000, 1161, 5) for suffix in ("_title", "_progress", "_desc"))
    keys.extend(("confirmation", "confirm", "cancel", "GachaPanel.ReplaceRaffleTicket",
                 "ItemInfoDialog.Limit.QuickBuyConditionNotMet", "ui_ItemIcon_count_key",
                 "common_format_args1", "ui_common_item_not_enough", "item_325_name", "item_2_name", "item_3_name",
                 "UI_Pay_ChangeTitle", "UI_Pay_Exchange_Diamond_Confirm"))
    wanted = {xxhash.xxh64_intdigest(key.encode()): key for key in keys}
    texts = {}
    for record in language_inputs.values():
        raw = apk.archive.read(record["source"]["path"])
        if sha256(raw) != record["sha256"]:
            raise ValueError("Pinned APK language data differs from its recorded hash")
        count = struct.unpack_from("<i", raw)[0]
        start = 4 + count * 12
        for i in range(count):
            identity, offset = struct.unpack_from("<Qi", raw, 4 + i * 12)
            if identity not in wanted:
                continue
            pos = start + offset
            size = struct.unpack_from("<H", raw, pos)[0]
            texts[wanted[identity]] = raw[pos + 2:pos + 2 + size].decode("utf-8")
    required = set(keys[:9])
    required.update(node["localization_key"] for panel in panels for node in panel["nodes"] if node["localization_key"])
    # MultiLanguageText also accepts these literal format strings in the pinned prefabs.
    for literal in ("{0}", "{0}/{1}", "{0}<color=#E5A23D>+{1}</color>"):
        if literal in required:
            texts[literal] = literal
    if not required.issubset(texts):
        raise ValueError("Official goddess UI language keys are missing: " + str(required - texts.keys()))
    for panel in panels:
        for node in panel["nodes"]:
            if node["localization_key"]:
                node["text_value"] = texts[node["localization_key"]]
    guid_rows = {row["guid"]: row for row in read_asset_manifest(apk.source.manifest_bytes("DefaultPackage", apk.archive))}
    preview_guids = {row["Value"]["mAssetGuid"] for component in components
                     if component["type"] == "WorldStageInteractionPreviewContent"
                     for row in component["fields"]["mStagePreviewAssetGuidProxy"]["mSerializeBuffer"]}
    guid_prefabs = [{"guid": guid, "key": guid_rows[guid]["path"].rsplit("/", 1)[-1].removesuffix(".prefab")}
                   for guid in sorted(preview_guids)]
    # The extra-prop preview is not used by the WitchInfo rows, but its GUID remains an original binding.
    if any(row["key"] not in panel_keys and row["key"] != "WorldStageInteractionResultAwardExtraPropPreview" for row in guid_prefabs):
        raise ValueError("A witch preview GUID has no extracted native prefab.")
    colors_object = apk.asset("scriptableconfig_modules_systemsetting", "/GlobalColors.asset")
    colors = schemas.read(colors_object)
    common, common_sources = extract_common(apk, OUTPUT)
    minimap_settings = apk.asset("scriptableconfig_modules_mapcommon", "/MiniMapSettings.asset")
    settings = schemas.read(minimap_settings)
    world_table = apk.asset("config_luban_map", "/map.g.bytes")
    world_row = next(row for row in Constructors().table(
        world_table.read().m_Script.encode("utf-8", "surrogateescape"), "TbMapInfo") if row["ClassId"] == 11)
    minimap_setting = next(row for row in settings["Settings"] if row["MinimapConfigId"] == world_row["MinimapConfigId"])
    portrait_settings = apk.asset("scriptableconfig_modules_playerportrait", "/PlayerPortraitSettings.asset")
    head_settings = next(row for row in schemas.read(portrait_settings)["mPhotographyParameters"] if row["MyTypes"] == [0])
    game_object = apk.asset("config_luban", "/game_settings.g.bytes")
    game_settings = Constructors().table(game_object.read().m_Script.encode("utf-8", "surrogateescape"), "TbGameSettings")
    game_settings = game_settings[0] if isinstance(game_settings, list) else game_settings
    rating_config = apk.asset("scriptableconfig_modules_combatrating", "/CombatRatingConfig.asset")
    rating_fields = schemas.read(rating_config)
    rating_colors = [{"type": row["Key"], "colors": rating_fields["mColorTemplates"][row["Value"]]["LevelColors"]["mSerializeBuffer"]}
                     for row in rating_fields["mTypeToThemes"]["mSerializeBuffer"]]
    item_settings = [{"type": 4, "itemId": game_settings["FoodId"]}, {"type": 2, "itemId": game_settings["TiliId"]}]
    tili = next(row for row in Constructors().table(
        apk.asset("config_luban", "/item.g.bytes").read().m_Script.encode("utf-8", "surrogateescape"), "TbItemInfo")
        if row["ClassId"] == game_settings["TiliId"])
    item_settings[1]["maxCount"] = next(row["MaxCount"] for row in tili["Effects"] if row["_type"] == "ItemEffectAutoIncrease")
    payload = {"manifest": VERSION, "mapClassId": 11, "panels": panels, "sprites": records,
               "recommendCombatRatingIndex": game_settings["RecommendCombatRatingIndex"],
               "suppressUnlockRank": game_settings["SuppressUnlockRank"], "combatRatingColors": rating_colors,
               "minimapSettings": minimap_setting,
               "minimapCharacter": marker_nodes, "minimapMarks": minimap_marks,
               "minimapMarkPrefabs": mark_prefabs, "minimapMarkBindings": mark_bindings,
               "headPortraitSettings": head_settings,
               "topBarItems": item_settings,
               "commonSpriteAsset": common,
               "itemQualitySprite": quality_sprite["file"],
               "itemIcons": item_icons,
               "guidPrefabs": guid_prefabs,
               "enoughColor": [colors["EnoughColor"][channel] for channel in "rgba"],
               "lackColor": [colors["LackColor"][channel] for channel in "rgba"],
               "bindings": bindings, "iconBindings": icon_bindings, "worldBindings": world_bindings,
               "texts": [{"key": k, "value": v} for k, v in sorted(texts.items())],
               "sourceUiSha256": sha256(source_path.read_bytes()), "components": components,
               "shaderPrograms": shader_programs}
    data = json_bytes(payload)
    target = OUTPUT / "map11-ui.json"
    # This file is a deterministic source conversion; verify any previous version before replacing it.
    previous = OUTPUT / "source-manifest.json"
    known = {}
    if previous.exists():
        old = json.loads(previous.read_text(encoding="utf-8"))
        known[target.resolve()] = old["output"]["sha256"]
        known[previous.resolve()] = sha256(previous.read_bytes())
    output_manifest = {"manifest": VERSION, "mapClassId": 11,
                       "operation": "Preserve official prefab nodes, sprites, fields and managed StateSwitcher references; restore native Unity prefabs with explicit component bindings.",
                       "source": {"path": source_path.relative_to(ROOT).as_posix(), "sha256": payload["sourceUiSha256"],
                                  "manifestPath": manifest_path.relative_to(ROOT).as_posix(), "manifestSha256": sha256(manifest_path.read_bytes()),
                                  "prefabs": {k: manifest["prefabs"][k] for k in panel_keys},
                                  "itemIconConfig": icon_config_source,
                                  "commonSpriteAsset": common_sources,
                                  "minimapSettings": apk.provenance(minimap_settings),
                                  "minimapCharacter": apk.provenance(marker_root),
                                  "minimapMarks": apk.provenance(marks_object),
                                  "minimapMarkPrefabs": mark_sources,
                                  "minimapMarkInputs": [{"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path.read_bytes())} for path in mark_inputs],
                                  "headPortraitSettings": apk.provenance(portrait_settings),
                                  "topBarItemSettings": apk.provenance(game_object),
                                  "combatRatingConfig": apk.provenance(rating_config),
                                  "minimapWorldConfig": apk.provenance(world_table),
                                  "schemas": schemas.records,
                                  "nativeLayoutSchemas": ui_schemas.records,
                                  "nativeLayoutCode": [{"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path.read_bytes())}
                                      for path in [ROOT / ".tools/source-packages/official-code/framework-source/UGUIJszx" / (name + ".cs")
                                          for name in ("HorizontalLayoutGroupJSZX", "VerticalLayoutGroupJSZX", "HorizontalOrVerticalLayoutGroupJSZX",
                                                       "GridLayoutGroupJSZX", "GridLayoutSettings", "GridLayoutSetting")]],
                                  "numberFormatCode": [{"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path.read_bytes())}
                                      for path in [ROOT / ".tools/source-packages/official-code/framework-firstpass-source/Framework" / (name + ".cs")
                                          for name in ("NumberWithSizeArg", "CultureNumberFormatterBase", "NumberCultureFormatter_CN", "NumberFormatHelper")]],
                                  "language": language_inputs,
                                  "globalColors": apk.provenance(colors_object),
                                  "sprites": {uri: manifest["resources"][uri] for uri in sorted(sprites)},
                                  "portedCode": [{"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path.read_bytes())}
                                                 for path in [ROOT / ".tools/source-packages/official-code" /
                                                              ("framework-firstpass-source" if name in ("SafeArea", "PopupWindow", "MirrorImage") else "client-source") / (name + ".cs")
                                                              for name in ("GoddessTouchDialog", "LostGoddessTouchDialog", "StageTouchUI", "StageDialog",
                                                                           "WorldDialogEntityContent", "GoddessDialogIcon", "MapDialogIconPreviewSlot",
                                                                           "WorldPanel", "SafeArea", "PopupWindow", "GameMatchMiniSwitchPanel", "PlayAsYouDownloadEntry",
                                                                           "WorldExploreBtn", "ChatQuickMsgEntryPanel", "ChatActionQuickMsgEntryPanel",
                                                                           "WorldMapCharActionEndBtn", "PlayerCombatRatingText", "PlayerBlessLevelViewController",
                                                                           "TeamMiniPanel", "ShowMapRequire", "PayMoneyChangeDialog", "MirrorImage",
                                                                           "WorldWitchTouchUI", "StoryBookDialog", "PeriodicityInteractionStageUI",
                                                                           "WorldStageInteractionPreviewDialog", "WorldStageInteractionPreviewContent", "WorldStageInteractionPreviewContentLayoutElement",
                                                                           "WorldStageInteractionPreparePlayableDialog", "WorldStageInteractionAwardPreview",
                                                                           "WorldStageInteractionResultAwardStatusEffectPreview", "WorldStageInteractionResultAwardStatusEffectPreviewItem",
                                                                           "WorldStageInteractionResultModifyHpPercentPreview", "WorldStageInteractionResultSpawnEntityPreview", "ItemAwardIcon",
                                                                           "TalkDialog", "OptionTalkDialogPanel", "TalkDialogCharacterController", "TalkDialogCharacter", "TalkDialogContentSwitch", "TMP_TypeWriterEffect",
                                                                           "MiniMapMaskedUILoader", "MiniMapMaskedUI", "AreaInfoUI", "MiniMapCameraController", "MiniMapFullScreenPanel", "MiniMapSwitchTabItem",
                                                                           "MiniMapMarkUIFilter", "MiniMapMarkFilterItem", "MiniMapMarkManager", "MiniMapEntityMark", "MinimapMarkKeepSizeObjSupport",
                                                                           "HeadCharacterPortrait", "HeadCharacterPortraitBuilder", "PlayerExpSlider", "PlayerIconMeHpSlider",
                                                                           "TaskListItem", "TaskGoal", "TaskListRecommendCombatRatingItem", "UIColorByLevel", "CombatRatingConfig",
                                                                           "FunctionEntrysPanel", "FunctionEntryDialog", "FunctionEntryBigBtnItem", "FirstPayIconEntry", "TimeInfoUI", "PlayerDefaultItemUIHandler", "MePlayerItemUI")]]},
                       "output": {"path": "map11-ui.json", "sha256": sha256(data), "panels": len(panels), "sprites": len(records)}}
    write_ui_payload(target, data, known)
    write_ui_payload(previous, json_bytes(output_manifest), known)
    for directory in (OUTPUT, OUTPUT / "Sprites"):
        write_meta(directory, "DefaultImporter")
    for path in (target, previous):
        write_meta(path, "TextScriptImporter")
    print("Map11 official UI:", len(panels), "panels,", len(records), "sprites; SHA256", sha256(data))


if __name__ == "__main__":
    build()
