"""Regression checks against the pinned original Map13 story graphs."""
import hashlib
import json
import unittest
from pathlib import Path

from prepare_unity_map13_story_runtime import ROOT, SOURCE, normalize_story


class Map13StoryPlayableRegression(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.original = json.loads((SOURCE / "stories.json").read_bytes())
        cls.dialogues = json.loads((SOURCE / "dialogues.json").read_bytes())
        cls.localization = json.loads((SOURCE / "localization.json").read_bytes())
        cls.nodes = []
        for name, graph in cls.original.items():
            runtime = normalize_story(name, graph, {}, cls.dialogues, cls.localization, set())
            converted = {node["id"]: node for node in runtime["nodes"]}
            cls.nodes.extend((name, int(key), node, converted[int(key)])
                             for key, node in graph["nodes"].items()
                             if node.get("_type") == "PlayPlayableNode")

    def test_original_proxy_order_duplicates_and_natural_stop(self):
        self.assertEqual(len(self.nodes), 29)
        self.assertEqual(sum(len(source.get("PlayablesProxy", [])) > 1
                             for _, _, source, _ in self.nodes), 17)
        self.assertEqual(sum(source["PlayableDura"] == -1
                             for _, _, source, _ in self.nodes), 25)
        for name, key, source, node in self.nodes:
            with self.subTest(story=name, node=key):
                self.assertEqual(node["playableAssetGuids"],
                                 [p.get("mAssetGuid", "") for p in source.get("PlayablesProxy", [])])
                self.assertEqual(node["playableDuration"], source["PlayableDura"])
                self.assertEqual(node["waitPlayableStop"], bool(source["IsWaitPlayableStop"]))
        example = next(node for name, key, _, node in self.nodes
                       if name == "ByebyeWeiYang" and key == 72)
        self.assertEqual(len(example["playableAssetGuids"]), 4)
        self.assertEqual(len(set(example["playableAssetGuids"])), 2)

    def test_null_proxy_slot_keeps_entity_binding_index(self):
        graph = {"nodes": {"1": {"_type": "StartStoryNode"}, "2": {
            "_type": "PlayPlayableNode", "PlayableDura": -1,
            "PlayablesProxy": [{"mAssetGuid": "first"}, {}, {"mAssetGuid": "third"}]}}}
        runtime = normalize_story("slot-test", graph, {}, {}, {}, set())
        self.assertEqual(runtime["nodes"][1]["playableAssetGuids"], ["first", "", "third"])

    def test_native_conversion_matches_original_component_receipt(self):
        path = ROOT / "assets/reference/official-map/map13-native-story-playable-input-source-manifest.json"
        receipt = json.loads(path.read_bytes())
        self.assertEqual(receipt["manifest"], "871_156598")
        self.assertEqual(receipt["counts"]["prefabs"], 15)
        self.assertEqual(receipt["counts"]["bundles"], 278)
        for record in receipt["tools"] + receipt["outputs"]:
            self.assertEqual(hashlib.sha256((ROOT / record["path"]).read_bytes()).hexdigest(),
                             record["sha256"], record["path"])
        components = json.loads((ROOT / receipt["outputs"][0]["path"]).read_bytes())["components"]
        self.assertEqual(len(components), 3904)
        self.assertEqual(len({(item["cab"], item["path_id"]) for item in components}), len(components))
        exported = json.loads((ROOT / "assets/reference/official-map/map13-native-story-playable-export-source-manifest.json").read_bytes())
        self.assertEqual(exported["counts"]["registries"], receipt["counts"]["converted_registries"])
        self.assertEqual(exported["counts"]["reference_records"], 1)

    def test_all_original_assets_have_hash_verified_hierarchy(self):
        manifest = json.loads((ROOT / "assets/reference/official-map/map13-story-playables-source-manifest.json").read_bytes())
        for record in manifest["inputs"] + manifest["outputs"]:
            path = ROOT / record["path"]
            self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), record["sha256"], record["path"])
        catalog = json.loads((SOURCE / "story-playables.json").read_bytes())
        self.assertEqual(catalog["manifest"], "871_156598")
        prefabs = {p["guid"]: p for p in catalog["prefabs"]}
        objects = {o["identity"]: o for o in catalog["objects"]}
        requested = {p["mAssetGuid"] for _, _, source, _ in self.nodes
                     for p in source.get("PlayablesProxy", []) if p.get("mAssetGuid")}
        self.assertTrue(requested <= prefabs.keys())
        self.assertTrue(set(catalog["aliases"].values()) <= prefabs.keys())
        def check(node):
            self.assertEqual(objects[node["object"]]["type"], "GameObject")
            self.assertTrue(node["components"])
            for component in node["components"]:
                self.assertIn(component, objects)
            for child in node["children"]:
                check(child)
        for prefab in prefabs.values():
            check(prefab["hierarchy"])


if __name__ == "__main__":
    unittest.main()
