"""Restore skill icon prefab, empty slot and star bindings from the pinned official item bundle."""
import hashlib
import json
import re
import shutil
import struct
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / ".tools/python-packages"))
from official_apk import Apk
from UnityPy.classes.PPtr import PPtr


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def restore():
    module = ROOT / "unity/Assets/Official/Skills"
    manifest_path = module / "source-manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    layout = module / "skill-ui.json"
    known = {row["path"]: row["sha256"] for row in manifest["files"]}
    assert digest(layout) == known["Assets/Official/Skills/skill-ui.json"], "Skill UI payload was edited outside its source receipt"
    city_path = ROOT / "assets/reference/official-city/source-manifest.json"
    city = json.loads(city_path.read_text(encoding="utf-8"))
    ui_path = ROOT / "assets/official/ui.json"
    assert digest(ui_path) == city["data_sha256"]["ui.json"]
    ui = json.loads(ui_path.read_text(encoding="utf-8"))
    data = json.loads(layout.read_text(encoding="utf-8"))
    data["item_empty"] = ui["item_empty"]
    apk = Apk("854_156383", reuse_current_cache=True)
    bundle = "defaultpackage_assets_prefabs_ui_item.bundle"
    apk.load(bundle)
    wanted = {-6850642395015705196: "SkillStarInfo", 4575643289586601364: "StarMiniInfo"}
    objects = {obj.path_id: obj for obj in apk.env.objects if obj.path_id in wanted}
    records = []
    for path_id, name in wanted.items():
        obj = objects[path_id]
        value = obj.read(check_read=False)
        assert value.m_Script.read().m_ClassName == name
        records.append({"class": name, "path_id": str(path_id),
                        "sha256": hashlib.sha256(obj.get_raw_data()).hexdigest()})
    star = objects[-6850642395015705196]
    threshold = struct.unpack_from("<i", star.get_raw_data(), 92)[0]
    assert threshold == 4
    mini = objects[4575643289586601364]
    raw = mini.get_raw_data()
    count = struct.unpack_from("<i", raw, 44)[0]
    assert len(raw) == 48 + count * 16
    qualities = {}
    for index in range(count):
        quality, file_id, path_id = struct.unpack_from("<iiq", raw, 48 + index * 16)
        name = PPtr(m_FileID=file_id, m_PathID=path_id, assetsfile=mini.assets_file).read().m_Name
        matches = [uri for uri, record in city["resources"].items()
                   if record.get("object") == name and uri.startswith("res://assets/official/sprites/")]
        assert len(matches) == 1, (name, matches)
        qualities[str(quality)] = matches[0]
    data["skill_star_settings"] = {"threshold": threshold, "quality_sprites": qualities}
    uris = set(qualities.values())
    rules_path = ROOT / "assets/official/rules.json"
    assert digest(rules_path) == city["data_sha256"]["rules.json"]
    rules = json.loads(rules_path.read_text(encoding="utf-8"))
    currency_id = rules["game_settings"]["SkillSeniorMaterialItemId"]
    assert currency_id == 42200
    currency_uri = "res://assets/official/sprites/item_42200-617dec24f0.png"
    uris.add(currency_uri)
    from prepare_character_prop_bindings import BindingReader
    reader = BindingReader()
    reader.apk = apk
    character = reader.component_fields("defaultpackage_assets_prefabs_ui_character.bundle",
        -6326212988873864872, ("CharacterPanel", "PlayerWithPetContent"))
    from prepare_character_prop_bindings import node_path
    character_source = ui["character"]["source"]
    character_root = reader.object(character_source["bundle"], character_source["path_id"]).read()
    followers = {}
    for node in reader.walk(character_root):
        if node.m_Name != "ShadowAttachment":
            continue
        followers[node_path(node)] = reader.component_fields(character_source["bundle"], node.object_reader.path_id, ("AttachmentFollower",))["AttachmentFollower"]
    data["character_followers"] = followers
    skill_source = data["character_skills"]["source"]
    skill_root = reader.object(skill_source["bundle"], skill_source["path_id"]).read()
    skill_tab = next(node for node in reader.walk(skill_root)
        if node.m_Name == "CharacterPanelSkillDetailPanelSkillTabGroupItem_11")
    skill = reader.component_fields(skill_source["bundle"], skill_tab.object_reader.path_id, ("TabItemTopBarSwitcher",))
    assert skill["TabItemTopBarSwitcher"]["ShowTypeList"] == [18]
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    from official_unity_schema import ScriptSchemas
    text_schemas = ScriptSchemas()
    text_schemas.generator = TypeTreeGenerator("2022.3.57f1", generator="AssetsTools")
    text_schemas.generator.load_local_dll_folder("D:/Tool/Unity/Editor/Data/Managed/UnityEngine")
    ui_schema_folder = Path("D:/Tool/Unity/Editor/Data/Resources/PackageManager/ProjectTemplates/libcache/com.unity.template.2d-7.0.3/ScriptAssemblies")
    for name in ("UnityEngine.UI.dll", "Unity.TextMeshPro.dll"):
        candidate = ROOT / "unity/Library/ScriptAssemblies" / name
        if not candidate.is_file():
            candidate = ui_schema_folder / name
        text_schemas.generator.load_dll(candidate.read_bytes())
    text_schemas.generator.load_local_dll_folder(str(ROOT / "unity/Library/ScriptAssemblies"))
    for framework in ("D:/Tool/Unity/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll", "D:/Tool/Unity/Editor/Data/MonoBleedingEdge/lib/mono/4.5/mscorlib.dll"):
        text_schemas.generator.load_dll(Path(framework).read_bytes())
    text_sources = {}
    safe_sources = {}
    character_fonts = {}
    def layout_nodes(node):
        yield node
        for child in node.get("children", []):
            yield from layout_nodes(child)
    for template in list(data.values()):
        if not isinstance(template, dict) or "source" not in template or "root" not in template:
            continue
        source = template["source"]
        by_path = {node["path"]: node for node in layout_nodes(template["root"])}
        for node in reader.walk(reader.object(source["bundle"], source["path_id"]).read()):
            path = node_path(node)
            if path not in by_path:
                continue
            for entry in node.m_Component:
                component = entry.component.deref()
                if component.type.name != "MonoBehaviour" or component.read(check_read=False).m_Script.read().m_ClassName != "TextMeshProUGUI":
                    continue
                fields = text_schemas.read(component)
                text = by_path[path].setdefault("text", {})
                text.update(font_size=fields["m_fontSizeBase"], auto_size=bool(fields["m_enableAutoSizing"]),
                    font_size_min=fields["m_fontSizeMin"], font_size_max=fields["m_fontSizeMax"],
                    word_wrap=bool(fields["m_enableWordWrapping"]), overflow=fields["m_overflowMode"],
                    horizontal=fields["m_HorizontalAlignment"], vertical=fields["m_VerticalAlignment"])
                text_sources[path] = {"source": reader.apk.provenance(component), "style": {key: value for key, value in text.items() if key != "value"}}
    # Preserve SafeArea flags separately from the prefab's editor-time rectangle.
    for source in (character_source, skill_source):
        for node in reader.walk(reader.object(source["bundle"], source["path_id"]).read()):
            path = node_path(node)
            for entry in node.m_Component:
                component = entry.component.deref()
                if component.type.name != "MonoBehaviour":
                    continue
                name = component.read(check_read=False).m_Script.read().m_ClassName
                if name == "SafeArea":
                    fields = reader.schemas.read(component)
                    safe_sources[path] = {"source": reader.apk.provenance(component),
                        "fields": {key: fields[key] for key in ("PaddingTop", "PaddingBottom", "DisableTopAdapt", "DisableBottomAdapt")}}
                elif name == "TextMeshProUGUI" and source is character_source:
                    fields = text_schemas.read(component)
                    font = PPtr(**fields["m_fontAsset"], assetsfile=component.assets_file).deref()
                    character_fonts[path] = {"name": font.read(check_read=False).m_Name,
                        "source": reader.apk.provenance(component), "font_source": reader.apk.provenance(font)}
    from prepare_skill_system_messages import restore_system_message
    data["system_message"], message_uris = restore_system_message(reader, text_schemas, city)
    uris.update(message_uris)
    safe_sources.update(data["system_message"]["safe_area_bindings"])
    data["character_fonts"] = character_fonts
    data["safe_area_bindings"] = safe_sources
    data["text_style_inputs"] = text_sources
    gift_path = "/CharacterPanelSkillPanel/Content/AchievementPanelWithTypes"
    gift_bindings = {}
    classes = {"AchievementPanelBase", "AchievementPanelItem", "AchievementPanelWithTypes", "AchievementToggleType", "PanelTitle"}
    for node in reader.walk(skill_root):
        path = node_path(node)
        if path != gift_path and not path.startswith(gift_path + "/"):
            continue
        for entry in node.m_Component:
            component = entry.component.deref()
            if component.type.name != "MonoBehaviour":
                continue
            name = component.read(check_read=False).m_Script.read().m_ClassName
            if name not in classes:
                continue
            fields = reader.schemas.read(component)
            gift_bindings.setdefault(path, {})[name] = {key: reader.convert(value, component)
                for key, value in fields.items() if key not in ("m_GameObject", "m_Script", "m_Name")}
            reader.inputs[component.assets_file.name + ":" + str(component.path_id)] = reader.apk.provenance(component)
    data["gift_bindings"] = gift_bindings
    data["item"] = ui["item"]
    reward_ids = {int(key) for row in rules["achievement"] if row["Group"] in (9, 33) for key in row["Reward"]}
    item_rows = {row["ClassId"]: row for row in rules["item"]}
    item_icons = {}
    for item_id in sorted(reward_ids):
        name = item_rows[item_id]["RawIcon"].rsplit("/", 1)[-1]
        candidates = [uri for uri, record in city["resources"].items() if record.get("object") == name and uri.startswith("res://assets/official/sprites/")]
        assert len(candidates) == 1, (item_id, name, candidates)
        item_icons[str(item_id)] = candidates[0]
        uris.add(candidates[0])
    data["gift_item_icons"] = item_icons
    import xxhash
    language_path = ROOT / ".tools/source-packages/WwiseBnk/wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile"
    language_receipt = json.loads((ROOT / "assets/reference/official-map/localization-source-manifest.json").read_text(encoding="utf-8"))
    language_input = language_receipt["inputs"][language_path.name]
    raw = apk.archive.read(language_input["source"]["path"])
    assert hashlib.sha256(raw).hexdigest() == language_input["sha256"]
    count = struct.unpack_from("<i", raw)[0]
    offset_start = 4 + count * 12
    entries = dict(struct.unpack_from("<Qi", raw, 4 + i * 12) for i in range(count))
    texts = {}
    for key in ("ui_achievement_title_CostItem_42200", "ui_achievement_content_CostItem_42200",
                "ui_achievement_title_SkillScoreCount", "ui_achievement_content_SkillScoreCount",
                "ui_tip_skill_collect", "ui_tip_skill_uncollect", "ui_item_not_enough", "SkillPanel.PleaseEquipeSkill"):
        position = offset_start + entries[xxhash.xxh64_intdigest(key.encode("utf-8"))]
        size = struct.unpack_from("<H", raw, position)[0]
        texts[key] = raw[position + 2:position + 2 + size].decode("utf-8")
    data["gift_texts"] = texts
    data["gift_language_input"] = language_input
    shared_path = ROOT / "unity/Assets/Official/World/Map/11/UI/map11-ui.json"
    shared_manifest_path = shared_path.with_name("source-manifest.json")
    shared_manifest = json.loads(shared_manifest_path.read_text(encoding="utf-8"))
    assert digest(shared_path) == shared_manifest["output"]["sha256"]
    shared = json.loads(shared_path.read_text(encoding="utf-8"))
    world = next(panel for panel in shared["panels"] if panel["key"] == "WorldCanvas")
    player_path = "/WorldCanvas/Content/Content/WorldPanel/Anim/Left/PublicPlayerInfo"
    player_nodes = [node for node in world["nodes"] if node["path"].startswith(player_path + "/")]
    for node in player_nodes:
        uri = node.get("image_sprite_file", "")
        if uri.startswith("res://assets/official/"):
            uris.add(uri)
    data["shared_header"] = {"currency_type": 18, "currency_id": currency_id, "currency_icon": currency_uri,
        "character_bindings": character, "skill_top_bar": skill, "inputs": reader.inputs,
        "player_path": player_path, "player_nodes": player_nodes, "head_photography": shared["headPortraitSettings"],
        "shared_ui": {"path": shared_path.relative_to(ROOT).as_posix(), "sha256": digest(shared_path),
            "source_manifest": shared_manifest_path.relative_to(ROOT).as_posix(), "source_manifest_sha256": digest(shared_manifest_path)}}
    def walk(node):
        sprite = node.get("image", {}).get("sprite")
        if sprite:
            uris.add(sprite["file"])
        for child in node.get("children", []):
            walk(child)
    walk(data["item_empty"]["root"])
    walk(data["item"]["root"])
    world_source = shared_manifest["source"]["prefabs"]["WorldCanvas"]
    world_prefab = reader.object(world_source["bundle"], world_source["path_id"]).read()
    original_mask = next(node for node in reader.walk(world_prefab)
        if node_path(node) == "/WorldCanvas/Content/Popup/BlurMask")
    mask_fields = reader.component_fields(world_source["bundle"], original_mask.object_reader.path_id,
        ("WindowBlurMask",))["WindowBlurMask"]
    assert mask_fields["mMaskType"] == 3
    data["popup_mask"] = {"fields": mask_fields, "source": reader.apk.provenance(original_mask.object_reader),
        "prefab_resource": "Map11/UI/WorldCanvas", "prefab_path": "Content/Popup/BlurMask",
        "blur_material": "World/Mainline/ScreenBlur"}
    data["popup_masks"] = {}
    for name, class_name, mask_type in (("Mask", "WindowMask", 1), ("invisableMask", "WindowMask", 2), ("BlurMask", "WindowBlurMask", 3)):
        mask_node = next(node for node in reader.walk(world_prefab)
            if node_path(node) == "/WorldCanvas/Content/Popup/" + name)
        fields = reader.component_fields(world_source["bundle"], mask_node.object_reader.path_id, (class_name,))[class_name]
        assert fields["mMaskType"] == mask_type
        data["popup_masks"][str(mask_type)] = {"fields": fields, "source": reader.apk.provenance(mask_node.object_reader),
            "prefab_resource": "Map11/UI/WorldCanvas", "prefab_path": "Content/Popup/" + name,
            "blur_material": "World/Mainline/ScreenBlur"}
    for entry in original_mask.m_Component:
        component = entry.component.deref()
        if component.type.name != "MonoBehaviour" or component.read(check_read=False).m_Script.read().m_ClassName != "RawImage":
            continue
        raw = component.get_raw_data()
        uv = list(struct.unpack_from("<ffff", raw, len(raw) - 16))
        assert uv[2] > 0 and uv[3] > 0
        data["popup_masks"]["3"]["raw_image"] = {"m_UVRect": uv,
            "source": reader.apk.provenance(component), "source_sha256": hashlib.sha256(raw).hexdigest()}
    assert "raw_image" in data["popup_masks"]["3"]
    header_components = {}
    for node in reader.walk(reader.object(world_source["bundle"], world_source["path_id"]).read()):
        for entry in node.m_Component:
            component = entry.component.deref()
            if component.type.name == "MonoBehaviour" and component.read(check_read=False).m_Script.read().m_ClassName == "SafeArea":
                fields = reader.schemas.read(component)
                safe_sources[node_path(node)] = {"source": reader.apk.provenance(component),
                    "fields": {key: fields[key] for key in ("PaddingTop", "PaddingBottom", "DisableTopAdapt", "DisableBottomAdapt")}}
        from prepare_character_prop_bindings import node_path
        path = node_path(node)
        if not path.startswith(player_path + "/"):
            continue
        for entry in node.m_Component:
            component = entry.component.deref()
            if component.type.name != "MonoBehaviour":
                continue
            name = component.read(check_read=False).m_Script.read().m_ClassName
            if name not in ("AspectRatioFitter", "RawImage", "GraphicCapsuleMeshClipper"):
                continue
            raw = component.get_raw_data()
            if name == "RawImage":
                fields = {"m_UVRect": list(struct.unpack_from("<ffff", raw, len(raw) - 16))}
            elif name == "AspectRatioFitter":
                mode, ratio = struct.unpack_from("<if", raw, len(raw) - 8)
                assert mode in range(5) and ratio > 0
                fields = {"m_AspectMode": mode, "m_AspectRatio": ratio}
            else:
                fields = reader.schemas.read(component)
            fields["source_sha256"] = hashlib.sha256(raw).hexdigest()
            header_components.setdefault(path, {})[name] = {key: value for key, value in fields.items()
                if key not in ("m_GameObject", "m_Script", "m_Name")}
    data["shared_header"]["components"] = header_components
    files = {row["path"]: row for row in manifest["files"]}
    template = next((module / "Art/sprites").glob("*.png.meta")).read_text(encoding="utf-8")
    for uri in sorted(uris):
        relative = uri.removeprefix("res://assets/official/")
        source = ROOT / "assets/official" / relative
        expected = city["resources"][uri]["sha256"]
        assert digest(source) == expected
        target = module / "Art" / relative
        if not target.exists():
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
            guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs-official-skills/" + relative).hex
            target.with_suffix(target.suffix + ".meta").write_text(
                re.sub(r"^guid: .*$", "guid: " + guid, template, flags=re.M), encoding="utf-8")
        assert digest(target) == expected
        path = target.relative_to(ROOT / "unity").as_posix()
        files[path] = {"path": path, "bytes": target.stat().st_size, "sha256": expected}
    layout.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    files["Assets/Official/Skills/skill-ui.json"] = {"path": "Assets/Official/Skills/skill-ui.json",
        "bytes": layout.stat().st_size, "sha256": digest(layout)}
    receipt = {"manifest": "854_156383", "bundle": bundle, "bundle_hash": apk.index[bundle],
               "components": records, "threshold": threshold, "empty_prefab": data["item_empty"]["source"],
               "shared_header": data["shared_header"], "character_followers": followers,
               "safe_area_bindings": safe_sources, "character_fonts": character_fonts, "system_message": data["system_message"], "popup_mask": data["popup_mask"], "popup_masks": data["popup_masks"],
               "gift_bindings": gift_bindings, "gift_language_input": data["gift_language_input"], "text_style_inputs": text_sources,
               "rules_input": "assets/official/rules.json", "rules_input_sha256": digest(rules_path),
               "ui_input": "assets/official/ui.json", "ui_input_sha256": digest(ui_path),
               "resources": {uri: city["resources"][uri] for uri in sorted(uris)},
               "operation": "Recover pinned original skill UI, shared header, safe areas, fonts, system message, popup masks and icon bindings; copy verified decoded sprites."}
    receipt_path = ROOT / "assets/reference/skill-ui/unity-icon-source-manifest.json"
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    manifest["icon_bindings_source"] = {"path": receipt_path.relative_to(ROOT).as_posix(), "sha256": digest(receipt_path)}
    manifest["files"] = sorted(files.values(), key=lambda row: row["path"])
    manifest["prefabs"] = sorted(set(manifest["prefabs"] + ["item_empty"]))
    manifest["art_files"] = len(list((module / "Art").rglob("*.png")))
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"threshold": threshold, "qualities": qualities, "art_files": manifest["art_files"]}, ensure_ascii=False))


if __name__ == "__main__":
    restore()
