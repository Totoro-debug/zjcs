"""Decode native world exploration rules without using the legacy engine payload."""
from pathlib import Path
import json

from official_apk import Apk
from official_tables import Constructors
from prepare_unity_map10_entities import ROOT, sha256, json_bytes, write_meta
from prepare_unity_map11_ui import write_ui_payload

OUT = ROOT / "unity/Assets/Game/Resources/World/Mainline/exploration.json"
RECEIPT = ROOT / "assets/reference/official-map/world-exploration-source-manifest.json"


def build():
    apk, decoder, inputs = Apk(), Constructors(), {}

    def table(name, kind, map_table=False):
        obj = apk.asset("config_luban_map" if map_table else "config_luban", "/" + name + ".g.bytes")
        raw = obj.read().m_Script.encode("utf-8", "surrogateescape")
        inputs[name] = dict(apk.provenance(obj), sha256=sha256(raw))
        return decoder.table(raw, kind)

    settings = table("game_settings", "TbGameSettings")
    settings = settings[0] if isinstance(settings, list) else settings
    tokens = table("explore_token_group", "TbExploreTokenGroupInfo")
    explore_groups = table("explore_entity_group", "TbExploreEntityGroupInfo")
    groups = {value for row in explore_groups for value in row["EntityGroups"]}
    groups.add(51)  # Native WorldExplore guide result group; confirmed offline first-success policy.
    entity_groups = [row for row in table("map_entity_group", "TbMapEntityGroupInfo", True) if row["Group"] in groups]
    condition_rows = table("conditions", "TbConditionInfo")
    needed_conditions = set()

    def collect(condition):
        if condition and condition["_type"] in ("CompositeAndCondition", "CompositeOrCondition"):
            for key in condition["Ids"]:
                if key not in needed_conditions:
                    needed_conditions.add(key)
                    collect(next(row["Condition"] for row in condition_rows if row["ClassId"] == key))

    for row in entity_groups:
        collect(row["FilterCondition"])
    payload = dict(manifest="854_156383", tokenGroups=tokens, exploreGroups=explore_groups,
                   entityGroups=entity_groups,
                   conditions=[row for row in condition_rows if row["ClassId"] in needed_conditions],
                   limits=table("map_explore_limit", "TbMapExploreLimitInfo"),
                   settings={key: settings[key] for key in ("FoodId", "ExploreFoodCost", "ExploreFoodTokenCost",
                       "WorldExploreMaxTimes", "WorldExploreGuideFixedRewardId",
                       "ExploreEntityMaxDist", "ExploreHistoryRecordDays", "WorldExploreActionId", "WorldExploreMoreTimesActionId",
                       "DailyGachaMaxCountDict")})
    known = {}
    if RECEIPT.exists():
        old = json.loads(RECEIPT.read_text(encoding="utf-8"))
        known[OUT.resolve()] = old["output"]["sha256"]
        known[RECEIPT.resolve()] = sha256(RECEIPT.read_bytes())
    raw = json_bytes(payload)
    write_ui_payload(OUT, raw, known)
    receipt = dict(manifest="854_156383", inputs=inputs,
                   output=dict(path=OUT.relative_to(ROOT).as_posix(), sha256=sha256(raw)),
                   converter=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha256(Path(__file__).read_bytes())))
    write_ui_payload(RECEIPT, json_bytes(receipt), known)
    write_meta(OUT, "TextScriptImporter")
    print("World exploration:", len(tokens), "token groups,", len(entity_groups), "native entity group variants")


if __name__ == "__main__":
    build()
