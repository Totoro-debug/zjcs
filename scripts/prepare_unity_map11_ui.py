"""Restore the pinned Map11 goddess UI hierarchy and original serialized bindings."""
from __future__ import annotations

import json
import struct

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


PANELS = ("GoddessTouchDialog", "LostGoddessTouchDialog")
ICONS = ("6010_11_DialogIcon", "6011_11_DialogIcon")
OUTPUT = ROOT / "unity/Assets/Official/World/Map/11/UI"


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
    extra_trees, extra_sources, extra_resources, item_icons, icon_config_source, quality_sprite, ui_schemas = load_support_ui(apk, schemas)
    source.update(extra_trees)
    manifest["prefabs"].update(extra_sources)
    manifest["resources"].update(extra_resources)
    panel_keys = PANELS + ICONS + ("WorldCanvas",) + tuple(extra_trees)

    for key in panel_keys:
        root = apk.asset(manifest["prefabs"][key]["bundle"], key + ".prefab")
        paths, behaviours = {}, []

        def visit(obj, prefix=""):
            go = obj.read()
            path = prefix + "/" + go.m_Name
            paths[(obj.assets_file.name, obj.path_id)] = path
            for entry in go.m_Component:
                component = entry.component.deref()
                paths[(component.assets_file.name, component.path_id)] = path
                if component.type.name in ("Transform", "RectTransform"):
                    for child in component.read().m_Children:
                        visit(child.read().m_GameObject.deref(), path)
                elif component.type.name == "MonoBehaviour":
                    behaviours.append(component)

        visit(root)

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
            if script.m_ClassName in ("Image", "MirrorImage", "Mask"):
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
            elif script.m_ClassName == "HorizontalLayoutGroupJSZX":
                fields = resolve(ui_schemas.read(obj), obj)
                fields["spacing"] = fields["m_Spacing"]
                components.append({"panel": key, "path": paths[(obj.assets_file.name, obj.path_id)],
                                   "type": script.m_ClassName, "fields": fields,
                                   "source": apk.provenance(obj)})
            elif script.m_ClassName in (key, "StageDialog", "ConditionItem", "PropTextItem",
                                      "WorldDialogEntityContent", "GoddessDialogIcon", "SafeArea", "WorldPanel",
                                      "GameMatchMiniSwitchPanel", "PlayAsYouDownloadEntry", "WorldExploreBtn",
                                      "ChatQuickMsgEntryPanel", "ChatActionQuickMsgEntryPanel", "WorldMapCharActionEndBtn",
                                      "PlayerCombatRatingText", "PlayerBlessLevelViewController", "TeamMiniPanel", "ShowMapRequire",
                                      "ConfirmDialog", "CostItemConfirmDialog", "PlayerCostMoneyContent", "TopBarHandler", "ItemIcon"):
                components.append({"panel": key, "path": paths[(obj.assets_file.name, obj.path_id)],
                                   "type": script.m_ClassName, "fields": resolve(schemas.read(obj), obj),
                                   "source": apk.provenance(obj)})
            elif key in extra_trees and script.m_AssemblyName.removesuffix(".dll") == "UnityEngine.UI":
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
        panels.append({"key": key, "nodes": nodes})
        stack = [source[key]]
        while stack:
            node = stack.pop()
            sprite = (node.get("image") or {}).get("sprite") or {}
            if sprite.get("file"):
                sprites.setdefault(sprite["file"], sprite)
            stack.extend(node.get("children", []))

    for icon in item_icons:
        sprites.setdefault(icon["sprite"], icon.pop("metadata"))
    sprites.setdefault(quality_sprite["file"], quality_sprite)
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
    keys.extend("region_11_" + str(i) for i in range(77))
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
    if not required.issubset(texts):
        raise ValueError("Official goddess UI language keys are missing: " + str(required - texts.keys()))
    colors_object = apk.asset("scriptableconfig_modules_systemsetting", "/GlobalColors.asset")
    colors = schemas.read(colors_object)
    common, common_sources = extract_common(apk, OUTPUT)
    payload = {"manifest": VERSION, "mapClassId": 11, "panels": panels, "sprites": records,
               "commonSpriteAsset": common,
               "itemQualitySprite": quality_sprite["file"],
               "itemIcons": item_icons,
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
                                  "schemas": schemas.records,
                                  "nativeLayoutSchemas": ui_schemas.records,
                                  "nativeLayoutCode": [{"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path.read_bytes())}
                                      for path in [ROOT / ".tools/source-packages/official-code/framework-source/UGUIJszx" / (name + ".cs")
                                          for name in ("HorizontalLayoutGroupJSZX", "HorizontalOrVerticalLayoutGroupJSZX")]],
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
                                                                           "TeamMiniPanel", "ShowMapRequire", "PayMoneyChangeDialog", "MirrorImage")]]},
                       "output": {"path": "map11-ui.json", "sha256": sha256(data), "panels": len(panels), "sprites": len(records)}}
    write_generated(target, data, known)
    write_generated(previous, json_bytes(output_manifest), known)
    for directory in (OUTPUT, OUTPUT / "Sprites"):
        write_meta(directory, "DefaultImporter")
    for path in (target, previous):
        write_meta(path, "TextScriptImporter")
    print("Map11 goddess UI:", len(panels), "panels,", len(records), "sprites; SHA256", sha256(data))


if __name__ == "__main__":
    build()
