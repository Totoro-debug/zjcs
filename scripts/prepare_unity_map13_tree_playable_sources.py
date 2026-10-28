"""Recover every original object in the two trees' referenced performance prefabs."""
from pathlib import Path
from collections import Counter
import json

from official_apk import Apk, ROOT
from prepare_unity_map13_prop_hierarchies import key, sha, write, encode
from prepare_unity_story_attachment_runtime import record
from prepare_unity_map13_prop_parts import meta

SOURCE = ROOT / 'unity/Assets/Official/World/Map/13/Source'
DEST = SOURCE / 'native-tree-playables.json'
RECEIPT = ROOT / 'assets/reference/official-map/map13-native-tree-playables-source-manifest.json'


def build():
    graph_path = SOURCE / 'native-entity-object-graph.json'
    managed_path = SOURCE / 'native-entity-components.json'
    hierarchy_path = SOURCE / 'native-prop-hierarchies.json'
    models_path = RECEIPT.with_name('map13-native-tree-models-source-manifest.json')
    hierarchy = json.loads(hierarchy_path.read_bytes())
    roots = {v['root'] for v in json.loads(models_path.read_bytes()).get('models', []) if 'root' in v}
    # The Model receipt identifies its original Model; its source Transform
    # supplies the exact outer entity identity, without name-based selection.
    if not roots:
        nodes = json.loads((SOURCE / 'native-attachment-followers.json').read_bytes())['nodes']
        by_go = {v['key']: v for v in nodes}; by_tf = {v['transformKey']: v for v in nodes}
        roots = {by_tf[by_go[v['source']]['parentTransformKey']]['key']
                 for v in json.loads(models_path.read_bytes())['models']}
    selections = []
    for driver in hierarchy['objects']:
        if driver['type'] != 'ECViewDriver' or not any(
                p['field'] == '.m_GameObject' and key(p['cab'], p['path_id']) in roots for p in driver['references']):
            continue
        cab = driver['cab']
        for view in driver['fields']['references']['RefIds']:
            if view['rid'] == -2:
                continue
            fields, kind = view['data'], view['type']['class']
            pointers = []
            if kind == 'ECPlayableSupporterView':
                pointers = [(n, fields[n]) for n in ['mAppearPlayable', 'mLoopPlayable']]
            elif kind == 'EcMapEntityFadeOutDelayDestoryHandler':
                pointers = [('FadeOutPlayable', fields['FadeOutPlayable'])]
            elif kind == 'ECWorldStageCfgNormalProvider':
                pointers = [(f'mStateCfgs[{i}].{name}', row[name])
                            for i, row in enumerate(fields['mStateCfgs'])
                            for name in ['myStartPlayable', 'myLoopPlayable']]
            for field, pointer in pointers:
                if pointer['m_PathID']:
                    if pointer['m_FileID'] != 0:
                        raise ValueError('Tree performance now needs an external-CAB root resolver')
                    selections.append(dict(driver=driver['key'], view=kind, field=field,
                                           root=key(cab, pointer['m_PathID'])))
    if len({s['driver'] for s in selections}) != 2:
        raise ValueError('Both original tree drivers must supply their actual performances')
    graph = json.loads(graph_path.read_bytes())
    indexed = {key(v['cab'], v['path_id']): v for v in graph['objects']}
    managed = {key(v['cab'], v['path_id']): v for v in json.loads(managed_path.read_bytes())['components']}
    apk = Apk.current(); native, loaded, records = {}, set(), {}

    def read(identity):
        if identity in records:
            return records[identity]
        row = indexed[identity]
        if row['input'] not in loaded:
            loaded.add(row['input'])
            native.update({key(o.assets_file.name, o.path_id): o for o in apk.objects(row['input'])})
        obj = native[identity]
        digest = sha(obj.get_raw_data())
        if digest != row.get('original_payload_sha256', row['payload_sha256']):
            raise ValueError('Original performance object changed: ' + identity)
        body = managed[identity]['fields'] if obj.type.name == 'MonoBehaviour' else obj.read_typetree(check_read=True)
        kind = managed[identity]['type'] if obj.type.name == 'MonoBehaviour' else obj.type.name
        records[identity] = dict(key=identity, type=kind, payload_sha256=digest,
                                 source=apk.provenance(obj), fields=body, references=row['references'])
        return records[identity]

    def visit(identity, visited):
        if identity in visited:
            return
        visited.add(identity)
        game_object = read(identity)
        for pointer in game_object['references']:
            if not pointer['field'].startswith('.m_Component['):
                continue
            component = read(key(pointer['cab'], pointer['path_id']))
            if component['type'] == 'Transform':
                for child in component['references']:
                    if child['field'].startswith('.m_Children['):
                        tf = read(key(child['cab'], child['path_id']))
                        visit(key(child['cab'], tf['fields']['m_GameObject']['m_PathID']), visited)

    prefabs = []
    for root in sorted({row['root'] for row in selections}):
        visited = set(); before = set(records); visit(root, visited)
        prefabs.append(dict(root=root, name=read(root)['fields']['m_Name'],
                            game_objects=sorted(visited), objects=sorted(set(records) - before)))
    types = dict(Counter(row['type'] for row in records.values()))
    write(DEST, encode(dict(manifest=apk.version, mapClassId=13, selections=selections,
                            prefabs=prefabs, types=types, objects=[records[k] for k in sorted(records)])))
    meta(DEST, 'TextScriptImporter')
    metadata = Path(str(DEST) + '.meta')
    write(metadata, metadata.read_bytes().replace(b'DefaultImporter:', b'TextScriptImporter:'))
    sources = [graph_path, managed_path, hierarchy_path, models_path, SOURCE / 'native-attachment-followers.json']
    write(RECEIPT, encode(dict(manifest=apk.version, mapClassId=13,
        method='Follow the two verified original tree EC drivers into their actual appear, loop and fade-out prefab references. Read each complete native hierarchy from official bundle bytes, verify every source payload hash, and retain every managed field and native particle parameter. No runtime prefab conversion is claimed by this inventory.',
        inputs=[record(p) for p in sources], outputs=[record(DEST), record(Path(str(DEST) + '.meta'))],
        tools=[record(Path(__file__).resolve())], prefabs=len(prefabs), objects=len(records), types=types)))
    print('Original tree performances:', len(prefabs), 'prefabs,', len(records), 'objects,', types.get('ParticleSystem', 0), 'particle systems')


if __name__ == '__main__':
    build()
