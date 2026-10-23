"""Keep verified current UI flags through regeneration of the Home transport."""
from __future__ import annotations

import json
from pathlib import Path

import xxhash

from current_conversion_state import activated_receipt, records
from official_apk import ROOT
from official_source import OfficialSource
from refresh_current_character_meshes import preserve
from refresh_current_localization import language, sha


FIELDS = {'graphic_flags', 'raycast_target', 'maskable', 'has_tab_group',
          'tab_group_allow_switch_off', 'no_drawing_raycast', 'raycast_padding'}


def restore_missing(actual, expected, texts=None):
    """Only add missing official flag fields; reject every other difference."""
    if isinstance(actual, dict) and isinstance(expected, dict):
        key = actual.get('localization_key')
        if texts is not None and key and isinstance(actual.get('text'), dict):
            current = texts.get(xxhash.xxh64_intdigest(key.encode('utf8')))
            if current is not None and current == (expected.get('text') or {}).get('value'):
                actual['text']['value'] = current
        for key, value in expected.items():
            if key not in actual and key in FIELDS:
                actual[key] = value
            elif key in actual:
                restore_missing(actual[key], value, texts)
    elif isinstance(actual, list) and isinstance(expected, list) and len(actual) == len(expected):
        for left, right in zip(actual, expected):
            restore_missing(left, right, texts)


def build():
    version = OfficialSource().version
    receipt = ROOT / 'assets/reference/official-map/current-home-ui-regeneration-source-manifest.json'
    prior = None
    try:
        prior = activated_receipt(receipt, version)
        if prior is not None and prior.get('includes_wish_flag_inputs'):
            return prior
    except ValueError:
        # Recheck every field below; unrelated changes must still fail.
        prior = json.loads(receipt.read_bytes())
    text_receipt = ROOT / 'assets/reference/official-map/current-ui-text-bindings-source-manifest.json'
    text = json.loads(text_receipt.read_bytes())
    home = ROOT / 'unity/Assets/StreamingAssets/Home/ui.json'
    record = next(row for row in records(text) if row['path'] == home.relative_to(ROOT).as_posix())
    expected_raw = (ROOT / record['staged']).read_bytes()
    if sha(expected_raw) != record['sha256']:
        raise ValueError('The verified localized UI stage changed')
    expected = json.loads(expected_raw)
    actual_raw = home.read_bytes()
    actual = json.loads(actual_raw)
    identical_json = actual == expected
    texts, language_inputs = language(ROOT / '.tools/source-packages/WwiseBnk')
    restore_missing(actual, expected, texts)
    if actual != expected:
        raise ValueError('Home regeneration changed more than the verified missing flag fields')
    # Once the generator retains all fields, keep its stable serialization.
    # Full JSON equality above establishes the conversion, including every value.
    home_payload = actual_raw if identical_json else expected_raw

    inventory = ROOT / 'unity/Assets/StreamingAssets/Home/source-manifest.json'
    inventory_raw = inventory.read_bytes()
    source = json.loads(inventory_raw)
    if source.get('generator') != 'scripts/prepare_unity_home_runtime.py':
        raise ValueError('Unexpected Home inventory generator')
    for name, value in source['outputs'].items():
        payload = (inventory.parent / name).read_bytes()
        if len(payload) != value['bytes'] or sha(payload) != value['sha256']:
            raise ValueError('Regenerated inventory differs from its actual output: ' + name)
    previous = json.loads((ROOT / 'assets/reference/official-map/current-output-inventories-source-manifest.json').read_bytes())
    old_inventory = next(row for row in records(previous) if row['path'] == inventory.relative_to(ROOT).as_posix())
    outputs = [{'path': inventory.relative_to(ROOT).as_posix(),
                'previous_sha256': old_inventory['sha256'], 'sha256': sha(inventory_raw),
                'operation': 'Verify every regenerated output before rebinding its inventory; inventory bytes are unchanged.',
                'observed_regenerated_source': preserve(inventory)}]
    talk = ROOT / 'assets/official/home-tutorial/talk.json'
    talk_raw = talk.read_bytes()
    data = json.loads(talk_raw)
    panels = {panel['key']: panel for panel in expected['panels']}
    count = 0
    wish = ROOT / 'assets/official/home-wish-tutorial.json'
    wish_raw = wish.read_bytes()
    wish_data = json.loads(wish_raw)
    input_panels = [(key, panel['root'], 'talk_') for key, panel in data['panels'].items()]
    input_panels += [(key, panel, 'wish_') for key, panel in wish_data['panels'].items()]
    for key, input_root, prefix in input_panels:
        counterpart = panels[prefix + key]
        panel_records = [counterpart['root']] + counterpart['nodes']
        nodes = {node['path']: node for node in panel_records}
        if len(nodes) != len(panel_records):
            raise ValueError('Ambiguous tutorial node paths')

        def enrich(node):
            nonlocal count
            reference = nodes[node['path']]
            for field in FIELDS:
                if field in reference:
                    if field in node and node[field] != reference[field]:
                        raise ValueError('Tutorial flag conflicts with verified official flags')
                    node[field] = reference[field]
                    count += 1
            if isinstance(node.get('image'), dict):
                for field in FIELDS:
                    if field in (reference.get('image') or {}):
                        node['image'][field] = reference['image'][field]
                        count += 1
            for child in node.get('children', []):
                enrich(child)

        enrich(input_root)
    talk_payload = json.dumps(data, ensure_ascii=False, separators=(',', ':')).encode('utf8')
    wish_payload = json.dumps(wish_data, ensure_ascii=False, separators=(',', ':')).encode('utf8')
    for path, old, new in ((talk, talk_raw, talk_payload), (wish, wish_raw, wish_payload),
                           (home, actual_raw, home_payload)):
        saved = preserve(path)
        temporary = path.with_name(path.name + '.current.tmp')
        temporary.write_bytes(new)
        temporary.replace(path)
        # Keep the new bytes too, so another generator cannot erase the evidence.
        preserved_output = preserve(path)
        outputs.append({'path': path.relative_to(ROOT).as_posix(), 'previous_sha256': sha(old),
                        'sha256': sha(new), 'bytes': len(new), 'previous': saved,
                        'preserved_output': preserved_output})
        if path == home and identical_json:
            outputs[-1]['previous_sha256'] = record['sha256']
            outputs[-1]['observed_regenerated_sha256'] = sha(old)
            outputs[-1]['operation'] = 'Verify full JSON equality and retain the generator serialization.'
    result = {'manifest': version, 'operation': __doc__, 'outputs': outputs,
              'tutorial_flag_bindings': count,
              'includes_wish_flag_inputs': True,
              'inputs': [{'path': text_receipt.relative_to(ROOT).as_posix(), 'sha256': sha(text_receipt.read_bytes())}] + language_inputs,
              'generator_sha256': sha(Path(__file__).read_bytes()),
              'home_generator_sha256': sha((ROOT / source['generator']).read_bytes())}
    if prior is not None:
        result['previous_receipt'] = preserve(receipt)
        result['prior_outputs'] = prior.get('prior_outputs', []) + prior['outputs']
    receipt.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return result


if __name__ == '__main__':
    print('Verified Home regeneration bindings:', build()['tutorial_flag_bindings'])
