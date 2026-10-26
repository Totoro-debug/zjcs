"""Resolve native exploration icons and quality bindings, APK before cached CDN."""
from UnityPy.classes.PPtr import PPtr
from official_tables import Constructors


def extract_exploration_ui(apk, schemas, exporter, sprites, code_root=None):
    def rows(name, kind, bundle='config_luban'):
        obj = apk.asset(bundle, '/' + name + '.g.bytes')
        return Constructors(code_root).table(obj.read().m_Script.encode('utf-8', 'surrogateescape'), kind)

    outer = rows('explore_entity_group', 'TbExploreEntityGroupInfo')
    groups = {g for row in outer for g in row['EntityGroups']} | {51}
    entities = [row for row in rows('map_entity_group', 'TbMapEntityGroupInfo', 'config_luban_map') if row['Group'] in groups]

    def sprite(path):
        prefix = 'defaultpackage_assets_image_' + path.rsplit('/', 1)[0].replace('/', '_').lower()
        matches = [(key, pointer) for bundle in sorted(apk.index)
                   if bundle == prefix + '.bundle' or bundle.startswith(prefix + '_')
                   for key, pointer in apk.container(bundle)
                   if pointer.type.name == 'Sprite' and key.rsplit('/', 1)[-1].rsplit('.', 1)[0].lower() == path.rsplit('/', 1)[-1].lower()]
        if len(matches) != 1:
            raise ValueError('Native exploration sprite is not uniquely bound: ' + path)
        record = exporter.add_sprite(matches[0][1].deref())
        sprites[record['file']] = record
        return record['file']

    icons = [dict(name=path, sprite=sprite(path)) for path in sorted({row['Icon'] for row in entities})]
    quality_object = apk.asset('scriptableconfig_modules_itemicon', '/ItemIconConfig.asset')
    quality = schemas.read(quality_object)
    qualities = []
    names = ('None', 'White', 'Blue', 'Purple', 'Orange', 'Gold', 'Red', 'Rainbow')
    for index, name in enumerate(names):
        if index == 0:
            continue
        record = exporter.add_sprite(PPtr(**quality['QualityBgImages'][index], assetsfile=quality_object.assets_file).deref())
        sprites[record['file']] = record
        qualities.append(dict(quality=index, sprite=record['file'], detailSprite=sprite('UI/ItemDetailDialog/Img' + name + '_PopUps'),
                              awardLight=sprite('UI/Reward/ImgLight_' + name) if index >= 4 else None))
    rule = sorted((row for row in rows('rule_tip', 'TbRuleTipInfo') if row['RuleKey'] == 'MapExploreRule'), key=lambda row: row['Order'])
    return dict(icons=icons, qualities=qualities, rule=rule)
