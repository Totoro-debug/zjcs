"""Extract original StoryNodeRegister positions without loading visual textures."""
import hashlib
import json
from collections import Counter

import numpy as np
from UnityPy.classes.PPtr import PPtr
from prepare_map_regions import Regions, ART, REF, save, transform


def build():
    e = Regions()
    catalog = json.loads((ART / 'map-models/catalog.json').read_text(encoding='utf8'))
    adventure = json.loads((ART / 'adventure.json').read_text(encoding='utf8'))
    wanted = {n['RegisterKey'] for graph in adventure['stories'].values()
              for n in graph['nodes'].values() if n['_type'] == 'GetPosAroundStoryObjectNode'}
    models = {}
    grounds = {}
    entities = {}
    failures = {}
    behaviours = Counter()
    registrations = []
    sources = [(path, model.get('alternate_source') or 'Assets/Prefabs/' + path + '.prefab', models, -1, -1)
               for path, model in catalog['models'].items() if 'WorldBuilding' in path]
    for bundle in e.apk.index:
        if not bundle.startswith('defaultpackage_assets_prefabs_ecentity_mapworld_') or 'actionworld' in bundle:
            continue
        for full, _ in e.apk.container(bundle):
            identity = full.rsplit('/', 1)[-1].removesuffix('.prefab')
            if full.endswith('.prefab') and identity.isdecimal():
                sources.append((identity, full, entities, -1, -1))
    sources = list({(path, full, region, layer): (path, full, destination, region, layer)
                    for path, full, destination, region, layer in sources}.values())
    for cfg in adventure['tables']['Map/map']:
        full = ('Assets/Partition/' + cfg['MapGroundBarePath'] if cfg['MapGroundBarePath'] else
                'Assets/Prefabs/Map/' + cfg['MapGroundPath']) + '.prefab'
        sources.append((str(cfg['ClassId']), full, grounds, -1, -1))
        bundle, _ = e.locate(full)
        for obj in e.apk.objects(bundle):
            if obj.type.name != 'MonoBehaviour' or e.mono(obj)[0] != 'PartitionData':
                continue
            e.input(obj)
            partition = e.schemas.read(obj)
            for chunk in partition['SceneChunks'] + partition['Extensions']:
                sources.append((str(cfg['ClassId']), chunk['Res'] + '.prefab', grounds, -1, -1))
            for region, layers in enumerate(partition.get('RegionDatas') or []):
                for layer, definition in enumerate((layers or {}).get('LayerDatas') or []):
                    if not definition:
                        continue
                    for chunk in (definition.get('Scenes') or []) + (definition.get('Extensions') or []):
                        sources.append((str(cfg['ClassId']), chunk['Res'] + '.prefab', grounds, region, layer))
    for path, full, destination, region, layer in sources:
        try:
            _, root = e.locate(full)
            objects = {}
            registers = []

            def visit(obj, parent, active):
                go = obj.read()
                components = [x.component.deref() for x in go.m_Component]
                t = next((c.read() for c in components if c.type.name in ['Transform', 'RectTransform']), None)
                if t is None:
                    return
                matrix = parent @ transform(t)
                active = active and bool(go.m_IsActive)
                objects[obj.path_id] = (matrix, active, go.m_Name)
                for c in components:
                    if c.type.name != 'MonoBehaviour':
                        continue
                    cls = e.mono(c)[0]
                    behaviours[cls] += 1
                    if cls in ['StoryNodeRegister', 'StoryNodeDelayRegister']:
                        e.input(c)
                        cfg = e.schemas.read(c)
                        registrations.append(dict(prefab=full, keys=cfg['StoryNodeKeys']))
                        registers.append((c, cfg))
                for child in t.m_Children:
                    visit(child.read().m_GameObject.deref(), matrix, active)

            visit(root, np.eye(4), True)
            bindings = {}
            for obj, cfg in registers:
                for key, pointer in zip(cfg['StoryNodeKeys'], cfg['StoryNodes']):
                    if key not in wanted or not pointer.get('m_PathID'):
                        continue
                    target = PPtr(**pointer, assetsfile=obj.assets_file).deref()
                    if target.path_id not in objects:
                        raise ValueError('Registration target outside original prefab: ' + key)
                    matrix, active, name = objects[target.path_id]
                    e.input(target)
                    current = next(c.component.deref() for c in target.read().m_Component
                                   if c.component.type.name in ['Transform', 'RectTransform'])
                    while current:
                        e.input(current)
                        father = current.read().m_Father
                        current = father.deref() if father.path_id else None
                    bindings[key] = dict(offset=[float(matrix[0, 3]), float(matrix[1, 3])],
                                         active=active, object=name, path_id=target.path_id,
                                         region=region, layer=layer, prefab=full)
            if bindings:
                for key, binding in bindings.items():
                    destination.setdefault(path, {}).setdefault(key, []).append(binding)
                print('Story registrations', path, sorted(bindings), flush=True)
        except Exception as ex:
            failures[path] = type(ex).__name__ + ': ' + str(ex)
    found = {key for group in [models, grounds, entities] for bindings in group.values() for key in bindings}
    output = ART / 'map-story-bindings.json'
    save(output, dict(models=models, grounds=grounds, entities=entities))
    save(REF / 'story-bindings-source-manifest.json', dict(
        manifest='854_156383', inputs=e.source_records, resources=e.records,
        schemas=e.schemas.records, models=models, grounds=grounds, entities=entities, queried=sorted(wanted),
        missing=sorted(wanted - found), failures=failures, behaviours=dict(behaviours), registrations=registrations,
        runtime_selection='Nearest visible original entity carrying the key in the current offline map; repeated streamed registrations share the original key.',
        operation='Original StoryNodeRegister keys and target transforms in official map entity prefabs; display and partition layers also searched.',
        outputs={'map-story-bindings.json': hashlib.sha256(output.read_bytes()).hexdigest()}))
    print('Story bindings', len(entities), 'entity classes; missing', sorted(wanted - found), 'failures', failures, flush=True)
    if wanted - found or failures:
        raise RuntimeError('Incomplete original story registrations; see source receipt')


if __name__ == '__main__':
    build()
