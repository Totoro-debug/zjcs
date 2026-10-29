"""Complete home-shop artwork from the pinned APK and its verified CDN cache."""
import hashlib
import json
import struct
from prepare_official_panels import PanelExporter
from prepare_official_city import ART, REFERENCE, ROOT
from official_resource_manifest import write_resources


def home_items(rules):
    shops = [row for row in rules['shop'] if row['Id'] in (1, 2)]
    groups = {ident for shop in shops for ident in shop['GroupIdList']}
    goods = {ident for shop in shops for ident in (shop['FixedGoodsList'] or [])}
    goods.update(ident for row in rules['shop_goods_group'] if row['GroupId'] in groups
                 for ident in row['ShopGoodsList'])
    items = {row['ItemId'] for row in rules['shop_goods'] if row['Id'] in goods}
    items.update(row['ItemId'] for row in rules['home_visit_shop_goods'])
    return items


def build():
    exporter = PanelExporter()
    rules = json.loads((ART/'rules.json').read_text(encoding='utf8'))
    icons = json.loads((ART/'icons.json').read_text(encoding='utf8'))
    items = {row['ClassId']: row for row in rules['item']}
    containers = {}
    resolved = []
    for ident in sorted(home_items(rules)):
        if str(ident) in icons:
            continue
        icon = items[ident]['RawIcon']
        prefix = 'defaultpackage_assets_image_' + icon.rsplit('/', 1)[0].replace('/', '_').lower()
        for bundle in sorted(name for name in exporter.apk.index if name.startswith(prefix)):
            if bundle not in containers:
                containers[bundle] = {path.rsplit('/', 1)[-1].rsplit('.', 1)[0].lower(): ptr
                                      for path, ptr in exporter.apk.container(bundle) if ptr.type.name == 'Sprite'}
            ptr = containers[bundle].get(icon.rsplit('/', 1)[-1].lower())
            if ptr:
                icons[str(ident)] = exporter.sprite(ptr)
                resolved.append(icon)
                break
        else:
            raise ValueError(f'Home-shop icon was not found: {ident}: {icon}')

    # ShopGoodsItemIconBase selects the first quality dictionary for the item
    # types used by home shops. Keep the original GUID-to-Sprite mapping.
    config = exporter.apk.asset('scriptableconfig_modules_shop', '/ShopGoodsItemShowConfig.asset')
    raw = config.get_raw_data()
    offset = (32 + struct.unpack_from('<i', raw, 28)[0] + 3) & ~3
    count = struct.unpack_from('<i', raw, offset)[0]
    offset += 4
    qualities = {}
    for _ in range(count):
        quality, length = struct.unpack_from('<ii', raw, offset)
        guid = raw[offset+8:offset+8+length].decode()
        offset = (offset+8+length+3) & ~3
        sprite = exporter.guid_sprite(guid)
        if sprite is None:
            raise ValueError(f'Missing original shop quality sprite: {quality}: {guid}')
        icons[f'shop_quality:{quality}'] = sprite
        qualities[str(quality)] = {'guid': guid, 'sprite': sprite}

    # Merge only these additions into the latest shared catalogs.
    latest = json.loads((ART/'icons.json').read_text(encoding='utf8'))
    latest.update({str(i): icons[str(i)] for i in home_items(rules)})
    latest.update({f'shop_quality:{q}': entry['sprite'] for q, entry in qualities.items()})
    exporter.save('icons.json', latest)
    manifest = json.loads((REFERENCE/'source-manifest.json').read_text(encoding='utf8'))
    manifest['resources'].update(exporter.records)
    manifest['data_sha256']['icons.json'] = hashlib.sha256((ART/'icons.json').read_bytes()).hexdigest()
    unsupported = manifest.get('unsupported', {})
    if 'gameplay_icons_not_packaged' in unsupported:
        unsupported['gameplay_icons_not_packaged'] = [p for p in unsupported['gameplay_icons_not_packaged'] if p not in resolved]
    if 'shop_ingredient_icons' in unsupported:
        unsupported['shop_ingredient_icons']['status'] = 'Previously absent from the base APK; all seven extracted from verified 854_156383 CDN cache by prepare_shop_assets.py'
    (REFERENCE/'source-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf8')
    reference = ROOT/'assets/reference/shop-ui'
    reference.mkdir(parents=True, exist_ok=True)
    evidence = {'version': '854_156383', 'resources': exporter.records,
                'shop_item_show_config': exporter.apk.provenance(config), 'quality_sprites': qualities,
                'home_item_ids': sorted(home_items(rules)), 'new_icons': resolved,
                'logic': {'fixed_goods': 'Common/ShopInfo.cs: GenerateFixedGoods filters Condition before adding goods',
                          'name': 'ShopGoodsItemIconBase.cs: ShowName controls GoodsNameText',
                          'bottom': 'ShopGoodsShowContextCommon.cs and HomeVisitShopGoodsContextBase.cs: CanShowBottomTxt returns false'}}
    (reference/'source-manifest.json').write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding='utf8')
    write_resources(manifest)
    print(f'Home shop: {len(home_items(rules))} items have original art; {len(resolved)} icons and {count} quality sprites added')


if __name__ == '__main__':
    build()
