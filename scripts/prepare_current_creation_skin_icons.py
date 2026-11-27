"""Install current creation option icons with original Sprite atlas geometry."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_source import OfficialSource
from prepare_character_creation_skin_icons import BUNDLE_NAME, SPRITE_FIELDS, folder_meta
from prepare_unity_map12_team_panel_sprites import NativeTeamSprites
from prepare_unity_profession_reward_sprites import write


RELATIVE = Path('Assets/Official/CharacterCreation/Recovered/PlayerSkinIcons')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


class CurrentSkinSprites(NativeTeamSprites):
    def __init__(self, apk, cache):
        self.apk = apk
        self.cache = cache
        self.outputs = {}
        self.textures = {}
        self.sprites = {}

    def write(self, kind, obj, native):
        result = write(kind, obj.read().m_Name, obj, native, cache=self.cache)
        result.update(identity=obj.assets_file.name+':'+str(obj.path_id),
                      source=self.apk.provenance(obj), object_sha256=sha(obj.get_raw_data()))
        return result


def prepare(project):
    source = OfficialSource()
    cache = source.prepared_path/'character-creation/skin-icons'
    options_path = project/'Assets/Official/CharacterCreation/appearance-options.json'
    options = json.loads(options_path.read_bytes())
    tables_path = source.prepared_path/'official-data/tables.json'
    skin_rows = {row['Id']: row for row in json.loads(tables_path.read_bytes())['spine_skin']}
    required = set()
    for body in options['bodies'].values():
        for field in SPRITE_FIELDS:
            for option in body[field]:
                skin = options['skins'][str(option['skin_id'])]
                current = skin_rows[option['skin_id']]
                if current['SkinName'] != skin['SkinName'] or current['SkinType'] != skin['SkinType']:
                    raise ValueError('Migrate changed creation skin option first: '+str(option['skin_id']))
                required.add(skin['SkinName'])
    apk = Apk.current()
    converter = CurrentSkinSprites(apk, cache)
    try:
        for obj in apk.objects(BUNDLE_NAME):
            if obj.type.name == 'Sprite':
                converter.sprite(obj)
        names = [row['expected']['name'] for row in converter.sprites.values()]
        if len(set(names)) != len(names) or required - set(names):
            raise ValueError('Current creation skin icon inventory is incomplete or duplicated')
        first = next(iter(converter.sprites.values()))['source']
        bundle = dict(name=BUNDLE_NAME, origin=first['origin'], apk_sha256=source.apk_sha256,
                      apk_entry=first.get('apk_entry'), url=first.get('url'), bundle_md5=first['bundle_hash'],
                      bundle_sha256=first.get('raw_sha256', first.get('sha256')),
                      bytes=first.get('raw_size', first.get('size')))
    finally:
        apk.reader.close()
    outputs = []

    def install(record, relative):
        for suffix in ('', '.meta'):
            origin = Path(str(ROOT/record['path'])+suffix)
            raw = origin.read_bytes()
            if sha(raw) != record['meta_sha256' if suffix else 'sha256']:
                raise ValueError('Prepared native skin icon changed: '+str(origin))
            path = project/Path(str(relative)+suffix)
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            outputs.append(dict(path=(Path('unity')/Path(str(relative)+suffix)).as_posix(), sha256=sha(raw)))
        return relative.as_posix()

    textures = {}
    for key, row in converter.textures.items():
        relative = RELATIVE/'Native/Textures'/('atlas_'+sha(key.encode())[:16]+'.asset')
        textures[key] = dict(path=install(row, relative), native=row)
    sprites = []
    for row in sorted(converter.sprites.values(), key=lambda item: item['expected']['name']):
        name = row['expected']['name']
        if not re.fullmatch(r'[A-Za-z0-9_]+', name):
            raise ValueError('Inspect official skin icon asset name: '+name)
        relative = RELATIVE/'Native'/(name+'.asset')
        path = install(row, relative)
        sprites.append(dict(name=name, path=path, bytes=(project/path).stat().st_size,
                            sha256=row['sha256'], texture_path=textures[row['texture']]['path'], native=row))
    for relative in (RELATIVE/'Native', RELATIVE/'Native/Textures'):
        path = project/Path(str(relative)+'.meta')
        if not path.exists():
            path.write_bytes(folder_meta(relative.as_posix()))
        outputs.append(dict(path=(Path('unity')/Path(str(relative)+'.meta')).as_posix(), sha256=sha(path.read_bytes())))
    manifest = dict(manifest=source.version, apk_sha256=source.apk_sha256, source_bundle=bundle,
                    generator_sha256=sha(Path(__file__).read_bytes()),
                    atlas_generator_sha256=sha((ROOT/'scripts/prepare_unity_map12_team_panel_sprites.py').read_bytes()),
                    native_generator_sha256=sha((ROOT/'scripts/prepare_unity_profession_reward_sprites.py').read_bytes()),
                    options_sha256=sha(options_path.read_bytes()), table_sha256=sha(tables_path.read_bytes()),
                    required_option_icon_count=len(required), sprites=sprites,
                    textures=list(textures.values()), outputs=outputs)
    (project/RELATIVE/'source-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Installed current native skin icons:', len(sprites), 'atlases:', len(textures), flush=True)


def record_installed(project):
    path = project/RELATIVE/'source-manifest.json'
    manifest = json.loads(path.read_bytes())
    catalog = Path('Assets/Game/Resources/CharacterCreation/OfficialCharacterCreationAssetCatalog.asset')
    text = (project/catalog).read_text('utf-8-sig')
    section = text.split('  skinIcons:\n', 1)[1].split('  appearanceGroupIconSourceManifest:', 1)[0]
    bindings = re.findall(r'  - skinName: ([A-Za-z0-9_]+)\n    sprite: \{fileID: (\d+), guid: ([a-f0-9]{32}), type: (\d+)\}', section)
    expected = {(row['name'], '21300000', row['native']['guid'], '2') for row in manifest['sprites']}
    if set(bindings) != expected or len(bindings) != len(expected):
        raise ValueError('Installed native skin icon bindings differ from their sources')
    for output in manifest['outputs']:
        installed = project/Path(output['path']).relative_to('unity')
        if sha(installed.read_bytes()) != output['sha256']:
            raise ValueError('Installed native skin output changed: '+str(installed))
    manifest['binding_outputs'] = [dict(path=(Path('unity')/Path(str(catalog)+suffix)).as_posix(),
                                       sha256=sha((project/Path(str(catalog)+suffix)).read_bytes()))
                                   for suffix in ('', '.meta')]
    manifest['editor_binding_sha256'] = sha((project/'Assets/Game/Editor/CharacterCreation/CharacterCreationAssetTools.cs').read_bytes())
    manifest['generator_sha256'] = sha(Path(__file__).read_bytes())
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Verified installed current skin bindings:', len(bindings), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--record-installed', action='store_true')
    args = parser.parse_args()
    (record_installed if args.record_installed else prepare)(args.project.resolve())
