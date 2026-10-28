"""Validate recovered public graphs, source identities and follower data."""
import hashlib
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
def read(path):
    return json.loads((ROOT / path).read_bytes())

class DragonStoryDependencies(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.dependencies = read("unity/Assets/Official/World/Map/14/Source/story-dependencies.json")
        cls.receipt = read("assets/reference/official-map/map14-story-dependencies-source-manifest.json")
        cls.runtime = read("unity/Assets/Game/Resources/Map14/stories.json")

    def test_original_identity_and_hashes(self):
        self.assertEqual((self.dependencies["manifest"], self.dependencies["mapClassId"]), ("871_156598", 14))
        for entry in self.receipt["inputs"] + self.receipt["outputs"] + self.receipt["tools"]:
            self.assertEqual(hashlib.sha256((ROOT / entry["path"]).read_bytes()).hexdigest(), entry["sha256"], entry["path"])

    def test_public_graph_and_port_closure(self):
        graphs = self.dependencies["graphs"]
        self.assertEqual(set(graphs), {"CombatDoorActive", "EntityVineLock", "LevelDoorActive", "ProfessionTutor4_public", "PuzzleEnd", "PuzzleSolve", "RankDoorActive", "TowerActive3", "WitchStory2"})
        self.assertEqual(sum(len(graph["nodes"]) for graph in graphs.values()), 155)
        runtime = {row["key"]: row for row in self.runtime["stories"] + self.runtime["subgraphs"]}
        for name, graph in graphs.items():
            normalized = {str(row["id"]): row for row in runtime[name]["nodes"]}
            for identity, node in graph["nodes"].items():
                self.assertEqual(json.loads(normalized[identity]["sourceJson"]), node)
                for port in node["ports"].values():
                    for connection in port["connections"]:
                        self.assertIn(connection["node"], graph["nodes"])
                        self.assertIn(connection["port"], graph["nodes"][connection["node"]]["ports"])

    def test_cross_map_original_path_discrepancy_is_explicit(self):
        binding = self.receipt["resourceBindings"]["EntityVineLock"]
        self.assertFalse(binding["configuredPathMatches"])
        self.assertEqual(binding["guid"], "499633388cadee742a44e8e976dd624c")
        self.assertEqual(binding["configuredPath"], "Assets/ScriptableConfig/Story/Map10Story/ChickChaos/Entity/EntityVineLock.asset")
        self.assertEqual(binding["assetPath"], "Assets/ScriptableConfig/Story/Map10Story/ChickChaos/EntityVineLock.asset")

    def test_map_talk_follower_and_quick_buy_binding(self):
        self.assertEqual(self.runtime["mapTalkEntries"], self.dependencies["mapTalkEntries"])
        self.assertEqual(len(self.runtime["mapTalkEntries"]), 79)
        self.assertEqual(self.dependencies["followerEntityClassIds"], [8402, 8451, 8452, 8454, 8455])
        templates = {str(row["EntityClassId"]): row for row in self.runtime["entityTemplates"]}
        for identity, source in self.dependencies["entityTemplates"].items():
            actual = templates[identity]["Components"]
            self.assertEqual([row["Type"] for row in actual], list(source))
            self.assertEqual({row["Type"]: json.loads(row["Json"]) for row in actual}, source)
        self.assertEqual(self.runtime["quickBuys"], self.dependencies["quickBuys"])
        self.assertEqual(len(self.runtime["quickBuys"]), 1)

if __name__ == "__main__":
    unittest.main()
