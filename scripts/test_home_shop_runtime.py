#!/usr/bin/env python3
"""Focused source/runtime parity checks for the offline HomeShop payload."""

from __future__ import annotations

import hashlib
import json
import unittest
from pathlib import Path

import prepare_home_shop_runtime as prepare


class HomeShopRuntimeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.payload = json.loads((prepare.OUTPUT / "rules.json").read_text(encoding="utf-8"))
        cls.extension = json.loads((prepare.OUTPUT / "ui.json").read_text(encoding="utf-8"))
        cls.rows = {row["goods_id"]: row for row in cls.payload["fixed_goods"]}
        cls.grocery_groups = {row["group_id"]: row for row in cls.payload["grocery_groups"]}
        cls.treasury_groups = {row["group_id"]: row for row in cls.payload["treasury_groups"]}

    @staticmethod
    def condition_met(good, registered_days, sub_rank):
        condition = good["condition_type"]
        if condition == "NoneCondition":
            return True
        if condition == "SubRankBetweenCondition":
            return good["condition_from_sub_rank"] <= sub_rank <= good["condition_to_sub_rank"]
        if condition != "RegisteredDaysCondition":
            return False
        day = good["condition_day"]
        return {
            ">=": registered_days >= day,
            ">": registered_days > day,
            "==": registered_days == day,
            "<=": registered_days <= day,
            "<": registered_days < day,
        }.get(good["condition_op"], False)

    def test_fixed_goods_are_exact_official_shop_1_rows(self):
        source = prepare.read_json(prepare.OFFICIAL / "rules.json")
        shop = next(row for row in source["shop"] if row["Id"] == self.payload["shop_id"])
        goods = {row["Id"]: row for row in source["shop_goods"]}
        items = {row["ClassId"]: row for row in source["item"]}
        self.assertEqual(set(self.rows), set(shop["FixedGoodsList"]))
        for goods_id, output in self.rows.items():
            source_row = goods[goods_id]
            item = items[source_row["ItemId"]]
            self.assertEqual(output["item_id"], source_row["ItemId"])
            self.assertEqual(output["item_count"], source_row["Count"])
            self.assertEqual(output["currency_id"], source_row["Currency"])
            self.assertEqual(output["original_price"], source_row["OriginalPrice"])
            self.assertEqual(output["inventory"], source_row["Inventory"])
            self.assertEqual(output["total_buy_count_limit"], source_row["TotalBuyCountLimit"])
            self.assertEqual(output["item_name"], item["Name"])

    def test_official_unlock_conditions_and_unlimited_multi_buy(self):
        self.assertEqual([self.rows[key]["map_unlock_id"] for key in sorted(self.rows)], [0, 12, 13, 14, 17, 19, 21, 23])
        self.assertTrue(all(row["inventory"] == -1 for row in self.rows.values()))
        self.assertTrue(all(row["total_buy_count_limit"] == -1 for row in self.rows.values()))
        self.assertTrue(all(row["support_buy_multi_count"] for row in self.rows.values()))
        self.assertTrue(all(row["currency_id"] == 1 and row["item_count"] == 1 for row in self.rows.values()))

    def test_grocery_has_nine_official_random_draws_and_conditional_fixed_goods(self):
        source = prepare.read_json(prepare.OFFICIAL / "rules.json")
        shop = next(row for row in source["shop"] if row["Id"] == 1)
        self.assertEqual(self.payload["grocery_group_ids"], shop["GroupIdList"])
        self.assertEqual(self.payload["grocery_group_draw_counts"], shop["GroupRandomCounts"])
        self.assertEqual(sum(self.payload["grocery_group_draw_counts"]), 9)
        self.assertEqual(self.payload["grocery_random_count"], 9)
        self.assertTrue(self.payload["grocery_unique"])
        self.assertEqual(len(self.payload["fixed_goods"]), 8)
        self.assertEqual(self.grocery_groups[12001]["complex_group_ids"], [10101, 10801])
        self.assertEqual(self.grocery_groups[12001]["complex_group_weights"], [85, 15])
        self.assertEqual(self.grocery_groups[12002]["complex_group_ids"], [10201, 10801])
        self.assertEqual(self.grocery_groups[12003]["complex_group_ids"], [10101, 10201])
        self.assertEqual(self.grocery_groups[14001]["complex_group_ids"], [10301, 10401, 10501, 10601])
        self.assertEqual({good["item_name"] for group in self.grocery_groups.values()
                          for good in group["goods"]},
                         {"木材", "石块", "粗炼石", "时之砂", "稀有时之砂", "历战精华", "普通冻干",
                          "星之愿", "稀有女神牌", "结缘绳"})

    def test_shop_uniqueness_flags_match_official_configuration(self):
        source = prepare.read_json(prepare.OFFICIAL / "rules.json")
        shops = {row["Id"]: row for row in source["shop"]}
        self.assertEqual(self.payload["grocery_unique"], shops[1]["Unique"])
        self.assertEqual(self.payload["treasury_unique"], shops[2]["Unique"])
        self.assertTrue(self.payload["grocery_unique"])
        self.assertTrue(self.payload["treasury_unique"])

    def test_treasury_slots_match_official_group_order_and_offer_rows(self):
        self.assertEqual(self.payload["treasury_group_ids"],
                         [20101, 21801, 20201, 20501, 20401, 20701, 20601, 20801, 21501, 21401, 21301])
        self.assertEqual(self.payload["treasury_group_draw_counts"], [1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1])

        def goods(group_id):
            return self.treasury_groups[group_id]["goods"]

        stamina = goods(20101)[0]
        self.assertEqual((stamina["item_id"], stamina["item_count"], stamina["original_price"]), (57, 30, 144))
        self.assertEqual(stamina["price_off_options"][0]["values"], [0.0])
        fate_fruit = goods(20201)[0]
        self.assertEqual((fate_fruit["item_id"], fate_fruit["item_count"], fate_fruit["original_price"]),
                         (115, 1, 60))
        self.assertEqual(fate_fruit["price_off_options"][0]["values"], [0.5])
        self.assertEqual([row["item_count"] for row in goods(20401)], [100, 120, 150, 180, 200])
        self.assertEqual((goods(20401)[3]["goods_id"], goods(20401)[3]["original_price"]), (20404, 180))
        self.assertEqual((goods(20701)[0]["item_id"], goods(20701)[0]["item_count"],
                          goods(20701)[0]["original_price"]), (302, 5, 75))
        self.assertEqual((goods(20801)[0]["item_id"], goods(20801)[0]["item_count"],
                          goods(20801)[0]["original_price"]), (117, 1, 120))
        self.assertEqual((goods(21501)[0]["item_id"], goods(21501)[0]["original_price"],
                          goods(21501)[0]["inventory"]), (325, 50, -1))
        self.assertEqual((goods(21401)[0]["item_id"], goods(21401)[0]["original_price"],
                          goods(21401)[0]["inventory"]), (119, 100, -1))
        self.assertEqual((goods(21301)[0]["item_id"], goods(21301)[0]["original_price"],
                          goods(21301)[0]["refresh_cycle_type"]), (114, 500, 2))
        self.assertEqual({row["item_id"] for row in goods(21801)}, {110, 301, 107})
        self.assertEqual({row["item_id"] for row in goods(20501)}, {100, 101, 102, 103})
        self.assertEqual(len(goods(20601)), 17)

    def test_official_unique_goods_can_fill_nine_and_twelve_slots(self):
        grocery_goods = [good for group in self.grocery_groups.values() for good in group["goods"]]
        for sub_rank in range(1, 26):
            eligible_ids = {
                good["goods_id"] for good in grocery_goods
                if self.condition_met(good, 0, sub_rank)
            }
            self.assertGreaterEqual(len(eligible_ids), self.payload["grocery_random_count"], sub_rank)

        for sub_rank in range(1, 26):
            used = set()
            for group_id, draw_count in zip(self.payload["treasury_group_ids"],
                                            self.payload["treasury_group_draw_counts"]):
                goods = [good for good in self.treasury_groups[group_id]["goods"]
                         if self.condition_met(good, 3, sub_rank)]
                for _ in range(draw_count):
                    choices = [good for good in goods if good["goods_id"] not in used]
                    if choices:
                        used.add(choices[0]["goods_id"])
            self.assertEqual(len(used), self.payload["treasury_random_count"], sub_rank)

    def test_local_gift_milestones_and_official_five_row_view_are_available(self):
        achievements = self.payload["shop_score_achievements"]
        self.assertEqual(achievements["achievement_group"], 4)
        self.assertEqual(len(achievements["independent"]), 39)
        self.assertEqual(achievements["cycle_anchor"]["target_score"], 200000)
        self.assertEqual(achievements["cycle_count"], 10000)
        self.assertEqual(len(achievements["cycle_templates"]), 40)

        panel = next(value for value in self.extension["panels"] if value["key"] == "home_shop")
        paths = {node["path"] for node in panel["nodes"]}
        prefix = "/HomeShopPanel/BgPanel1/Content/AchievementPanel/BgPanel1/Content/MiddleGo/ScrollRect/ViewPort/Grid/AchievementPanelItem"
        for index in range(5):
            row = prefix + ("" if index == 0 else f" ({index})")
            self.assertIn(row, paths)
            self.assertIn(row + "/TitleTxt", paths)
            self.assertIn(row + "/ProgressGo/ProgressTxt", paths)
            self.assertIn(row + "/AwardGrid", paths)
            self.assertIn(row + "/BtnGroup/ReceiveBtn", paths)
            self.assertIn(row + "/BtnGroup/UncompletedGo", paths)

    def test_treasury_group_draw_counts_define_twelve_slot_order(self):
        source = prepare.read_json(prepare.OFFICIAL / "rules.json")
        shop = next(row for row in source["shop"] if row["Id"] == 2)
        self.assertEqual(self.payload["treasury_group_ids"], shop["GroupIdList"])
        self.assertEqual(self.payload["treasury_group_draw_counts"], shop["GroupRandomCounts"])
        self.assertEqual(self.payload["treasury_random_count"], 12)
        self.assertEqual(self.payload["treasury_group_ids"],
                         [20101, 21801, 20201, 20501, 20401, 20701, 20601, 20801, 21501, 21401, 21301])
        self.assertEqual(self.payload["treasury_group_draw_counts"], [1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1])
        self.assertEqual(self.treasury_groups[21301]["goods"][0]["refresh_cycle_type"], 2)
        self.assertEqual(self.treasury_groups[21301]["goods"][0]["inventory"], 1)
        self.assertEqual(self.payload["weekly_start_day_of_week"], 1)

    def test_random_discounts_match_official_goods_price_off_table(self):
        source = prepare.read_json(prepare.OFFICIAL / "rules.json")
        price_off = {}
        for row in source["shop_goods_price_off"]:
            price_off.setdefault(row["Id"], []).append({
                "sub_rank": row["SubRank"], "values": row["PriceOffList"] or []
            })
        runtime = {good["goods_id"]: good for group in
                   list(self.grocery_groups.values()) + list(self.treasury_groups.values())
                   for good in group["goods"]}
        for goods_id, good in runtime.items():
            self.assertEqual(good["price_off_options"], price_off.get(goods_id, []), goods_id)
        self.assertEqual(self.treasury_groups[20101]["goods"][0]["price_off_options"][0]["values"], [0.0])
        self.assertEqual(self.treasury_groups[20501]["goods"][0]["price_off_options"][0]["values"], [0.4, 0.5, 0.6])
        self.assertEqual(self.treasury_groups[21301]["goods"][0]["price_off_options"], [])

    def test_prefab_extension_has_expected_official_panels(self):
        by_key = {panel["key"]: panel for panel in self.extension["panels"]}
        self.assertEqual(set(by_key), {"home_shop", "home_shop_goods", "home_shop_item"})
        self.assertGreater(len(by_key["home_shop"]["nodes"]), 400)
        self.assertEqual(by_key["home_shop"]["root"]["name"], "HomeShopPanel")
        for panel in by_key.values():
            known_paths = {panel["root"]["path"]}
            for node in panel["nodes"]:
                self.assertIn(node["parent_path"], known_paths)
                self.assertNotIn(node["path"], known_paths)
                known_paths.add(node["path"])

    def test_shop_item_animation_curve_keys_preserve_official_values(self):
        panel = next(value for value in self.extension["panels"]
                     if value["key"] == "home_shop_goods")
        animation = next(value for value in panel["root"]["animations"]
                         if value["name"] == "ShopGoodsItem_In")
        track = next(value for value in animation["tracks"]
                     if value["path"] == "/ShopGoodsItem/Content/ImgBg"
                     and value["property"] == "m_Alpha")
        expected = [
            {"time": 0.0, "value": 0.5, "in_slope": 0.0, "out_slope": 0.0,
             "in_step": False, "out_step": False},
            {"time": 0.15, "value": 1.0, "in_slope": 0.0, "out_slope": 0.0,
             "in_step": False, "out_step": False},
        ]
        self.assertEqual(len(track["keys"]), len(expected))
        for actual, source in zip(track["keys"], expected):
            self.assertAlmostEqual(actual["time"], source["time"], places=6)
            for field in ("value", "in_slope", "out_slope", "in_step", "out_step"):
                self.assertEqual(actual[field], source[field])

    def test_home_ui_animation_payloads_use_named_key_objects(self):
        for path in (prepare.OUTPUT.parent / "Home" / "ui.json", prepare.OUTPUT / "ui.json"):
            payload = json.loads(path.read_text(encoding="utf-8"))
            panels = payload.get("panels") or []
            for panel in panels:
                nodes = [panel.get("root") or {}, *panel.get("nodes", [])]
                for node in nodes:
                    for animation in node.get("animations") or []:
                        for track in animation.get("tracks") or []:
                            self.assertTrue(all(isinstance(key, dict) for key in track.get("keys") or []),
                                            f"{path}: {animation.get('name')}/{track.get('property')}")

    def test_animation_array_converter_preserves_step_tangents(self):
        converted = prepare.normalize_home_ui_animations([{
            "name": "step",
            "tracks": [{"path": "/node", "property": "m_Alpha",
                        "keys": [[0.0, 0.0, None, 0.0], [1.0, 1.0, 0.0, None]]}],
        }])
        self.assertEqual(converted[0]["tracks"][0]["keys"], [
            {"time": 0.0, "value": 0.0, "in_slope": 0.0, "out_slope": 0.0,
             "in_step": True, "out_step": False},
            {"time": 1.0, "value": 1.0, "in_slope": 0.0, "out_slope": 0.0,
             "in_step": False, "out_step": True},
        ])

    def test_runtime_icons_match_source_and_copy_hashes(self):
        manifest = prepare.read_json(prepare.SHOP_SOURCE_MANIFEST)
        icon_outputs = manifest["runtime_sprites"]
        runtime_rows = list(self.rows.values())
        runtime_rows.extend(good for group in list(self.grocery_groups.values()) + list(self.treasury_groups.values())
                            for good in group["goods"])
        seen = set()
        for row in runtime_rows:
            for file_name in (row["icon_file"], row["currency_icon_file"]):
                if file_name in seen:
                    continue
                seen.add(file_name)
                output = prepare.OUTPUT / "sprites" / file_name
                self.assertTrue(output.is_file(), file_name)
                entry = icon_outputs[file_name]
                self.assertEqual(entry["sha256"], hashlib.sha256(output.read_bytes()).hexdigest())
                self.assertEqual(entry["sha256"], entry["source_sha256"])


if __name__ == "__main__":
    unittest.main()
