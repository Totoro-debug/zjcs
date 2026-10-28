"""Verify the original TeamInviteNpc selector and its selected Map12 battle data."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / 'assets/reference/official-map/map12-stele-npcs-source-manifest.json'


def build(write):
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if version != '871_156598':
        raise ValueError('Map12 NPC source selection changed')
    current = ROOT / '.tools/source-packages/current' / version
    inputs = []

    def record(path):
        return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())

    def read(path):
        inputs.append(record(path))
        return json.loads(path.read_bytes())

    tables_path = current / 'official-data/tables.json'
    tables = read(tables_path)
    proof = read(current / 'table-proof-source-manifest.json')
    if proof['manifest'] != version or proof['table_sha256'] != inputs[0]['sha256']:
        raise ValueError('Selected table constructor proof does not match')
    payload_path = ROOT / 'unity/Assets/Game/Resources/DailyChallenges/npc-assistance-rules.json'
    payload = read(payload_path)
    runtime_source = read(payload_path.with_name('npc-assistance-rules-source-manifest.json'))
    if payload['manifest'] != version or runtime_source['manifest'] != version or \
            runtime_source['output_sha256'] != hashlib.sha256(payload_path.read_bytes()).hexdigest():
        raise ValueError('Selected packaged NPC tables do not match')
    for key in ('npc', 'npc_level_detail', 'npc_profession_detail', 'conditions', 'skill', 'player_robot_invite'):
        if payload[key] != tables[key]:
            raise ValueError('Packaged NPC table differs from selected source: ' + key)
    code = current / 'official-code'
    originals = ('common-source/Common/NpcInfoParser.cs', 'common-source/Common/NpcInfo.cs',
                 'common-source/Common/NpcType.cs', 'common-source/Common/NpcFightData.cs',
                 'common-source/Common/NpcLevelDetailInfoParser.cs', 'common-source/Common/NpcTargetData.cs',
                 'common-source/Common/TeamTargetBattleStele.cs', 'client-source/TeamInviteNpc.cs',
                 'client-source/NpcUtils.cs', 'client-source/BattleSteleWorldStageDialog.cs')
    for name in originals:
        inputs.append(record(code / name))
    parser_source = (code / originals[0]).read_text(encoding='utf-8-sig')
    invite_source = (code / 'client-source/TeamInviteNpc.cs').read_text(encoding='utf-8-sig')
    if 'GetNpcs(NpcType ignoreType)' not in parser_source or 'data.NpcType != ignoreType' not in parser_source or \
            'GetNpcs(NpcType.MapExploreBoss)' not in invite_source:
        raise ValueError('Original exclusion selector changed')
    level_ids = {row['ClassId'] for row in tables['npc_level_detail']}
    candidates = [row for row in tables['npc'] if row['NpcType'] != 2 and row['FightEntityId'] > 0
                  and row['Id'] in level_ids]
    companions = [row for row in candidates if row['NpcType'] == 4 and row['CanDevelop']]
    extras = [row for row in candidates if row not in companions]
    if len(candidates) != 95 or len(companions) != 85 or len(extras) != 10 or \
            sum(row['NpcType'] == 3 for row in extras) != 9 or sum(row['NpcType'] == 1 for row in extras) != 1:
        raise ValueError('Original team invitation candidate closure changed')
    adaptations = [ROOT / name for name in (
        'unity/Assets/Game/Runtime/Official/Combat/FightOfficialNpcAssistanceCatalog.cs',
        'scripts/fixtures/map12_stele_npc_harness.cs', 'scripts/test_map12_stele_npcs.ps1',
        'scripts/prepare_unity_map12_stele_npcs.py')]
    result = dict(format=1, manifest=version, mapClassId=12, inputs=inputs,
                  assemblies=[record(code / 'managed/decoded' / name)
                              for name in ('Console.dll', 'Assembly-CSharp.dll')],
                  originalSelector='NpcType != MapExploreBoss && FightEntityId > 0 && level detail exists',
                  candidateNpcClassIds=[row['Id'] for row in candidates],
                  additionalNpcClassIds=[row['Id'] for row in extras],
                  requiresEncounter=sum(row['IsOnlyMeetCanAssist'] for row in candidates),
                  eligibility='Apply original IsOnlyMeetCanAssist and AssistCondition before inviting.',
                  adaptation='Explicit team API reuses existing official properties, level ranges and skill assembly; '
                             'existing companion API remains scoped to its 85 candidates.',
                  scope='Candidate eligibility and battle-unit API only; no UI, map team, counts or settlement wiring.',
                  outputs=[record(path) for path in adaptations])
    if write:
        RECEIPT.write_bytes((json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
    elif json.loads(RECEIPT.read_bytes()) != result:
        raise ValueError('Map12 team NPC receipt drift')
    print('Original team NPC source verified: 95 candidates, 10 additional configured fighters')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
