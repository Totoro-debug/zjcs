import hashlib
import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


class Map10RuntimeDataTests(unittest.TestCase):
    def test_runtime_outputs_match_pinned_sources(self):
        manifest = json.loads((ROOT / "assets/reference/official-map/map10-runtime-source-manifest.json").read_text(encoding="utf-8"))
        for name in ("battles", "stories"):
            output = manifest["outputs"][name]
            data = ROOT / output["path"]
            self.assertEqual(hashlib.sha256(data.read_bytes()).hexdigest(), output["sha256"])
            source = ROOT / manifest["inputs"]["battles" if name == "battles" else "adventure"]["path"]
            self.assertEqual(hashlib.sha256(source.read_bytes()).hexdigest(),
                manifest["inputs"]["battles" if name == "battles" else "adventure"]["sha256"])

    def test_every_map10_stage_has_official_scene_and_roster(self):
        data = json.loads((ROOT / "unity/Assets/Official/World/Map/10/Resources/Map10/battle-runtime.json").read_text(encoding="utf-8"))
        self.assertEqual(data["manifest"], "854_156383")
        self.assertEqual(data["mapClassId"], 10)
        scenes = {str(scene["GameId"]): scene for scene in data["battleScenes"]}
        rosters = {roster["StageId"]: roster for roster in data["monsterRosters"]}
        self.assertEqual(len(data["stages"]), 21)
        self.assertEqual(len(rosters), 21)
        self.assertEqual(len(scenes), 4)
        for stage in data["stages"]:
            self.assertIn(str(stage["GameId"]), scenes)
            self.assertIn(stage["Id"], rosters)
        first = rosters[1033]
        units = [unit for wave in first["Waves"] for unit in wave["Units"]]
        self.assertEqual([unit["MonsterId"] for unit in units], [3281, 3281, 3281])
        self.assertEqual([unit["Position"] for unit in units],
            [{"X": 10, "Y": 13}, {"X": 12, "Y": 12}, {"X": 11, "Y": 13}])
        self.assertEqual(sum(len(wave["Units"]) for wave in rosters[9910]["Waves"]), 0)
        stages = {stage["Id"]: stage for stage in data["stages"]}
        self.assertEqual(stages[1096]["Repeat"], -1)
        self.assertEqual(stages[1033]["Repeat"], -1)

    def test_initial_island_story_nodes_and_dialogues_are_official(self):
        data = json.loads((ROOT / "unity/Assets/Official/World/Map/10/Resources/Map10/stories.json").read_text(encoding="utf-8"))
        roles = {role["RoleId"]: role for role in data["dialogRoles"]}
        self.assertEqual(set(roles), {840200, 840401, 840402, 841700})
        self.assertEqual(roles[840200]["PortraitPath"], "UI/Portrait/Npc_Portrait_8402")
        self.assertEqual(roles[840401]["PortraitPath"], "UI/Portrait/Npc_Portrait_8404")
        self.assertEqual(roles[840402]["PortraitPath"], "UI/Portrait/Npc_Portrait_8404")
        self.assertEqual(roles[841700]["PortraitPath"], "UI/Portrait/Npc_Portrait_8417")
        stories = {story["key"]: story for story in data["stories"]}
        self.assertEqual(set(stories), {
            "FirstEnterMap", "FirstBattle", "FirstMeetChaos", "SavingChick",
            "ChickYell", "ChickYell2", "ChickChat", "FangjianbeiActive", "UnlockFangBei",
        })
        chaos = stories["FirstMeetChaos"]
        graph = json.loads(chaos["graphJson"])
        self.assertEqual(graph["nodes"]["57"]["EntityClassId"], 8402)
        self.assertEqual(graph["nodes"]["58"]["_type"], "WaitGameResultNode")
        self.assertEqual(len(chaos["dialogueGroups"][0]["Lines"]), 2)
        self.assertEqual(len(stories["FirstEnterMap"]["dialogueGroups"][0]["Lines"]), 1)
        self.assertEqual(sum(len(group["Lines"]) for group in stories["FirstBattle"]["dialogueGroups"]), 6)
        first_battle_nodes = {node["id"]: node for node in stories["FirstBattle"]["nodes"]}
        self.assertEqual(first_battle_nodes[8]["type"], "ClickMapPosNode")
        self.assertEqual((first_battle_nodes[8]["positionX"], first_battle_nodes[8]["positionY"]), (23, 22))
        self.assertEqual((first_battle_nodes[8]["positionWidth"], first_battle_nodes[8]["positionHeight"]), (150.0, 150.0))
        self.assertTrue(first_battle_nodes[8]["needMask"])
        self.assertTrue(first_battle_nodes[17]["checkInteractiveRange"])
        self.assertEqual(first_battle_nodes[11]["storyUiKey"], "FightMainPanel.StartBtn")
        self.assertEqual(first_battle_nodes[59]["playableKey"], "FeiLe_appear")
        chaos_nodes = {node["id"]: node for node in chaos["nodes"]}
        self.assertTrue(chaos_nodes[30]["skipStageOperation"])
        self.assertEqual(chaos_nodes[56]["removePlayableKey"], "FeiLe_disappear")
        self.assertEqual(len(stories["FangjianbeiActive"]["dialogueGroups"]), 0)
        exit_graph = json.loads(stories["FangjianbeiActive"]["graphJson"])
        self.assertIn("HideAllUINode", {node["_type"] for node in exit_graph["nodes"].values()})
        self.assertIn("PlayPlayableNode", {node["type"] for node in stories["UnlockFangBei"]["nodes"]})
        unlock_nodes = {node["id"]: node for node in stories["UnlockFangBei"]["nodes"]}
        self.assertEqual(unlock_nodes[108]["playableAssetGuid"], "9b248cde91299ee4287533b25b7dd468")
        self.assertFalse(unlock_nodes[108]["waitPlayableStop"])
        self.assertEqual(unlock_nodes[111]["mapLogicStates"], [{"Tag": "村长_初始五", "State": -1}])

        playables = {key: playable for playable in data["playables"] for key in
                     [playable["AssetGuid"], *playable["Keys"]]}
        self.assertAlmostEqual(playables["FeiLe_appear"]["Duration"], 1.0)
        self.assertAlmostEqual(playables["FeiLe_disappear"]["Duration"], 1.8)
        self.assertAlmostEqual(playables["9b248cde91299ee4287533b25b7dd468"]["Duration"], 1.3)

        dialogs = {dialog["Id"]: dialog for dialog in data["nativeDialogs"]}
        self.assertEqual(set(dialogs), {1, 2, 3})
        first_dialog = {node["NodeId"]: node for node in dialogs[1]["Nodes"]}
        self.assertEqual(first_dialog[2]["TextKey"], "dialog_node_1_2")
        self.assertEqual(first_dialog[9]["OptionFilterProfession"], 10)
        self.assertEqual(first_dialog[12]["Profession"], 10)
        self.assertEqual(first_dialog[12]["ReturnNodeId"], 6)
        self.assertEqual([item["ItemId"] for item in first_dialog[7]["AwardItems"]],
                         [20000, 10008, 10013, 10012, 10501, 10562])
        self.assertEqual(first_dialog[21]["Story"], "UnlockFangBei")

        map_tags = {tag["LogicId"]: tag["Tag"] for tag in data["mapLogicTags"]
                    if tag["EntityClass"] == 8417}
        story_by_tag = {entry["Tag"]: entry["TalkId"] for entry in data["mapTalkEntries"]
                        if entry["EntityId"] == "entity_8417_name" and entry["Scenario"] == 1}
        self.assertEqual({placement: story_by_tag[map_tags[placement]]
                          for placement in (203, 303, 403, 503)}, {
                              203: "SavingChick", 303: "ChickYell",
                              403: "ChickYell2", 503: "ChickChat"})


if __name__ == "__main__":
    unittest.main()
