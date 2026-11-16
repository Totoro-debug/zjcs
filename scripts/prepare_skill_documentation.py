"""Recover official skill wording and keep skill pages limited to the requested seven fields."""
from __future__ import annotations

import argparse
import hashlib
import html
import json
import re
from pathlib import Path

import xxhash

from official_apk import Apk
from official_source import OfficialSource
from official_tables import Constructors
from refresh_current_localization import language

ROOT = Path(__file__).resolve().parents[1]
PAGES = ("warrior", "mage", "knight", "fighter", "warlock", "sage")
SCOPE = {1, 10, 2, 6, 11, 15}
RECEIPT = ROOT / "assets/reference/skill-documentation-source-manifest.json"
HEADER = "| 技能类型 | 技能 ID | 技能名 | 技能描述 | 特殊词条解释 | 是否完成 | 实现要点 |"
NOTES = {
    10016: "已确认按官方配置补齐单机选点与直线路径；保留原伤害与演出图，完整运行验证与交付未完成。",
    10042: "多段范围与状态消耗时机尚待确认。",
    10061: "已验证合成培养与实战；烈焰每实体唯一，后来者替换来源并重计两回合；获得、回合开始、移动结束及敌方进入时触发，伤害归施法者；正式交付未完成。",
    10509: "受战技伤害后累积下次战技增伤，最多十层；全盾可叠层，闪避、致盲、免疫或死亡不叠层。",
    10516: "按负面或异常状态是否存在判断增伤条件。",
    10518: "仅致命伤害触发一次，先保留1点生命，再执行原治疗与两回合不屈；入场一血或非致命降至一血不消耗次数。已验证规则与原生实战；正式交付未完成。",
    10537: "持有者反击攻击者，继承秘法培养；全盾可触发，闪避、致盲、免疫不触发。",
    10540: "护盾状态存在即增攻；护盾消失立即撤销增攻。",
    10541: "存活持有者在格挡后反击攻击者，继承秘法培养并按自身防御计算；全盾可触发，闪避、致盲、免疫不触发。",
    10563: "仅伤害打破最后一个护盾时触发。",
    10582: "入场、受伤及治疗后按当前缺血比例重算，每15%一层、最多5层，不累计失血或回血余量。已验证规则与原生实战；正式交付未完成。",
    10588: "已修复碎片类型与合成品阶，接通合成、装配、升级、进阶及读档；完整技能交付仍未完成。",
    10589: "入场、受伤及治疗后按当前缺血比例重算，每15%一层、最多5层，不累计失血或回血余量。已验证规则与原生实战；正式交付未完成。",
}


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def plain(value):
    value = re.sub(r"<br\s*/?>", "\n", value or "", flags=re.I)
    return html.unescape(re.sub(r"<[^>]*>", "", value)).strip()


def cell(value):
    return plain(value).replace("|", "&#124;").replace("\r", "").replace("\n", "<br>")


def source(path):
    return {"path": path.relative_to(ROOT).as_posix(), "sha256": sha(path.read_bytes())}


def build(write):
    apk = Apk.current()
    if apk.version != "884_156692" or OfficialSource().version != apk.version:
        raise ValueError("Review the selected APK/language version before rebuilding skill documentation.")
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    constructors = Constructors(code)
    table_inputs = []

    def table(name, cls):
        obj = apk.asset("config_luban", "/" + name + ".g.bytes")
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        table_inputs.append({"name": name, "source": apk.provenance(obj), "sha256": sha(raw)})
        return constructors.table(raw, cls)

    professions = {row["Profession"]: row for row in table("profession_base", "TbProfessionBaseInfo")}
    skills = {row["ClassId"]: row for row in table("skill", "TbSkillInfo")}
    items = {row["ClassId"]: row for row in table("item", "TbItemInfo")}
    texts, language_inputs = language(ROOT / ".tools/source-packages/WwiseBnk")

    def text(key, required=True):
        value = texts.get(xxhash.xxh64_intdigest(key.encode("utf8")))
        if required and value is None:
            raise ValueError("Missing original language key: " + key)
        return value

    # Keep explicit routing and existing rank anchors; never infer a profession from its displayed name.
    routes = {}
    titles = {}
    existing_notes = {}
    for page in PAGES:
        path = ROOT / f"docs/features/skills-{page}.md"
        old = path.read_text(encoding="utf-8-sig")
        titles[page] = old.splitlines()[0]
        routes[page] = [(int(rank), int(profession), title) for title, rank, profession in re.findall(
            r"^### (.*?Rank=(\d+)，?[,，]?\s*Profession=(\d+).*?)$", old, re.M)]
        if not routes[page]:
            # Existing source pages use an ASCII comma between Rank and Profession.
            routes[page] = [(int(rank), int(profession), title) for title, rank, profession in re.findall(
                r"^### (.*?Rank=(\d+), Profession=(\d+).*?)$", old, re.M)]
        if not routes[page]:
            raise ValueError("No explicit profession/rank routing in " + str(path))
        if HEADER in old:
            for line in old.splitlines():
                fields = [value.strip() for value in line.strip("|").split("|")]
                if len(fields) == 7 and fields[1].isdigit():
                    existing_notes[int(fields[1])] = (fields[5], fields[6])

    rows_by_id = {}
    scope_ids = {item for prof in SCOPE for item in professions[prof]["Skills"] + professions[prof]["FightUsedSkills"]}
    if len(scope_ids) != 104:
        raise ValueError("Review the requested first/second profession scope.")
    documents = {}
    for page in PAGES:
        lines = [titles[page], "", "[总索引](../README.md) · [技能目录](skills.md)", "", "状态：未完成", ""]
        glossary = {}
        for rank, profession, title in routes[page]:
            prof = professions[profession]
            if prof["Rank"] != rank:
                raise ValueError("Document profession rank differs from the official table.")
            lines.extend([f'<a id="rank-{rank}"></a>', "", "### " + title, "", HEADER,
                          "| --- | --- | --- | --- | --- | --- | --- |"])
            for item_id in prof["Skills"] + prof["FightUsedSkills"]:
                skill = skills[item_id]
                name = text(f"item_{item_id}_name")
                description = text(f"item_{item_id}_func_desc")
                terms = []
                for index, (kind, parameter) in enumerate(zip(skill["HyperLinkTypes"] or [], skill["HyperLinkDatas"] or [])):
                    match = re.search(r"(?:EntryId|MonsterId):(\d+)", parameter)
                    if kind not in (0, 1) or not match:
                        if item_id in scope_ids:
                            raise ValueError("Review an unsupported original skill hyperlink: " + str(item_id))
                        continue
                    term_id = int(match[1])
                    if kind == 0:
                        term_name, term_desc = text(f"entry_{term_id}_name"), text(f"entry_{term_id}_desc")
                    else:
                        term_name = text(f"monster_{term_id}")
                        term_desc = text(f"monster_desc_{term_id}", required=False)
                        if term_desc is None:
                            # The original monster popup may have no description key.
                            # Its summoning skill's exact wording still explains the named unit.
                            term_desc = description
                    anchor = f"term-{kind}-{term_id}"
                    glossary[anchor] = (term_name, term_desc)
                    terms.append(f"[{cell(term_name)}](#{anchor})")
                if item_id in NOTES:
                    note = NOTES[item_id]
                elif profession not in SCOPE:
                    note = "保留官方说明；不纳入本次一、二阶迁移完成范围。"
                elif items[item_id]["ItemType"] == 101 and not skill["PassiveStatusIdList"]:
                    note = "已接入属性培养与装配；完整获得流程待补齐。"
                elif items[item_id]["ItemType"] == 101:
                    note = "已接入被动配置；触发效果与状态组合仍待完整迁移。"
                else:
                    note = "已接入施法；条件、子技能、状态及原生演出仍待完整验收。"
                complete, note = existing_notes.get(item_id, ("未完成", note))
                kind_name = "内置战斗项" if item_id in prof["FightUsedSkills"] else {100: "主动战技", 101: "被动秘法"}[items[item_id]["ItemType"]]
                lines.append("| " + " | ".join([kind_name, str(item_id), cell(name), cell(description),
                    "、".join(terms) or "无", complete, note]) + " |")
                rows_by_id[item_id] = {"id": item_id, "name": name, "description": description,
                    "hyperlink_types": skill["HyperLinkTypes"], "hyperlink_data": skill["HyperLinkDatas"]}
            lines.append("")
        lines.extend(["## 特殊词条解释", "", "| 特殊词条 | 解释 |", "| --- | --- |"])
        for anchor, (name, description) in glossary.items():
            lines.append(f'| <a id="{anchor}"></a>{cell(name)} | {cell(description)} |')
        lines.append("")
        documents[f"docs/features/skills-{page}.md"] = "\n".join(lines).encode("utf8")
    common = ["# 技能目录", "", "[总索引](../README.md)", "", "状态：未完成", "",
              "本次迁移范围：一阶初始职业与二阶首次转职的全部技能，共 104 项；转职后继承前置职业技能。", "",
              "技能记录仅包含技能类型、技能 ID、技能名、官方描述、特殊词条解释、是否完成与实现要点。", "",
              "| 职业技能 | 是否完成 |", "| --- | --- |"]
    labels = {"warrior": "战士（一阶）", "mage": "法师（一阶）", "knight": "骑士（二阶）",
              "fighter": "斗士（二阶）", "warlock": "术士（二阶）", "sage": "贤者（二阶）"}
    for page in PAGES:
        common.append(f"| [{labels[page]}](skills-{page}.md) | 未完成 |")
    common.extend(["", '<a id="自定义实现"></a>', '<a id="尚未实现"></a>', "", "## 实现要点", "",
        "按官方描述迁移主动战技、被动秘法、被动与触发效果、子技能、增益与减益、动画及人物动作；已确认的单机边界只在对应技能的实现要点中简述。", "",
        "合成、装配、升级、进阶与存档使用公共培养及界面流程，技能差异由官方配置驱动。", "",
        "装配仅使用已持有且职业可用的技能；空战技方案允许进入战斗，正常结算回合与被动，不自动补战技。", "",
        "连锁下一跳以上一命中格为中心沿用原范围，遵守重复、回跳与优先未访配置；无合法目标即中止。合法目标按最近身体格选择，同距按单位 ID、原身体格顺序稳定选择。", "",
        "完整技能流程、原生表现与 Windows 发布仍未完成。高阶职业仅保留既有技能说明，不计入本次完成范围。", ""])
    documents["docs/features/skills.md"] = "\n".join(common).encode("utf8")
    for name, payload in documents.items():
        path = ROOT / name
        if write:
            path.write_bytes(payload)
        elif path.read_bytes() != payload:
            raise ValueError("Skill documentation differs from its official wording/allowed fields: " + name)
    proof = {"manifest": apk.version, "scope_ids": sorted(scope_ids), "tables": table_inputs,
        "language": language_inputs, "official_code": [source(code / name) for name in (
            "common-source/Common/LanguageUtils.cs", "common-source/Common/SkillInfo.cs",
            "client-source/SkillHyperLinkDataProvider.cs", "client-source/HyperLinkClickHandlerEntry.cs",
            "client-source/HyperLinkClickHandlerMonster.cs")],
        "skills": [rows_by_id[key] for key in sorted(rows_by_id)],
        "outputs": [{"path": name, "sha256": sha(payload)} for name, payload in documents.items()],
        "conversion": "Use decompiled LanguageUtils and SkillHyperLinkDataProvider keys; resolve verified selected Chinese language records. Remove only display markup, retain wording and glossary explanations; preserve manually maintained completion and concise implementation notes. No detailed implementation or test history in skill pages."}
    if write:
        RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    elif not RECEIPT.exists() or json.loads(RECEIPT.read_bytes()) != proof:
        raise ValueError("Skill documentation source receipt differs.")
    print(f"Official skill documentation verified: {len(scope_ids)} requested skills; {len(rows_by_id)} existing skill records; seven permitted fields.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    build(parser.parse_args().write)
