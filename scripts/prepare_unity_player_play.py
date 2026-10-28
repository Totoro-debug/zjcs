"""Port selected official player-play mutex value types without changing their rules."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/PlayerPlay'
RECEIPT = ROOT / 'assets/reference/official-map/map12-player-play-source-manifest.json'
NAMES = (
    'IPlayerPlayAction', 'IPlayerPlayState', 'IPlayerPlayParam',
    'PlayerPlayActionType', 'PlayerPlayStateType', 'PlayerPlayStateMutexType',
    'PlayerPlayActionNoneParam', 'PlayerPlayFightStateParam',
    'PlayerPlayMatchStateParam', 'PlayerPlayRoomMatchStateParam',
    'PlayerPlayConfig', 'PlayerPlayStateMutexConfig', 'GameFastType', 'PlayerPlayTeamAction',
)


def replace_once(text, old, new):
    if text.count(old) != 1:
        raise ValueError('Original source shape changed: ' + old)
    return text.replace(old, new, 1)


def port(name, text):
    text = replace_once(text, 'namespace Common;', 'namespace Zjcs.Official.PlayerPlay\n{')
    if name in ('PlayerPlayMatchStateParam', 'PlayerPlayRoomMatchStateParam'):
        text = replace_once(text, name + '(bool isMatchDone)', name)
        text = replace_once(text, 'public readonly bool IsMatchDone = isMatchDone;',
                            'public readonly bool IsMatchDone;\n\n\tpublic ' + name +
                            '(bool isMatchDone)\n\t{\n\t\tIsMatchDone = isMatchDone;\n\t}')
    if name == 'PlayerPlayConfig':
        text = replace_once(text, 'PlayerPlayConfig<T>(T type, IPlayerPlayParam param)', 'PlayerPlayConfig<T>')
        text = replace_once(text, 'public readonly T Type = type;', 'public readonly T Type;')
        text = replace_once(text, 'public readonly IPlayerPlayParam Param = param;',
                            'public readonly IPlayerPlayParam Param;\n\n\tpublic PlayerPlayConfig(T type, IPlayerPlayParam param)'
                            '\n\t{\n\t\tType = type;\n\t\tParam = param;\n\t}')
    return ('// Selected official Common implementation; namespace / C# syntax adaptation only.\n' +
            text.rstrip() + '\n}\n').encode('utf-8')


def metadata(path, folder=False):
    guid = hashlib.md5(('zjcs-player-play:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
    header = 'fileFormatVersion: 2\nguid: ' + guid + '\n'
    if folder:
        body = 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
    elif path.suffix == '.cs':
        body = ('MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n'
                '  executionOrder: 0\n  icon: {instanceID: 0}\n')
    else:
        body = 'AssemblyDefinitionImporter:\n  externalObjects: {}\n'
    return (header + body + '  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()


def build(write):
    selected = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if selected != '871_156598':
        raise ValueError('Player-play source selection changed')
    source = ROOT / '.tools/source-packages/current' / selected / 'official-code'
    inputs, outputs = [], {}

    def record(path):
        return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())

    for name in NAMES:
        original = source / 'common-source/Common' / (name + '.cs')
        inputs.append(record(original))
        outputs[DEST / (name + '.cs')] = port(name, original.read_text(encoding='utf-8-sig'))
    for name in ('PlayerPlayUtil', 'PlayerPlayFightAction', 'StageInfo'):
        inputs.append(record(source / 'common-source/Common' / (name + '.cs')))
    for name in ('PlayerPlayStateModule', 'IPlayerPlayActionEnv', 'PlayerPlayStateExitHandleBase',
                 'BattleSteleWorldStageDialog'):
        inputs.append(record(source / 'client-source' / (name + '.cs')))
    outputs[DEST / 'Zjcs.Official.PlayerPlay.asmdef'] = (
        '{\n  "name": "Zjcs.Official.PlayerPlay",\n  "rootNamespace": "Zjcs.Official.PlayerPlay",\n'
        '  "references": [],\n  "noEngineReferences": true\n}\n').encode()
    outputs[DEST.with_suffix('.meta')] = metadata(DEST, folder=True)
    for path in list(outputs):
        if path.suffix != '.meta':
            outputs[path.with_name(path.name + '.meta')] = metadata(path)
    # Service boundaries and the recovered async algorithm are maintained C# ports.
    for name in ('PlayerPlayFightAction', 'PlayerPlayStateModule', 'PlayerPlayServices'):
        path = DEST / (name + '.cs')
        outputs[path.with_name(path.name + '.meta')] = metadata(path)
    if write:
        DEST.mkdir(parents=True, exist_ok=True)
    for path, data in outputs.items():
        if write:
            path.write_bytes(data)
        elif not path.exists() or path.read_bytes().replace(b'\r\n', b'\n') != data:
            raise ValueError('Player-play port drift: ' + str(path))
    receipt = dict(format=1, source_version=selected, inputs=inputs,
                   assemblies=[record(source / 'managed/decoded' / name)
                               for name in ('Assembly-CSharp.dll', 'Console.dll')],
                   adapters=[record(ROOT / name) for name in
                             ('scripts/prepare_unity_player_play.py', 'scripts/test_player_play.ps1',
                              'scripts/fixtures/player_play_harness.cs')],
                   transformations=['Common namespace to Zjcs.Official.PlayerPlay',
                                    'Three primary constructors expanded for Unity C#',
                                    'UniTask state machine recovered as Task async with identical branch order',
                                    'Player, stage parser, exit handles and language broadcast supplied by local services'],
                   scope='Source migration only; main-map state observers, exit handlers and popup callback not connected.',
                   outputs=[record(path) for path in sorted(outputs)] +
                           [record(DEST / (name + '.cs')) for name in
                            ('PlayerPlayFightAction', 'PlayerPlayStateModule', 'PlayerPlayServices')])
    data = (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode()
    if write:
        RECEIPT.write_bytes(data)
    elif json.loads(RECEIPT.read_bytes()) != receipt:
        raise ValueError('Player-play receipt drift')
    print(f'Player-play source verified: {len(inputs)} inputs, {len(receipt["outputs"])} outputs')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
