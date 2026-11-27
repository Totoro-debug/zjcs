"""Record the selected official APK's pet descriptions and shared glossary links."""
import argparse
import hashlib
import html
import json
from pathlib import Path
import re
import struct

from official_apk import Apk, ROOT
from official_tables import Constructors
import xxhash


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def plain(value):
    value = re.sub(r"<br\s*/?>", "\n", value or "", flags=re.I)
    return html.unescape(re.sub(r"<[^>]*>", "", value)).strip()


def cell(value):
    return plain(value).replace("|", "&#124;").replace("\r", "").replace("\n", "<br>")


def load_inputs():
    apk = Apk.current()
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    parser = Constructors(code)
    mapping = {name: cls for cls, name in re.findall(
        r'new (Tb\w+)\(loader\("([^"]+)"\)\)', parser.source("Tables"))}
    inputs = []

    def table(name):
        obj = apk.asset("config_luban", "/" + name + ".g.bytes")
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        inputs.append(dict(name=name, source=apk.provenance(obj), sha256=sha(raw)))
        return parser.table(raw, mapping[name])

    texts = {}
    for name in ("wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile",
                 "wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile"):
        verified = apk.reader.read("WwiseBnk", name)
        raw = verified.data
        count, = struct.unpack_from("<i", raw)
        start = 4 + count * 12
        if count < 0 or start > len(raw):
            raise ValueError("Invalid original language index")
        for index in range(count):
            identity, offset = struct.unpack_from("<Qi", raw, 4 + 12 * index)
            position = start + offset
            if offset < 0 or position + 2 > len(raw):
                raise ValueError("Invalid original language offset")
            size, = struct.unpack_from("<H", raw, position)
            if position + 2 + size > len(raw):
                raise ValueError("Truncated original language entry")
            texts[identity] = raw[position + 2:position + 2 + size].decode("utf8")
        inputs.append(dict(name=name, source=dict(verified.provenance), sha256=sha(raw)))

    def text(key, required=True):
        value = texts.get(xxhash.xxh64_intdigest(key.encode("utf8")))
        if value is None and required:
            raise ValueError("Missing original language key: " + key)
        return value

    return apk, code, mapping, inputs, table, text


PAGE = ROOT / "docs/character/pet-catalog.md"
GLOSSARY = ROOT / "docs/character/skills-glossary.md"
RECEIPT = ROOT / "assets/reference/pet-documentation-source-manifest.json"
TABLES = ("pet", "pet_evolution", "skill", "item", "game_settings", "npc_pet",
          "gacha", "chest", "reward_rule", "random_reward", "shop", "shop_goods_group",
          "shop_goods", "special_gacha", "special_gacha_award_group", "activity_special_gacha",
          "activity_festival_stage", "general_activity_shop")
PHASES = {1: "Childhood", 2: "Adulthood", 3: "CompleteForm"}
TYPES = {0: "Attacker", 1: "Tank", 2: "Assistance"}
QUALITIES = {3: "Purple", 4: "Orange", 5: "Gold"}


def acquisition(pet, data, text):
    """Follow configured reward edges; a candidate does not imply a live event."""
    pet_id, piece_id = pet["ClassId"], pet["PetPieceId"]
    randoms = {r["Id"]: r for r in data["random_reward"]}
    rules = {r["Id"]: r for r in data["reward_rule"]}

    def reward_items(rule_id):
        result = set()
        for ident in rules[rule_id]["IdList"] or []:
            if ident in randoms:
                row = randoms[ident]
                # Relevant pet pools use explicit candidates, never approximate filters.
                if row["IdList"]:
                    result.update(row["IdList"])
            else:
                result.add(ident)
        return result

    sources, evidence = [], {}
    gacha = next(r for r in data["gacha"] if r["GachaType"] == 3)
    chest = next(r for r in data["chest"] if r["ClassId"] == gacha["ChestId"])
    normal_rules = set(chest["RewardRules"] or []) | {i for group in chest["GachaRewardOfTimes"] or [] for i in group}
    normal_items = set().union(*(reward_items(i) for i in normal_rules))
    evidence["normal_draw"] = pet_id in normal_items
    if pet_id in normal_items:
        sources.append("日常幻兽结缘")

    shop = next(r for r in data["shop"] if r["Id"] == 7)
    groups = [r for r in data["shop_goods_group"] if r["GroupId"] in shop["GroupIdList"]]
    goods_ids = set(shop["FixedGoodsList"] or []) | {i for r in groups for i in r["ShopGoodsList"]}
    goods = [r for r in data["shop_goods"] if r["Id"] in goods_ids and r["ItemId"] == pet_id]
    evidence["exchange_goods"] = [r["Id"] for r in goods]
    if goods:
        prices = sorted({f'{r["OriginalPrice"]:g} ' + plain(text("item_" + str(r["Currency"]) + "_name")) for r in goods})
        sources.append("幻兽兑换（" + "／".join(prices) + "；受商品组开放条件与库存限制）")

    award_groups = {r["GachaAwardGroupId"] for r in data["special_gacha_award_group"]
                    if any(pet_id in reward_items(i) for i in r["Awards"])}
    special_ids = {r["GachaId"] for r in data["special_gacha"] if award_groups.intersection(r["AwardGroups"])}
    activities = [r for r in data["activity_special_gacha"] if special_ids.intersection(r["GachaIds"])]
    evidence["special_gacha_activities"] = [r["ActivityId"] for r in activities]
    if activities:
        sources.append("活动抽取／活动结缘（活动开放时的配置候选）")
    stages = [r for r in data["activity_festival_stage"] if str(pet_id) in {str(k) for k in r["Reward"] or {}}]
    evidence["festival_stages"] = [dict(activity=r["ActivityId"], stage=r["StageId"]) for r in stages]
    if stages:
        sources.append("联动活动关卡奖励（" + "、".join(f'活动{r["ActivityId"]}第{r["StageId"]}阶段' for r in stages) + "）")
    boxes = [r["ClassId"] for r in data["chest"]
             if str(pet_id) in {str(k) for k in (r["OptionalRewards"] or {})}]
    evidence["optional_chests"] = boxes
    if boxes:
        sources.append("、".join(plain(text(f"item_{i}_name")) for i in boxes) + "自选奖励")
    npc = [r["Id"] for r in data["npc_pet"] if r["PetId"] == pet_id]
    evidence["npc_ids"] = npc
    if npc:
        sources.append("NPC 专属关联（" + "、".join(str(i) for i in npc) + "）；当前未找到玩家直接获取入口")
    evidence["activity_piece_shop_ids"] = [r["Id"] for r in data["general_activity_shop"]
        if str(piece_id) in {str(k) for k in r["Award"]}]
    if not sources:
        sources.append("已配置幻兽与合成材料，玩家直接获取入口尚待核实")
    piece = plain(text(f"item_{piece_id}_name"))
    sources.append(f"碎片合成配置：{piece} ×{pet['PieceMergeCount']}（须先有碎片来源及可用合成入口）")
    if evidence["activity_piece_shop_ids"]:
        sources.append("活动礼包另有专属碎片奖励")
    return "；".join(sources) + "。", evidence


def render(data, text):
    items = {r["ClassId"]: r for r in data["item"]}
    skills = {r["ClassId"]: r for r in data["skill"]}
    glossary = GLOSSARY.read_text(encoding="utf-8-sig")
    terms = {}
    pet_records = []
    old = PAGE.read_text(encoding="utf-8-sig") if PAGE.exists() else ""
    existing_notes = {int(i): note.strip() for i, note in re.findall(
        r'<a id="pet-(\d+)"></a>.*?### 迁移详情\n\n(.*?)(?=\n<a id="pet-|\Z)', old, re.S)}

    def term(entry, energy=False):
        anchor = f"term-{'9' if energy else '0'}-{entry}"
        name, desc = cell(text(f"entry_{entry}_name")), cell(text(f"entry_{entry}_desc"))
        if energy:
            setting = data["game_settings"]["PetSetting"]
            desc = desc.format("对应实体化技能的能量上限", "技能基础初始能量＋能力树初始能量加成",
                               f'{setting["NaturalRecoverEnergy"]:g}', f'{setting["MasterLoseHpRecoverEnergy"]:g}')
            desc += "<br>上限和基础初始值见各幻兽实体化技能；能力树加成随培养变化。"
        row = f'| <a id="{anchor}"></a>{name} | {desc} |'
        found = re.search(r'^\| <a id="' + re.escape(anchor) + r'"></a>.*$', glossary, re.M)
        if found and found[0] != row:
            raise ValueError("Review wording conflict with existing glossary: " + anchor)
        terms[anchor] = row
        return f"[{name}](skills-glossary.md#{anchor})"

    def skill_row(stage, kind, ident):
        skill = skills[ident]
        links = []
        kinds, params = skill["HyperLinkTypes"] or [], skill["HyperLinkDatas"] or []
        if len(kinds) != len(params):
            raise ValueError("Unpaired official skill hyperlinks")
        for link_kind, param in zip(kinds, params):
            if link_kind in (0, 9):
                entry = int(re.search(r'EntryId:(\d+)', param)[1])
                link = term(entry, link_kind == 9)
            elif link_kind == 8:
                pet_id = int(re.search(r'PetId:(\d+)', param)[1])
                link = f'[{cell(text(f"pet_{pet_id}_name"))}](#pet-{pet_id})'
            else:
                raise ValueError("Unhandled original pet hyperlink: " + str(link_kind))
            if link not in links:
                links.append(link)
        description = text(f"item_{ident}_func_desc", required=False)
        if description is None and not skill["Invisible"]:
            raise ValueError("Missing visible pet skill description: " + str(ident))
        description = cell(description) if description else "隐藏配置项；官方未提供独立技能说明，效果参见所列词条。"
        return f'| {stage} | {kind} | {ident} {cell(text(f"item_{ident}_name"))} | {description} | {"、".join(links) or "无"} |'

    lines = ["# 幻兽资料与迁移详情", "", "[总索引](../README.md) · [幻兽系统](pets.md) · [特殊词条](skills-glossary.md) · [来源凭据](../../assets/reference/pet-documentation-source-manifest.json)", "",
             "状态：未完成", "",
             "按所选官方 APK 及已下载配套官方资源中的幻兽、进化、技能和简体中文配置整理，每只幻兽单独成章；各输入的实际来源分别记录在来源凭据中。保留幼年与成年援护的配置差异，战斗技能同时记录本体主动、被动及另行配置的附加技能。仅移除颜色、下划线等显示标记，技能原文中的固定数值保持不变。", "",
             "获取方式记录客户端可证实的奖励和候选关系，不表示限时活动目前开放。约尔德拉为 NPC 关联幻兽，也保留其可提取资料；未将其写成可由玩家抽取的幻兽。", "",
             "迁移详情分别说明资源接入、规则配置和正式流程的边界；配置已收录不能据此判定整只迁移完成。养成及单机约定见[幻兽系统](pets.md)，结缘和兑换的共用实现见[幻兽结缘](../city-and-home/functions/pet-gacha.md)。", ""]
    for pet in data["pet"]:
        ident = pet["ClassId"]
        lines.append(f'- [{cell(text(f"pet_{ident}_name"))}](#pet-{ident})')
    lines.append("")
    for pet in data["pet"]:
        ident = pet["ClassId"]
        evolutions = sorted([r for r in data["pet_evolution"] if r["ClassId"] == ident], key=lambda r: r["EvolutionPhase"])
        if not evolutions:
            raise ValueError("Pet has no original evolution definition")
        source_desc, source_evidence = acquisition(pet, data, text)
        lines += [f'<a id="pet-{ident}"></a>', "", f'## {cell(text(f"pet_{ident}_name"))}', "",
                  f'- 品质：{cell(text("Quality." + QUALITIES[items[ident]["Quality"]]))}。',
                  f'- 类型：{cell(text("PetType." + TYPES[pet["Type"]]))}。',
                  '- 进化阶段：' + "、".join(cell(text("PetEvolutionPhase." + PHASES[r["EvolutionPhase"]])) for r in evolutions) + "。",
                  f'- 获取方式：{source_desc}', ""]
        categories = {"援护技能": [], "实体化技能": [], "战斗技能": []}
        used_ids = set()
        for evolution in evolutions:
            stage = cell(text("PetEvolutionPhase." + PHASES[evolution["EvolutionPhase"]]))
            def add(category, kind, skill_ids):
                for skill_id in skill_ids or []:
                    used_ids.add(skill_id)
                    categories[category].append(skill_row(stage, kind, skill_id))
            add("援护技能", "援护", evolution["SupportSkills"])
            if evolution["EntitySkill"]:
                add("实体化技能", "实体化", [evolution["EntitySkill"]])
            add("战斗技能", "本体主动", evolution["EntityActiveSkills"])
            add("战斗技能", "本体被动", evolution["EntityPassiveSkills"])
            for field, kind in (("ActiveSkills", "附加主动"), ("PassiveSkills", "附加被动")):
                extra = set(evolution[field] or []) - set(evolution["SupportSkills"] or [])
                add("战斗技能", kind, sorted(extra))
        for category, rows in categories.items():
            lines += ["### " + category, ""]
            if rows:
                lines += ["| 阶段 | 分类 | 技能 | 官方描述 | 特殊词条 |", "| --- | --- | --- | --- | --- |", *rows, ""]
            else:
                lines += ["官方当前仅配置幼年期，未配置" + ("实体化技能。" if category == "实体化技能" else "实体化后的战斗技能。"), ""]
        entity_ids = [r["EntitySkill"] for r in evolutions if r["EntitySkill"]]
        if entity_ids:
            energy = skills[entity_ids[0]]
            lines += [f'实体化基础初始能量：{energy["InitEnergy"]}；能量上限：{energy["TriggerNeedEnergy"]}。能力树另有初始能量加成，充能说明见[幻兽能量](skills-glossary.md#term-9-101)。', ""]
        if ident in existing_notes:
            note = existing_notes[ident]
        else:
            art = "部分接入：幼年官方模型、随行及转场资源已接入；完整专属动作、特效和音效仍待逐只验证。"
            if entity_ids:
                art += "成年模型与实体化完整表现仍待逐只核对。"
            if ident == 1619:
                art += "幼年列表图沿已确认方案无图、保留名称，不绘替代图。"
            get_status = "日常抽取或兑换已接入公共本地事务；条件开放、重复转碎片与完整交互仍须逐只验证。" if source_evidence["normal_draw"] or source_evidence["exchange_goods"] else "专属活动／联动奖励入口尚未完成，不能通过日常结缘代替。"
            if source_evidence["npc_ids"]:
                get_status = "仅证实 NPC 关联；NPC 援护与存档恢复仍待完整流程验证，未配置玩家日常获取。"
            note = "\n".join(["| 项目 | 迁移详情 |", "| --- | --- |",
                "| 整体是否完成 | 未完成；尚无本只从正常获取、养成到战斗及读档的完整交付结论。 |",
                "| 美术资源 | " + art + " |",
                "| 技能 | 援护与" + ("实体化、战斗主动／被动" if entity_ids else "幼年战斗关联") + "配置已收录，公共战斗执行已接入部分规则；逐技能触发时机、状态组合与完整演出仍待验证。 |",
                "| 数值 | 品质、类型、等级属性组、技能培养及能力树配置已有本地载荷，公共属性与战力计算已接入；全部培养档位及实战数值仍待逐只比对。 |",
                "| 获取方式 | " + get_status + "碎片合成配方已收录，正常合成入口及持有状态仍待验证。 |",
                "| 养成与存档 | 已有持有、技能方案装配、随行及培养字段的本地保存结构；喂养、进化、潜力、能力树、改名和重置的完整正式入口与读档仍未完成。 |"])
        for required in ("整体是否完成", "美术资源", "技能", "数值", "获取方式", "养成与存档"):
            if f"| {required} |" not in note:
                raise ValueError(f"Missing migration detail for pet {ident}: {required}")
        lines += ["### 迁移详情", "", note, ""]
        pet_records.append(dict(id=ident, quality=items[ident]["Quality"], type=pet["Type"],
                                phases=[r["EvolutionPhase"] for r in evolutions], skill_ids=sorted(used_ids),
                                acquisition=source_evidence))
    for anchor, row in terms.items():
        if f'id="{anchor}"' not in glossary:
            glossary = glossary.rstrip() + "\n" + row + "\n"
    return "\n".join(lines), glossary, pet_records, sorted(terms)


def build(write=False):
    apk, code, mapping, inputs, table, text = load_inputs()
    try:
        data = {name: table(name) for name in TABLES}
        page, glossary, records, terms = render(data, text)
        outputs = {PAGE: page, GLOSSARY: glossary}
        proof = dict(manifest=apk.version, tables_and_language=inputs,
            generator=dict(path=Path(__file__).resolve().relative_to(ROOT).as_posix(), sha256=sha(Path(__file__).read_bytes())),
            official_code=[dict(path=(code / p).relative_to(ROOT).as_posix(), sha256=sha((code / p).read_bytes()))
                for p in ("common-source/Common/PetInfoParser.cs", "common-source/Common/PetEvolutionInfoParser.cs",
                          "common-source/Common/PetType.cs", "common-source/Common/Quality.cs", "common-source/PetSettings.cs",
                          "common-source/Common/LanguageUtils.cs", "client-source/SkillHyperLinkDataProvider.cs",
                          "client-source/PetSkillEnergyEntryNavigateHandler.cs")],
            pets=records, glossary_anchors=terms,
            outputs=[dict(path=PAGE.relative_to(ROOT).as_posix(), sha256=sha(page.encode("utf8"))),
                     dict(path=GLOSSARY.relative_to(ROOT).as_posix(), selection=terms,
                          selected_rows_sha256=sha("\n".join(re.search(
                              r'^\| <a id="' + re.escape(anchor) + r'"></a>.*$', glossary, re.M)[0]
                              for anchor in terms).encode("utf8")))],
            conversion="Decode selected source tables and Chinese strings. Preserve official wording and phase-specific skills; resolve Entry, PetEntity and PetEnergyEntry hyperlinks. Preserve maintained migration detail tables on regeneration. Acquisition describes configured candidates, not current event availability.")
        for path, content in outputs.items():
            if write:
                path.write_bytes(content.encode("utf8"))
            elif path.read_bytes() != content.encode("utf8"):
                raise ValueError("Official pet documentation differs: " + str(path))
        if write:
            RECEIPT.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + "\n").encode("utf8"))
        elif json.loads(RECEIPT.read_bytes()) != proof:
            raise ValueError("Pet documentation source receipt differs")
        print(f'Official pet documentation verified: {len(records)} pets, '
              f'{len(set(i for r in records for i in r["skill_ids"]))} skills, {len(terms)} glossary anchors.')
    finally:
        apk.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    build(parser.parse_args().write)
