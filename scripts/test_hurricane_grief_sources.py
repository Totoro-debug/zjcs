"""Check current hurricane-grief source payloads and migrated entity bindings."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SPEC = ROOT / "scripts/profession_skill_sources/fourth/10598.json"
RULES = ROOT / "unity/Assets/Official/Combat/skill-rules.json"
BATTLE = ROOT / "unity/Assets/Official/Combat/battle-rules.json"
PACK = ROOT / "unity/Assets/Game/Resources/Combat/SkillRules/10598.json"
PACK_PROOF = PACK.with_name("10598-source-manifest.json")
GRAPHS = ROOT / "unity/Assets/Game/Resources/Combat/hurricane-grief-current-graphs.json"
GRAPH_PROOF = ROOT / "assets/reference/battle-animation/hurricane-grief-current-graphs-source-manifest.json"
SKILL_GRAPHS = ROOT / "unity/Assets/Game/Resources/Combat/hurricane-grief-current-skill-graphs.json"
SKILL_GRAPH_PROOF = SKILL_GRAPHS.with_name("hurricane-grief-current-skill-graphs-source-manifest.json")
GRAPH_INDEX = ROOT / "unity/Assets/Game/Resources/Combat/skill-native-graph-sources.json"
RULE_PACK_INDEX = ROOT / "unity/Assets/Game/Resources/Combat/skill-rule-packs.json"


def read(path: Path):
    return json.loads(path.read_bytes().decode("utf-8-sig"))


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    spec = read(SPEC)
    skill_rules = read(RULES)
    battle_rules = read(BATTLE)
    pack = read(PACK)
    pack_proof = read(PACK_PROOF)
    graph_data = read(GRAPHS)
    graph_proof = read(GRAPH_PROOF)
    skill_graph_data = read(SKILL_GRAPHS)
    skill_graph_proof = read(SKILL_GRAPH_PROOF)
    assert digest(PACK) == pack_proof["output_sha256"]
    status_output = next(row for row in graph_proof["outputs"]
        if row["path"] == GRAPHS.relative_to(ROOT).as_posix())
    assert digest(GRAPHS) == status_output["sha256"]
    assert digest(SKILL_GRAPHS) == skill_graph_proof["output_sha256"]
    assert pack["manifest"] == graph_data["manifest"] == pack_proof["manifest"] == graph_proof["manifest"]
    assert skill_graph_data["manifest"] == skill_graph_proof["manifest"] == pack["manifest"]
    entities = dict(skill_rules["entities"])
    entities.update(battle_rules["entities"])
    entities.update(pack["entities"])
    assert {int(key) for key in spec["entities"]} == {25099, 24488}
    for entity in (25099, 24488):
        assert str(entity) in entities, f"missing current rule entity {entity}"
    listener = entities["25099"]["FightStatusDoDamageHandleComponent"]
    assert listener["HitSkillElementTypes"] == [100]
    assert listener["IgnoreTeammate"] and listener["Rate"] == 1
    assert len(listener["StatusCfgs"]) == 1
    tear = listener["StatusCfgs"][0]
    assert tear["StatusId"] == 24488 and abs(tear["BasePercent"] - .35) < 1e-6
    assert tear["AffectedByProp"] and tear["ApplyTarget"] == 5
    assert entities["24488"]["FightStatusActionStartComponent"]["Skills"][0]["SkillId"] == 12660
    skill = entities["12660"]["FightSkillComponent"]
    assert skill["ElementType"] == 100 and skill["HitList"][0]["ClassId"] == 12661
    hit = (skill_rules["entities"].get("12661") or battle_rules["entities"].get("12661"))["FightHitFixedComponent"]
    assert hit["DamageId"] == 10004 and hit["DamageType"] == 1 and not hit["IsTrueDamage"]
    mapped = {row["id"]: row["resource"] for row in graph_data["graphs"]}
    assert mapped[24488].endswith("/24488")
    assert skill_graph_data["skills"][0]["skill_entity_id"] == 12660
    assert skill_graph_data["skills"][0]["entity_resource"].endswith("/12660")
    assert skill_graph_data["skills"][0]["playables"][0]["resource"].endswith("/playable12660")
    assert "Combat/hurricane-grief-current-skill-graphs" in read(GRAPH_INDEX)["resources"]
    assert "Combat/SkillRules/10598" in read(RULE_PACK_INDEX)["resources"]
    print("Current hurricane-grief source and native bindings passed: 903_156948, 35% wind hit, movement tear and 12660 wind damage.")


if __name__ == "__main__":
    main()
