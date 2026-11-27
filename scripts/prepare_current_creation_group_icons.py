"""Install current official creation group icons with their native Sprite geometry."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_source import OfficialSource
from npc_travel_language import language
from prepare_unity_profession_reward_sprites import build as native_sprite
from prepare_character_creation_group_icons import GROUP_ITEMS, ITEM_BUNDLES, folder_meta


RELATIVE = Path('Assets/Official/CharacterCreation/Recovered/AppearanceGroupIcons')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare(project):
    source = OfficialSource()
    target = project/RELATIVE
    prior = json.loads((target/'source-manifest.json').read_bytes())
    old = {row['group_id']: row for row in prior['groups']}
    tables_path = source.prepared_path/'official-data/tables.json'
    tables = json.loads(tables_path.read_bytes())
    groups = {row['GroupId']: row for row in tables['append_appearance_group']}
    appearances = {row['ClassId']: row for row in tables['append_appearance']}
    skins = {row['Id']: row for row in tables['spine_skin']}
    for group_id, (sex, item_id) in GROUP_ITEMS.items():
        row = groups[group_id]
        sex_id = '0' if sex == 'male' else '1'
        if row['ItemIdDict'][sex_id] != item_id:
            raise ValueError('Review changed official group item: '+str(group_id))
        if row['AppearanceIdList'] != old[group_id]['appearance_ids']:
            raise ValueError('Migrate changed group appearance dependencies first: '+str(group_id))
        for skin in old[group_id]['appearance_skins']:
            if appearances[skin['appearance_id']]['SkinDict'][sex_id] != skin['skin_id']:
                raise ValueError('Migrate changed group skin dependency first')
            current_skin = skins[skin['skin_id']]
            if current_skin['SkinName'] != skin['skin_name'] or current_skin['SkinType'] != skin['skin_type']:
                raise ValueError('Migrate changed group preview skin first')
    labels, language_sources = language(['append_appearance_group_'+str(k) for k in GROUP_ITEMS], source=source)
    text_name = 'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile'
    text_source = language_sources[text_name]
    text_bundle = next(row for row in source.manifest('WwiseBnk')['bundles'] if row['name'] == text_name)
    localization = dict(name=text_name, origin=text_source['origin'], apk_sha256=source.apk_sha256,
                        apk_entry=text_source.get('apk_entry'), url=text_source.get('url'),
                        bundle_md5=text_bundle['hash'], bundle_sha256=text_source['sha256'],
                        bytes=text_source.get('raw_size', text_source.get('size')))
    apk = Apk.current()
    cache = source.prepared_path/'character-creation/group-icons'
    icons, bundles, outputs, records = {}, {}, [], []
    try:
        wanted = {item_id for _, item_id in GROUP_ITEMS.values()}
        for bundle in sorted(set(ITEM_BUNDLES.values())):
            for obj in apk.objects(bundle):
                if obj.type.name != 'Sprite':
                    continue
                match = re.fullmatch(r'item_(\d+)', obj.read().m_Name)
                if not match or int(match[1]) not in wanted:
                    continue
                item_id = int(match[1])
                if item_id in icons:
                    raise ValueError('Duplicate current group icon')
                result = native_sprite(apk, obj, cache=cache)
                provenance = apk.provenance(obj)
                bundles[bundle] = dict(name=bundle, origin=provenance['origin'],
                    apk_sha256=source.apk_sha256, apk_entry=provenance.get('apk_entry'), url=provenance.get('url'),
                    bundle_md5=provenance['bundle_hash'],
                    bundle_sha256=provenance.get('raw_sha256', provenance.get('sha256')),
                    bytes=provenance.get('raw_size', provenance.get('size')))
                installed = {}
                for kind, relative in [('sprite', RELATIVE/'Native'/('item_'+str(item_id)+'.asset')),
                                       ('texture', RELATIVE/'Native/Textures'/('item_'+str(item_id)+'.texture.asset'))]:
                    origin = ROOT/result[kind]['path']
                    destination = project/relative
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    for suffix in ('', '.meta'):
                        raw = Path(str(origin)+suffix).read_bytes()
                        expected = result[kind]['meta_sha256' if suffix else 'sha256']
                        if sha(raw) != expected:
                            raise ValueError('Prepared native icon changed')
                        Path(str(destination)+suffix).write_bytes(raw)
                        outputs.append(dict(path=(Path('unity')/Path(str(relative)+suffix)).as_posix(), sha256=sha(raw)))
                    installed[kind] = relative.as_posix()
                icons[item_id] = dict(result=result, installed=installed, source=provenance)
        if set(icons) != wanted:
            raise ValueError('Current official group icon inventory differs')
    finally:
        apk.reader.close()
    for relative in (RELATIVE/'Native', RELATIVE/'Native/Textures'):
        path = project/Path(str(relative)+'.meta')
        if not path.exists():
            path.write_bytes(folder_meta(relative.as_posix()))
        outputs.append(dict(path=(Path('unity')/Path(str(relative)+'.meta')).as_posix(), sha256=sha(path.read_bytes())))
    for group_id, (sex, item_id) in GROUP_ITEMS.items():
        record = dict(old[group_id])
        icon = icons[item_id]
        payload = project/icon['installed']['sprite']
        record.update(path=icon['installed']['sprite'], bytes=payload.stat().st_size, sha256=sha(payload.read_bytes()),
                      localized_label=labels['append_appearance_group_'+str(group_id)],
                      condition_id=groups[group_id]['TryOnConditionId'],
                      path_id=icon['source']['path_id'], native=icon['result'],
                      texture_path=icon['installed']['texture'])
        records.append(record)
    manifest = dict(manifest=source.version, apk_sha256=source.apk_sha256,
        generator_sha256=sha(Path(__file__).read_bytes()), native_generator_sha256=sha((ROOT/'scripts/prepare_unity_profession_reward_sprites.py').read_bytes()),
        table_sha256=sha(tables_path.read_bytes()), source_localization=localization, language_sources=language_sources,
        source_bundles=list(bundles.values()), groups=records, outputs=outputs)
    (target/'source-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Installed current native group icons:', len(records), 'project:', project, flush=True)


def record_installed(project):
    path = project/RELATIVE/'source-manifest.json'
    manifest = json.loads(path.read_bytes())
    catalog = Path('Assets/Game/Resources/CharacterCreation/OfficialCharacterCreationAssetCatalog.asset')
    text = (project/catalog).read_text('utf-8-sig')
    section = text.split('  appearanceGroupIcons:\n', 1)[1]
    bindings = re.findall(r'  - groupId: (\d+)\n    sex: (\d+)\n    sprite: \{fileID: (\d+), guid: ([a-f0-9]{32}), type: (\d+)\}', section)
    expected = {(str(row['group_id']), '0' if row['sex'] == 'male' else '1', '21300000',
                 row['native']['sprite']['guid'], '2') for row in manifest['groups']}
    if set(bindings) != expected or len(bindings) != len(expected):
        raise ValueError('Installed native group Sprite bindings differ from their sources')
    for output in manifest['outputs']:
        installed = project/Path(output['path']).relative_to('unity')
        if sha(installed.read_bytes()) != output['sha256']:
            raise ValueError('Installed native group output changed: '+str(installed))
    manifest['binding_outputs'] = [dict(path=(Path('unity')/Path(str(catalog)+suffix)).as_posix(),
                                       sha256=sha((project/Path(str(catalog)+suffix)).read_bytes()))
                                   for suffix in ('', '.meta')]
    manifest['editor_binding_sha256'] = sha((project/'Assets/Game/Editor/CharacterCreation/CharacterCreationAssetTools.cs').read_bytes())
    manifest['generator_sha256'] = sha(Path(__file__).read_bytes())
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Verified installed current group bindings:', len(bindings), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--record-installed', action='store_true')
    args = parser.parse_args()
    (record_installed if args.record_installed else prepare)(args.project.resolve())
