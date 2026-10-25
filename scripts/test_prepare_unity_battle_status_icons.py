import sys
import unittest
import hashlib
import json
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import prepare_unity_battle_status_icons as status_icons


class PrepareUnityBattleStatusIconsTests(unittest.TestCase):
    def test_runtime_catalog_pins_the_generated_status_icon_payload(self):
        payload_path = status_icons.ROOT / status_icons.OUTPUT_PATH
        payload_bytes = payload_path.read_bytes()
        payload = json.loads(payload_bytes)
        runtime = (status_icons.ROOT / "unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudCatalog.cs")
        runtime_text = runtime.read_text(encoding="utf-8")
        digest = hashlib.sha256(payload_bytes).hexdigest()

        self.assertIn('StatusIconCatalogSha256 = "' + digest + '"', runtime_text)
        self.assertIn('StatusIconEntryTableSha256 = "' + payload["entryTableSha256"] + '"', runtime_text)
        self.assertIn('StatusIconBattleRulesSha256 = "' + payload["battleRulesSha256"] + '"', runtime_text)
        self.assertIn('StatusIconSpriteManifestSha256 = "' + payload["spriteManifestSha256"] + '"', runtime_text)
        self.assertIn('StatusIconStageRulesSha256 = "' + payload["stageStatusRulesSha256"] + '"', runtime_text)
        self.assertEqual(payload["statusEntityCount"], 1595)
        self.assertEqual(len(payload["entries"]), 715)
        self.assertEqual(payload["stageStatusTextCount"], 41)
        self.assertEqual(sum(bool(item.get("nameText") and item.get("descriptionText"))
                             for item in payload["entries"]), 41)

    def test_status_sprites_resolve_uniquely_in_the_official_hud_sprite_catalog(self):
        payload = json.loads((status_icons.ROOT / status_icons.OUTPUT_PATH).read_text(encoding="utf-8"))
        manifest = json.loads((status_icons.ROOT / "assets/reference/battle-hud/source-manifest.json")
                              .read_text(encoding="utf-8"))
        sprite_names = {}
        for path in manifest["sprites"]:
            name = Path(path).name
            sprite_names.setdefault(name, []).append(path)
        status_files = {
            item[key]
            for item in payload["entries"]
            for key in ("iconFile", "backgroundFile")
            if item.get(key)
        }

        self.assertEqual(len(status_files), 39)
        for name in status_files:
            self.assertEqual(len(sprite_names.get(name, [])), 1, name)

    def test_uses_status_entity_id_and_official_stack_display_flag(self):
        entities = {
            "24181": {"FightStatusComponent": {"IsOpenStack": True},
                       "FightStatusRoundStartComponent": {"MaxCount": 3}},
            "24182": {"FightStatusComponent": {"IsOpenStack": False}},
            "24183": {"FightRoleComponent": {}},
        }
        rows = [
            {"Id": 24181, "Icon": "ItemIcon/Props/P_DmgReduce", "Bg": "ItemIcon/Props/BG_Buff"},
            {"Id": 24182, "Icon": "", "Bg": ""},
        ]
        sprites = {
            "res://assets/official/battle-hud/sprites/P_DmgReduce-abc.png": {"object": "P_DmgReduce"},
            "res://assets/official/battle-hud/sprites/BG_Buff-def.png": {"object": "BG_Buff"},
        }
        files = {
            "ItemIcon/Props/P_DmgReduce": "P_DmgReduce-abc.png",
            "ItemIcon/Props/BG_Buff": "BG_Buff-def.png",
        }

        count, bindings = status_icons.build_status_bindings(
            entities, rows, sprites, root=Path("unused"),
            sprite_resolver=lambda key, _manifest, _root: files[key] if key else "")

        self.assertEqual(count, 2)
        self.assertEqual(bindings, [
            {
                "statusId": 24181,
                "iconKey": "ItemIcon/Props/P_DmgReduce",
                "backgroundKey": "ItemIcon/Props/BG_Buff",
                "iconFile": "P_DmgReduce-abc.png",
                "backgroundFile": "BG_Buff-def.png",
                "showStackCount": True,
                "showSpriteAboveUnit": True,
                "showRoundReplaceStack": False,
                "showCustomCount": False,
                "hasRoundStartComponent": True,
            },
            {
                "statusId": 24182,
                "iconKey": "",
                "backgroundKey": "",
                "iconFile": "",
                "backgroundFile": "",
                "showStackCount": False,
                "showSpriteAboveUnit": True,
                "showRoundReplaceStack": False,
                "showCustomCount": False,
                "hasRoundStartComponent": False,
            },
        ])

    def test_reads_serialized_status_display_flags(self):
        raw = bytearray(108)
        raw[-48] = 0
        raw[-8] = 1
        raw[-4] = 1
        self.assertEqual(status_icons.parse_status_display_flags(raw), {
            "showSpriteAboveUnit": False,
            "showRoundReplaceStack": True,
            "showCustomCount": True,
        })

    def test_rejects_invalid_serialized_status_display_flags(self):
        raw = bytearray(108)
        raw[-8] = 2
        with self.assertRaisesRegex(ValueError, "not valid booleans"):
            status_icons.parse_status_display_flags(raw)

    def test_rejects_ambiguous_official_sprite_objects(self):
        sprites = {
            "res://assets/official/battle-hud/sprites/P_DmgReduce-a.png": {"object": "P_DmgReduce"},
            "res://assets/official/battle-hud/sprites/P_DmgReduce-b.png": {"object": "P_DmgReduce"},
        }
        with self.assertRaisesRegex(ValueError, "Expected one official HUD sprite"):
            status_icons.build_status_bindings(
                {"24181": {"FightStatusComponent": {}}},
                [{"Id": 24181, "Icon": "ItemIcon/Props/P_DmgReduce", "Bg": ""}],
                sprites,
                root=Path("unused"),
            )

    def test_binds_only_officially_localized_stage_status_text(self):
        entities = {"26004": {"FightStatusComponent": {"IsOpenStack": False}}}
        rows = [{"Id": 26004, "Icon": "", "Bg": ""}]
        count, bindings = status_icons.build_status_bindings(
            entities, rows, {}, root=Path("unused"),
            sprite_resolver=lambda *_: "",
            status_texts={26004: {"name": "name", "description": "description"}})
        self.assertEqual(count, 1)
        self.assertEqual(bindings[0]["nameText"], "name")
        self.assertEqual(bindings[0]["descriptionText"], "description")

    def test_reads_requested_keys_from_official_language_table(self):
        import struct
        import xxhash

        key = "entry_26004_name"
        value = "状态名"
        encoded = value.encode("utf-8")
        payload = (struct.pack("<i", 1)
                   + struct.pack("<Qi", xxhash.xxh64_intdigest(key.encode("utf-8")), 0)
                   + struct.pack("<H", len(encoded)) + encoded)
        self.assertEqual(status_icons.parse_language_text_table(payload, {key}), {key: value})

        malformed = (struct.pack("<i", 1)
                     + struct.pack("<Qi", xxhash.xxh64_intdigest(key.encode("utf-8")), 20)
                     + b"\x00\x00")
        with self.assertRaisesRegex(ValueError, "offset is invalid"):
            status_icons.parse_language_text_table(malformed, {key})


if __name__ == "__main__":
    unittest.main()
