"""Restore serialized Image and TMP data removed by the current AssetRipper export."""
from __future__ import annotations

import hashlib
import io
import json
from pathlib import Path
import re
import struct
import sys

ROOT = Path(__file__).resolve().parents[1]
VERSION = "871_156598"
CODE = ROOT / ".tools/source-packages/current" / VERSION / "official-code"
RUNTIME = ROOT / "unity/Assets/StreamingAssets/Dungeons/UI"
BINDINGS = RUNTIME / "official-bindings.json"
RECEIPT = ROOT / "assets/reference/dungeon-ui/prefab-binding-source-manifest.json"
PREFABS = (
    ("TravelNotesPanel", "prefabs_ui_home", "/TravelNotesPanel.prefab"),
    ("DailyDungeonEntryPanel", "prefabs_ui_dailydungeon", "/DailyDungeonEntryPanel.prefab"),
    ("DailyDungeonEntryPanelItem", "prefabs_ui_dailydungeon", "/DailyDungeonEntryPanelItem.prefab"),
    ("DailyDungeonConfirmEnterPanel", "prefabs_ui_dailydungeon", "/DailyDungeonConfirmEnterPanel.prefab"),
    ("DailyDungeonDialog", "prefabs_ui_dailydungeon", "/DailyDungeonDialog.prefab"),
    ("DailyDungeonDialogPrivateMap", "prefabs_ui_dailydungeon", "/DailyDungeonDialogPrivateMap.prefab"),
    ("DailyDungeonBigMapDialog", "prefabs_ui_dailydungeon", "/DailyDungeonBigMapDialog.prefab"),
    ("DailyDungeonGradeItem", "prefabs_ui_dailydungeon", "/DailyDungeonGradeItem.prefab"),
    ("DailyDungeonBigMapRoomItem", "prefabs_ui_dailydungeon", "/DailyDungeonBigMapRoomItem.prefab"),
    ("DailyDungeonRoomItem", "prefabs_ui_dailydungeon", "/DailyDungeonRoomItem.prefab"),
    ("ArenaPanel", "prefabs_ui_arena", "/ArenaPanel.prefab"),
)
GUID = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def relative_transform_path(game_object, path=(), names=()):
    transform = next((pair.component.read() for pair in game_object.m_Component
                      if pair.component.type.name in ("RectTransform", "Transform")), None)
    yield game_object, path, names + (game_object.m_Name,), transform
    if transform is None:
        return
    for index, child in enumerate(transform.m_Children):
        yield from relative_transform_path(child.read().m_GameObject.read(), path + (index,),
                                           names + (game_object.m_Name,))


def sprite_binding(sprite_cache, assets_file, file_id, path_id):
    from prepare_dungeon_ui_sprites import asset_meta, ensure_folder_meta
    sprite = sprite_cache.sprite_image(assets_file, file_id, path_id)
    if sprite is None:
        raise ValueError("Official UI Sprite could not be resolved: "
                         + str(path_id))
    source_object = sprite_cache.lookup(assets_file, file_id, path_id)
    source_key = source_object.assets_file.name + ":" + str(source_object.path_id)
    provenance = sprite_cache.sources[source_key]
    source_bundle = provenance["bundle"]
    sprite_key = source_bundle + ":" + str(source_object.path_id)
    filename = re.sub(r"[^A-Za-z0-9_.-]+", "_", sprite["name"]).strip("._")
    filename = (filename or "sprite") + "-" + sha256(sprite_key.encode())[:12] + ".png"
    relative = (Path("Sprites") / source_bundle.removesuffix(".bundle") / filename)
    target = RUNTIME / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    ensure_folder_meta(target.parent)
    buffer = io.BytesIO()
    sprite["image"].save(buffer, format="PNG", optimize=False)
    payload = buffer.getvalue()
    if target.exists() and target.read_bytes() != payload:
        raise ValueError("Refusing to overwrite changed dungeon UI sprite: " + str(target))
    if not target.exists():
        target.write_bytes(payload)
    source_guid = hashlib.sha256(("zjcs-dungeon-ui-bound-sprite:" + sprite_key)
                                 .encode()).hexdigest()[:32]
    meta = target.with_name(target.name + ".meta")
    meta_bytes = asset_meta(source_guid)
    if meta.exists():
        existing = GUID.search(meta.read_text(encoding="ascii"))
        if existing is None or existing.group(1) != source_guid:
            raise ValueError("Dungeon UI sprite GUID changed: " + str(meta))
    else:
        meta.write_bytes(meta_bytes)
    return {
        "file": relative.as_posix(),
        "name": sprite["name"],
        "pivot": sprite["pivot"],
        "border": sprite["border"],
        "pixelsPerUnit": sprite["ppu"],
        "source": provenance,
        "sha256": sha256(payload),
    }


def button_binding(reader, path_key, object_names, reference_path):
    raw = reader.get_raw_data()
    if len(raw) < 232: raise ValueError("Truncated official route Button")
    if any(struct.unpack_from("<iq", raw, offset)[1] for offset in (40, 52, 64, 76)):
        raise ValueError("Unbound route navigation target")
    if any(struct.unpack_from("<iq", raw, offset)[1] for offset in (180, 192, 204, 216)):
        raise ValueError("Unbound route button SpriteState")
    offset, triggers = 228, []
    for _ in range(5):
        length = struct.unpack_from("<i", raw, offset)[0]
        if length < 0 or offset + 4 + length > len(raw): raise ValueError("Invalid route Button trigger")
        triggers.append(raw[offset+4:offset+4+length].decode("utf8"))
        offset = (offset + 4 + length + 3) & ~3
    interactable = struct.unpack_from("<i", raw, offset)[0]
    file_id, path_id = struct.unpack_from("<iq", raw, offset+4)
    if offset+20 != len(raw) or struct.unpack_from("<i", raw, offset+16)[0]:
        raise ValueError("Unbound official route Button event")
    return dict(path=path_key, objects=object_names,
        enabled=bool(reader.read(check_read=False).m_Enabled),
        navigation=struct.unpack_from("<i", raw, 32)[0], wrapAround=bool(struct.unpack_from("<i", raw, 36)[0]),
        transition=struct.unpack_from("<i", raw, 88)[0], colors=list(struct.unpack_from("<22f", raw, 92)),
        triggers=triggers, interactable=bool(interactable),
        target=reference_path({"m_FileID":file_id, "m_PathID":path_id}))


def private_mask_binding(apk, schemas):
    """Resolve the original WorldCanvas mask and pin the existing desktop conversion."""
    from UnityPy.classes.PPtr import PPtr
    prefab = apk.asset("prefabs_ui_map_world", "/WorldCanvas.prefab")
    nodes = list(relative_transform_path(prefab.read()))
    mask_nodes = [(go, names) for go, _, names, _ in nodes if "/".join(names[1:]) == "Content/Popup/BlurMask"]
    if len(mask_nodes) != 1:
        raise ValueError("Original private popup BlurMask hierarchy is ambiguous")
    root, root_names = mask_nodes[0]
    readers = [pair.component.deref() for pair in root.m_Component if pair.component.type.name == "MonoBehaviour"
               and pair.component.deref().read(check_read=False).m_Script.read().m_ClassName == "WindowBlurMask"]
    if len(readers) != 1:
        raise ValueError("Original WindowBlurMask component is missing")
    reader = readers[0]
    fields = schemas.read(reader)
    if fields["mMaskType"] != 3 or fields["mMaskImg"]["m_PathID"]:
        raise ValueError("Original private popup mask type or optional Image changed")
    names_by_id = {go.object_reader.path_id: names for go, _, names, _ in nodes}
    def reference(field, expected):
        pointer = PPtr(**fields[field], assetsfile=reader.assets_file)
        target = pointer.deref()
        if target.read(check_read=False).m_Script.read().m_ClassName != expected:
            raise ValueError("Original private popup mask reference type changed: " + field)
        names = names_by_id[target.read(check_read=False).m_GameObject.path_id]
        if names[:len(root_names)] != root_names:
            raise ValueError("Original private popup mask has an external hierarchy reference")
        return "/".join(names[len(root_names):])
    native_receipt_path = ROOT / "assets/reference/official-map/map12-ui-native-source-manifest.json"
    native_receipt = json.loads(native_receipt_path.read_bytes())
    if native_receipt["manifest"] != VERSION or native_receipt["mapClassId"] != 12:
        raise ValueError("Original private popup mask conversion uses another source version")
    records = {row["path"]: row for row in native_receipt["outputs"]}
    native_paths = ["unity/Assets/Game/Resources/Map12/UI/WorldCanvas.prefab", "unity/Assets/Game/Resources/Map12/UI/WorldCanvas.prefab.meta"]
    blur_receipt_path = ROOT / "assets/reference/official-map/popup-effects-source-manifest.json"
    blur_receipt = json.loads(blur_receipt_path.read_bytes())
    apk.load_builtin("assets/bin/Data/data.unity3d")
    builtin = apk.env.get_cab("unity_builtin_extra")
    shader = builtin.objects[int(blur_receipt["shader"]["path_id"])]
    if shader.type.name != "Shader" or shader.read().m_ParsedForm.m_Name != "CustomRenderTexture/ScreenBlur" or sha256(shader.get_raw_data()) != blur_receipt["shader"]["objectSha256"]:
        raise ValueError("Current official ScreenBlur differs from the recorded desktop conversion")
    records.update({row["path"]: row for row in blur_receipt["outputs"]})
    native_paths += ["unity/Assets/Game/Runtime/Presentation/World/Shaders/ScreenBlur.shader", "unity/Assets/Game/Resources/World/Mainline/ScreenBlur.mat"]
    assets = []
    for path in native_paths:
        payload = (ROOT / path).read_bytes()
        if sha256(payload) != records[path]["sha256"]:
            raise ValueError("Private popup mask native output changed after conversion: " + path)
        assets.append(dict(path=path, sha256=sha256(payload)))
    mask_nodes = list(relative_transform_path(root))
    local_paths = {go.object_reader.path_id: ("/".join(map(str, path)), list(names)) for go, path, names, _ in mask_nodes}
    def button_target(pointer):
        if not pointer["m_PathID"]:
            return None
        target = PPtr(**pointer, assetsfile=reader.assets_file).deref().read(check_read=False)
        return local_paths[target.m_GameObject.path_id][0]
    button_reader = PPtr(**fields["mBtn"], assetsfile=reader.assets_file).deref()
    button_path, button_names = local_paths[button_reader.read(check_read=False).m_GameObject.path_id]
    buttons = [button_binding(button_reader, button_path, button_names, button_target)]
    return dict(canvasResource="Map12/UI/WorldCanvas", path="/".join(root_names[1:]),
        image=reference("mImage", "RawImage"), button=reference("mBtn", "Button"),
        buttons=buttons, materialResource="World/Mainline/ScreenBlur", enabled=bool(reader.read(check_read=False).m_Enabled),
        source=apk.provenance(reader), prefab_source=apk.provenance(prefab), shader_source=apk.provenance(shader),
        native_assets=assets, receipts=[dict(path=path.relative_to(ROOT).as_posix(), sha256=sha256(path.read_bytes()))
            for path in (native_receipt_path, blur_receipt_path)])


def private_popup_binding(apk, schemas, reader, root, path):
    """Reuse the native common clips only after checking the selected source curves."""
    import math
    import yaml
    from UnityPy.classes.PPtr import PPtr
    from official_animation import mecanim_tracks

    fields = schemas.read(reader)
    if path:
        raise ValueError("Private popup must remain on its original prefab root")
    receipt_path = ROOT / "assets/reference/official-map/map12-ui-native-source-manifest.json"
    receipt = json.loads(receipt_path.read_bytes())
    if receipt["manifest"] != VERSION or receipt["mapClassId"] != 12:
        raise ValueError("Private popup animation receipt uses another source version")
    outputs = {row["path"]: row for row in receipt["outputs"]}
    def tree(game_object, relative=""):
        transform = next(pair.component.read() for pair in game_object.m_Component
                         if pair.component.type.name in ("RectTransform", "Transform"))
        return dict(name=game_object.m_Name, path=relative, children=[tree(child.read().m_GameObject.read(),
            relative + ("/" if relative else "") + child.read().m_GameObject.read().m_Name)
            for child in transform.m_Children])
    hierarchy = tree(root)
    clips = []
    for field in ("mShowClip", "mHideClip", "mLoopClip"):
        pointer = fields[field]
        if not pointer["m_PathID"]:
            continue
        obj = PPtr(**pointer, assetsfile=reader.assets_file).deref()
        name = obj.read().m_Name
        if (field, name) not in (("mShowClip", "UIDialogCommon_Show"), ("mHideClip", "UIDialogCommon_Hide")):
            raise ValueError("Unverified private popup native clip: " + name)
        native_path = "unity/Assets/Game/Resources/Map12/UI/" + name + ".anim"
        payload = (ROOT / native_path).read_bytes()
        meta_path = native_path + ".meta"
        meta_payload = (ROOT / meta_path).read_bytes()
        if sha256(payload) != outputs[native_path]["sha256"] or sha256(meta_payload) != outputs[meta_path]["sha256"]:
            raise ValueError("Private popup native animation changed after conversion: " + name)
        source_tracks, duration, loop, unknown = mecanim_tracks(obj, hierarchy)
        if loop or any(row["reason"] != "target_absent" or row["property"] is None for row in unknown):
            raise ValueError("Private popup animation has unsupported source channels: " + name)
        if obj.read_typetree()["m_Events"]:
            raise ValueError("Private popup source animation has unhandled events: " + name)
        native = yaml.safe_load("\n".join(line for line in payload.decode("utf8").splitlines()
                                        if not line.startswith(("%", "---"))))["AnimationClip"]
        if native["m_Name"] != name or native["m_Legacy"] or native["m_Events"] or native["m_AnimationClipSettings"]["m_LoopTime"]:
            raise ValueError("Private popup native playback settings differ: " + name)
        expected = {}
        for track in source_tracks:
            prop = track["property"]
            prop = "m_LocalScale." + prop[6:] if prop.startswith("scale.") else prop
            expected[(track["path"] or "", prop)] = track["keys"]
        if any(native[group] for group in ("m_RotationCurves", "m_CompressedRotationCurves", "m_EulerCurves", "m_PositionCurves", "m_PPtrCurves")):
            raise ValueError("Private popup native animation has extra runtime curves: " + name)
        actual = {}
        for row in native["m_ScaleCurves"]:
            for axis in "xyz":
                actual[(row["path"] or "", "m_LocalScale." + axis)] = [[key["time"], key["value"][axis],
                    key["inSlope"][axis], key["outSlope"][axis]] for key in row["curve"]["m_Curve"]]
        for row in native["m_FloatCurves"]:
            if row["attribute"] != "m_Alpha" or row["classID"] != 225 or row["script"]["fileID"]:
                raise ValueError("Private popup native animation scalar binding differs: " + name)
            actual[(row["path"] or "", row["attribute"])] = [[key["time"], key["value"], key["inSlope"], key["outSlope"]]
                for key in row["curve"]["m_Curve"]]
        if set(expected) != set(actual):
            raise ValueError("Private popup native animation target set differs: " + name)
        for key, source_keys in expected.items():
            if len(source_keys) != len(actual[key]) or any(not math.isclose(a, b, rel_tol=2e-6, abs_tol=2e-6)
                    for source_key, native_key in zip(source_keys, actual[key]) for a, b in zip(source_key, native_key)):
                raise ValueError("Private popup native animation curve differs: " + name + " " + str(key))
        if not math.isclose(native["m_AnimationClipSettings"]["m_StopTime"], duration, abs_tol=2e-6):
            raise ValueError("Private popup native animation duration differs: " + name)
        clips.append(dict(field=field, name=name, resource="Map12/UI/" + name,
            duration=duration, loop=loop, source=apk.provenance(obj), object_raw_sha256=sha256(obj.get_raw_data()),
            native=dict(path=native_path, sha256=sha256(payload), meta_sha256=sha256(meta_payload)),
            curve_count=len(source_tracks), absent_source_channels=unknown))
    if len(clips) != 2:
        raise ValueError("Private popup must keep its original show and hide clips")
    return dict(path=path, objects=[root.m_Name], enabled=bool(reader.read(check_read=False).m_Enabled), clips=clips, mask=private_mask_binding(apk, schemas),
        maskType=fields["mMaskType"], windowType=fields["mWindowType"], fullScreen=bool(fields["mFullScreen"]),
        closeByClickMask=bool(fields["mCloseByClickMask"]), autoCloseWhenContainerDisable=bool(fields["mAutoCloseWhenContainerDisable"]),
        isDuplicate=bool(fields["mIsDuplicate"]),
        native_source_receipt=dict(path=receipt_path.relative_to(ROOT).as_posix(), sha256=sha256(receipt_path.read_bytes())))


def prefab_bindings(apk, schemas, sprite_cache, key, bundle, asset_path):
    from prepare_character_creation_opening import decode_image, decode_text

    prefab = apk.asset(bundle, asset_path)
    transforms = []
    safe_areas = []
    layouts = []
    scrolls = []
    buttons = []
    localizations = []
    room_view = None
    room_button = None
    selected_players = []
    route_window = None
    grade_item = None
    private_detail = None
    private_dialog = None
    private_unlock = None
    popup = None
    grade_grids = []
    room_animation = None
    vote_animation = None
    nodes = list(relative_transform_path(prefab.read()))
    paths = {go.object_reader.path_id: "/".join(map(str, path)) for go, path, _, _ in nodes}

    def reference_path(pointer):
        if not pointer["m_PathID"]:
            return None
        if pointer["m_FileID"]:
            raise ValueError("External hierarchy binding: " + key)
        target = prefab.assets_file.objects[pointer["m_PathID"]].read(check_read=False)
        target_id = target.m_GameObject.path_id if hasattr(target, "m_GameObject") else pointer["m_PathID"]
        return paths[target_id]
    def object_states(fields, name):
        references = {ref["rid"]: ref for ref in fields["references"]["RefIds"]}
        configs = fields[name]["mStateConfigs"]
        if len(configs) != 1:
            raise ValueError("Private route state binding needs its original single config: " + name)
        reference = references[configs[0]["rid"]]
        if reference["type"]["class"] != "StateGameObjectConfig":
            raise ValueError("Unexpected private route state config: " + name)
        states = [dict(state=row["State"]["mValue"], objects=[reference_path(value) for value in row["GameObjects"]])
                  for row in reference["data"]["mStateGameObjectData"]["GoConfig"]]
        if any(path is None for state in states for path in state["objects"]):
            raise ValueError("Private route state config has a null hierarchy reference: " + name)
        return states

    images = []
    texts = []
    for game_object, path, names, transform in nodes:
        path_key = "/".join(map(str, path))
        object_names = list(names)
        if transform is not None:
            position = transform.m_LocalPosition
            rotation = transform.m_LocalRotation
            scale = transform.m_LocalScale
            record = {
                "path": path_key,
                "objects": object_names,
                "isRectTransform": hasattr(transform, "m_AnchorMin"),
                "localPosition": [float(position.x), float(position.y), float(position.z)],
                "localRotation": [float(rotation.x), float(rotation.y), float(rotation.z), float(rotation.w)],
                "localScale": [float(scale.x), float(scale.y), float(scale.z)],
            }
            if record["isRectTransform"]:
                record.update({
                    "anchorMin": [float(transform.m_AnchorMin.x), float(transform.m_AnchorMin.y)],
                    "anchorMax": [float(transform.m_AnchorMax.x), float(transform.m_AnchorMax.y)],
                    "anchoredPosition": [float(transform.m_AnchoredPosition.x),
                                         float(transform.m_AnchoredPosition.y)],
                    "sizeDelta": [float(transform.m_SizeDelta.x), float(transform.m_SizeDelta.y)],
                    "pivot": [float(transform.m_Pivot.x), float(transform.m_Pivot.y)],
                })
            transforms.append(record)
        for pair in game_object.m_Component:
            reader = pair.component.deref()
            if reader.type.name != "MonoBehaviour":
                continue
            script = reader.read(check_read=False).m_Script.read().m_ClassName
            if script == "DailyDungeonBigMapDialog" and key == "DailyDungeonBigMapDialog":
                fields = schemas.read(reader)
                route_window = {name: reference_path(fields[field]) for name, field in (
                    ("scroll", "ScrollRect"), ("chooseScroll", "ChooseScrollRect"), ("grid", "GridLayoutGroup"),
                    ("chooseGrid", "ChooseGridLayoutGroup"), ("gradeSlot", "GradeSlot"),
                    ("chooseGradeSlot", "ChooseGradeSlot"), ("team", "mTeamGo"), ("tip", "mTipText"),
                    ("countdown", "mNextStageConfirmTimeTxt"))}
                route_window["gradePrefab"] = prefab.assets_file.objects[fields["GradeObj"]["m_PathID"]].read().m_Name
                references = {ref["rid"]: ref for ref in fields["references"]["RefIds"]}
                route_window["chooseStates"] = []
                for reference in fields["ChooseAreaStateSwitcher"]["mStateConfigs"]:
                    data = references[reference["rid"]]
                    if data["type"]["class"] != "StateGameObjectConfig":
                        raise ValueError("Unbound original route choose-state config")
                    route_window["chooseStates"].extend(dict(state=row["State"]["mValue"],
                        objects=[reference_path(value) for value in row["GameObjects"]])
                        for row in data["data"]["mStateGameObjectData"]["GoConfig"])
            elif script == "DailyDungeonBigMapDialogGradeGrid" and key == "DailyDungeonBigMapDialog":
                fields = schemas.read(reader)
                grade_grids.append(dict(path=path_key, scroll=reference_path(fields["ScrollRect"]),
                    rect=reference_path(fields["RectTransform"])))
            elif script == "DailyDungeonDialog" and key == "DailyDungeonDialogPrivateMap":
                fields = schemas.read(reader)
                private_dialog = {name: reference_path(fields[field]) for name, field in (
                    ("background", "mBgImage"), ("icon", "IconImage"), ("title", "mTitleText"))}
                private_dialog["titleStates"] = object_states(fields, "mTitleStateSwitcher")
            elif script == "DailyDungeonDetailContent" and key == "DailyDungeonDialogPrivateMap":
                fields = schemas.read(reader)
                private_detail = dict(isPrivate=bool(fields["mIsPrivateMap"]),
                    title=reference_path(fields["DailyDungeonName"]), description=reference_path(fields["DailyDungeonDesc"]),
                    gradeRange=reference_path(fields["GradeRangeText"]), lockStates=object_states(fields, "LockContentStateSwitcher"))
            elif script == "DailyDungeonUnlockContent" and key == "DailyDungeonDialogPrivateMap":
                fields = schemas.read(reader)
                source = (CODE / "client-source/DailyDungeonUnlockContent.cs").read_text(encoding="utf8")
                enum = re.search(r"public enum BtnShowState\s*\{([^}]+)\}", source).group(1)
                if "=" in enum: raise ValueError("Unexpected explicit private-entry enum values")
                names = [name.strip() for name in enum.split(",") if name.strip()]
                private_unlock = dict(buttonStates=object_states(fields, "BtnStateSwitcher"), enterState=names.index("Enter"),
                    rewardTitle=reference_path(fields["mRewardTitleText"]), rewardTitleLayout=reference_path(fields["mRewardTitleLayout"]))
            elif script == "DailyDungeonGradeItem" and key == "DailyDungeonGradeItem":
                fields = schemas.read(reader)
                grade_item = {name: reference_path(fields[field]) for name, field in (
                    ("image", "Image"), ("fill", "FillImage"), ("text", "GradeText"))}
            elif script == "PopupWindow" and key == "DailyDungeonDialogPrivateMap":
                popup = private_popup_binding(apk, schemas, reader, prefab.read(), path_key)
            elif script == "Button" and key in ("DailyDungeonBigMapDialog", "DailyDungeonBigMapRoomItem", "DailyDungeonDialogPrivateMap"):
                buttons.append(button_binding(reader, path_key, object_names, reference_path))
            elif key in ("DailyDungeonBigMapDialog", "DailyDungeonDialogPrivateMap") and script in ("GridLayoutGroup", "ContentSizeFitter", "RectMask2D", "VerticalLayoutGroup", "HorizontalLayoutGroup", "HorizontalLayoutGroupJSZX", "LayoutElement", "LayoutElementClamper"):
                raw = reader.get_raw_data()
                layout = {"path": path_key, "objects": object_names, "type": script,
                          "enabled": bool(reader.read(check_read=False).m_Enabled)}
                if script == "GridLayoutGroup":
                    if len(raw) != 84: raise ValueError("Unexpected official GridLayoutGroup: " + path_key)
                    values = struct.unpack_from("<7i4f2i", raw, 32)
                    layout.update(padding=list(values[:4]), alignment=values[4], startCorner=values[5],
                        startAxis=values[6], cellSize=list(values[7:9]), spacing=list(values[9:11]),
                        constraint=values[11], constraintCount=values[12])
                elif script in ("VerticalLayoutGroup", "HorizontalLayoutGroup"):
                    if len(raw) != 84: raise ValueError("Unexpected official horizontal/vertical layout: " + path_key)
                    values = struct.unpack_from("<5if7i", raw, 32)
                    layout.update(padding=list(values[:4]), alignment=values[4], layoutSpacing=values[5], flags=list(values[6:]))
                elif script in ("HorizontalLayoutGroupJSZX", "LayoutElement"):
                    from prepare_character_creation_opening import decode_layout_component
                    decoded = {"path": path_key}
                    if not decode_layout_component(script, raw, decoded): raise ValueError("Unbound route layout")
                    if script == "LayoutElement": layout["element"] = decoded["layout_element"]
                    else:
                        value = decoded["horizontal_layout"]
                        layout.update(padding=value["padding"], alignment=value["alignment"],
                            layoutSpacing=value["spacing"], spacingList=value["spacing_list"], flags=value["flags"])
                elif script == "LayoutElementClamper":
                    fields = schemas.read(reader)
                    layout["clamp"] = {name: fields[name] for name in ("mMaxPreferredWidth", "mMaxPreferredHeight")}
                elif script == "ContentSizeFitter":
                    if len(raw) != 40: raise ValueError("Unexpected official ContentSizeFitter: " + path_key)
                    layout["fit"] = list(struct.unpack_from("<2i", raw, 32))
                else:
                    if len(raw) != 56: raise ValueError("Unexpected official RectMask2D: " + path_key)
                    layout["padding"] = list(struct.unpack_from("<4f", raw, 32))
                    layout["softness"] = list(struct.unpack_from("<2i", raw, 48))
                layouts.append(layout)
            elif key == "DailyDungeonDialogPrivateMap" and script == "ScrollRect":
                raw = reader.get_raw_data()
                if len(raw) != 128: raise ValueError("Unexpected original dungeon ScrollRect length")
                if any(struct.unpack_from("<iq", raw, offset)[1] for offset in (84, 96)):
                    raise ValueError("Unbound original dungeon ScrollRect scrollbar")
                if struct.unpack_from("<i", raw, 124)[0]:
                    raise ValueError("Unbound original dungeon ScrollRect event")
                def scroll_reference(offset):
                    file_id, path_id = struct.unpack_from("<iq", raw, offset)
                    return reference_path(dict(m_FileID=file_id, m_PathID=path_id))
                scrolls.append(dict(type=script, path=path_key, objects=object_names,
                    enabled=bool(reader.read(check_read=False).m_Enabled),
                    content=scroll_reference(32), viewport=scroll_reference(72),
                    horizontal=bool(struct.unpack_from("<i", raw, 44)[0]), vertical=bool(struct.unpack_from("<i", raw, 48)[0]),
                    movementType=struct.unpack_from("<i", raw, 52)[0], elasticity=struct.unpack_from("<f", raw, 56)[0],
                    inertia=bool(struct.unpack_from("<i", raw, 60)[0]), decelerationRate=struct.unpack_from("<f", raw, 64)[0],
                    scrollSensitivity=struct.unpack_from("<f", raw, 68)[0],
                    horizontalScrollbarVisibility=struct.unpack_from("<i", raw, 108)[0],
                    verticalScrollbarVisibility=struct.unpack_from("<i", raw, 112)[0],
                    horizontalScrollbarSpacing=struct.unpack_from("<f", raw, 116)[0],
                    verticalScrollbarSpacing=struct.unpack_from("<f", raw, 120)[0]))
            elif key == "DailyDungeonBigMapDialog" and script == "LoopVerticalScrollRect":
                fields = schemas.read(reader)
                if any(fields[k]["m_PathID"] for k in ("m_HorizontalScrollbar", "m_VerticalScrollbar")):
                    raise ValueError("Unbound official route scrollbar")
                if fields["m_OnValueChanged"]["m_PersistentCalls"]["m_Calls"]:
                    raise ValueError("Unbound official route scroll event")
                scrolls.append(dict(type=script, path=path_key, objects=object_names, enabled=bool(fields["m_Enabled"]),
                    content=reference_path(fields["m_Content"]), viewport=reference_path(fields["m_Viewport"]),
                    totalCount=fields["totalCount"], reverseDirection=bool(fields["reverseDirection"]),
                    reverseIndex=bool(fields["reverseIndex"]), horizontal=bool(fields["m_Horizontal"]),
                    vertical=bool(fields["m_Vertical"]), movementType=fields["m_MovementType"],
                    elasticity=fields["m_Elasticity"], inertia=bool(fields["m_Inertia"]),
                    decelerationRate=fields["m_DecelerationRate"], scrollSensitivity=fields["m_ScrollSensitivity"],
                    prefab=prefab.assets_file.objects[fields["DefaultPrefabSource"]["Prefab"]["m_PathID"]].read().m_Name))
            elif script == "DailyDungeonRoomItem" and key == "DailyDungeonBigMapRoomItem":
                fields = schemas.read(reader)
                room_view = {key: reference_path(fields[field]) for key, field in (
                    ("content", "ShowContentObj"), ("icon", "IconImage"), ("frame", "FrameImage"),
                    ("select", "SelectImage"), ("reward", "mRewardIconImage"))}
                room_view["roads"] = [dict(path=reference_path(road["RoadObj"]),
                    image=reference_path(road["RoadImage"]), animation=reference_path(road["Animation"]),
                    offsetType=road["OffsetType"]) for road in fields["RoomRoadDatas"]]
            elif script == "DailyDungeonBigMapRoomItem" and key == "DailyDungeonBigMapRoomItem":
                fields = schemas.read(reader)
                room_button = reference_path(fields["ClickBtn"])
                selected_players = [reference_path(value) for value in fields["mSelectedPlayerIcons"]]
                room_animation = reference_path(fields["mAnimation"])
                vote_animation = reference_path(fields["mVoteAnimation"])
            elif script == "SafeArea" and key in ("DailyDungeonBigMapDialog", "DailyDungeonDialogPrivateMap"):
                fields = schemas.read(reader)
                safe_areas.append({"path": path_key, "objects": object_names,
                    "enabled": bool(reader.read(check_read=False).m_Enabled),
                    "paddingTop": float(fields["PaddingTop"]), "paddingBottom": float(fields["PaddingBottom"]),
                    "disableTopAdapt": bool(fields["DisableTopAdapt"]),
                    "disableBottomAdapt": bool(fields["DisableBottomAdapt"])})
            elif script == "MultiLanguageText" and key in ("DailyDungeonBigMapDialog", "DailyDungeonDialogPrivateMap", "DailyDungeonGradeItem"):
                fields = schemas.read(reader)
                localizations.append(dict(path=path_key, objects=object_names, key=fields["myKey"],
                    enabled=bool(reader.read(check_read=False).m_Enabled)))
            elif script == "Image":
                fields = schemas.read(reader)
                decoded = decode_image(fields)
                image = {
                    "path": path_key,
                    "objects": object_names,
                    "enabled": bool(reader.read(check_read=False).m_Enabled),
                    "color": decoded["color"],
                    "type": decoded["type"],
                    "preserveAspect": decoded["preserve"],
                    "raycastTarget": bool(fields["m_RaycastTarget"]),
                    "maskable": bool(fields["m_Maskable"]),
                    "sprite": None,
                }
                file_id, path_id = decoded["sprite"]
                if path_id:
                    image["sprite"] = sprite_binding(sprite_cache, reader.assets_file, file_id, path_id)
                images.append(image)
            elif script in ("TextMeshProUGUI", "TextMeshPro"):
                decoded = decode_text(reader.get_raw_data(), key + ":" + path_key)
                texts.append({
                    "path": path_key,
                    "objects": object_names,
                    "text": decoded["text"],
                    "fontSize": decoded["font_size"],
                    "fontWeight": decoded["font_weight"],
                    "fontStyle": decoded["font_style"],
                    "autoSizing": decoded["auto_sizing"],
                    "fontSizeRange": decoded["font_size_range"],
                    "alignment": decoded["alignment"],
                    "wordWrap": decoded["word_wrap"],
                    "overflow": decoded["overflow"],
                    "margin": decoded["margin"],
                    "color": decoded["color"],
                })
    if room_view is not None:
        room_view["selectedPlayers"] = selected_players
        room_view["animation"] = room_animation
        room_view["voteAnimation"] = vote_animation
    return {"name": key, "transforms": transforms, "safeAreas": safe_areas, "layouts": layouts, "scrolls": scrolls,
            "roomView": room_view, "roomButton": room_button, "buttons": buttons, "popup": popup,
            "routeWindow": route_window, "gradeItem": grade_item, "privateDetail": private_detail, "privateDialog": private_dialog, "privateUnlock": private_unlock, "gradeGrids": grade_grids, "localizations": localizations, "images": images, "texts": texts}, apk.provenance(prefab)


def route_texts(apk, keys, required_keys):
    import xxhash
    wanted = {xxhash.xxh64_intdigest(key.encode()): key for key in keys}
    texts, inputs = {}, {}
    wwise = {bundle["name"]: bundle for bundle in apk.source.manifest("WwiseBnk", apk.archive)["bundles"]}
    for name in ("wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile",
                 "wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile"):
        record = wwise[name]
        entry = "assets/yoo/WwiseBnk/" + record["file"]
        if entry in apk.available:
            raw = apk.archive.read(entry)
            origin = dict(origin="apk_zip", apk_entry=entry, apk_sha256=apk.source.apk_sha256)
        else:
            cached = apk.cloud.read("WwiseBnk", record)
            if cached is None: raise FileNotFoundError("Selected original route language is not cached: " + name)
            raw, origin = cached
        if len(raw) != record["size"] or hashlib.md5(raw).hexdigest() != record["hash"]:
            raise ValueError("Selected route language does not match its manifest: " + name)
        inputs[name] = dict(origin, sha256=sha256(raw), bytes=len(raw), md5=record["hash"])
        count = struct.unpack_from("<i", raw)[0]
        start = 4 + count * 12
        for index in range(count):
            identity, offset = struct.unpack_from("<Qi", raw, 4 + index * 12)
            if identity not in wanted: continue
            position = start + offset
            size = struct.unpack_from("<H", raw, position)[0]
            if offset < 0 or position + 2 + size > len(raw): raise ValueError("Route language index is outside its source")
            # LocalizationBinaryTextService uses the first reader containing a key.
            texts.setdefault(wanted[identity], raw[position+2:position+2+size].decode("utf8"))
    if not set(required_keys).issubset(texts):
        raise ValueError("Official route language keys missing: " + str(set(required_keys)-set(texts)))
    missing = sorted(set(keys)-set(texts))
    # Current LocalizationTextService.GetMissingKeyText returns the original key.
    return [dict(key=key, value=texts.get(key, key)) for key in keys], inputs, missing


def route_settings(apk, schemas, sprites, prefabs):
    from official_manifest import read_asset_manifest
    assets = read_asset_manifest(apk.source.manifest_bytes("DefaultPackage", apk.archive))
    settings = next(asset for asset in assets if asset["path"].endswith("/DailyDungeonSettings.asset"))
    reader = apk.asset(settings["bundle"], settings["path"])
    fields = schemas.read(reader)
    by_guid = {asset["guid"]: asset for asset in assets if asset["guid"]}

    def resolve(proxy):
        guid = proxy["mAssetGuid"]
        if not guid:
            return None
        asset = by_guid[guid]
        candidates = [pointer.deref() for path, pointer in apk.container(asset["bundle"])
                      if path == asset["path"] and pointer.type.name == "Sprite"]
        if len(candidates) != 1:
            raise ValueError("Official route appearance GUID is not a unique Sprite: " + guid)
        target = candidates[0]
        result = sprite_binding(sprites, target.assets_file, 0, target.path_id)
        result["sourceGuid"] = guid
        return result

    rooms = [dict(type=row["RoomType"], **{name: resolve(row[field]) for name, field in (
        ("passIcon", "PassSpriteProxy"), ("noPassIcon", "NoPassSpriteProxy"),
        ("passFrame", "PassFrameSpriteProxy"), ("noPassFrame", "NoPassFrameSpriteProxy"),
        ("select", "SelectSpriteProxy"), ("reward", "RewardIconSpriteProxy"))})
        for row in fields["RoomDatas"]]
    roads = [dict(status=row["RoadStatusType"], color=[row["Color"][key] for key in ("r", "g", "b", "a")])
             for row in fields["RoadStatusTypeAndColors"]]
    enum_source = (CODE / "common-source/Common/BattleResultGradeType.cs").read_text(encoding="utf8")
    names = re.findall(r"^\s*(\w+),?\s*$", enum_source.split("{", 1)[1].rsplit("}", 1)[0], re.M)
    grades = [dict(grade=row["GradeType"], key="ui_daily_dungeon_grade_text_" + names[row["GradeType"]],
        color=[row["Color"][key] for key in ("r", "g", "b", "a")]) for row in fields["GradeDatas"]]
    grade_keys = [grade["key"] for grade in grades]
    template_path = ROOT / "unity/Assets/Official/World/Map/12/Source/templates.json"
    template = json.loads(template_path.read_text(encoding="utf8"))["6103"]
    normal = template["StageBehStageDailyDungeonComponent"]["StageInfoList"]
    private = template["StageBehStageDailyDungeonPrivateMapComponent"]["StageBehInfo"]
    ids = {entry["DungeonBattleId"] for entry in normal}
    if len(ids) != 1: raise ValueError("Ambiguous original private-dialog display dungeon")
    difficulty_source = (CODE / "common-source/Common/DailyDungeonDifficulty.cs").read_text(encoding="utf8")
    difficulty_names = {int(value): name for name, value in re.findall(r"(\w+)\s*=\s*(\d+)", difficulty_source)}
    display = dict(dungeonId=next(iter(ids)), privateDungeonId=private["DungeonBattleId"], privateDifficulty=private["Difficulty"],
        difficulties=[dict(value=entry["Difficulty"], name=difficulty_names[entry["Difficulty"]],
            key="ui_daily_dungeon_difficulty_text_" + difficulty_names[entry["Difficulty"]]) for entry in normal])
    appearance = next(row for row in fields["Datas"] if row["DungeonId"] == display["dungeonId"])
    display.update(background=resolve(appearance["BgSpriteProxy"]), icon=resolve(appearance["IconSpriteProxy"]))
    if display["background"] is None or display["icon"] is None:
        raise ValueError("Original private-dialog appearance is not bound")
    private_keys = ["ui_daily_dungeon_item_title_without_difficulty_" + str(display["dungeonId"]),
                    "ui_daily_dungeon_dialog_reward_title_without_difficulty"]
    private_keys.extend(row["key"] for row in display["difficulties"])
    private_keys.extend("ui_daily_dungeon_dialog_desc_content_" + str(display["dungeonId"]) + "_" + row["name"]
                        for row in display["difficulties"])
    keys = sorted(set(grade_keys + private_keys) | {entry["key"] for prefab in prefabs for entry in prefab["localizations"] if entry["key"]})
    texts, language_inputs, missing = route_texts(apk, keys, grade_keys + private_keys)
    return dict(rooms=rooms, roads=roads, grades=grades, texts=texts, privateDisplay=display), dict(asset=settings, source=apk.provenance(reader),
        raw_sha256=sha256(reader.get_raw_data()), language_inputs=language_inputs, missing_translation_keys=missing,
        display_source=dict(path=template_path.relative_to(ROOT).as_posix(), sha256=sha256(template_path.read_bytes()),
            public_component="StageBehStageDailyDungeonComponent", private_component="StageBehStageDailyDungeonPrivateMapComponent"))


def native_route_dependencies():
    source_path = ROOT / "assets/reference/dungeon-ui/source-manifest.json"
    source = json.loads(source_path.read_text(encoding="utf8"))
    if source["manifest"] != VERSION:
        raise ValueError("Private route native dependencies use another source version")
    records = {row["path"]: row for row in source["outputs"] + source["runtime_prefabs"]}
    records.update({row["shared_asset"]: dict(row, path=row["shared_asset"])
                    for row in source["shared_bindings"]})
    by_guid = {row["guid"]: path for path, row in records.items()}
    pending = ["unity/Assets/Game/Resources/Dungeons/UI/" + name + ".prefab" for name in (
        "DailyDungeonDialogPrivateMap", "DailyDungeonBigMapDialog", "DailyDungeonBigMapRoomItem", "DailyDungeonGradeItem")]
    selected = {}
    while pending:
        path = pending.pop()
        if path in selected: continue
        record = records[path]
        payload = (ROOT / path).read_bytes()
        if sha256(payload) != record["sha256"]:
            raise ValueError("Private route native output differs from its conversion source: " + path)
        selected[path] = dict(path=path, guid=record["guid"], sha256=record["sha256"])
        if Path(path).suffix not in (".prefab", ".anim", ".mat", ".controller", ".overrideController", ".asset", ".shader"):
            continue
        text = payload.decode("utf8")
        scripts = set(re.findall(r"m_Script: \{[^\r\n}]*guid: ([a-f0-9]{32})", text))
        for guid in re.findall(r"guid: ([a-f0-9]{32})", text):
            # Script ports and explicit serialized bindings are recorded separately.
            if guid in scripts or guid.startswith("0000000000000000"): continue
            if guid not in by_guid:
                raise ValueError("Private route native material/animation dependency is unresolved: " + guid)
            pending.append(by_guid[guid])
    return dict(source_receipt=dict(path=source_path.relative_to(ROOT).as_posix(), sha256=sha256(source_path.read_bytes())),
        assets=[selected[path] for path in sorted(selected)])


def build():
    sys.path.insert(0, str(ROOT / "scripts"))
    sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from prepare_character_creation_opening import SpriteCache
    from prepare_dungeon_ui_sprites import asset_meta, ensure_output_folders

    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError("Dungeon UI bundle version is not the active official version")
    ensure_output_folders()
    schemas = ScriptSchemas(code_root=CODE)
    sprites = SpriteCache(apk)
    prefabs = []
    sources = []
    for key, bundle, path in PREFABS:
        prefab, source = prefab_bindings(apk, schemas, sprites, key, bundle, path)
        prefabs.append(prefab)
        sources.append({"name": key, "path": path, "source": source,
                        "image_count": len(prefab["images"]),
                        "text_count": len(prefab["texts"])})
    styles, settings_source = route_settings(apk, schemas, sprites, prefabs)
    document = {"manifest": VERSION, "prefabs": prefabs, "routeStyles": styles}
    encoded = (json.dumps(document, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")
    BINDINGS.parent.mkdir(parents=True, exist_ok=True)
    update = "--update" in sys.argv[1:]
    if BINDINGS.exists() and BINDINGS.read_bytes() != encoded and not update:
        raise ValueError("Refusing to overwrite changed official dungeon UI bindings")
    if not BINDINGS.exists() or update:
        BINDINGS.write_bytes(encoded)
    meta = BINDINGS.with_name(BINDINGS.name + ".meta")
    if not meta.exists():
        guid = hashlib.sha256(b"zjcs-dungeon-ui-official-bindings:" + VERSION.encode()).hexdigest()[:32]
        meta.write_text("fileFormatVersion: 2\n"
                        f"guid: {guid}\nTextScriptImporter:\n"
                        "  externalObjects: {}\n  userData: \n  assetBundleName: \n"
                        "  assetBundleVariant: \n", encoding="ascii")
    sprite_outputs = []
    all_sprites = [entry["sprite"] for prefab in prefabs for entry in prefab["images"] if entry["sprite"]]
    all_sprites.extend(sprite for room in styles["rooms"] for key, sprite in room.items()
                       if key != "type" and sprite is not None)
    all_sprites.extend(styles["privateDisplay"][key] for key in ("background", "icon"))
    for relative in sorted({sprite["file"] for sprite in all_sprites}):
        path = RUNTIME / relative
        sprite_outputs.append({"path": path.relative_to(ROOT).as_posix(),
                               "sha256": sha256(path.read_bytes()),
                               "meta_sha256": sha256(path.with_name(path.name + ".meta").read_bytes())})
    code_paths = ["client-source/DailyDungeonBigMapDialog.cs", "client-source/DailyDungeonRoomItem.cs",
        "client-source/DailyDungeonBigMapRoomItem.cs", "client-source/DailyDungeonSettings.cs", "client-source/DailyDungeonDetailContent.cs", "client-source/DailyDungeonUnlockContent.cs", "client-source/LayoutElementClamper.cs", "client-source/DailyDungeonDialog.cs", "client-source/DailyDungeonUtils.cs",
        "common-source/Common/DailyDungeonDifficulty.cs",
        "common-source/Common/DailyDungeonMapGroupInfoParser.cs", "framework-firstpass-source/SafeArea.cs", "framework-firstpass-source/PopupWindow.cs", "framework-firstpass-source/WindowMaskType.cs", "framework-firstpass-source/WindowMaskBase.cs", "framework-firstpass-source/WindowBlurMask.cs", "framework-firstpass-source/BlurManager.cs", "framework-firstpass-source/ScreenBlurPass.cs", "framework-firstpass-source/Framework.UI/WindowContainer.cs", "client-source/DailyDungeonGradeItem.cs",
        "client-source/DailyDungeonBigMapDialogGradeGrid.cs", "common-source/Common/LanguageUtils.cs",
        "common-source/Common/BattleResultGradeType.cs", "framework-firstpass-source/MultiLanguageText.cs",
        "framework-source/StateGameObjectConfig.cs", "framework-source/StateGameObjectData.cs",
        "framework-firstpass-source/Framework/LocalizationTextService.cs",
        "framework-firstpass-source/Framework/LocalizationBinaryTextService.cs"]
    code_sources = [dict(path=(CODE / path).relative_to(ROOT).as_posix(),
        sha256=sha256((CODE / path).read_bytes())) for path in code_paths]
    receipt = {"manifest": VERSION, "code_sources": code_sources,
        "operation": "Read original prefab transforms, Image/TMP fields and route GridLayoutGroup, ContentSizeFitter, RectMask2D, SafeArea, LoopVerticalScrollRect and Button fields. Resolve DailyDungeonSettings room Sprite GUIDs against the selected AssetTable; verify private popup show/hide runtime curves against the current source and reuse the recorded native Map12 clips; decode original atlas pixels and preserve pivot, border and PPU.", "bindings": {
        "path": BINDINGS.relative_to(ROOT).as_posix(), "sha256": sha256(encoded),
        "meta_sha256": sha256(meta.read_bytes())},
        "prefabs": sources, "sprites": sprite_outputs, "route_settings": settings_source,
        "native_route_dependencies": native_route_dependencies(),
        "sprite_sources": sorted({sprite["source"]["bundle"] for sprite in all_sprites})}
    receipt_encoded = json.dumps(receipt, ensure_ascii=False, sort_keys=True, indent=2) + "\n"
    if RECEIPT.exists() and RECEIPT.read_text(encoding="utf-8") != receipt_encoded and not update:
        raise ValueError("Refusing to overwrite changed dungeon UI binding receipt")
    if not RECEIPT.exists() or update:
        RECEIPT.parent.mkdir(parents=True, exist_ok=True)
        RECEIPT.write_text(receipt_encoded, encoding="utf-8", newline="\n")
    print(json.dumps({"manifest": VERSION, "prefabs": sources,
                      "sprites": len(sprite_outputs), "receipt": RECEIPT.relative_to(ROOT).as_posix()},
                     ensure_ascii=False))


if __name__ == "__main__":
    build()
