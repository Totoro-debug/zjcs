"""Resolve creation UI Sprite bindings against the current official prefabs."""
import argparse
import collections
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_source import OfficialSource
from current_component_schemas import CurrentSchemas
from prepare_current_creation_skin_icons import CurrentSkinSprites
from prepare_unity_profession_reward_sprites import write
from prepare_character_creation_group_icons import folder_meta, text_meta
from UnityPy.classes.PPtr import PPtr


RELATIVE = Path('Assets/Official/CharacterCreation/RuntimeUI')
RECEIPT = RELATIVE/'native-sprites-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


class CurrentUiSprites(CurrentSkinSprites):
    def write(self, kind, obj, native):
        identity = obj.assets_file.name+':'+str(obj.path_id)
        # Two original atlases contain distinct Sprites named BtnRandom.
        name = obj.read().m_Name+'-'+sha(identity.encode())[:12]
        result = write(kind, name, obj, native, cache=self.cache)
        result.update(identity=identity, source=self.apk.provenance(obj), object_sha256=sha(obj.get_raw_data()))
        return result


def source_bindings(apk, schemas, layout):
    current, roots = {}, []

    def walk(obj, parent='', label=None):
        go = obj.read()
        path = parent+'/'+(label or go.m_Name)
        parts = [part.component.deref() for part in go.m_Component]
        transform = next(part for part in parts if part.type.name in ('Transform', 'RectTransform'))
        images = []
        for component in parts:
            if component.type.name != 'MonoBehaviour':
                continue
            script = component.read(check_read=False).m_Script.read()
            if script.m_ClassName not in ('Image', 'MirrorImage', 'SlicedFilledImage'):
                continue
            pointer = schemas.read(component)['m_Sprite']
            if pointer['m_PathID']:
                images.append(PPtr(**pointer, assetsfile=component.assets_file).deref())
        current[path] = images
        seen = collections.Counter()
        for pointer in transform.read().m_Children:
            child = pointer.read().m_GameObject.deref()
            name = child.read().m_Name
            index = seen[name]
            seen[name] += 1
            walk(child, path, name+(' ('+str(index)+')' if index else ''))

    for key, bundle in [('creation_root', 'prefabs_ui_createplayer'),
                        ('diy_root', 'prefabs_ui_createplayer'),
                        ('confirm_root', 'prefabs_ui_confirmdialog')]:
        name = layout['nodes'][layout[key]]['name']
        obj = apk.asset(bundle, '/'+name+'.prefab')
        walk(obj)
        roots.append(dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data())))
    bindings, uses = {}, collections.defaultdict(list)
    for node in layout['nodes']:
        pointer = node.get('image', {}).get('sprite')
        if not pointer:
            continue
        images = current.get(node['path'], [])
        if len(images) != 1:
            raise ValueError('Review changed original UI image binding: '+node['path'])
        obj = images[0]
        identity = obj.assets_file.name+':'+str(obj.path_id)
        key = pointer['file']
        if key in bindings and bindings[key][0] != identity:
            raise ValueError('Review changed shared original UI Sprite: '+key)
        bindings[key] = identity, obj
        uses[key].append(node['path'])
    if set(bindings) != {row['key'] for row in layout['sprites']}:
        raise ValueError('Current UI Sprite inventory differs from the consumed bindings')
    return bindings, uses, roots


def canonical_assets(project):
    result = {}
    for family, key in [('AppearanceGroupIcons', 'groups'), ('PlayerSkinIcons', 'sprites')]:
        path = project/'Assets/Official/CharacterCreation/Recovered'/family/'source-manifest.json'
        manifest = json.loads(path.read_bytes())
        if manifest['manifest'] != OfficialSource().version:
            raise ValueError('Migrate creation icon dependencies first')
        for row in manifest[key]:
            native = row['native']['sprite'] if key == 'groups' else row['native']
            result[native['guid']] = dict(path=row['path'], sha256=native['sha256'])
            if key == 'groups':
                texture = row['native']['texture']
                result[texture['guid']] = dict(path=row['texture_path'], sha256=texture['sha256'])
        if key == 'sprites':
            for row in manifest['textures']:
                result[row['native']['guid']] = dict(path=row['path'], sha256=row['native']['sha256'])
    return result


def prepare(project):
    source = OfficialSource()
    layout_path = project/RELATIVE/'runtime-ui.json'
    previous_layout = layout_path.read_bytes()
    layout = json.loads(previous_layout)
    converter = CurrentUiSprites(Apk.current(), source.prepared_path/'character-creation/ui-sprites')
    schemas = CurrentSchemas()
    try:
        bindings, uses, roots = source_bindings(converter.apk, schemas, layout)
        aliases = {key: converter.sprite(obj) for key, (_, obj) in bindings.items()}
    finally:
        converter.apk.reader.close()
    existing = canonical_assets(project)
    outputs, dependencies = {}, {}

    def install(row, relative):
        if row['guid'] in existing:
            reused = existing[row['guid']]
            raw = (project/reused['path']).read_bytes()
            if sha(raw) != row['sha256'] or sha(raw) != reused['sha256']:
                raise ValueError('A canonical original UI dependency differs: '+reused['path'])
            for suffix in ('', '.meta'):
                path = Path(reused['path']+suffix)
                dependencies[path.as_posix()] = dict(path=(Path('unity')/path).as_posix(), sha256=sha((project/path).read_bytes()))
            return reused['path']
        for suffix in ('', '.meta'):
            origin = Path(str(ROOT/row['path'])+suffix)
            raw = origin.read_bytes()
            if sha(raw) != row['meta_sha256' if suffix else 'sha256']:
                raise ValueError('Prepared original UI dependency changed: '+str(origin))
            path = project/Path(str(relative)+suffix)
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            outputs[path] = dict(path=(Path('unity')/Path(str(relative)+suffix)).as_posix(), sha256=sha(raw))
        return relative.as_posix()

    textures = {}
    for identity, native in converter.textures.items():
        path = install(native, RELATIVE/'NativeSprites/Textures'/('texture_'+sha(identity.encode())[:16]+'.asset'))
        textures[identity] = dict(path=path, native=native)
    sprites = []
    for entry in layout['sprites']:
        key = entry['key']
        native = aliases[key]
        path = install(native, RELATIVE/'NativeSprites'/('sprite_'+sha(native['identity'].encode())[:16]+'.asset'))
        entry.update(path=path, border=[native['expected']['border'][axis] for axis in 'xyzw'], ppu=native['expected']['ppu'])
        sprites.append(dict(key=key, path=path, nodes=uses[key], native=native,
                            texture_path=textures[native['texture']]['path']))
    for relative in (RELATIVE/'NativeSprites', RELATIVE/'NativeSprites/Textures'):
        path = project/Path(str(relative)+'.meta')
        if not path.exists():
            path.write_bytes(folder_meta(relative.as_posix()))
        outputs[path] = dict(path=(Path('unity')/Path(str(relative)+'.meta')).as_posix(), sha256=sha(path.read_bytes()))
    meta = project/Path(str(RECEIPT)+'.meta')
    if not meta.exists():
        meta.write_bytes(text_meta(RECEIPT.as_posix()))
    outputs[meta] = dict(path=(Path('unity')/Path(str(RECEIPT)+'.meta')).as_posix(), sha256=sha(meta.read_bytes()))
    layout['sprite_source_manifest'] = RECEIPT.as_posix()
    layout_path.write_text(json.dumps(layout, ensure_ascii=False, indent=2)+'\n', 'utf8')
    outputs[layout_path] = dict(path=(Path('unity')/RELATIVE/'runtime-ui.json').as_posix(), sha256=sha(layout_path.read_bytes()))
    manifest = dict(manifest=source.version, apk_sha256=source.apk_sha256,
                    scope='Current original Sprite, atlas, full-mip texture and explicit image bindings; layout, animation and audio retain their separate source receipts.',
                    generator_sha256=sha(Path(__file__).read_bytes()), previous_layout_sha256=sha(previous_layout),
                    roots=roots, sprites=sprites, textures=list(textures.values()),
                    outputs=list(outputs.values()), canonical_dependencies=list(dependencies.values()),
                    supporting_scripts={name: sha((ROOT/'scripts'/name).read_bytes()) for name in (
                        'prepare_unity_map12_team_panel_sprites.py', 'prepare_unity_profession_reward_sprites.py',
                        'prepare_current_creation_skin_icons.py', 'current_component_schemas.py')})
    (project/RECEIPT).write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Installed native creation UI Sprites:', len(sprites), 'textures:', len(textures),
          'canonical dependencies:', len(dependencies), flush=True)


def record_installed(project):
    path = project/RECEIPT
    manifest = json.loads(path.read_bytes())
    for row in manifest['outputs']+manifest['canonical_dependencies']:
        asset = project/Path(row['path']).relative_to('unity')
        if sha(asset.read_bytes()) != row['sha256']:
            raise ValueError('Installed original UI resource changed: '+str(asset))
    prefabs = [RELATIVE/(name+'.prefab') for name in ('CreatePlayerPanel', 'CreatePlayerPanelDIYContent', 'ConfirmDialog')]
    text = '\n'.join((project/path).read_text('utf-8-sig') for path in prefabs)
    for row in manifest['sprites']:
        if 'guid: '+row['native']['guid'] not in text:
            raise ValueError('Original UI Sprite has no native prefab binding: '+row['key'])
    manifest['binding_outputs'] = [dict(path=(Path('unity')/Path(str(path)+suffix)).as_posix(),
                                       sha256=sha((project/Path(str(path)+suffix)).read_bytes()))
                                   for path in prefabs for suffix in ('', '.meta')]
    manifest['editor_binding_sha256'] = sha((project/'Assets/Game/Editor/CharacterCreation/CharacterCreationUiTools.cs').read_bytes())
    manifest['generator_sha256'] = sha(Path(__file__).read_bytes())
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Verified original creation UI Sprite bindings:', len(manifest['sprites']), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--record-installed', action='store_true')
    args = parser.parse_args()
    (record_installed if args.record_installed else prepare)(args.project.resolve())
