"""Restore official Mask visibility and enabled flags in existing UI payloads."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from activate_current_payloads import activate
from current_conversion_state import activated_receipt
from official_apk import Apk, ROOT


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def dictionaries(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from dictionaries(child)
    elif isinstance(value, list):
        for child in value:
            yield from dictionaries(child)


def object_path(game_object):
    names = []
    seen = set()
    while game_object is not None:
        if game_object.path_id in seen:
            raise ValueError('Cyclic UI transform hierarchy')
        seen.add(game_object.path_id)
        data = game_object.read()
        names.append(data.m_Name)
        transform = next(c.component.read() for c in data.m_Component
                         if c.component.type.name in ('Transform', 'RectTransform'))
        game_object = transform.m_Father.read().m_GameObject.deref() if transform.m_Father.path_id else None
    return '/' + '/'.join(reversed(names))


def build():
    receipt_path = ROOT / 'assets/reference/official-map/current-ui-masks-source-manifest.json'
    prior = activated_receipt(receipt_path)
    if prior is not None:
        return prior
    upstream = ROOT / 'assets/official'
    source_paths = sorted(set(upstream.glob('*.json')) | set(upstream.glob('*/ui.json')))
    bundles = set()
    for path in source_paths:
        for node in dictionaries(json.loads(path.read_bytes())):
            source = node.get('source')
            if isinstance(source, dict) and isinstance(node.get('root'), dict) and source.get('bundle'):
                bundles.add(source['bundle'])
    apk = Apk.current()
    flags, sources = {}, []
    for bundle in sorted(bundles):
        for obj in apk.objects(bundle):
            if obj.type.name != 'MonoBehaviour':
                continue
            basic = obj.read(check_read=False)
            if not basic.m_Script.path_id:
                continue
            script = basic.m_Script.read()
            if script.m_ClassName != 'Mask' or script.m_Namespace != 'UnityEngine.UI':
                continue
            raw = obj.get_raw_data()
            # MonoBehaviour header with an empty m_Name, then one aligned bool.
            if len(raw) != 36 or raw[28:32] != b'\0' * 4 or raw[32] not in (0, 1) or raw[33:] != b'\0' * 3:
                raise ValueError('Unexpected official Mask layout: ' + bundle + ':' + str(obj.path_id))
            value = {'mask_component_flags': True, 'mask_enabled': bool(basic.m_Enabled),
                     'mask_show_graphic': bool(raw[32])}
            path = object_path(basic.m_GameObject.deref())
            if path in flags and flags[path] != value:
                raise ValueError('Official UI mask path is ambiguous: ' + path)
            flags[path] = value
            sources.append({'path': path, 'source': apk.provenance(obj), 'object_sha256': sha(raw), **value})
        print('Read official masks: ' + bundle, flush=True)
    targets = source_paths + [
        ROOT / 'unity/Assets/StreamingAssets/Home/ui.json',
        ROOT / 'unity/Assets/Game/Resources/Commerce/commerce-runtime.json',
        ROOT / 'unity/Assets/Game/Resources/Wishes/wishes-runtime.json']
    staged_root = ROOT / '.tools/source-packages/current' / apk.version / 'ui-mask-payloads'
    staged_root.mkdir(parents=True, exist_ok=True)
    outputs = []
    for path in targets:
        raw = path.read_bytes()
        document = json.loads(raw)
        changes = []
        for node in dictionaries(document):
            if node.get('mask') is not True:
                continue
            name = node.get('path')
            if name not in flags:
                continue
            for key, value in flags[name].items():
                if node.get(key) != value:
                    node[key] = value
                    changes.append({'path': name, 'field': key, 'value': value})
        if not changes:
            continue
        converted = (json.dumps(document, ensure_ascii=False, separators=(',', ':')) + '\n').encode('utf8')
        staged = staged_root / (sha(converted) + '.json')
        staged.write_bytes(converted)
        outputs.append({'path': path.relative_to(ROOT).as_posix(), 'previous_sha256': sha(raw),
                        'sha256': sha(converted), 'bytes': len(converted),
                        'staged': staged.relative_to(ROOT).as_posix(), 'changes': changes})
    result = {'manifest': apk.version, 'operation': __doc__, 'inputs': sources,
              'generator_sha256': sha(Path(__file__).read_bytes()), 'outputs': outputs}
    (staged_root / 'source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--activate', action='store_true')
    options = parser.parse_args()
    receipt = build()
    if options.activate:
        activate(ROOT / '.tools/source-packages/current' / receipt['manifest'] / 'ui-mask-payloads/source-manifest.json',
                 Path(__file__), ROOT / 'assets/reference/official-map/current-ui-masks-source-manifest.json')
    print('Official mask payloads:', len(receipt['outputs']))
