"""Package original NPC data for the independent Unity bone/slot/attachment graph."""
import argparse
import gzip
import hashlib
import json
import struct
from pathlib import Path

from official_apk import ROOT
from prepare_native_character_geometry import atlas_regions
from prepare_unity_map12_sources import encode, sha, source_path
from prepare_unity_map12_team_npc_skeletons import CATALOG, RECEIPT as SOURCE_RECEIPT

DEST = ROOT / 'unity/Assets/Official/World/Map/12/NativeTeamNpcData'
FOLDER = DEST / 'Resources/Map12/TeamNpcSkeletons'
RUNTIME_DEST = ROOT / 'unity/Assets/Game/Resources/Map12/Native/TeamNpcBattle'
RUNTIME_FOLDER = RUNTIME_DEST / 'TeamNpcSkeletons'
RECEIPT = ROOT / 'assets/reference/official-map/map12-team-npc-data-source-manifest.json'
PREFABS = ROOT / 'unity/Assets/Official/World/Map/12/Source/team-npc-prefabs.json'
PREFAB_RECEIPT = ROOT / 'assets/reference/official-map/map12-team-npc-prefabs-source-manifest.json'


def build(write=False):
    source_receipt = json.loads(SOURCE_RECEIPT.read_bytes())
    record, = [row for row in source_receipt['outputs'] if row['path'] == source_path(CATALOG)]
    if sha(CATALOG.read_bytes()) != record['sha256']:
        raise ValueError('Original NPC skeleton catalog changed')
    source = json.loads(CATALOG.read_bytes())
    if source['manifest'] != '871_156598' or len(source['skeletons']) != 34:
        raise ValueError('Original NPC skeleton scope differs')
    prefab_receipt = json.loads(PREFAB_RECEIPT.read_bytes())
    prefab_record, = [row for row in prefab_receipt['outputs'] if row['path'] == source_path(PREFABS)]
    if sha(PREFABS.read_bytes()) != prefab_record['sha256']:
        raise ValueError('Original NPC prefab source changed')
    prefabs = json.loads(PREFABS.read_bytes())
    prefab_objects = {row['identity']: row for row in prefabs['objects']}
    skeleton_indices = {row['identity']: index for index, row in enumerate(source['skeletons'])}
    source_renderers = {row['identity']: row for row in source['renderers']}
    renderers = []
    for model in prefabs['models']:
        for node in model['nodes']:
            components = [prefab_objects[identity] for identity in node['components']]
            graphics = [source_renderers[row['identity']] for row in components
                        if row['identity'] in source_renderers]
            if not graphics:
                continue
            combine = [row['fields']['CombineSkins'] for row in components
                       if 'CombineSkins' in row['fields']]
            if len(graphics) != 1 or len(combine) > 1:
                raise ValueError('Original NPC prefab has ambiguous skeleton or combined-skin components')
            graphic = graphics[0]
            renderers.append(dict(npcClassId=model['npcClassId'],
                skeletonIndex=skeleton_indices[graphic['skeleton']],
                startingAnimation=graphic['fields']['startingAnimation'],
                combineSkins=combine[0] if combine else []))
    if len(renderers) != 92 or len(prefabs['models']) != 91:
        raise ValueError('Original NPC prefab renderer inventory differs')
    outputs, models = {}, []

    def read(row):
        raw = (ROOT / row['path']).read_bytes()
        if sha(raw) != row['sha256']:
            raise ValueError('Original NPC source hash differs: ' + row['path'])
        return raw

    def save(path, raw, folder=False):
        if not folder:
            outputs[path] = raw
        outputs[Path(str(path) + '.meta')] = ('fileFormatVersion: 2\nguid: ' +
            hashlib.md5(('zjcs:map12:npc-native-data:' + source_path(path)).encode()).hexdigest() +
            ('\nfolderAsset: yes\nDefaultImporter:\n' if folder else '\nTextScriptImporter:\n') +
            '  externalObjects: {}\n').encode()
        if path == FOLDER / 'catalog.json' or (path.parent == FOLDER and path.suffix == '.bytes'):
            runtime_path = RUNTIME_DEST / ('catalog.json' if path.suffix == '.json' else 'TeamNpcSkeletons/' + path.name)
            runtime_raw = raw
            if path.suffix == '.json':
                runtime_raw = raw.replace(b'"Map12/TeamNpcSkeletons/', b'"Map12/Native/TeamNpcBattle/TeamNpcSkeletons/')
            outputs[runtime_path] = runtime_raw
            outputs[Path(str(runtime_path) + '.meta')] = outputs[Path(str(path) + '.meta')]

    def string(value):
        raw = value.encode()
        return struct.pack('<I', len(raw)) + raw

    def block(raw):
        zipped = gzip.compress(raw, compresslevel=9, mtime=0)
        return struct.pack('<I', len(raw)) + hashlib.sha256(raw).digest() + struct.pack('<I', len(zipped)) + zipped

    for item in source['skeletons']:
        payload = gzip.decompress(read(item['decoded']))
        if sha(payload) != item['decodedSha256']:
            raise ValueError('Original decoded NPC skeleton bytes differ')
        if len(item['atlases']) != 1:
            raise ValueError('Bind additional original NPC atlases explicitly')
        regions = atlas_regions(read(item['atlases'][0]['output']).decode('utf-8-sig'))
        atlas = json.dumps(regions, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode()
        skeleton = json.loads(payload)
        name = item['original']['name']
        raw = b'ZJCSNPCS' + struct.pack('<I', 1) + string(source['manifest']) + string(name) + \
            struct.pack('<f', item['fields']['scale']) + block(payload) + block(atlas)
        filename = sha(item['identity'].encode())[:24]
        path = FOLDER / (filename + '.bytes')
        save(path, raw)
        attachments, paths = {}, []
        slots = {row['name']: i for i, row in enumerate(skeleton['slots'])}
        for skin in skeleton['skins']:
            for slot, entries in skin.get('attachments', {}).items():
                for key, attachment in entries.items():
                    kind = attachment.get('type', 'region')
                    attachments[kind] = attachments.get(kind, 0) + 1
                    if kind == 'path':
                        paths.append(dict(skin=skin['name'], slot=slots[slot], key=key,
                            vertices=attachment['vertexCount'], lengths=attachment['lengths'],
                            closed=attachment.get('closed', False), constantSpeed=attachment.get('constantSpeed', True)))
        models.append(dict(identity=item['identity'], name=name, resource='Map12/TeamNpcSkeletons/' + filename,
            sha256=sha(raw), skeletonHash=sha(payload), atlasHash=sha(atlas), scale=item['fields']['scale'],
            bones=len(skeleton['bones']), slots=len(skeleton['slots']), skins=len(skeleton['skins']),
            animations=len(skeleton['animations']), regions=len(regions), attachments=sum(attachments.values()),
            constraints=sum(len(skeleton.get(kind, [])) for kind in ('ik', 'transform', 'path', 'physics')),
            attachmentTypes=attachments, paths=paths,
            startingAnimations=sorted({row['fields']['startingAnimation'] for row in source['renderers']
                                       if row['skeleton'] == item['identity'] and row['fields']['startingAnimation']})))
    save(FOLDER / 'catalog.json', encode(dict(manifest=source['manifest'], models=models, renderers=renderers,
        prefabSourceSha256=sha(PREFABS.read_bytes()))))
    parent = FOLDER
    while parent != DEST.parent:
        save(parent, b'', True)
        parent = parent.parent
    for folder in (RUNTIME_DEST, RUNTIME_FOLDER):
        outputs[Path(str(folder) + '.meta')] = ('fileFormatVersion: 2\nguid: ' +
            hashlib.md5(('zjcs:map12:npc-native-runtime:' + source_path(folder)).encode()).hexdigest() +
            '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI'
    code_paths = [runtime / name for name in ('ProfessionNativeSkeletonData.cs', 'ProfessionNativeSkeletonData.Npc.cs',
        'ProfessionNativePathAttachment.cs', 'ProfessionNativeNpcSkins.cs')]
    fixture = ROOT / 'unity/Assets/Game/Editor/ProfessionUI/Map12NpcDataValidation.cs'
    for path in (*code_paths[1:], fixture):
        outputs[path.with_name(path.name + '.meta')] = ('fileFormatVersion: 2\nguid: ' +
            hashlib.md5(('zjcs:map12:npc-native-code:' + source_path(path)).encode()).hexdigest() +
            '\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n'
            '  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
    def record(path):
        return dict(path=source_path(path), sha256=sha(path.read_bytes()))
    receipt = encode(dict(format=1, manifest=source['manifest'], mapClassId=12,
        source=record(CATALOG), sourceReceipt=record(SOURCE_RECEIPT),
        prefabSource=record(PREFABS), prefabSourceReceipt=record(PREFAB_RECEIPT),
        tools=[record(Path(__file__)), record(ROOT / 'scripts/prepare_native_character_geometry.py')],
        runtime=[record(path) for path in code_paths], fixture=record(fixture),
        outputs=[dict(path=source_path(path), sha256=sha(raw)) for path, raw in sorted(outputs.items())],
        counts=dict(skeletons=len(models), attachments=sum(row['attachments'] for row in models),
                    paths=sum(len(row['paths']) for row in models), prefabs=len(prefabs['models']),
                    renderers=len(renderers)),
        validatedScope='DX12 Unity Play Mode loaded all 34 actual Resources packages and verified their byte hashes, '
                       'bone/slot/skin/attachment/constraint counts, seven path layouts and all 92 original renderer, '
                       'starting-animation and ordered CombineSkins bindings from 91 NPC prefabs. '
                       'Existing male/female packages loaded through the unchanged original player format.',
        operation='Package the exact decoded source JSON and original atlas-region projection in a separate '
                  'hash-verified NPC format. Reuse the independent native bone/slot/skin/attachment graph, '
                  'add original weighted path control points without claiming a path constraint solver. '
                  'Preserve all source animation and constraint fields; player/weapon package format is unchanged.',
        limitations=['Data loading alone does not verify complete animation, constraint solving or rendered NPC UI. '
                     'Formal invitation, party save, battle and Windows publication remain pending.']))
    outputs[RECEIPT] = receipt
    for path, raw in outputs.items():
        if write:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        elif not path.is_file() or path.read_bytes() != raw:
            raise ValueError('Original native NPC data drift: ' + source_path(path))
    print('Original native NPC packages verified:', len(models), 'models', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
