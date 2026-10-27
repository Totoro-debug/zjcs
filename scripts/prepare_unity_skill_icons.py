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
    skill_source = data["character_skills"]["source"]
    skill_root = reader.object(skill_source["bundle"], skill_source["path_id"]).read()
    skill_tab = next(node for node in reader.walk(skill_root)
        if node.m_Name == "CharacterPanelSkillDetailPanelSkillTabGroupItem_11")
    skill = reader.component_fields(skill_source["bundle"], skill_tab.object_reader.path_id, ("TabItemTopBarSwitcher",))
    assert skill["TabItemTopBarSwitcher"]["ShowTypeList"] == [18]
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
    world_source = shared_manifest["source"]["prefabs"]["WorldCanvas"]
    header_components = {}
    for node in reader.walk(reader.object(world_source["bundle"], world_source["path_id"]).read()):
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
               "shared_header": data["shared_header"],
               "rules_input": "assets/official/rules.json", "rules_input_sha256": digest(rules_path),
               "ui_input": "assets/official/ui.json", "ui_input_sha256": digest(ui_path),
               "resources": {uri: city["resources"][uri] for uri in sorted(uris)},
               "operation": "Copy original empty icon prefab; read SkillStarInfo threshold and StarMiniInfo Quality-to-Sprite PPtrs; copy verified decoded sprites."}
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
