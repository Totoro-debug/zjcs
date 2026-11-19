"""Verify official flame-spray, burning-ground, native graph and local bindings."""
import argparse
import copy
import json
import re
import subprocess
from pathlib import Path

from official_apk import Apk
from prepare_battle_water_dragon_sources import local_identity
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'assets/reference/battle-rules/flame-spray-source-manifest.json'


def verify_flame_graph(path):
    text = path.read_text(encoding='utf-8-sig')
    blocks = re.split(r'(?=^--- !u!)', text, flags=re.M)
    names, scripts = {}, {}
    for block in blocks:
        header = re.match(r'--- !u!(\d+) &(\d+)', block)
        if not header:
            continue
        kind, file_id = header.groups()
        if kind == '1':
            name = re.search(r'^  m_Name: (.*)$', block, re.M)
            if name:
                names[file_id] = name[1]
        elif kind == '114':
            scripts[file_id] = block
    sequences = [block for block in scripts.values() if 'myPlayables:' in block]
    if len(sequences) != 1:
        raise ValueError('The official flame sequence is ambiguous')
    effects = []
    for time, file_id in re.findall(r'- myTime: ([\d.]+)\s+myPlayable: \{fileID: (\d+)\}', sequences[0]):
        block = scripts[file_id]
        game_object = re.search(r'm_GameObject: \{fileID: (\d+)', block)[1]
        if names.get(game_object) == 'effect (2)':
            effects.append((float(time), bool(re.search(r'AttachTo: 2\s+myBoneName: body', block))))
    if effects != [(.34, True)] or 'BoneName: shouR_1' not in text:
        raise ValueError('The official flame attachment or timing changed: ' + str(path))


def build(use_index):
    combat = ROOT / 'unity/Assets/Official/Combat'
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, components in json.loads((combat / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(components)
    keys = ('11690', '11691', '11692', '11580', '11581', '11582', '11590', '11591', '10021', '25045')
    definitions = {key: copy.deepcopy(entities[key]) for key in keys}
    normalized = []
    for key, value in definitions.items():
        damage = value.get('FightDamageComponent')
        if damage is not None and 'ExcludeCharacterTypes' not in damage['DisperseStatus']:
            damage['DisperseStatus']['ExcludeCharacterTypes'] = []
            normalized.append(key + '/DisperseStatus/ExcludeCharacterTypes')
    roots = definitions['11690']['FightSkillComponent']['HitList']
    hit = definitions['11691']['FightHitFixedComponent']
    if (len(roots) != 3 or any(root['ClassId'] != 11691 or abs(root['Delay'] - (.34 + index * .5)) > .00001
            for index, root in enumerate(roots)) or hit['DamageId'] != 11692 or len(hit['Scope']) != 3
            or any(row['Pos'] != {'X': 0, 'Y': index + 1} or abs(row['Delay'] - index * .1) > .00001
                   for index, row in enumerate(hit['Scope']))):
        raise ValueError('The official flame waves or scopes changed')
    summon = definitions['11581']['FightHitSummonComponent']['SummonCfg']
    status = definitions['11582']['FightStatusMoveNearComponent']
    passive = definitions['25045']['FightStatusHitScopeComponent']
    if (summon['SummonId'] != 3320 or summon['SummonStatus'] != [11582]
            or not status['TryAtStart'] or not any(row['SkillId'] == 11590 for row in status['Skills'])
            or definitions['11591']['FightHitFixedComponent']['DamageId'] != 10021
            or passive['SkillId'] != 11580 or abs(passive['Rate'] - .2) > .00001):
        raise ValueError('The official burning-ground trigger chain changed')
    rules = json.loads((combat / 'rules.json').read_bytes())
    items = [row for row in rules['skill'] if row['ClassId'] in (10072, 10576)]
    if {row['ClassId']: row['EcEntityId'] for row in items} != {10072: 11690, 10576: 25045}:
        raise ValueError('The official flame or passive item binding changed')
    configuration = verify_selected_components(definitions, items)
    with Apk.current() as apk:
        version = apk.version
    code = ROOT / '.tools/source-packages/current' / version / 'official-code'
    originals = [identity(code / 'common-source/Common' / (name + '.cs')) for name in (
        'FightSkillComponentInfo', 'FightHitFixedComponentInfo', 'FightHitSummonComponentInfo',
        'FightStatusHitScopeComponent', 'FightStatusMoveNearComponent', 'FightDamageComponentInfo', 'HitScopCfg')]
    originals += [identity(code / 'client-source' / (name + '.cs')) for name in (
        'MyFightHitFixed', 'MyFightStatusMoveNear', 'MyPlayableFollowBone')]
    originals.append(identity(code / 'game-framework-source/GameFramework/MyPlayableSequence.cs'))
    paths = [ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json',
             ROOT / 'assets/reference/battle-animation/grid-entity-assets-source-manifest.json']
    native, outputs = [], []
    for path in paths:
        data = subprocess.check_output(['git', 'show', ':' + path.relative_to(ROOT).as_posix()], cwd=ROOT) if use_index else path.read_bytes()
        doc = json.loads(data)
        for key, result in (('native_inputs', native), ('outputs', outputs)):
            for row in doc[key]:
                if any('/' + entity + '/' in row['path'] for entity in ('11690', '11580', '11590')):
                    if identity(ROOT / row['path']) != row:
                        raise ValueError('The official flame graph differs: ' + row['path'])
                    result.append(row)
                    if re.search(r'/playable11690_(Up|Down)\.prefab$', row['path']):
                        verify_flame_graph(ROOT / row['path'])
    if sum('playable11690_' in row['path'] and row['path'].endswith('.prefab') for row in native) != 2:
        raise ValueError('The official directional flame graphs are incomplete')
    bindings = ROOT / 'unity/Assets/Game/Resources/Combat/skill-playables.json'
    cue, = (row for row in json.loads(bindings.read_bytes())['skills'] if row['skill_entity_id'] == 11690)
    if not any(row['kind'] == 1 and row['animation'] == '200/skill_23' for row in cue['playables']):
        raise ValueError('The official flame character action is not bound')
    local = [ROOT / path for path in (
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleFlameSprayImpact.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleLiveImpactSession.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleLiveGraphDriver.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleGridEntityPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillGraphPlayback.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleHitPlaybackAcceptance.FlameSpray.cs',
        'scripts/test_flame_spray_unity.ps1')]
    return dict(manifest=version, configuration=configuration, explicit_empty_dispel_defaults=normalized,
                original_code=originals, source_manifests=[local_identity(path, use_index) for path in paths],
                native_inputs=native, native_outputs=outputs, character_binding=identity(bindings),
                migration=[local_identity(path, use_index) for path in local], generator=local_identity(Path(__file__), use_index))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--index', action='store_true')
    args = parser.parse_args()
    proof = build(args.index)
    if args.check:
        if not OUTPUT.exists() or json.loads(OUTPUT.read_bytes()) != proof:
            raise ValueError('The flame-spray source receipt differs')
    else:
        OUTPUT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Verified official flame-spray rules, ground trigger chain, graphs and character action.')
