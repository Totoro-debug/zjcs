"""Restore official Image hit testing and TabGroup selection parameters."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from activate_current_payloads import activate
from current_conversion_state import activated_receipt
from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from refresh_current_ui_masks import dictionaries


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def normalize_booleans(value):
    fields = {'graphic_flags', 'raycast_target', 'maskable', 'has_tab_group',
              'tab_group_allow_switch_off', 'no_drawing_raycast'}
    if isinstance(value, dict):
        for key, child in value.items():
            if key in fields and type(child) is int and child in (0, 1):
                value[key] = bool(child)
            else:
                normalize_booleans(child)
    elif isinstance(value, list):
        for child in value:
            normalize_booleans(child)


def normalize_prior(prior, apk, receipt):
    stage = ROOT / '.tools/source-packages/current' / apk.version / 'ui-graphics-json-booleans'
    stage.mkdir(parents=True, exist_ok=True)
    outputs = []
    for row in prior['outputs']:
        path = ROOT / row['path']
        raw = path.read_bytes()
        data = json.loads(raw)
        normalize_booleans(data)
        payload = json.dumps(data, ensure_ascii=False, separators=(',', ':')).encode('utf8')
        target = stage / (sha(payload) + '.json')
        target.write_bytes(payload)
        outputs.append({'path': row['path'], 'previous_sha256': sha(raw), 'sha256': sha(payload),
                        'bytes': len(payload), 'staged': target.relative_to(ROOT).as_posix()})
    normalize_booleans(prior['inputs'])
    result = {'manifest': apk.version, 'operation': __doc__, 'inputs': prior['inputs'], 'outputs': outputs,
              'flags_as_json_booleans': True, 'generator_sha256': sha(Path(__file__).read_bytes()),
              'prior_outputs': prior.get('prior_outputs', []) + prior['outputs']}
    source = stage / 'source-manifest.json'
    source.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return activate(source, Path(__file__), receipt)


PATHS = {}


def object_path(obj):
    key = (id(obj.assets_file), obj.path_id)
    if key in PATHS:
        return PATHS[key]
    data = obj.read()
    transform = next(c.component.read() for c in data.m_Component
                     if c.component.type.name in ('Transform', 'RectTransform'))
    name = data.m_Name
    if not transform.m_Father.path_id:
        result = '/' + name, str(obj.path_id)
        PATHS[key] = result
        return result
    parent = transform.m_Father.read()
    index = 0
    for child in parent.m_Children:
        sibling = child.read().m_GameObject
        if sibling.path_id == obj.path_id:
            break
        if sibling.read().m_Name == name:
            index += 1
    prefix, root_id = object_path(parent.m_GameObject.deref())
    result = prefix + '/' + name + (f' ({index})' if index else ''), root_id
    PATHS[key] = result
    return result


def build(activate_outputs=False):
    apk = Apk.current()
    receipt = ROOT / 'assets/reference/official-map/current-ui-graphics-source-manifest.json'
    prior = activated_receipt(receipt, apk.version)
    if prior is not None:
        if not prior.get('flags_as_json_booleans'):
            if not activate_outputs:
                raise ValueError('Activate the verified Boolean transport normalization first')
            return normalize_prior(prior, apk, receipt)
        return prior
    upstream = ROOT / 'assets/official'
    paths = sorted(set(upstream.glob('*.json')) | set(upstream.glob('*/ui.json'))
                   | {upstream / 'wish-performance/performance.json'})
    bundles = set()
    roots = {}
    for path in paths:
        for node in dictionaries(json.loads(path.read_bytes())):
            source = node.get('source')
            if isinstance(source, dict) and isinstance(node.get('root'), dict) and source.get('bundle'):
                bundles.add(source['bundle'])
                root_path = node['root'].get('path')
                if root_path and source.get('path_id'):
                    roots.setdefault(root_path, set()).add((source['bundle'], str(source['path_id'])))
    schemas = ScriptSchemas(code_root=ROOT / '.tools/source-packages/current' / apk.version / 'official-code')
    flags, sources = {}, []
    for bundle in sorted(bundles):
        for obj in apk.objects(bundle):
            if obj.type.name != 'MonoBehaviour':
                continue
            basic = obj.read(check_read=False)
            if not basic.m_Script.path_id:
                continue
            script = basic.m_Script.read()
            kind = script.m_Namespace + '.' + script.m_ClassName
            if kind not in ('UnityEngine.UI.Image', 'UnityEngine.UI.Ext.TabGroup', '.NoDrawingRayCast'):
                continue
            data = schemas.read(obj)
            name, root_id = object_path(basic.m_GameObject.deref())
            if kind == 'UnityEngine.UI.Image':
                value = {'image': {'graphic_flags': True, 'raycast_target': bool(data['m_RaycastTarget']),
                                   'maskable': bool(data['m_Maskable'])}}
            elif kind == '.NoDrawingRayCast':
                value = {'no_drawing_raycast': True, 'raycast_target': bool(data['m_RaycastTarget']),
                         'raycast_padding': [data['m_RaycastPadding'][key] for key in ('x', 'y', 'z', 'w')]}
            else:
                value = {'has_tab_group': True, 'tab_group_allow_switch_off': bool(data['m_AllowSwitchOff'])}
            existing = flags.setdefault((bundle, root_id, name), {})
            for field, setting in value.items():
                if field in existing and existing[field] != setting:
                    raise ValueError('Ambiguous official graphic path: ' + name + ':' + field)
                existing[field] = setting
            sources.append({'path': name, 'root_path_id': root_id, 'source': apk.provenance(obj),
                            'object_sha256': sha(obj.get_raw_data()), **value})
        print('Read official UI graphics:', bundle, flush=True)
    paths += [ROOT / 'unity/Assets/StreamingAssets/Home/ui.json',
              ROOT / 'unity/Assets/Game/Resources/Commerce/commerce-runtime.json',
              ROOT / 'unity/Assets/Game/Resources/Wishes/wishes-runtime.json']
    stage = ROOT / '.tools/source-packages/current' / apk.version / 'ui-graphics'
    stage.mkdir(parents=True, exist_ok=True)
    outputs = []
    for path in paths:
        raw = path.read_bytes()
        document = json.loads(raw)
        changes = 0
        def apply(node, binding=None):
            nonlocal changes
            if isinstance(node, list):
                for child in node:
                    apply(child, binding)
                return
            if not isinstance(node, dict):
                return
            source = node.get('source')
            root = node.get('root')
            nodes = node.get('nodes')
            if isinstance(source, dict) and isinstance(root, dict) and source.get('bundle'):
                binding = (source['bundle'], str(source['path_id']))
            elif isinstance(root, dict) or isinstance(nodes, list) and nodes:
                root_path = root.get('path') if isinstance(root, dict) else nodes[0].get('path')
                candidates = roots.get(root_path, set())
                if len(candidates) == 1:
                    binding = next(iter(candidates))
                elif len(candidates) > 1:
                    raise ValueError('Runtime prefab needs an explicit source binding: ' + str(root_path))
            value = flags.get((*binding, node.get('path'))) if binding else None
            if value:
                for key, item in value.items():
                    if key == 'image':
                        target = node.get('image')
                        if isinstance(target, dict) and any(target.get(k) != v for k, v in item.items()):
                            target.update(item)
                            changes += 1
                    elif node.get(key) != item:
                        node[key] = item
                        changes += 1
            for child in list(node.values()):
                apply(child, binding)
        apply(document)
        if not changes:
            continue
        payload = json.dumps(document, ensure_ascii=False, separators=(',', ':')).encode('utf8')
        destination = stage / (sha(payload) + '.json')
        destination.write_bytes(payload)
        outputs.append({'path': path.relative_to(ROOT).as_posix(), 'previous_sha256': sha(raw),
                        'sha256': sha(payload), 'bytes': len(payload), 'bindings': changes,
                        'staged': destination.relative_to(ROOT).as_posix()})
    result = {'manifest': apk.version, 'operation': __doc__, 'inputs': sources, 'outputs': outputs,
              'flags_as_json_booleans': True,
              'generator_sha256': sha(Path(__file__).read_bytes())}
    stage_receipt = stage / 'source-manifest.json'
    stage_receipt.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    if activate_outputs:
        return activate(stage_receipt, Path(__file__), receipt)
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--activate', action='store_true')
    result = build(parser.parse_args().activate)
    print('Converted UI graphic payloads:', len(result['outputs']))
