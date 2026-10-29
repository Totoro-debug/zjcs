"""Convert the pinned official mainline nation-shop tables to a local runtime catalog."""

import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
TABLES = ROOT / ".tools/source-packages/current/871_156598/official-data/tables.json"
SOURCE_DIR = ROOT / ".tools/source-packages/current/871_156598/official-data"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Commerce/nation-shops.json"
MANIFEST = ROOT / "assets/reference/official-map/mainline-nation-shop-source-manifest.json"


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def guid_for(path):
    return hashlib.md5(("zjcs:" + path.relative_to(ROOT).as_posix()).encode()).hexdigest()


def write_meta(path):
    path.with_name(path.name + ".meta").write_text(
        "fileFormatVersion: 2\nguid: " + guid_for(path) + "\nTextScriptImporter:\n  externalObjects: {}\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="ascii")


def build():
    if not TABLES.is_file():
        raise SystemExit("Pinned official table payload is missing: " + str(TABLES))
    tables = json.loads(TABLES.read_text(encoding="utf-8"))
    groups = tables["map_nation_shop"]
    group_ids = {row["Id"] for row in groups}
    levels = [row for row in tables["shop_level_group"] if row["Id"] in group_ids]
    levels_by_group = {}
    shop_ids = set()
    result_levels = []
    result_groups = []
    for group in groups:
        group_id = group["Id"]
        rows = sorted((row for row in levels if row["Id"] == group_id), key=lambda row: row["Level"])
        currencies = {int(item_id) for row in rows for item_id in row["Currencies"]}
        if len(currencies) != 1 or not rows or rows[0]["ExpBoundary"] != 0:
            raise SystemExit("Nation-shop group currency or level configuration is ambiguous: " + str(group_id))
        result_groups.append({"groupId": group_id, "currencyItemId": currencies.pop()})
        levels_by_group[group_id] = rows
        for row in rows:
            shop_ids.add(row["ShopId"])
            result_levels.append({"groupId": group_id, "level": row["Level"], "shopId": row["ShopId"],
                                 "experienceBoundary": row["ExpBoundary"]})

    shops_by_id = {row["Id"]: row for row in tables["shop"] if row["Id"] in shop_ids}
    goods_by_id = {row["Id"]: row for row in tables["shop_goods"]}
    result_shops = []
    result_goods = []
    for shop_id in sorted(shop_ids):
        shop = shops_by_id[shop_id]
        result_shops.append({"id": shop_id, "refreshLimit": shop["RefreshCountsLimit"],
                             "refreshCycleType": shop["RefreshCycleType"], "fixedGoods": shop["FixedGoodsList"]})
        for goods_id in shop["FixedGoodsList"]:
            good = goods_by_id[goods_id]
            price = good["OriginalPrice"]
            if not float(price).is_integer():
                raise SystemExit("Nation-shop currency price is not an integer: " + str(goods_id))
            if good["Condition"] is not None or good["BuyCondition"] is not None:
                raise SystemExit("Nation-shop good has an unimplemented condition: " + str(goods_id))
            result_goods.append({"id": goods_id, "shopId": shop_id, "itemId": good["ItemId"],
                                 "itemCount": good["Count"], "currencyItemId": good["Currency"],
                                 "price": int(price), "inventory": good["Inventory"],
                                 "totalBuyLimit": good["TotalBuyCountLimit"],
                                 "refreshCycleType": good["RefreshCycleType"],
                                 "supportsMultiple": good["SupportBuyMultiCount"]})

    source_files = [TABLES] + [SOURCE_DIR / name for name in (
        "map_nation_shop.g.bytes", "shop_level_group.g.bytes", "shop.g.bytes", "shop_goods.g.bytes")]
    missing = [str(path) for path in source_files if not path.is_file()]
    if missing:
        raise SystemExit("Pinned official nation-shop source tables are missing: " + ", ".join(missing))
    output = {"manifest": "871_156598", "groups": result_groups,
              "levels": sorted(result_levels, key=lambda row: (row["groupId"], row["level"])),
              "shops": result_shops, "goods": sorted(result_goods, key=lambda row: row["id"])}
    source_records = [{"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path), "size": path.stat().st_size}
                      for path in source_files]
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(output, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    write_meta(OUTPUT)
    manifest = {"manifest": "871_156598", "apkManifest": "854_156383", "conversion": "official tables to local nation-shop catalog",
                "sources": source_records, "output": {"path": OUTPUT.relative_to(ROOT).as_posix(),
                "sha256": sha256(OUTPUT), "size": OUTPUT.stat().st_size},
                "counts": {"groups": len(result_groups), "levels": len(result_levels),
                           "shops": len(result_shops), "goods": len(result_goods)}}
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Wrote {} groups, {} level shops, {} fixed goods.".format(
        len(result_groups), len(result_shops), len(result_goods)))


if __name__ == "__main__":
    build()
