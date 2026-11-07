"""Decode current official Type 57 daily-dungeon leaderboard gifts."""
from pathlib import Path
import hashlib
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    from official_apk import Apk
    from official_tables import Constructors
    from prepare_dungeon_ui_bindings import route_texts
    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Review Type 57 source version before reproduction")
    code = ROOT / ".tools/source-packages/current/884_156692/official-code"
    tables_source = (code / "common-source/Common/Tables.cs").read_text(encoding="utf-8-sig")
    table_type, = re.findall(r'new (Tb\w+)\(loader\("achievement"\)\)', tables_source)
    obj = apk.asset("config_luban", "/achievement.g.bytes")
    raw = obj.read().m_Script.encode("utf8", "surrogateescape")
    decoded = Constructors(code).table(raw, table_type)
    difficulties = dict(Normal=0, Hard=1, Nightmare=2, Hell=3, Abyss=4)
    grades = dict(F=1, E=2, D=3, C=4, B=5, A=6, S=7, SS=8, SSS=9)
    rows, indexes, names = [], {}, {}
    for row in sorted(decoded, key=lambda value: value["Id"]):
        if row["AchievementType"] != 57:
            continue
        group = re.fullmatch(r"\{DungeonId:(\d+),Difficulty:'(\w+)',RangeType:'Domain'\}", row["GroupParams"])
        complete = re.fullmatch(r"\{GradeType:'(\w+)',Count:(\d+)\}", row["CompleteParams"])
        if group is None or complete is None or row["CycleCount"] or row["CycleIncrementParams"]:
            raise ValueError("Unsupported Type 57 parameters: " + str(row))
        index = indexes.get(row["Group"], 0)
        indexes[row["Group"]] = index + 1
        rows.append(dict(Id=(index << 12) | row["Group"], SourceId=row["Id"], Group=row["Group"],
            DungeonId=int(group[1]), Difficulty=difficulties[group[2]], Grade=grades[complete[1]],
            RequiredCount=int(complete[2]), Rewards=[dict(ItemId=int(k), Count=int(v)) for k,v in row["Reward"].items()]))
        names[rows[-1]["Id"]] = ("ui_daily_dungeon_item_title_" + group[1] + "_" + group[2], complete[1])
    if len(rows) != 409 or any(not row["Rewards"] or row["RequiredCount"] <= 0 for row in rows):
        raise ValueError("Official Type 57 catalog failed validation")
    title_key = "ui_achievement_content_DailyDungeonGradeTop"
    keys = set(key for key, _ in names.values()) | {title_key}
    texts, language_inputs, missing = route_texts(apk, keys, [title_key])
    translations = {row["key"]: row["value"] for row in texts}
    for row in rows:
        key, grade = names[row["Id"]]
        row["Title"] = translations[title_key].format(translations[key], grade) if key in translations else None
    supported = json.loads((ROOT / "unity/Assets/Game/Resources/Dungeons/daily-dungeon-leaderboards.json").read_text())
    supported_keys = {(row["dungeonId"],row["difficulty"]) for row in supported["levels"]}
    if any(row["Title"] is None and (row["DungeonId"],row["Difficulty"]) in supported_keys for row in rows):
        raise ValueError("An active leaderboard gift title is missing")
    output = ROOT / "unity/Assets/Game/Resources/Dungeons/daily-dungeon-top-achievements.json"
    output.write_text(json.dumps(dict(Manifest=apk.version, AchievementType=57, Rows=rows), ensure_ascii=False, indent=2)+"\n", encoding="utf8", newline="\n")
    meta = output.with_name(output.name + ".meta")
    if not meta.exists():
        meta.write_text("fileFormatVersion: 2\nguid: " + hashlib.sha256(b"zjcs-daily-dungeon-top-achievements-884").hexdigest()[:32] + "\nTextScriptImporter:\n  externalObjects: {}\n", encoding="ascii")
    sources = [code / "common-source/Common" / (name+".cs") for name in (table_type,"Tables","AchievementTaskDailyDungeonGradeTop","AchievementDataDailyDungeonGradeTop","AchievementDataKeyDailyDungeonGradeTop","AchievementInfo","DailyDungeonDifficulty","BattleResultGradeType")]
    sources.extend(code / "client-source" / (name+".cs") for name in ("DailyDungeonTopPanel", "AchievementContentDailyDungeonGradeTop", "AchievementPanelItem"))
    sources.append(code / "common-source/Common/LanguageUtils.cs")
    receipt = dict(manifest=apk.version, apk_manifest=apk.source.apk_version, apk_sha256=apk.source.apk_sha256,
        table=apk.provenance(obj), raw_sha256=hashlib.sha256(raw).hexdigest(), language_inputs=language_inputs,
        missing_inactive_title_keys=missing,
        operation="Decode original Luban achievement table; filter Type 57 Domain rows; retain group/index achievement ID, dungeon/difficulty/grade, Count and rewards without cycle generation.",
        code_sources=[dict(path=s.relative_to(ROOT).as_posix(),sha256=digest(s)) for s in sources],
        tools=[dict(path=s.relative_to(ROOT).as_posix(),sha256=digest(s)) for s in (Path(__file__),ROOT/"scripts/official_tables.py")],
        outputs=[dict(path=s.relative_to(ROOT).as_posix(),sha256=digest(s)) for s in (output,meta)],rows=len(rows))
    receipt_path=ROOT/"assets/reference/dungeon-ui/daily-dungeon-top-achievements-source-manifest.json"
    receipt_path.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+"\n",encoding="utf8",newline="\n")
    print(json.dumps(dict(rows=len(rows),groups=len(indexes))))


if __name__ == "__main__":
    build()
