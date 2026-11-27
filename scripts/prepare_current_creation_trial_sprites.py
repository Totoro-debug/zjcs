"""Bind the current original trial-item images and quality configuration."""
import argparse
import collections
import hashlib
import json
from pathlib import Path

from UnityPy.classes.PPtr import PPtr
from official_apk import Apk, ROOT
from official_source import OfficialSource
from current_component_schemas import CurrentSchemas
from prepare_current_creation_ui_sprites import CurrentUiSprites, canonical_assets
from prepare_character_creation_group_icons import folder_meta, text_meta


RELATIVE = Path('Assets/Official/CharacterCreation/TrialUI')
RECEIPT = RELATIVE/'native-sprites-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare(project):
    source = OfficialSource()
    path = project/RELATIVE/'trial-ui.json'
    previous = path.read_bytes()
    layout = json.loads(previous)
    schemas = CurrentSchemas()
    converter = CurrentUiSprites(Apk.current(), source.prepared_path/'character-creation/trial-sprites')
    bindings, uses = {}, collections.defaultdict(list)

    def bind(key, obj, use):
        identity = obj.assets_file.name+':'+str(obj.path_id)
        if key in bindings and bindings[key][0] != identity:
            raise ValueError('Review changed shared trial image: '+key)
        bindings[key] = identity, obj
        uses[key].append(use)

    def walk(obj, parent='', label=None):
        go = obj.read()
        node_path = parent+'/'+(label or go.m_Name)
        parts = [p.component.deref() for p in go.m_Component]
        transform = next(p for p in parts if p.type.name in ('Transform', 'RectTransform'))
        old = next((n for n in layout['nodes'] if n['path'] == node_path), None)
        images = []
        for component in parts:
            if component.type.name == 'MonoBehaviour' and component.read(check_read=False).m_Script.read().m_ClassName in ('Image', 'MirrorImage', 'SlicedFilledImage'):
                pointer = schemas.read(component)['m_Sprite']
                if pointer['m_PathID']:
                    images.append(PPtr(**pointer, assetsfile=component.assets_file).deref())
        sprite = old.get('image', {}).get('sprite') if old else None
        if sprite:
            if len(images) != 1:
                raise ValueError('Review changed trial image binding: '+node_path)
            bind(sprite['file'], images[0], node_path)
        seen = collections.Counter()
        for pointer in transform.read().m_Children:
            child = pointer.read().m_GameObject.deref()
            name = child.read().m_Name
            index = seen[name]
            seen[name] += 1
            walk(child, node_path, name+(' ('+str(index)+')' if index else ''))

    try:
        root = converter.apk.asset('prefabs_ui_item', '/ItemIconAppendAppearance.prefab')
        walk(root)
        names = [name for name in converter.apk.index if 'scriptableconfig' in name and 'itemicon' in name]
        if len(names) != 1:
            raise ValueError('Current ItemIconConfig bundle is missing or ambiguous')
        config = converter.apk.asset(names[0], '/ItemIconConfig.asset')
        quality = schemas.read(config)['QualityBgImages']
        if len(quality) != len(layout['quality_keys']):
            raise ValueError('Review changed official trial quality count')
        for index, pointer in enumerate(quality):
            bind(layout['quality_keys'][index], PPtr(**pointer, assetsfile=config.assets_file).deref(), 'QualityBgImages['+str(index)+']')
        if set(bindings) != {row['key'] for row in layout['sprites']}:
            raise ValueError('Current trial images differ from the consumed bindings')
        tables = source.prepared_path/'official-data/tables.json'
        items = {row['ClassId']: row for row in json.loads(tables.read_bytes())['item']}
        for row in layout['trial_items']:
            if items[row['id']]['Quality'] != row['quality']:
                raise ValueError('Review changed trial item quality: '+str(row['id']))
        aliases = {key: converter.sprite(obj) for key, (_, obj) in bindings.items()}
        roots = [dict(source=converter.apk.provenance(obj), object_sha256=sha(obj.get_raw_data())) for obj in (root, config)]
    finally:
        converter.apk.close()
    existing = canonical_assets(project)
    ui = json.loads((project/'Assets/Official/CharacterCreation/RuntimeUI/native-sprites-source-manifest.json').read_bytes())
    if ui['manifest'] != source.version:
        raise ValueError('Migrate creation UI image dependencies first')
    for row in ui['sprites']+ui['textures']:
        existing[row['native']['guid']] = dict(path=row['path'], sha256=row['native']['sha256'])
    outputs, dependencies = {}, {}

    def install(native, relative):
        if native['guid'] in existing:
            old = existing[native['guid']]
            if sha((project/old['path']).read_bytes()) != native['sha256'] or old['sha256'] != native['sha256']:
                raise ValueError('Canonical trial dependency changed: '+old['path'])
            for suffix in ('', '.meta'):
                target = Path(old['path']+suffix)
                dependencies[target.as_posix()] = dict(path=(Path('unity')/target).as_posix(), sha256=sha((project/target).read_bytes()))
            return old['path']
        for suffix in ('', '.meta'):
            raw = Path(str(ROOT/native['path'])+suffix).read_bytes()
            if sha(raw) != native['meta_sha256' if suffix else 'sha256']:
                raise ValueError('Prepared trial image changed')
            target = project/Path(str(relative)+suffix)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(raw)
            outputs[target] = dict(path=(Path('unity')/Path(str(relative)+suffix)).as_posix(), sha256=sha(raw))
        return relative.as_posix()

    textures = {}
    for identity, native in converter.textures.items():
        target = install(native, RELATIVE/'NativeSprites/Textures'/('texture_'+sha(identity.encode())[:16]+'.asset'))
        textures[identity] = dict(path=target, native=native)
    sprites = []
    for entry in layout['sprites']:
        native = aliases[entry['key']]
        entry.update(path=install(native, RELATIVE/'NativeSprites'/('sprite_'+sha(native['identity'].encode())[:16]+'.asset')),
                     border=[native['expected']['border'][axis] for axis in 'xyzw'], ppu=native['expected']['ppu'])
        sprites.append(dict(key=entry['key'], path=entry['path'], nodes=uses[entry['key']], native=native,
                            texture_path=textures[native['texture']]['path']))
    for relative in (RELATIVE/'NativeSprites', RELATIVE/'NativeSprites/Textures'):
        meta = project/Path(str(relative)+'.meta')
        if not meta.exists():
            meta.write_bytes(folder_meta(relative.as_posix()))
        outputs[meta] = dict(path=(Path('unity')/Path(str(relative)+'.meta')).as_posix(), sha256=sha(meta.read_bytes()))
    meta = project/Path(str(RECEIPT)+'.meta')
    if not meta.exists():
        meta.write_bytes(text_meta(RECEIPT.as_posix()))
    outputs[meta] = dict(path=(Path('unity')/Path(str(RECEIPT)+'.meta')).as_posix(), sha256=sha(meta.read_bytes()))
    layout['sprite_source_manifest'] = RECEIPT.as_posix()
    path.write_text(json.dumps(layout, ensure_ascii=False, indent=2)+'\n', 'utf8')
    outputs[path] = dict(path=(Path('unity')/RELATIVE/'trial-ui.json').as_posix(), sha256=sha(path.read_bytes()))
    manifest = dict(manifest=source.version, apk_sha256=source.apk_sha256,
                    scope='Current original trial-item Sprite, atlas, full-mip texture and quality bindings; layout and animation retain their separate source receipt.',
                    roots=roots, sprites=sprites, textures=list(textures.values()), outputs=list(outputs.values()),
                    canonical_dependencies=list(dependencies.values()), previous_layout_sha256=sha(previous),
                    item_table_sha256=sha(tables.read_bytes()), generator_sha256=sha(Path(__file__).read_bytes()))
    (project/RECEIPT).write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Installed current trial images:', len(sprites), 'textures:', len(textures), 'shared dependencies:', len(dependencies), flush=True)


def record_installed(project):
    path = project/RECEIPT
    manifest = json.loads(path.read_bytes())
    for row in manifest['outputs']+manifest['canonical_dependencies']:
        asset = project/Path(row['path']).relative_to('unity')
        if sha(asset.read_bytes()) != row['sha256']:
            raise ValueError('Installed original trial resource changed: '+str(asset))
    layout = json.loads((project/RELATIVE/'trial-ui.json').read_bytes())
    folder = Path('Assets/Game/Resources/CharacterCreation/Trial')
    prefabs = [folder/('Item'+str(row['id'])+'.prefab') for row in layout['trial_items']]
    manifest['binding_outputs'] = [dict(path=(Path('unity')/Path(str(p)+suffix)).as_posix(),
                                       sha256=sha((project/Path(str(p)+suffix)).read_bytes()))
                                   for p in prefabs for suffix in ('', '.meta')]
    used = {node['image']['sprite']['file'] for node in layout['nodes']
            if node.get('image', {}).get('sprite') and not node['path'].endswith('/Content/QualityImg')}
    used.update(layout['quality_keys'][row['quality']] for row in layout['trial_items'])
    text = '\n'.join((project/p).read_text('utf-8-sig') for p in prefabs)
    for row in manifest['sprites']:
        if row['key'] in used and 'guid: '+row['native']['guid'] not in text:
            raise ValueError('Trial image has no native prefab binding: '+row['key'])
    manifest['editor_binding_sha256'] = sha((project/'Assets/Game/Editor/CharacterCreation/CharacterCreationUiTools.cs').read_bytes())
    manifest['generator_sha256'] = sha(Path(__file__).read_bytes())
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Verified current trial image bindings:', len(prefabs), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--record-installed', action='store_true')
    args = parser.parse_args()
    (record_installed if args.record_installed else prepare)(args.project.resolve())
