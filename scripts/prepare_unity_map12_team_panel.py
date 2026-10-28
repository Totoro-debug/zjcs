"""Pin the original team panel, member operations and consumed Windows ports."""
import argparse
import hashlib
import json
from pathlib import Path
import uuid

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'assets/reference/official-map/map12-team-panel-source-manifest.json'
TEAM = ('TeamPanel', 'TeamPanelItem', 'TeamPanelItemButton', 'TeamPanelServices',
        'TeamHeadCharacterPortrait', 'TeamToggleStatusBase', 'TeamToggleObject',
        'TeamPlayerUIBase', 'TeamPlayerIconMeHpSliderNormal')
PLAY = ('PlayerPlayConfirmEnv', 'PlayerPlayStateClientUtils')


def build(write=False):
    selected = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if selected != '871_156598':
        raise ValueError('Recheck original team panel version')
    base = ROOT / '.tools/source-packages/current' / selected
    code = base / 'official-code'
    manifest = json.loads((base / 'code-manifest.json').read_bytes())

    def record(path):
        return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())

    assemblies = []
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        row = record(code / 'managed/decoded' / name)
        if row['sha256'] != manifest['assemblies'][name]['decoded_sha256']:
            raise ValueError('Original team assembly changed')
        assemblies.append(row)
    outputs = []
    for folder, names in (('TeamUI', TEAM), ('PlayerPlay', PLAY)):
        for name in names:
            path = ROOT / 'unity/Assets/Game/Runtime/Official' / folder / (name + '.cs')
            meta = path.with_name(path.name + '.meta')
            raw = ('fileFormatVersion: 2\nguid: ' + uuid.uuid5(uuid.NAMESPACE_URL,
                'zjcs:official:map12:team:' + name).hex + '\n').encode()
            if write:
                meta.write_bytes(raw)
            elif not meta.is_file() or meta.read_bytes() != raw:
                raise ValueError('Original team panel metadata differs: ' + name)
            outputs.extend((record(path), record(meta)))
    outputs.append(record(ROOT / 'unity/Assets/Game/Runtime/Official/TeamUI/Zjcs.Official.TeamUI.asmdef'))
    validation = []
    for name in ('TeamPanelImport', 'TeamPanel'):
        editor = ROOT / ('unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.' + name + '.cs')
        meta = editor.with_name(editor.name + '.meta')
        meta_raw = ('fileFormatVersion: 2\nguid: ' + uuid.uuid5(uuid.NAMESPACE_URL,
            'zjcs:official:map12:team:' + name + ':acceptance').hex + '\n').encode()
        if write:
            meta.write_bytes(meta_raw)
        elif not meta.is_file() or meta.read_bytes() != meta_raw:
            raise ValueError('Team panel validation metadata differs')
        validation.extend((record(editor), record(meta)))
    original = [record(code / 'client-source' / (name + '.cs')) for name in
        ('TeamPanel', 'TeamPanelItem', 'TeamPanelItemButton', 'HeadCharacterPortrait',
         'PlayerUIBase', 'PlayerIconMeHpSlider', 'PlayerIconMeHpSliderNormal', *PLAY)]
    original.extend(record(code / 'framework-firstpass-source' / (name + '.cs'))
                    for name in ('UIToggleStatusBase', 'UIToggleObject'))
    original.append(record(code / 'framework-source/Framework/TransformExtensions.cs'))
    value = dict(format=1, manifest=selected, mapClassId=12, assemblies=assemblies,
        inputs=[record(base / 'code-manifest.json')], originalCode=original, outputs=outputs,
        validation=validation,
        adapters=[record(Path(__file__).resolve())],
        operation='Migrate original TeamPanel/member/button lifecycle, event order, selected-target keys, '
        'pool size/order, guards, direct leave operation, team-action dismissal confirmation, portrait and '
        'HP controls. Recover async bodies from original MoveNext and use Task at the same await boundaries. '
        'Project manager/RPC/data/icon services explicitly; preserve the exact Main(-1,-1) ActiveSkill '
        'selection and slot-level operations in the host projection. Reuse official UIBase/pools/state/text '
        'components and portrait request pipeline. Chat/nearby operations reach approved offline policy.',
        limitations=['Full native TeamPanel/member/button prefabs and isolated DX12 input fixtures are supplied '
                     'separately. Formal map host, real player/NPC services, equipped skills, roster/formation '
                     'persistence and battle integration remain pending.',
                     'Only consumed team action/confirmation methods are ported; unrelated client helpers '
                     'are not replaced by empty methods. Optional HpSpriteSlider retains its contract; '
                     'the selected TeamPanelItem original pointer is null.'])
    raw = (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode()
    if write:
        OUTPUT.write_bytes(raw)
    elif not OUTPUT.is_file() or OUTPUT.read_bytes() != raw:
        raise ValueError('Original team panel source/port receipt differs')
    print('Original team panel/member/confirmation ports verified.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
