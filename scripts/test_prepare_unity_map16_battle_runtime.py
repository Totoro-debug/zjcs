import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
import prepare_unity_map16_battle_runtime as builder


def test_runtime_matches_source():
    source = json.loads(builder.SOURCE.read_bytes())
    runtime = json.loads(builder.OUTPUT.read_bytes())
    receipt = json.loads(builder.RECEIPT.read_bytes())
    assert runtime["manifest"] == "884_156692"
    assert runtime["mapClassId"] == 16
    assert runtime["sourceSha256"] == builder.sha(builder.SOURCE.read_bytes())
    assert len(runtime["stages"]) == 70
    assert len(runtime["monsterRosters"]) == 70
    assert len(runtime["battleScenes"]) == 9
    assert len(runtime["entityStageBindings"]) == 237
    assert {row["StageId"] for row in runtime["monsterRosters"]} == {row["Id"] for row in source["stages"]}
    assert receipt["outputs"][0]["sha256"] == builder.sha(builder.OUTPUT.read_bytes())
    assert receipt["counts"] == {
        "entityStageBindings": 237,
        "monsterGroups": 38,
        "monsterRosters": 70,
        "rosterUnits": 540,
        "scenes": 9,
        "stages": 70,
    }


if __name__ == "__main__":
    test_runtime_matches_source()
    print("Map16 battle runtime checks passed")
