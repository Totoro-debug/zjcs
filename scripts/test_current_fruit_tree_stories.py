"""Check graph routing against the independent official client story exports."""
import json
from pathlib import Path
import unittest
import wave
import yaml

from prepare_current_fruit_tree_stories import ROOT, OUTPUT, RECEIPT, sha


class FruitTreeStorySourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.story = json.loads(OUTPUT.read_bytes())

    def test_provenance_and_payload(self):
        proof = json.loads(RECEIPT.read_bytes())
        self.assertEqual(sha(OUTPUT.read_bytes()), proof['output']['sha256'])
        self.assertEqual(sha(Path(str(OUTPUT) + '.meta').read_bytes()), proof['output']['meta_sha256'])
        for path, digest in proof['sources'].items():
            with self.subTest(source=path):
                self.assertEqual(sha((ROOT / path).read_bytes()), digest)

    def test_all_connections_match_client_export(self):
        for name, graph in self.story['graphs'].items():
            exported = {str(data['NodeId']): data for row in self.story['serverRecords'][name]
                        for data in [json.loads(row['Item2'])]}
            self.assertEqual(set(exported), set(graph['nodes']))
            for key, node in graph['nodes'].items():
                original_ports = exported[key].get('Ports', exported[key].get('ports'))
                self.assertEqual(set(node['ports']), set(original_ports))
                for port, value in node['ports'].items():
                    expected = [(str(link['NodeId']), link['fieldName'])
                                for link in original_ports[port]['connections']]
                    actual = [(link['node'], link['port']) for link in value['connections']]
                    self.assertEqual(actual, expected, (name, key, port))

    def test_repair_and_reward_gates_preserve_official_order(self):
        paths = {
            'FruitTreeUnlock1': [1, 6, 7, 12, 10, 17, 18, 8, 19, 11, 20, 21, 22, 23, 24, 25, 26, 27, 3],
            'FruitTreeUnlock2': [1, 21, 25, 22, 24, 23, 26, 39, 31, 38, 27, 3],
        }
        for name, path in paths.items():
            graph = self.story['graphs'][name]['nodes']
            for current, following in zip(path, path[1:]):
                self.assertEqual(graph[str(current)]['ports']['Exit']['connections'],
                                 [dict(node=str(following), port='Entry')])
        first = self.story['graphs']['FruitTreeUnlock1']['nodes']
        self.assertEqual(first['11']['Furniture'], 4)
        self.assertEqual(first['24']['_type'], 'WaitGotFruitTreeRewardNode')
        self.assertFalse(first['24']['NeedMask'])
        self.assertEqual(first['23']['StoryUIKey'], 'Home_FruitTree')
        self.assertEqual(self.story['graphs']['FruitTreeUnlock2']['nodes']['31']['Furniture'], 11)

    def test_original_dialogue_roles_text_and_voice_keys(self):
        roles = {row['RoleId'] for row in self.story['dialogRoles']}
        for name, groups in self.story['dialogues'].items():
            for dialog, rows in groups.items():
                self.assertEqual([row['Order'] for row in rows], sorted(row['Order'] for row in rows))
                for row in rows:
                    self.assertIn(row['RoleId'], roles)
                    text = self.story['localization'][f"dialog_{name}_{dialog}_{row['Order']}"]
                    self.assertTrue(text and '\ufffd' not in text)
                    if row['DialogType'] == 1:
                        self.assertTrue(row['VoiceEventKey'])

    def test_native_dialogue_voice_bindings_and_pcm(self):
        from prepare_fruit_tree_story_audio import RECEIPT as audio_receipt, ASSET, BASE_ASSET
        proof = json.loads(audio_receipt.read_bytes())
        for section in ('inputs', 'outputs'):
            for name, digest in proof[section].items():
                self.assertEqual(sha((ROOT / name).read_bytes()), digest, name)
        def asset(path):
            return yaml.safe_load(path.read_text('utf8').split('--- !u!114 &11400000\n', 1)[1])['MonoBehaviour']
        native, base = asset(ASSET), asset(BASE_ASSET)
        self.assertEqual(native['OptionTalkDialog'], base['OptionTalkDialog'])
        self.assertEqual(native['InvisibleMask'], base['InvisibleMask'])
        self.assertEqual(native['StorySource']['guid'], yaml.safe_load(Path(str(OUTPUT) + '.meta').read_bytes())['guid'])
        self.assertIn('NativeFormatImporter', yaml.safe_load(Path(str(ASSET) + '.meta').read_bytes()))
        keys = {row['VoiceEventKey'] for groups in self.story['dialogues'].values()
                for rows in groups.values() for row in rows if row['DialogType'] == 1}
        self.assertEqual(keys, {row['EventKey'] for row in native['Voices']})
        self.assertEqual(keys, {row['eventKey'] for row in proof['entries']})
        for row in proof['entries']:
            matches = [name for name in proof['outputs'] if name.endswith('/' + str(row['media_id']) + '.wav')]
            self.assertEqual(len(matches), 1)
            path = ROOT / matches[0]
            with wave.open(str(path), 'rb') as audio:
                self.assertEqual(audio.getnframes(), row['format']['frames'])
                self.assertGreater(audio.getnframes(), 0)
            binding = next(value for value in native['Voices'] if value['EventKey'] == row['eventKey'])
            self.assertEqual(binding['Clip']['guid'], yaml.safe_load(Path(str(path) + '.meta').read_bytes())['guid'])


if __name__ == '__main__':
    unittest.main()
