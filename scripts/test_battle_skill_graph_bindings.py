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
        self.assertEqual(1884, len(rows))
        self.assertEqual(1884, len({row['skill_id'] for row in rows}))

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
        self.assertEqual(2286, len(refs))
        for ref in refs:
            self.assertIn('path', ref)
            self.assertTrue((audit.EXPORT / ref['path']).is_file())
        self.assertEqual(2216, len({ref['path'] for ref in refs}))
        self.assertEqual(2216, len(self.source['unique_playable_prefabs']))


if __name__ == '__main__':
    unittest.main()
