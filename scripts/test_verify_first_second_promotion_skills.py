import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import verify_first_second_promotion_skills as migration


class FirstSecondPromotionSkillMigrationTests(unittest.TestCase):
    def test_scope_has_all_official_items_and_shared_bindings(self):
        receipt = migration.verify()
        self.assertEqual(104, receipt["scope_count"])
        builtins = {10125, 10126, 10127, 10128, 10129, 10130, 10131, 10132}
        regular = [row for row in receipt["skills"] if row["item_id"] not in builtins]
        self.assertEqual(50, sum(row["item_type"] == 100 for row in regular))
        self.assertEqual(46, sum(row["item_type"] == 101 for row in regular))
        self.assertEqual(8, len([row for row in receipt["skills"] if row["item_id"] in builtins]))
        self.assertEqual(104, len({row["item_id"] for row in receipt["skills"]}))
        self.assertEqual(len(migration.COMMON_MODULES), len(receipt["common_modules"]))

    def test_active_graphs_have_children_and_animation(self):
        receipt = migration.verify()
        active = [row for row in receipt["skills"] if row["item_type"] == 100]
        self.assertTrue(all(row["graph_entities"] for row in active))
        self.assertTrue(all(row["graph_playable_count"] > 0 for row in active))
        self.assertTrue(all(row["animation_count"] > 0 for row in active))
        self.assertTrue(all(row["view_bound"] for row in active))


if __name__ == "__main__":
    unittest.main()
