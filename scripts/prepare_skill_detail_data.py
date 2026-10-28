"""Recover skill tags and range diagrams from the pinned official configuration."""
import hashlib
import re

import msgpack

from official_apk import ROOT


def restore(reader, rules, ui, city):
    obj = reader.apk.asset("defaultpackage_assets_config_ec.bundle", "/SkillItemRangeInfos.g.bytes")
    raw = obj.read().m_Script.encode("utf-8", "surrogateescape")
    decoded = msgpack.unpackb(raw, raw=False, strict_map_key=False)
    assert len(decoded) == 1 and isinstance(decoded[0], dict)
    ranges = {}
    for key, row in decoded[0].items():
        diameter, center, positions, types = row
        assert diameter > 0 and len(center) == 2 and len(positions) == len(types)
        assert len({tuple(value) for value in positions}) == len(positions)
        assert all(isinstance(value, int) for value in types)
        ranges[str(key)] = row
    enum_path = ROOT / ".tools/source-packages/official-code/common-source/Common/ESkillTag.cs"
    enum_raw = enum_path.read_bytes()
    body = enum_raw.decode("utf-8-sig").split("{", 1)[1].rsplit("}", 1)[0]
    tag_names = {}
    value = 0
    for item in body.split(","):
        match = re.fullmatch(r"\s*(\w+)(?:\s*=\s*(\d+))?\s*", item)
        if not match:
            assert not item.strip()
            continue
        if match[2] is not None:
            value = int(match[2])
        tag_names[str(value)] = match[1]
        value += 1
    assert tag_names["22"] == "SingleHit" and tag_names["23"] == "MultipleHit"
    tag = ui["pet_skill_tag"]
    original = reader.object(tag["source"]["bundle"], tag["source"]["path_id"])
    assert original.read().m_Name == "SkillTagItem"
    professions = {}
    skill_professions = {}
    for row in rules["profession_base"]:
        name = row["Icon1"].rsplit("/", 1)[-1]
        matches = [uri for uri, entry in city["resources"].items()
                   if uri.startswith("res://assets/official/sprites/") and entry.get("object") == name
                   and entry.get("bundle") == "defaultpackage_assets_image_itemicon_professionsmall.bundle"]
        assert len(matches) == 1, (row["Profession"], name, matches)
        professions[str(row["Profession"])] = {"previous": row["PrePro"], "icon": matches[0]}
        if row["Skills"]:
            for skill_id in row["Skills"] + row["FightUsedSkills"]:
                skill_professions[str(skill_id)] = row["Profession"]
    return {
        "ranges": ranges,
        "skills": {str(row["ClassId"]): {"tags": row["Tags"], "user_type": row["UserType"]} for row in rules["skill"]},
        "tag_names": tag_names,
        "professions": professions,
        "skill_professions": skill_professions,
        "active_skill_types": rules["game_settings"]["ActiveSkillTypes"],
        "inputs": {
            "range_config": {"source": reader.apk.provenance(obj), "payload_sha256": hashlib.sha256(raw).hexdigest(), "rows": len(ranges)},
            "tag_prefab": {"source": reader.apk.provenance(original), "payload_sha256": hashlib.sha256(original.get_raw_data()).hexdigest()},
            "tag_enum": {"path": enum_path.relative_to(ROOT).as_posix(), "sha256": hashlib.sha256(enum_raw).hexdigest()},
            "source_code": {".tools/source-packages/official-code/" + name: hashlib.sha256(
                (ROOT / ".tools/source-packages/official-code" / name).read_bytes()).hexdigest()
                for name in ("common-source/PropConfigUtils.cs", "common-source/Common/PropConfigUtils.cs",
                    "common-source/Common/ProfessionBaseInfoParser.cs", "common-source/Common/LanguageUtils.cs",
                    "client-source/SkillPanelPropPanel.cs", "client-source/SkillDetailDialog.cs",
                    "client-source/SkillTagItem.cs", "client-source/SkillTagGo.cs", "client-source/SkillDetailDialogRangeUI.cs",
                    "client-source/ItemDetailDialog.cs", "client-source/BlessLevelTextViewChanger.cs")},
        },
    }


def restore_grid(reader, schemas, data):
    from prepare_character_prop_bindings import node_path
    source = data["skill_panel_detail"]["source"]
    root = reader.object(source["bundle"], source["path_id"]).read()
    for node in reader.walk(root):
        if not node_path(node).endswith("/SkillDetailDialogRangeUI/GridLayoutGroup"):
            continue
        for entry in node.m_Component:
            obj = entry.component.deref()
            if obj.type.name != "MonoBehaviour" or obj.read(check_read=False).m_Script.read().m_ClassName != "LoopScrollGridLayoutGroup":
                continue
            fields = schemas.read(obj)
            names = ("m_Padding", "m_ChildAlignment", "m_StartCorner", "m_StartAxis", "m_CellSize", "m_Spacing", "m_Constraint", "m_ConstraintCount")
            data["detail_data"]["range_grid"] = {name: fields[name] for name in names}
            data["detail_data"]["inputs"]["range_grid"] = {"source": reader.apk.provenance(obj), "payload_sha256": hashlib.sha256(obj.get_raw_data()).hexdigest()}
    assert "range_grid" in data["detail_data"]
