"""Check the active official entity/child identity boundary before graph conversion."""
import contextlib
import io
import json
import unittest

import inspect_battle_skill_graph_bindings as audit


class OfficialSkillGraphBindingsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            audit.main()
        cls.source = json.loads(output.getvalue())

    def test_all_official_entities_keep_distinct_ids(self):
        rows = self.source['skills']
        self.assertEqual('884_156692', self.source['manifest'])
        self.assertEqual(1906, len(rows))
        self.assertEqual(1906, len({row['skill_id'] for row in rows}))
        self.assertEqual(1884, sum(not row['entity_prefab'].startswith('Move/') for row in rows))

    def test_ordinary_movement_keeps_entities_and_original_empty_graphs(self):
        rows = {row['skill_id']: row for row in self.source['skills']
                if row['entity_prefab'].startswith('Move/')}
        self.assertEqual({15300, 15310, 15320, 15330, 15340, 19800, 19810, 19820,
                          19830, 19840, 19850, 19870, 19890, 19900, 19910, 19920,
                          19930, 19950, 19960, 19970, 19980, 19990}, set(rows))
        self.assertEqual('Move/15300/15300.prefab', rows[15300]['entity_prefab'])
        self.assertEqual('Move/15300/playable15300.prefab',
                         rows[15300]['playables']['myPlayablePrefab']['path'])
        self.assertEqual({}, rows[15320]['playables'])
        self.assertEqual({}, rows[19810]['playables'])
        self.assertEqual(10, sum(bool(row['playables']) for row in rows.values()))

    def test_child_in_parent_directory_does_not_replace_parent_graph(self):
        by_id = {row['skill_id']: row for row in self.source['skills']}
        self.assertEqual('10160/10163.prefab', by_id[10163]['entity_prefab'])
        self.assertEqual('12730/12730.prefab', by_id[12730]['entity_prefab'])
        self.assertEqual('12730/playable12730.prefab',
                         by_id[12730]['playables']['myPlayablePrefab']['path'])
        self.assertEqual('12730/12733.prefab', by_id[12733]['entity_prefab'])
        self.assertEqual({}, by_id[12733]['playables'])

    def test_complete_original_direction_references_resolve(self):
        refs = [ref for row in self.source['skills'] for ref in row['playables'].values()]
        self.assertEqual(2296, len(refs))
        for ref in refs:
            self.assertIn('path', ref)
            self.assertTrue((audit.EXPORT / ref['path']).is_file())
        self.assertEqual(2226, len({ref['path'] for ref in refs}))
        self.assertEqual(2226, len(self.source['unique_playable_prefabs']))


if __name__ == '__main__':
    unittest.main()
