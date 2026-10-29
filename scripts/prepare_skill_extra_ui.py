"""Extract original skill auxiliary windows and their explicit component bindings."""
import hashlib
import io
import math
from pathlib import Path

from UnityPy.classes.PPtr import PPtr

from official_animation import mecanim_tracks
from prepare_character_prop_bindings import node_path
from prepare_cooking_module import CookingConverter
from official_tmp_input_schema import read_input_field, encoding_evidence


def recover_prefab(reader, text_schemas, bundle, prefab_name, city):
    if not bundle.endswith(".bundle"):
        bundle = "defaultpackage_assets_" + bundle + ".bundle"
    converter = CookingConverter.__new__(CookingConverter)
    converter.apk = reader.apk
    converter.schemas = reader.schemas
    converter.text_mesh_pro_fields = text_schemas.read
    sprites = {}
    def sprite(obj):
        value = obj.read(check_read=False)
        source = reader.apk.provenance(obj)
        matches = [uri for uri, row in city["resources"].items()
                   if uri.startswith("res://assets/official/sprites/") and row.get("path_id") == str(obj.path_id)
                   and row.get("bundle_hash") == source["bundle_hash"]]
        if len(matches) == 1:
            uri = matches[0]
        else:
            assert not matches, (prefab_name, value.m_Name, matches)
            output = io.BytesIO()
            value.image.save(output, format="PNG")
            raw = output.getvalue()
            digest = hashlib.sha256(raw).hexdigest()
            uri = "res://assets/official/sprites/skill_aux_" + digest + ".png"
            sprites[uri] = {"bytes": raw, "source": source, "sha256": digest, "name": value.m_Name}
        return {"file": uri, "border": [getattr(value.m_Border, axis) for axis in "xyzw"]}
    converter.add_sprite = sprite
    def components(game_object):
        transform, result = None, []
        ui_classes = {"ContentSizeFitter", "VerticalLayoutGroup", "HorizontalLayoutGroup", "GridLayoutGroup",
                      "LayoutElement", "Image", "TextMeshProUGUI", "RectMask2D", "Mask", "Button", "Toggle"}
        for entry in game_object.m_Component:
            obj = entry.component.deref()
            name, fields = obj.type.name, None
            if name in ("RectTransform", "Transform"):
                transform = obj.read(check_read=False)
                continue
            if name == "MonoBehaviour":
                name = obj.read(check_read=False).m_Script.read().m_ClassName
                if name == "MirrorImage":
                    name, fields = "Image", reader.schemas.read(obj)
                elif name in ui_classes:
                    fields = text_schemas.read(obj)
            elif name in ("CanvasGroup", "Canvas"):
                fields = obj.read_typetree()
            result.append((name, obj, fields))
        return transform, result
    converter.game_object_components = components
    flat, _ = converter.object_tree(prefab_name, bundle)
    rows = flat["nodes"]
    for row in rows:
        if "rotation" in row:
            row["rotation"] = math.radians(row["rotation"])
        row["children"] = []
    for row in rows[1:]:
        rows[row["parentIndex"]]["children"].append(row)
    by_path = {row["path"]: row for row in rows}
    path, pointer = next((path, ptr) for path, ptr in reader.apk.container(bundle) if path.endswith("/" + prefab_name + ".prefab"))
    def convert(value, owner):
        if isinstance(value, list):
            return [convert(item, owner) for item in value]
        if not isinstance(value, dict):
            return value
        if "m_PathID" in value:
            if not value["m_PathID"]:
                return None
            target = PPtr(**value, assetsfile=owner.assets_file).deref()
            if target.type.name == "Sprite":
                return sprite(target)
            read = target.read(check_read=False)
            if target.type.name == "GameObject":
                return node_path(read)
            go = getattr(read, "m_GameObject", None)
            if go is not None and go.path_id:
                return node_path(go.read())
            return {"source": reader.apk.provenance(target), "name": getattr(read, "m_Name", ""), "type": target.type.name}
        return {key: convert(item, owner) for key, item in value.items()}
    classes = {"StageDamageTestPanel", "StageDamageTestSettingItem", "UIToggleGroup", "UIToggle", "ToggleText", "UIDropdown",
               "DropdownItem", "NumScrollView", "NumCell", "Scroller", "PopupWindow", "PanelTitle", "ItemSourceItem",
               "RuleIntroductionDialog", "SkillSkinReplaceDialog", "SkillSkinReplaceDialogItem", "SafeArea",
               "MultiLanguageText", "ItemSourceGo", "IntroductionPopBtn", "PopTipMutilDesc", "PopTipDescItem",
               "ItemDetailDialog", "SkillDetailDialog", "ShareItemBtn", "SkillSkinReplaceBtn", "PopTipNormal", "TipInfo",
               "PlayerPropItem", "SkillPanelPropPanel", "SkillPromotePanelRankUpContent", "SkillPromotePanelLevelUpContent",
               "SkillSkinBtn", "ItemIconSkill", "UIVideoPlayer", "VideoPlayerProxy", "Toggle", "ToggleGroup", "LoopVerticalScrollRect", "ScrollRect",
               "EntryDetailDialog", "EntryDetailDialogItem", "LoopVerticalScrollWithMaxHeight", "UIToggleObject"}
    classes.update(("HorizontalLayoutGroupJSZX", "VerticalLayoutGroupJSZX", "GridLayoutGroupJSZX", "LayoutElementClamper",
                    "UIPlayVideoBtn", "PlayVideoDialog", "AspectRatioFitter", "Slider", "GameEndPanelDamageTest", "GameEndPanelBase",
                    "PlayerIconMe", "PlayerInfoUI", "PlayerHeadPortraitUI", "PlayerIconStatus", "ItemDetailDialogAddition",
                    "FightEndReportPanel", "FightEndReportPlayerTabView", "FightEndReportSkillTabView",
                    "FightEndReportPanelTabItem", "FightEndReportSkillTabItem", "TabGroup", "TabItem",
                    "LocalFightRecordPanel", "LocalFightRecordInfoItem", "FightRecordInfoItem",
                    "FightRecordDownloadPlayButton", "TMP_InputField", "ContentModerationClickMask",
                    "CharacterPanelSkillListPanelItem", "CharacterPanelSkillListPanelItemNewMark",
                    "SkillPanelRedDotItem", "SkillPanelCanRankUpRedDotItem", "SkillCanMergeRedDotItem",
                    "ItemIconAttachmentRedDotHandler", "ItemIconAttachmentHint", "RedDotMultiItem",
                    "SkillPromotePanel", "PlayerUseItemContent", "CharacterPanelSkillItem", "CharacterPanelSkillContent",
                    "CharacterPanelSkillPanel", "CharacterPanelSkillItemDragContent", "TMPEffect",
                    "SkillContentPanelSkillGroup", "SkillRankUpNotifyDialog", "SkillStarInfo", "StarMiniInfo", "SkillPromotePanelItem", "ItemProgress", "ItemIcon", "LoopHorizontalScrollRect", "SkillSlotlCanLevelUpRedDotItem"))
    for node in reader.walk(pointer.read()):
        row = by_path[node_path(node)]
        for entry in node.m_Component:
            obj = entry.component.deref()
            if obj.type.name == "MonoBehaviour":
                name = obj.read(check_read=False).m_Script.read().m_ClassName
                if name in ("HorizontalLayoutGroup", "VerticalLayoutGroup") and "layout" in row:
                    row["layout"]["enabled"] = bool(text_schemas.read(obj)["m_Enabled"])
                if name not in classes:
                    continue
                fields = read_input_field(text_schemas, obj) if name == "TMP_InputField" else text_schemas.read(obj) if name in ("Toggle", "ToggleGroup", "LoopVerticalScrollRect", "LoopHorizontalScrollRect", "ScrollRect",
                    "HorizontalLayoutGroupJSZX", "VerticalLayoutGroupJSZX", "GridLayoutGroupJSZX", "AspectRatioFitter", "Slider") else reader.schemas.read(obj)
                row.setdefault("original_bindings", {})[name] = {key: convert(value, obj) for key, value in fields.items()
                    if key not in ("m_GameObject", "m_Enabled", "m_Script", "m_Name")}
                if name == "TMPEffect":
                    row["original_bindings"][name]["m_Enabled"] = bool(fields["m_Enabled"])
                if name == "ItemIconAttachmentRedDotHandler":
                    for raw, converted in zip(fields["mAttachmentDatas"], row["original_bindings"][name]["mAttachmentDatas"]):
                        reference = PPtr(**raw["RedDotItem"], assetsfile=obj.assets_file).deref().read(check_read=False)
                        converted["RedDotClass"] = reference.m_Script.read().m_ClassName
                elif name == "RedDotMultiItem":
                    row["original_bindings"][name]["myItemClasses"] = [
                        PPtr(**value, assetsfile=obj.assets_file).deref().read(check_read=False).m_Script.read().m_ClassName
                        for value in fields["myItems"]]
            elif obj.type.name == "VideoPlayer":
                fields = obj.read_typetree()
                row.setdefault("original_bindings", {})["VideoPlayer"] = {key: convert(value, obj) for key, value in fields.items()
                    if key not in ("m_GameObject", "m_Enabled", "m_Name", "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset")}
            elif obj.type.name == "Animator":
                animator = obj.read()
                if not animator.m_Controller.path_id:
                    continue
                controller_obj = animator.m_Controller.deref()
                overrides = {}
                while controller_obj.type.name == "AnimatorOverrideController":
                    controller = controller_obj.read()
                    for pair in controller.m_Clips:
                        if pair.m_OverrideClip.path_id:
                            overrides[pair.m_OriginalClip.path_id] = pair.m_OverrideClip
                    controller_obj = controller.m_Controller.deref()
                for source_clip in controller_obj.read().m_AnimationClips:
                    clip = overrides.get(source_clip.path_id, source_clip)
                    if not clip.path_id:
                        continue
                    tracks, duration, loop, unknown = mecanim_tracks(clip.deref(), row, property_names=("m_Interactable",))
                    # Unity ignores clip bindings whose target is absent from a
                    # prefab variant. Keep those original bindings in the receipt.
                    assert all(value.get("reason") == "target_absent" for value in unknown), (prefab_name, node.m_Name, unknown)
                    row.setdefault("animations", []).append({"name": clip.read().m_Name, "owner": row["path"], "duration": duration,
                        "loop": loop, "tracks": tracks, "unbound_source_tracks": unknown, "source": reader.apk.provenance(clip.deref())})
    popup = rows[0].get("original_bindings", {}).get("PopupWindow", {})
    for field in ("mShowClip", "mHideClip", "mLoopClip"):
        clip = popup.get(field)
        if not clip or any(item["name"] == clip["name"] for item in rows[0].get("animations", [])):
            continue
        source = clip["source"]
        original = reader.object(source["bundle"], source["path_id"])
        tracks, duration, loop, unknown = mecanim_tracks(original, rows[0], property_names=("m_Interactable",))
        assert all(value.get("reason") == "target_absent" for value in unknown), (prefab_name, unknown)
        rows[0].setdefault("animations", []).append({"name": clip["name"], "duration": duration, "loop": loop,
            "tracks": tracks, "unbound_source_tracks": unknown, "source": source, "popup_field": field})
    return {"root": rows[0], "source": {"bundle": bundle, "bundle_hash": reader.apk.index[bundle], "path_id": str(pointer.path_id), "prefab": path}}, sprites


def restore(reader, schemas, data, city, rules):
    from official_apk import ROOT
    from official_tables import Constructors
    from official_unity_schema import ScriptSchemas
    # The full UI assembly is required for PlayVideoDialog.AspectRatioFitter.
    # Supply it at generator construction: AssetsTools cannot replace a loaded DLL.
    reader.schemas = ScriptSchemas(assembly_overrides={"UnityEngine.UI": ROOT / "unity/Library/ScriptAssemblies/UnityEngine.UI.dll"})
    reader.schemas.generator.load_dll(Path("D:/Tool/Unity/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll").read_bytes())
    # Engine reference assembly supplies only Unity's VideoPlayer field type;
    # the serialized client schemas still come from the pinned APK assemblies.
    reader.schemas.generator.load_dll(Path("D:/Tool/Unity/Editor/Data/Managed/UnityEngine/UnityEngine.VideoModule.dll").read_bytes())
    schemas.generator.load_dll((ROOT / ".tools/source-packages/official-code/managed/decoded/Framework.dll").read_bytes())
    templates = {
        "damage_test": ("prefabs_ui_stagedamagetest", "StageDamageTestPanel"),
        "damage_test_num_cell": ("prefabs_ui_stagedamagetest", "StageLevelCellItem"),
        "damage_test_setting": ("prefabs_ui_stagedamagetest", "StageSetToggleItem"),
        "damage_test_toggle": ("prefabs_ui_stagedamagetest", "ToggleText"),
        "damage_test_dropdown_item": ("prefabs_ui_common", "DropdownItem"),
        "skill_skin_replace": ("prefabs_ui_skill", "SkillSkinReplaceDialog"),
        "skill_video_preview": ("prefabs_ui_video", "PlayVideoDialog"),
        "damage_test_result": ("prefabs_ui_fight_gameendpaneldamagetest", "GameEndPanelDamageTest"),
        "skill_gift_item_detail": ("prefabs_ui_itemdetail", "ItemDetailDialog"),
        "skill_source_item": ("prefabs_ui_itemdetail", "ItemSourceItem"),
        "skill_rule_tip": ("prefabs_ui_tips", "PopTipMutilDesc"),
        "skill_rule_tip_item": ("prefabs_ui_common", "TipInfo2"),
        "skill_entry_tip": ("prefabs_ui_tips", "PopTipEntry"),
        "skill_entry_tip_item": ("prefabs_ui_item", "EntryDetailItem"),
        "damage_test_report": ("prefabs_ui_fight_fightendreportpanel_fightendreportpanel", "FightEndReportPanel"),
        "damage_test_records": ("prefabs_ui_record", "LocalFightRecordPanel"),
        "damage_test_record_item": ("prefabs_ui_record", "FightRecordInfoItem"),
    }
    sprites = {}
    inputs = {}
    inputs["hyperlink_code"] = []
    for name in ("HyperLinkClickListener", "SkillHyperLinkDataProvider", "HyperLinkClickHandlerEntry", "EntryDetailDialog", "EntryDetailDialogItem",
                 "HyperLinkClickHandlerPetEnergyEntry", "SkillDetailDialog", "FightEndReportSkillTabViewItem"):
        path = ROOT / ".tools/source-packages/official-code/client-source" / (name + ".cs")
        inputs["hyperlink_code"].append({"source": path.relative_to(ROOT).as_posix(),
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    for key, (bundle, prefab) in templates.items():
        data[key], extracted = recover_prefab(reader, schemas, bundle, prefab, city)
        sprites.update(extracted)
        inputs[key] = data[key]["source"]
    # Resolve attachment GUIDs through the pinned APK manifest and preserve the
    # config's array order, which is the original sibling/layer order.
    from official_manifest import read_asset_manifest
    attachment_object = reader.apk.asset("scriptableconfig_modules_itemicon", "/ItemIconAttachmentConfig.asset")
    attachment_config = reader.schemas.read(attachment_object)
    manifest_assets = read_asset_manifest(reader.apk.archive.read(
        "assets/yoo/DefaultPackage/PackageManifest_DefaultPackage_854_156383.v3.bytes"))
    attachments = []
    for layer, row in enumerate(attachment_config["Templates"]):
        kind = int(row["Type"])
        if kind not in (4, 8, 10, 13, 14):
            continue
        asset = next(value for value in manifest_assets if value["guid"] == row["GuidProxy"]["mAssetGuid"])
        key = "skill_attachment_" + str(kind)
        data[key], extracted = recover_prefab(reader, schemas, asset["bundle"], Path(asset["path"]).stem, city)
        sprites.update(extracted)
        attachments.append({"type": kind, "layer": layer, "guid": asset["guid"],
                            "asset_path": asset["path"], "template": key, "source": data[key]["source"]})
    assert {row["type"] for row in attachments} == {4, 8, 10, 13, 14}
    data["skill_list_attachments"] = attachments
    inputs["skill_list_attachments"] = {"source": reader.apk.provenance(attachment_object),
        "sha256": hashlib.sha256(attachment_object.get_raw_data()).hexdigest(), "templates": attachments}
    appearance_object = reader.apk.asset("scriptableconfig_modules_appearance", "/AppearanceConfig.asset")
    appearance_fields = reader.schemas.read(appearance_object)
    data["result_append_skins"] = next(row["mAppendSkins"] for row in appearance_fields["mAppendSkinConfigs"]
        if row["mAppendSkinType"] == 1)  # Original AppendSkinType.UI.
    inputs["result_append_skins"] = {"source": reader.apk.provenance(appearance_object),
        "sha256": hashlib.sha256(appearance_object.get_raw_data()).hexdigest(), "append_type": 1}
    inputs["skill_list_code"] = []
    for name in ("CharacterPanelSkillListPanelItem", "CharacterPanelSkillListPanelItemNewMark", "ItemNewMarkManager",
                 "SkillPanelRedDotItem", "SkillPanelCanRankUpRedDotItem", "SkillPanelCanPromoteRedDotItemBase",
                 "SkillCanMergeRedDotItem", "ItemIconAttachmentRedDotHandler", "ItemIconAttachmentHint", "ItemIconSkill", "RedDotMultiItem"):
        path = ROOT / ".tools/source-packages/official-code/client-source" / (name + ".cs")
        inputs["skill_list_code"].append({"source": path.relative_to(ROOT).as_posix(),
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    def walk(node):
        yield node
        for child in node.get("children", []):
            yield from walk(child)
    for key in ("skill_panel_detail", "skill_plain_detail", "skill_promote", "skill_promote_item", "skill_promote_prop", "skill_rank_notify", "skill_rank_notify_prop", "item", "item_skill", "item_skill_piece", "character_skills", "skill_list_item", "skill_piece_list_item"):
        source = data[key]["source"]
        original = reader.object(source["bundle"], source["path_id"])
        template, extracted = recover_prefab(reader, schemas, source["bundle"], original.read().m_Name, city)
        sprites.update(extracted)
        nodes = {node["path"]: node for node in walk(data[key]["root"])}
        for node in walk(template["root"]):
            if node["path"] in nodes:
                nodes[node["path"]].setdefault("original_bindings", {}).update(node.get("original_bindings", {}))
    inputs["cultivation_code"] = []
    for name in ("SkillPromotePanel", "SkillPromotePanelRankUpContent", "SkillPromotePanelLevelUpContent",
                 "PlayerUseItemContent", "CharacterPanelSkillItem", "SkillPromotePanelItem", "SkillRankUpNotifyDialog",
                 "SkillStarInfo", "StarMiniInfo", "PlayerPropItem", "CharacterPanelSkillContent",
                 "CharacterPanelSkillPanel", "CharacterPanelSkillItemDragContent", "BlessLevelTextViewChanger"):
        path = ROOT / ".tools/source-packages/official-code/client-source" / (name + ".cs")
        inputs["cultivation_code"].append({"source": path.relative_to(ROOT).as_posix(),
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    for source in ("common-source/Common/PlayerSkillDataExt.cs", "common-source/SkillItemComparer.cs",
                   "common-source/TextColorList.cs", "common-source/GlobalColors.cs", "common-source/ColorType.cs",
                   "common-source/Common/SystemLevelGapLimitInfoParser.cs", "common-source/Common/SystemLevelLimitInfoParser.cs",
                   "common-source/Common/PlayerSkllSlotDataExt.cs", "common-source/Common/AstrologicalSeasonConfigParser.cs",
                   "common-source/Common/FormulaHandler.cs", "common-source/Common/LanguageUtils.cs"):
        path = ROOT / ".tools/source-packages/official-code" / source
        inputs["cultivation_code"].append({"source": path.relative_to(ROOT).as_posix(),
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    from prepare_skill_cultivation_scroll import restore as restore_cultivation_scroll
    inputs["cultivation_scroll"] = restore_cultivation_scroll()
    palette_receipt = __import__("json").loads((ROOT / "assets/reference/skill-quality-text/source-manifest.json").read_text(encoding="utf-8"))["palette"]
    palette_object = reader.object(palette_receipt["bundle"], palette_receipt["path_id"])
    assert hashlib.sha256(palette_object.get_raw_data()).hexdigest() == palette_receipt["sha256"]
    palette_fields = reader.schemas.read(palette_object)
    light = next(row for row in palette_fields["TextColorConfig"]["TextColorLists"] if row["ThemeColorType"] == 1)
    data["cultivation_colors"] = {"Green": palette_fields["Green"], "LightRed": light["TextColors"][light["TypeList"].index(3)]["Color"]}
    inputs["cultivation_colors"] = palette_receipt
    inputs["plan_input_schema"] = encoding_evidence()
    configs = {}
    # CharacterPanelSkillPlan initializes this original non-interactive scrollbar
    # with GameSettings.SkillPlanMaxCount steps, retaining its prefab handle size.
    source = data["character_skills"]["source"]
    original = reader.object(source["bundle"], source["path_id"]).read()
    bars = []
    for node in reader.walk(original):
        if node.m_Name == "SkillPlanName":
            for part in node.m_Component:
                obj = part.component.deref()
                if obj.type.name == "MonoBehaviour" and obj.read(check_read=False).m_Script.read().m_ClassName == "TMP_InputField":
                    inputs["plan_input"] = {"source": reader.apk.provenance(obj), "sha256": hashlib.sha256(obj.get_raw_data()).hexdigest()}
        if node.m_Name != "PlanSelectedBar":
            continue
        for part in node.m_Component:
            obj = part.component.deref()
            if obj.type.name == "MonoBehaviour" and obj.read(check_read=False).m_Script.read().m_ClassName == "Scrollbar":
                bars.append(obj)
    assert len(bars) == 1
    obj = bars[0]
    fields = schemas.read(obj)
    configs["plan_scrollbar"] = {key: reader.convert(fields[key], obj) for key in
        ("m_HandleRect", "m_TargetGraphic", "m_Direction", "m_Size", "m_Interactable", "m_Colors", "m_Transition", "m_Navigation")}
    configs["plan_scrollbar"]["number_of_steps"] = rules["game_settings"]["SkillPlanMaxCount"]
    inputs["plan_scrollbar"] = {"source": reader.apk.provenance(obj),
        "sha256": hashlib.sha256(obj.get_raw_data()).hexdigest()}
    # The older report prefab export retained its RawImage as an empty script.
    # Recover that component from its original object rather than synthesizing a portrait layout.
    import json
    portrait_bundle = "defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportplayertabviewitem.bundle"
    portraits = [obj for obj in reader.apk.objects(portrait_bundle) if obj.type.name == "MonoBehaviour"
        and obj.read(check_read=False).m_Script.read().m_ClassName == "RawImage"
        and obj.read(check_read=False).m_GameObject.read().m_Name == "HeadImg"]
    assert len(portraits) == 1
    portrait_object = portraits[0]
    portrait_fields = schemas.read(portrait_object)
    # The editor placeholder is DefaultPortrait; PlayerHeadPortraitUI replaces
    # it with the selected player's captured head at runtime.
    assert not portrait_fields["m_Material"]["m_PathID"]
    from UnityPy.classes.PPtr import PPtr
    default_portrait = PPtr(**portrait_fields["m_Texture"], assetsfile=portrait_object.assets_file).deref()
    assert default_portrait.read().m_Name == "DefaultPortrait"
    configs["report_portrait"] = {key: portrait_fields[key] for key in ("m_Color", "m_UVRect", "m_RaycastTarget")}
    inputs["report_portrait"] = {"source": reader.apk.provenance(portrait_object), "sha256": hashlib.sha256(portrait_object.get_raw_data()).hexdigest()}
    inputs["report_portrait"]["placeholder"] = {"source": reader.apk.provenance(default_portrait),
        "sha256": hashlib.sha256(default_portrait.get_raw_data()).hexdigest()}
    for key, typename in (("damage_test_difficulty_level", "TbDamageTestDifficultyLevel"),
                          ("damage_test_stage_monster", "TbDamageTestStageMonsterInfo"), ("item_source", "TbItemSourceInfo"),
                          ("entry", "TbEntryInfo")):
        obj = reader.apk.asset("defaultpackage_assets_config_luban.bundle", "/" + key + ".g.bytes")
        raw = obj.read().m_Script.encode("utf-8", "surrogateescape")
        configs[key] = Constructors().table(raw, typename)
        inputs[key] = {"source": reader.apk.provenance(obj), "sha256": hashlib.sha256(raw).hexdigest()}
    import re
    entry_ids = {int(match[1]) for skill in rules["skill"] for value in skill["HyperLinkDatas"]
                 if (match := re.search(r"EntryId:(\d+)", value))}
    configs["entry"] = [row for row in configs["entry"] if row["Id"] in entry_ids]
    entry_icons = {}
    for bundle in reader.apk.index:
        if not bundle.startswith("defaultpackage_assets_image_itemicon_props_"):
            continue
        for path, pointer in reader.apk.container(bundle):
            if pointer.type.name != "Sprite":
                continue
            entry_icons.setdefault(path.rsplit("/", 1)[-1].removesuffix(".png"), []).append(pointer)
    resolved_icons = {}
    for row in configs["entry"]:
        for field in ("Icon", "Bg"):
            if not row[field]:
                row[field + "_uri"] = ""
                continue
            name = row[field].rsplit("/", 1)[-1]
            matches = entry_icons.get(name, [])
            assert len(matches) == 1, (row["Id"], field, row[field], matches)
            if name not in resolved_icons:
                obj = matches[0].deref()
                output = io.BytesIO()
                obj.read().image.save(output, format="PNG")
                raw = output.getvalue()
                digest = hashlib.sha256(raw).hexdigest()
                uri = "res://assets/official/sprites/skill_aux_" + digest + ".png"
                sprites[uri] = {"bytes": raw, "source": reader.apk.provenance(obj),
                                "sha256": digest, "name": name}
                resolved_icons[name] = uri
            row[field + "_uri"] = resolved_icons[name]
    configs["rounds"] = rules["game_settings"]["StageDamageRoundList"]
    configs["pet_energy"] = {"settings": rules["game_settings"]["PetSetting"],
        "skills": {str(row["ClassId"]): {key: row[key] for key in ("TriggerNeedEnergy", "InitEnergy")}
                   for row in rules["skill"] if 9 in row["HyperLinkTypes"]}}
    from prepare_unity_map10_battle_data import (collect_battle_data, collect_monster_rosters,
        _read_verified_json, _read_property_ids, BATTLE_SCENES_PATH, BATTLE_SCENES_MANIFEST_PATH,
        COMBAT_RULES_PATH, COMBAT_RULES_MANIFEST_PATH, SKILL_RULES_PATH, SKILL_RULES_MANIFEST_PATH)
    import json
    stage_ids = {row["Id"] for row in configs["damage_test_stage_monster"]}
    stages = [row for row in rules["stage"] if row["Id"] in stage_ids]
    assert stage_ids == {row["Id"] for row in stages} and all(row["Type"] == 43 for row in stages)
    array_obj = reader.apk.asset("config_luban", "Assets/Config/Luban/stage_monster_array.g.bytes")
    array_raw = array_obj.read().m_Script.encode("utf-8", "surrogateescape")
    arrays = Constructors().table(array_raw, "TbStageMonsterArrayInfo")["InfoDict"]
    scene_manifest = json.loads(BATTLE_SCENES_MANIFEST_PATH.read_text(encoding="utf-8"))
    scene_raw = BATTLE_SCENES_PATH.read_bytes()
    assert scene_manifest["manifest"] == "854_156383" and hashlib.sha256(scene_raw).hexdigest() == scene_manifest["output"]["sha256"]
    scenes = json.loads(scene_raw)["stages"]
    selected_arrays, selected_scenes = collect_battle_data(stages, arrays, scenes)
    combat, combat_source = _read_verified_json(COMBAT_RULES_PATH, COMBAT_RULES_MANIFEST_PATH, "output_sha256")
    skill_rules, skill_source = _read_verified_json(SKILL_RULES_PATH, SKILL_RULES_MANIFEST_PATH, "output_sha256")
    property_ids, property_hash = _read_property_ids()
    configs["battle"] = {"stages": stages, "monsterArrays": selected_arrays, "battleScenes": selected_scenes,
        "monsterRosters": collect_monster_rosters(stages, arrays, selected_scenes, combat, rules,
            rules["skill"], skill_rules["entities"], property_ids)}
    # JsonUtility needs named vector fields and arrays for the shared battle DTOs.
    import copy
    runtime_scenes = []
    for game_id, original_scene in selected_scenes.items():
        scene = copy.deepcopy(original_scene)
        scene["GameId"] = int(game_id)
        origin = scene["FightGridGroundComponent"]["OriginPos"]
        scene["FightGridGroundComponent"]["OriginPos"] = dict(zip(("X", "Y", "Z"), origin))
        runtime_scenes.append(scene)
    runtime_rosters = copy.deepcopy(configs["battle"]["monsterRosters"])
    for roster in runtime_rosters:
        for wave in roster["Waves"]:
            for unit in wave["Units"]:
                unit["Level"] = round(unit["Level"])
    configs["battle_runtime_json"] = json.dumps({"manifest": "854_156383", "stages": stages,
        "battleScenes": runtime_scenes, "monsterRosters": runtime_rosters}, ensure_ascii=False, separators=(",", ":"))
    inputs["damage_test_battle"] = {"monster_arrays": {"source": reader.apk.provenance(array_obj),
        "sha256": hashlib.sha256(array_raw).hexdigest()}, "battle_scenes_sha256": hashlib.sha256(scene_raw).hexdigest(),
        "combat": combat_source, "skills": skill_source, "property_enum_sha256": property_hash}
    for stage in configs["damage_test_stage_monster"]:
        name = stage["ImgPath"].rsplit("/", 1)[-1]
        obj = next(pointer.deref() for path, pointer in reader.apk.container("defaultpackage_assets_image_ui_damagetest.bundle")
                   if path.endswith("/" + name + ".jpg") and pointer.type.name == "Sprite")
        raw = io.BytesIO()
        value = obj.read(check_read=False)
        # Container may point at the Texture2D; use its original Sprite object.
        if obj.type.name != "Sprite":
            obj = next(candidate for candidate in obj.assets_file.objects.values()
                       if candidate.type.name == "Sprite" and candidate.read(check_read=False).m_Name == value.m_Name)
            value = obj.read(check_read=False)
        value.image.save(raw, format="PNG")
        payload = raw.getvalue()
        sha = hashlib.sha256(payload).hexdigest()
        uri = "res://assets/official/sprites/skill_aux_" + sha + ".png"
        sprites[uri] = {"bytes": payload, "source": reader.apk.provenance(obj), "sha256": sha, "name": value.m_Name}
        stage["image_uri"] = uri
    data["auxiliary_data"] = configs
    data["auxiliary_inputs"] = inputs
    # The older icons.json omits eighteen skill items present in the pinned
    # item table. Resolve their actual typed Sprite addresses in the APK.
    import json
    from official_apk import ROOT
    from official_manifest import read_asset_manifest
    icons = json.loads((ROOT / "assets/official/icons.json").read_text(encoding="utf-8"))
    manifest = read_asset_manifest(reader.apk.source.manifest_bytes("DefaultPackage", reader.apk.archive))
    missing_icons = {}
    for item in rules["item"]:
        if item["ItemType"] not in (100, 101) or str(item["ClassId"]) in icons:
            continue
        address = "Assets/Image/" + item["RawIcon"]
        matches = [row for row in manifest if row["address"] == address
                   or row["path"].removesuffix(".png") == address]
        assert len(matches) == 1, (item["ClassId"], address, matches)
        record, = matches
        obj, = [pointer.deref() for path, pointer in reader.apk.container(record["bundle"])
                if path == record["path"] and pointer.type.name == "Sprite"]
        raw = io.BytesIO()
        obj.read().image.save(raw, format="PNG")
        payload = raw.getvalue()
        sha = hashlib.sha256(payload).hexdigest()
        uri = "res://assets/official/sprites/skill_aux_" + sha + ".png"
        sprites[uri] = {"bytes": payload, "source": reader.apk.provenance(obj),
                        "sha256": sha, "name": obj.read().m_Name, "address": record,
                        "object_sha256": hashlib.sha256(obj.get_raw_data()).hexdigest()}
        missing_icons[str(item["ClassId"])] = uri
    data["skill_item_icons"] = missing_icons
    return sprites
