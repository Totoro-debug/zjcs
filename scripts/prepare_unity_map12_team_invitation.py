"""Pin selected original invitation branches and their maintained Windows C# ports."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'assets/reference/official-map/map12-team-invitation-source-manifest.json'


def build(write=False):
    selected = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if selected != '871_156598':
        raise ValueError('Original team invitation source selection changed')
    source = ROOT / '.tools/source-packages/current' / selected
    manifest_path = source / 'code-manifest.json'
    manifest = json.loads(manifest_path.read_bytes())
    if manifest['manifest'] != selected:
        raise ValueError('Original invitation code manifest differs')

    def record(path):
        return dict(path=path.relative_to(ROOT).as_posix(),
                    sha256=hashlib.sha256(path.read_bytes()).hexdigest())

    assemblies = []
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        item = record(source / 'official-code/managed/decoded' / name)
        if item['sha256'] != manifest['assemblies'][name]['decoded_sha256']:
            raise ValueError('Original invitation assembly differs: ' + name)
        assemblies.append(item)
    original = [record(source / 'official-code/client-source' / (name + '.cs')) for name in (
        'TeamInviter', 'InviterBase', 'TeamUtils', 'AllowMemberInviteToggle')]
    original.extend(record(source / 'official-code/common-source/Common' / (name + '.cs'))
                    for name in ('ActorTargetData', 'NpcTargetData', 'PlayerPlayTeamAction'))
    ports = []
    for name in ('WorldInviterBase', 'WorldTeamInviter', 'WorldAllowMemberInviteToggle'):
        path = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI' / (name + '.cs')
        ports.extend((record(path), record(path.with_name(path.name + '.meta'))))
    checks = [record(ROOT / path) for path in (
        'scripts/fixtures/map12_stele_context_harness.cs', 'scripts/test_map12_stele_context.py',
        'scripts/test_map12_stele_context.ps1',
        'unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.TeamControls.cs',
        'unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.TeamControls.cs.meta')]
    value = dict(format=1, manifest=selected, mapClassId=12, codeManifest=record(manifest_path),
        assemblies=assemblies, originalCode=original, adapters=[record(Path(__file__).resolve())],
        outputs=ports, ruleChecks=checks,
        operation='Recover TeamInviter async branches and AllowMemberInviteToggle directly from selected official '
                  'code. Substitute local service interfaces and Task for the RPC/UniTask boundary; retain signed '
                  'Int64 identity, check order, leader short-circuit, await, error-code consumption, the ignored RPC '
                  'bool, original message keys/arguments, target limit tips, event lifetime and Toggle notifications.',
        limitations=['The actor projection contains only the fields consumed by these ports; it does not replace '
                     'the complete official ActorTargetData/PlayerNpcTargetDataWrap implementation.',
                     'Original team prefab binding, local team services, roster persistence and formal map battle '
                     'flow remain pending. Rule checks and compilation do not establish a playable team window.',
                     'No multiplayer, invitation counters, reward changes or alternate UI are introduced.'])
    raw = (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + '\n').encode()
    if write:
        OUTPUT.write_bytes(raw)
    elif OUTPUT.read_bytes() != raw:
        raise ValueError('Original team invitation source receipt drift')
    print('Original team invitation branches and three maintained component ports verified.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
