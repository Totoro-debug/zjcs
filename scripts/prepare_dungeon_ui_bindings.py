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
            elif script == "Button" and key in ("DailyDungeonBigMapDialog", "DailyDungeonBigMapRoomItem"):
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
                buttons.append(dict(path=path_key, objects=object_names,
                    enabled=bool(reader.read(check_read=False).m_Enabled),
                    navigation=struct.unpack_from("<i", raw, 32)[0], wrapAround=bool(struct.unpack_from("<i", raw, 36)[0]),
                    transition=struct.unpack_from("<i", raw, 88)[0], colors=list(struct.unpack_from("<22f", raw, 92)),
                    triggers=triggers, interactable=bool(interactable),
                    target=reference_path({"m_FileID":file_id, "m_PathID":path_id})))
            elif key == "DailyDungeonBigMapDialog" and script in ("GridLayoutGroup", "ContentSizeFitter", "RectMask2D", "VerticalLayoutGroup", "HorizontalLayoutGroupJSZX", "LayoutElement"):
                raw = reader.get_raw_data()
                layout = {"path": path_key, "objects": object_names, "type": script,
                          "enabled": bool(reader.read(check_read=False).m_Enabled)}
                if script == "GridLayoutGroup":
                    if len(raw) != 84: raise ValueError("Unexpected official GridLayoutGroup: " + path_key)
                    values = struct.unpack_from("<7i4f2i", raw, 32)
                    layout.update(padding=list(values[:4]), alignment=values[4], startCorner=values[5],
                        startAxis=values[6], cellSize=list(values[7:9]), spacing=list(values[9:11]),
                        constraint=values[11], constraintCount=values[12])
                elif script == "VerticalLayoutGroup":
                    if len(raw) != 84: raise ValueError("Unexpected official VerticalLayoutGroup: " + path_key)
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
                elif script == "ContentSizeFitter":
                    if len(raw) != 40: raise ValueError("Unexpected official ContentSizeFitter: " + path_key)
                    layout["fit"] = list(struct.unpack_from("<2i", raw, 32))
                else:
                    if len(raw) != 56: raise ValueError("Unexpected official RectMask2D: " + path_key)
                    layout["padding"] = list(struct.unpack_from("<4f", raw, 32))
                    layout["softness"] = list(struct.unpack_from("<2i", raw, 48))
                layouts.append(layout)
            elif key == "DailyDungeonBigMapDialog" and script == "LoopVerticalScrollRect":
                fields = schemas.read(reader)
                if any(fields[k]["m_PathID"] for k in ("m_HorizontalScrollbar", "m_VerticalScrollbar")):
                    raise ValueError("Unbound official route scrollbar")
                if fields["m_OnValueChanged"]["m_PersistentCalls"]["m_Calls"]:
                    raise ValueError("Unbound official route scroll event")
                scrolls.append(dict(path=path_key, objects=object_names, enabled=bool(fields["m_Enabled"]),
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
            elif script == "SafeArea" and key == "DailyDungeonBigMapDialog":
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
            "roomView": room_view, "roomButton": room_button, "buttons": buttons,
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
        "client-source/DailyDungeonBigMapRoomItem.cs", "client-source/DailyDungeonSettings.cs", "client-source/DailyDungeonDetailContent.cs", "client-source/DailyDungeonUnlockContent.cs", "client-source/DailyDungeonDialog.cs", "client-source/DailyDungeonUtils.cs",
        "common-source/Common/DailyDungeonDifficulty.cs",
        "common-source/Common/DailyDungeonMapGroupInfoParser.cs", "framework-firstpass-source/SafeArea.cs", "client-source/DailyDungeonGradeItem.cs",
        "client-source/DailyDungeonBigMapDialogGradeGrid.cs", "common-source/Common/LanguageUtils.cs",
        "common-source/Common/BattleResultGradeType.cs", "framework-firstpass-source/MultiLanguageText.cs",
        "framework-source/StateGameObjectConfig.cs", "framework-source/StateGameObjectData.cs",
        "framework-firstpass-source/Framework/LocalizationTextService.cs",
        "framework-firstpass-source/Framework/LocalizationBinaryTextService.cs"]
    code_sources = [dict(path=(CODE / path).relative_to(ROOT).as_posix(),
        sha256=sha256((CODE / path).read_bytes())) for path in code_paths]
    receipt = {"manifest": VERSION, "code_sources": code_sources,
        "operation": "Read original prefab transforms, Image/TMP fields and route GridLayoutGroup, ContentSizeFitter, RectMask2D, SafeArea, LoopVerticalScrollRect and Button fields. Resolve DailyDungeonSettings room Sprite GUIDs against the selected AssetTable; decode original atlas pixels and preserve pivot, border and PPU.", "bindings": {
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
