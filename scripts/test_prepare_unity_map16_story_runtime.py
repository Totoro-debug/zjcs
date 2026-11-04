import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
import prepare_unity_map16_story_runtime as builder


def test_runtime_matches_pinned_source():
    source = json.loads(builder.SOURCE.read_bytes())
    runtime = json.loads(builder.OUTPUT.read_bytes())
    receipt = json.loads(builder.OUTPUT_RECEIPT.read_bytes())
    assert runtime["manifest"] == "884_156692"
    assert runtime["mapClassId"] == 16
    assert runtime["sourceSha256"] == builder.sha(builder.SOURCE.read_bytes())
    assert len(runtime["stories"]) == 6
    assert len(runtime["subgraphs"]) == 1
    assert sum(len(story["nodes"]) for story in runtime["stories"] + runtime["subgraphs"]) == 94
    assert sum(len(group["Lines"]) for story in runtime["stories"] + runtime["subgraphs"]
               for group in story["dialogueGroups"]) == 45
    assert len(runtime["dialogRoles"]) == 2
    assert len(runtime["mapLogicTags"]) == 3
    assert runtime["handInItems"] == [{"Id": 3, "CanHandTimes": 1,
                                        "Items": [{"ItemId": 43039, "Count": 5}]}]
    assert runtime["fixedRewards"] == []
    assert runtime["playables"] == []
    assert {row["StoryName"] for row in source["storyOptions"]} <= {
        story["key"] for story in runtime["stories"] + runtime["subgraphs"]}
    assert receipt["outputs"][0]["sha256"] == builder.sha(builder.OUTPUT.read_bytes())
    assert receipt["counts"] == {
        "dialogRoles": 2, "dialogueGroups": 17, "dialogueLines": 45, "fixedRewards": 0,
        "graphs": 7, "handInItems": 1, "mainStories": 6, "mapLogicTags": 3,
        "nodes": 94, "storyOptions": 6, "subgraphs": 1,
    }


if __name__ == "__main__":
    test_runtime_matches_pinned_source()
    print("Map16 story runtime checks passed")
