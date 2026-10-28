"""Regression checks against the pinned original Map13 story graphs."""
import hashlib
import json
import unittest
from pathlib import Path

from prepare_unity_map13_story_runtime import ROOT, SOURCE, normalize_story, mainline_tasks, condition_closure


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


class Map13StoryDependencyRegression(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tables = json.loads((SOURCE / "tables.json").read_bytes())
        cls.dependencies = json.loads((SOURCE / "story-dependencies.json").read_bytes())
        cls.runtime = json.loads((ROOT / "unity/Assets/Game/Resources/Map13/stories.json").read_bytes())

    def test_shared_dependency_receipt_matches_sources_and_outputs(self):
        receipt = json.loads((ROOT / "assets/reference/official-map/map13-story-dependencies-source-manifest.json").read_bytes())
        self.assertEqual(receipt["manifest"], "871_156598")
        for key in ("inputs", "outputs", "tools", "officialCode"):
            for record in receipt[key]:
                self.assertEqual(hashlib.sha256((ROOT / record["path"]).read_bytes()).hexdigest(),
                                 record["sha256"], record["path"])

    def test_shared_graphs_keep_original_nodes_and_dialogue(self):
        self.assertEqual(len(self.dependencies["graphs"]), 9)
        self.assertEqual(sum(len(g["nodes"]) for g in self.dependencies["graphs"].values()), 249)
        converted = {g["key"]: g for g in self.runtime["stories"] + self.runtime["subgraphs"]}
        for name, graph in self.dependencies["graphs"].items():
            expected = normalize_story(name, graph, {}, self.dependencies["dialogues"],
                                       self.dependencies["localization"], set())
            self.assertEqual(converted[name]["nodes"], expected["nodes"], name)
            self.assertEqual(converted[name]["dialogueGroups"], expected["dialogueGroups"], name)
        for talk in self.runtime["mapTalkEntries"]:
            if talk["TalkId"]:
                self.assertIn(talk["TalkId"], converted)
        self.assertEqual(len(self.runtime["mapTalkEntries"]), 64)

    def test_followers_use_original_ec_components_in_order(self):
        templates = json.loads((SOURCE / "templates.json").read_bytes())
        templates.update(self.dependencies["entityTemplates"])
        actual = {r["EntityClassId"]: r for r in self.runtime["entityTemplates"]}
        for identity in (8402, 8440, 8441, 8442):
            fields = templates[str(identity)]
            self.assertEqual([c["Type"] for c in actual[identity]["Components"]], list(fields))
            self.assertEqual({c["Type"]: json.loads(c["Json"]) for c in actual[identity]["Components"]}, fields)

    def test_mainline_retains_non_region_task_and_stops_at_next_map(self):
        actual = mainline_tasks(self.tables["task"], 13)
        self.assertEqual(self.runtime["mainlineTasks"], actual)
        self.assertIn(1355, [r["ClassId"] for r in actual])
        for row in actual:
            self.assertIn(row["FinishCondition"].get("MapClassId"), (None, 13))
        def task(identity, map_id, children):
            return dict(ClassId=identity, TaskType=10, FinishCondition={} if map_id is None else {"MapClassId": map_id}, NextIds=children, Order=identity)
        rows = [task(1, 13, [2]), task(2, None, [3]), task(3, 14, [4]), task(4, None, [])]
        self.assertEqual([r["ClassId"] for r in mainline_tasks(rows, 13)], [1, 2])

    def test_condition_closure_keeps_composites_cycles_and_rejects_missing(self):
        talks = [{"Condition": {"Ids": [1]}}]
        conditions = [{"ClassId": 1, "Condition": {"Ids": [2]}}, {"ClassId": 2, "Condition": {"Ids": [1]}}]
        self.assertEqual(condition_closure(talks, conditions), conditions)
        with self.assertRaisesRegex(ValueError, "Missing original MapTalk condition: 3"):
            condition_closure(talks, conditions, [3])
        expected = condition_closure(self.runtime["mapTalkEntries"], self.tables["conditions"], self.runtime["requiredAccessConditionIds"])
        self.assertEqual(self.runtime["mapTalkConditions"], expected)
        self.assertEqual(len(self.runtime["requiredAccessConditionIds"]), 13)


if __name__ == "__main__":
    unittest.main()
