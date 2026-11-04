"""Verify Map16 graphs normalize through the shared mainline story adapter."""
from __future__ import annotations

import json
import sys
import unittest
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "unity/Assets/Official/World/Map/16/Source/story-source.json"
ALONE = ROOT / "unity/Assets/Game/Resources/Map16/world-alone-configuration.json"
sys.path.insert(0, str(ROOT / "scripts"))
from prepare_unity_map13_story_runtime import normalize_story  # noqa: E402


class Map16StoryNormalizationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = json.loads(SOURCE.read_bytes())
        cls.alone = json.loads(ALONE.read_bytes())
        cls.rows = {row["StoryName"]: row for row in cls.source["storyRows"]}
        cls.subgraphs = {
            node.get("subgraph_name", node.get("SubgraphName", ""))
            for graph in cls.source["graphs"].values()
            for node in graph["nodes"].values()
            if node["_type"] == "SubgraphRunNode"
            and node.get("subgraph_name", node.get("SubgraphName", ""))
        }
        cls.records = {
            key: normalize_story(key, graph, cls.rows[key], cls.source["dialogues"],
                                 cls.source["localization"], cls.subgraphs)
            for key, graph in cls.source["graphs"].items()
        }

    def test_all_official_graphs_have_one_entry_and_complete_subgraph_closure(self):
        self.assertEqual(7, len(self.records))
        self.assertEqual({"SubmitRopes"}, self.subgraphs)
        self.assertTrue(self.subgraphs <= set(self.records))
        for key, record in self.records.items():
            original = self.source["graphs"][key]
            original_starts = [int(node_id) for node_id, node in original["nodes"].items()
                               if node["_type"] == "StartStoryNode"]
            self.assertEqual(1, len(original_starts), key)
            self.assertEqual(original_starts[0], record["entryNodeId"], key)
            self.assertEqual(self.rows[key]["MaxCount"], record["maxCount"], key)
            self.assertEqual(len(original["nodes"]), len(record["nodes"]), key)

    def test_every_node_keeps_its_original_payload_and_connected_ports(self):
        for key, record in self.records.items():
            original_nodes = self.source["graphs"][key]["nodes"]
            nodes = {node["id"]: node for node in record["nodes"]}
            self.assertEqual({int(node_id) for node_id in original_nodes}, set(nodes), key)
            for node_id, original in original_nodes.items():
                normalized = nodes[int(node_id)]
                self.assertEqual(original["_type"], normalized["type"], (key, node_id))
                self.assertEqual(original, json.loads(normalized["sourceJson"]), (key, node_id))
                expected = sorted((int(connection["node"]), connection["port"])
                                  for port in original.get("ports", {}).values()
                                  for connection in port.get("connections", [])
                                  if connection.get("node"))
                actual = sorted((connection["nodeId"], connection["port"])
                                for port in normalized["ports"]
                                for connection in port["connections"])
                self.assertEqual(expected, actual, (key, node_id))

    def test_dialogue_and_option_texts_are_resolved_from_the_selected_source(self):
        lines = sum((group["Lines"] for story in self.records.values()
                     for group in story["dialogueGroups"]), [])
        self.assertEqual(45, len(lines))
        self.assertTrue(all(line["Text"] for line in lines))
        option_nodes = [node for story in self.records.values() for node in story["nodes"]
                        if node["type"] == "OptionDialogNode"]
        self.assertEqual(4, len(option_nodes))
        counts = Counter(node["type"] for story in self.records.values() for node in story["nodes"])
        self.assertEqual(94, sum(counts.values()))
        self.assertEqual(7, counts["StartStoryNode"])

    def test_confirmed_manual_story_triggers_match_official_anchors(self):
        manual = {name for name, row in self.rows.items() if not row["AutoRunOnMeetCondition"]}
        self.assertEqual({"LookForWhaleIsland", "SubmitRopes", "DiscoverWhaleIsland",
                          "WhaleIslandArrive", "WhaleIslandFinishLoop", "WhaleIslandLeave"}, manual)

        def nodes(story):
            return list(self.source["graphs"][story]["nodes"].values())

        look_nodes = nodes("LookForWhaleIsland")
        self.assertTrue(any(node["_type"] == "AddMapExclusiveNpcAroundPlayerNode"
                            and node.get("EntityClassId") == 8402 for node in look_nodes))
        self.assertTrue(any(node["_type"] == "SubgraphRunNode"
                            and node.get("subgraph_name") == "SubmitRopes" for node in look_nodes))

        discover_nodes = nodes("DiscoverWhaleIsland")
        entry_buttons = [node.get("StoryUIKey") for node in discover_nodes
                         if node["_type"] == "ClickBtnNode"]
        self.assertIn("AloneWorldMapEntry.NormalBtn", entry_buttons)
        self.assertIn("AloneWorldTouchDialog.EnterBtn", entry_buttons)
        self.assertEqual("DiscoverWhaleIsland", self.alone["aloneWorld"]["PrepareEndStoryId"])
        self.assertEqual("WhaleIslandLeave", self.alone["aloneWorld"]["FinishStoryId"])

        rope_graph = nodes("SubmitRopes")
        self.assertTrue(any(node["_type"] == "GetEntityNode" and node.get("LoEntityId") == 10801
                            and node.get("Tag") == "大泽荣一_本土码头" for node in rope_graph))
        self.assertTrue(any(node["_type"] == "CanHandInItemNode" and node.get("HandInItemId") == 3
                            for node in rope_graph))
        self.assertTrue(any(node["_type"] == "PlayerWorldAloneAppearNode"
                            and node.get("MapAloneWorldClassId") == 16 for node in rope_graph))

        arrive_graph = nodes("WhaleIslandArrive")
        self.assertTrue(any(node["_type"] == "GetEntityNode" and node.get("LoEntityId") == 106
                            and node.get("Tag") == "大泽荣一_登陆鲸岛" for node in arrive_graph))
        finish_graph = nodes("WhaleIslandFinish")
        self.assertTrue(any(node["_type"] == "AddMapExclusiveNpcAtPosNode"
                            and node.get("EntityClassId") == 8443 for node in finish_graph))
        self.assertEqual(-1, self.rows["WhaleIslandFinishLoop"]["MaxCount"])
        self.assertTrue(self.rows["WhaleIslandFinish"]["AutoRunOnMeetCondition"])


if __name__ == "__main__":
    unittest.main()
