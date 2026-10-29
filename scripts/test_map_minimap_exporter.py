"""Keep the map-specific minimap exporters on the shared extraction seam."""
import ast
import unittest
from pathlib import Path

import prepare_map12_minimap_ui as map12
import prepare_map13_minimap_ui as map13
from prepare_map_minimap_ui import MinimapExporterBase, digest, world_prefab


class MinimapExporterCompatibilityTests(unittest.TestCase):
    def test_map_exporters_share_conversion_implementation(self):
        self.assertTrue(issubclass(map12.MinimapExporter, MinimapExporterBase))
        self.assertTrue(issubclass(map13.MinimapExporter, MinimapExporterBase))
        self.assertIs(map12.digest, digest)
        self.assertIs(map13.digest, digest)
        self.assertIs(map12.world_prefab, world_prefab)
        self.assertIs(map13.world_prefab, world_prefab)

    def test_map_exporters_only_keep_map_assembly_functions(self):
        for path in [Path(map12.__file__), Path(map13.__file__)]:
            tree = ast.parse(path.read_text(encoding='utf8'))
            names = {node.name for node in tree.body
                     if isinstance(node, (ast.FunctionDef, ast.ClassDef))}
            self.assertIn('build', names)
            self.assertIn('map_details', names)
            self.assertNotIn('world_prefab', names)
            self.assertNotIn('digest', names)

    def test_map_exporters_keep_distinct_sprite_outputs(self):
        self.assertEqual(map12.MinimapExporter.minimap_sprite_directory,
                         'assets/official/map12-minimap-sprites')
        self.assertEqual(map13.MinimapExporter.minimap_sprite_directory,
                         'assets/official/map13-minimap-sprites')


if __name__ == '__main__':
    unittest.main()
