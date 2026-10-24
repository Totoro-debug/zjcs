import unittest

import prepare_unity_home_skins as prepare


class HomeSkinEntityTests(unittest.TestCase):
    def test_fixed_home_skin_catalog_has_base_and_weather_variants(self):
        payload = prepare.load_home_skin_entities()

        self.assertEqual("854_156383", payload["version"])
        self.assertEqual(131, len(payload["entities"]))
        self.assertEqual(83, sum(entity["environment_skin_id"] == 0 for entity in payload["entities"]))
        self.assertEqual(48, sum(entity["environment_skin_id"] != 0 for entity in payload["entities"]))
        self.assertTrue(all(entity["layers"] for entity in payload["entities"]))

    def test_all_entity_textures_are_available_from_verified_official_sources(self):
        payload = prepare.load_home_skin_entities()
        names = {
            layer["sprite"]["file"]
            for entity in payload["entities"]
            for layer in entity["layers"]
        }
        sources = [prepare.OFFICIAL / "sprites", prepare.DECOR / "sprites"]

        missing = [name for name in names if not any((folder / name).is_file() for folder in sources)]

        self.assertEqual(628, len(names))
        self.assertEqual([], missing)

    def test_entity_vertices_are_flat_pairs_for_unity_scene_renderer(self):
        payload = prepare.load_home_skin_entities()

        for entity in payload["entities"]:
            for layer in entity["layers"]:
                self.assertGreaterEqual(len(layer["vertices"]), 8, layer["path"])
                self.assertEqual(0, len(layer["vertices"]) % 2, layer["path"])


if __name__ == "__main__":
    unittest.main()
