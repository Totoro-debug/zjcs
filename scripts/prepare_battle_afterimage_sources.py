"""Verify the selected original afterimage graphs and refresh their source receipt."""
import hashlib
import json
from pathlib import Path
import re

from verify_battle_skill_graph_rules import verify_selected_components

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / 'assets/reference/battle-animation/skill-afterimage-runtime-source-manifest.json'


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def capture_block(path):
    text = path.read_text(encoding='utf-8-sig')
    blocks = re.findall(r'^  AfterimageDatas:\n(.*?)(?=^  \w)', text, re.M | re.S)
    assert len(blocks) == 1, ('Unexpected original capture nodes', path, len(blocks))
    return blocks[0].strip()


def main():
    selected = json.loads((ROOT / 'official-source.json').read_text(encoding='utf-8-sig'))
    assert selected['active'] == '884_156692', 'Review a changed official source version.'
    receipt = json.loads(RECEIPT.read_text(encoding='utf-8'))
    for row in receipt['official_client_sources']:
        assert identity(ROOT / row['path']) == row, ('Official source changed', row['path'])
    for row in receipt['migrated_outputs'] + receipt['meta_outputs']:
        row.update(identity(ROOT / row['path']))

    graph_receipt = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    graphs = json.loads(graph_receipt.read_text(encoding='utf-8'))
    native_inputs = {row['path']: row for row in graphs['native_inputs']}
    outputs = {row['path']: row for row in graphs['outputs']}
    wanted = {'11510:myPlayablePrefab', '12260:myPlayablePrefab', '13970:myPlayablePrefab',
              '13970:UpPlayablePrefab', '13970:DownPlayablePrefab'}
    bindings = []
    for row in graphs['originals']:
        if row['id'] not in wanted:
            continue
        native = ROOT / row['native_export_meta']['path'][:-5]
        assert identity(native) == native_inputs[native.relative_to(ROOT).as_posix()]
        assert identity(Path(str(native) + '.meta')) == row['native_export_meta']
        relative = native.as_posix().split('/ExportedProject/Assets/', 1)[1]
        target = ROOT / 'unity/Assets/Official/Combat/SkillGraphRuntime/Resources/Combat/SkillGraphs' / relative
        assert identity(target) == outputs[target.relative_to(ROOT).as_posix()]
        config = capture_block(native)
        assert config == capture_block(target), ('Original capture timing/color differs', row['id'])
        times = [float(value) for value in re.findall(r'RecordTime: (\S+)', config)]
        assert len(times) == (4 if row['id'].startswith('13970:') else 6)
        bindings.append(dict(binding=row['id'], original=row, native=identity(native),
                             migrated=identity(target), record_times=times, capture_yaml=config))
    assert {row['binding'] for row in bindings} == wanted

    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, value in json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_text(encoding='utf-8-sig'))['entities'].items():
            entities.setdefault(key, {}).update(value)
    ids = ('11510', '11512', '11513', '11514', '12260', '12261', '12262', '12263', '12264')
    definitions = {key: entities[key] for key in ids}
    defaults = []
    for key, definition in definitions.items():
        disperse = definition.get('FightDamageComponent', {}).get('DisperseStatus')
        if disperse is not None and 'ExcludeCharacterTypes' not in disperse:
            assert disperse['BasePercent'] == 0 and disperse['Count'] == 0
            disperse['ExcludeCharacterTypes'] = []
            defaults.append(dict(entity=key, field='FightDamageComponent.DisperseStatus.ExcludeCharacterTypes', value=[]))
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_text(encoding='utf-8-sig'))
    items = [row for row in rules['skill'] if row['EcEntityId'] in (11510, 12260)]
    receipt['additional_selected_configuration'] = verify_selected_components(definitions, items)
    receipt['additional_selected_configuration']['historical_empty_defaults'] = defaults
    receipt['graph_bindings'] = bindings
    receipt['graph_source_receipt'] = identity(graph_receipt)
    receipt['generator'] = identity(Path(__file__))
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified all five original afterimage prefabs and nine selected-version skill/hit/movement/damage entities.')


if __name__ == '__main__':
    main()
