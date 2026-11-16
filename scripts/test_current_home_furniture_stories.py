"""Verify the recovered furniture story flows and their dialogue/voice closure."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
STORY = ROOT / 'unity/Assets/Official/Home/Source/furniture-unlock-stories.json'
AUDIO = ROOT / 'assets/reference/home-furniture-unlock-story-audio-source-manifest.json'


class FurnitureStorySourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.story = json.loads(STORY.read_bytes())
        cls.audio = json.loads(AUDIO.read_bytes())

    def next_node(self, graph, node_id, port='Exit'):
        connections = graph['nodes'][str(node_id)]['ports'][port]['connections']
        self.assertEqual(len(connections), 1)
        self.assertEqual(connections[0]['port'], 'Entry')
        return int(connections[0]['node'])

    def test_dining_repair_occurs_between_dialogue_sections(self):
        graph = self.story['graphs']['UnlockCommunication']
        path = [1, 45, 44, 17, 46, 18, 23, 24, 25, 3]
        self.assertEqual([self.next_node(graph, node) for node in path[:-1]], path[1:])
        self.assertEqual(graph['nodes']['23']['_type'], 'RepairFurnitureNode')
        self.assertEqual(graph['nodes']['23']['Furniture'], 2)
        self.assertEqual(graph['nodes']['18']['DialogId'], 0)
        self.assertEqual(graph['nodes']['25']['DialogId'], 1)

    def test_shop_options_preserve_both_branches(self):
        graph = self.story['graphs']['SenSenHomeStore']
        self.assertEqual(graph['nodes']['49']['Options'], [910, 911])
        self.assertEqual(self.next_node(graph, 49, 'Options 0'), 50)
        self.assertEqual(self.next_node(graph, 49, 'Options 1'), 51)
        self.assertEqual(self.next_node(graph, 50), 53)
        self.assertEqual(self.next_node(graph, 51), 53)
        self.assertEqual(graph['nodes']['53']['Options'], [912])
        self.assertEqual(self.next_node(graph, 53, 'Options 0'), 54)
        self.assertEqual(self.next_node(graph, 54), 3)
        self.assertFalse(any(node['_type'] == 'RepairFurnitureNode' for node in graph['nodes'].values()))

    def test_dialogue_text_roles_and_voice_events_are_complete(self):
        self.assertEqual(self.story['manifest'], '884_156692')
        self.assertEqual(self.audio['manifest'], '884_156692')
        roles = {row['RoleId'] for row in self.story['dialogRoles']}
        for role in self.story['dialogRoles']:
            self.assertEqual(self.story['localization'][f"dialog_role_{role['RoleId']}_name"], role['RoleName'])
        events = {row['eventKey'] for row in self.audio['entries']}
        actual_events = set()
        for name, groups in self.story['dialogues'].items():
            for dialog_id, rows in groups.items():
                self.assertEqual([row['Order'] for row in rows], sorted(row['Order'] for row in rows))
                for row in rows:
                    self.assertIn(row['RoleId'], roles)
                    key = f"dialog_{name}_{dialog_id}_{row['Order']}"
                    text = self.story['localization'][key]
                    self.assertTrue(text and '\ufffd' not in text)
                    if row['DialogType'] == 1:
                        actual_events.add(row['VoiceEventKey'])
        self.assertEqual(actual_events, events)
        self.assertEqual(len(events), 13)


if __name__ == '__main__':
    unittest.main()
