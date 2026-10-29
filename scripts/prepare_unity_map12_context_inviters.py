"""Bind the original invitation-list components retained by all five modes."""
from __future__ import annotations
import argparse
import json
from pathlib import Path

from prepare_unity_player_play import port, metadata
from prepare_unity_map12_sources import ROOT, encode, sha, source_path

CODE = ROOT / '.tools/source-packages/current/871_156598/official-code'
RECEIPT = ROOT / 'assets/reference/official-map/map12-context-inviters-source-manifest.json'


def build(write=False):
    selected = json.loads((ROOT / 'official-source.json').read_bytes())
    manifest_path = CODE.parent / 'code-manifest.json'
    manifest = json.loads(manifest_path.read_bytes())
    if selected['active'] != '871_156598' or manifest['manifest'] != selected['active']:
        raise ValueError('Original context inviter selection differs')
    inputs, outputs = [], {}

    def record(path):
        return dict(path=source_path(path), sha256=sha(path.read_bytes()))

    assemblies = []
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        path = CODE / 'managed/decoded' / name
        if sha(path.read_bytes()) != manifest['assemblies'][name]['decoded_sha256']:
            raise ValueError('Original context inviter assembly changed')
        assemblies.append(record(path))
    for name in ('RoomInviter', 'ChallengeInviter', 'CompanionInviter'):
        source = CODE / 'client-source' / (name + '.cs')
        inputs.append(record(source))
        path = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI' / ('World' + name + '.cs')
        outputs[path] = path.read_bytes()
        outputs[path.with_name(path.name + '.meta')] = metadata(path)
    for name in ('WorldContextInviteServices',):
        path = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI' / (name + '.cs')
        outputs[path] = path.read_bytes()
        outputs[path.with_name(path.name + '.meta')] = metadata(path)
    source = CODE / 'common-source/Common/PlayerPlayRoomAction.cs'
    inputs.append(record(source))
    path = ROOT / 'unity/Assets/Game/Runtime/Official/PlayerPlay/PlayerPlayRoomAction.cs'
    outputs[path] = port('PlayerPlayRoomAction', source.read_text(encoding='utf8'))
    outputs[path.with_name(path.name + '.meta')] = metadata(path)
    value = dict(format=1, manifest=manifest['manifest'], mapClassId=12,
        codeManifest=record(manifest_path), assemblies=assemblies, originalCode=inputs,
        adapters=[record(Path(__file__).resolve()), record(ROOT / 'scripts/prepare_unity_player_play.py'),
                  record(ROOT / 'scripts/test_map12_team_invite_rules.py'),
                  record(ROOT / 'scripts/fixtures/map12_team_invite_rules_harness.cs')],
        consumerBindings=[record(ROOT / 'unity/Assets/Game/Runtime/Official/TeamUI' / name)
                          for name in ('TeamInvitePlayerItem.cs', 'TeamInviteUiServices.cs')],
        outputs=[dict(path=source_path(path), sha256=sha(raw)) for path, raw in outputs.items()],
        operation='Recover all awaits and branches from the original RoomInviter, ChallengeInviter '
                  'and CompanionInviter async state machines. Preserve NPC early-return, room/team '
                  'capacity error codes, response payload forwarding, challenge append-before-message, '
                  'name-cache lookup, confirmation completion and cancellation code 112. '
                  'The local service boundary replaces RPC, lite-info and popup service access.',
        limitations=['These components retain the complete original inactive invitation prefab hierarchy. '
                     'Their original online modes remain subject to the confirmed offline availability rules.',
                     'Formal local NPC invitation and full window integration remain pending.'])
    for path, raw in outputs.items():
        if write:
            path.write_bytes(raw)
        elif not path.is_file() or path.read_bytes() != raw:
            raise ValueError('Original context inviter output drift: ' + source_path(path))
    raw = encode(value)
    if write:
        RECEIPT.write_bytes(raw)
    elif not RECEIPT.is_file() or RECEIPT.read_bytes() != raw:
        raise ValueError('Original context inviter receipt drift')
    print('Original context invitation components verified: 3 inviters and Room action', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
