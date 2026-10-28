"""Pin original team portrait/member code and the consumed Windows service ports."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'assets/reference/official-map/map12-team-icons-source-manifest.json'


def build(write=False):
    selected = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if selected != '871_156598':
        raise ValueError('Original team portrait source selection changed')
    base = ROOT / '.tools/source-packages/current' / selected
    manifest = json.loads((base / 'code-manifest.json').read_bytes())
    code = base / 'official-code'

    def record(path):
        return dict(path=path.relative_to(ROOT).as_posix(),
                    sha256=hashlib.sha256(path.read_bytes()).hexdigest())

    assemblies = []
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        row = record(code / 'managed/decoded' / name)
        if row['sha256'] != manifest['assemblies'][name]['decoded_sha256']:
            raise ValueError('Original team portrait assembly changed: ' + name)
        assemblies.append(row)
    source_path = ROOT / 'unity/Assets/Official/World/Map/12/Source/team-ui-prefabs.json'
    source = json.loads(source_path.read_bytes())
    status_rows = [r for r in source['objects'] if r.get('scriptType') == 'PlayerIconOther']
    if len(status_rows) != 3 or any(r['fields']['myIconStatus'] != {'m_FileID': 0, 'm_PathID': 0} for r in status_rows):
        raise ValueError('Recheck original team portrait status dependencies')
    directory = ROOT / 'unity/Assets/Game/Runtime/Official/TeamUI'
    output_files = sorted(directory.glob('*')) + [directory.with_name(directory.name + '.meta')]
    if len(output_files) != 13 or any(not p.is_file() for p in output_files):
        raise ValueError('Recheck maintained team portrait port coverage')
    original = [record(code / 'client-source' / (name + '.cs')) for name in
                ('PlayerIcon', 'PlayerIconOther', 'PlayerIconMe', 'PlayerInfoUI', 'NpcIconUI', 'TeamMemberIcon')]
    original.append(record(code / 'framework-firstpass-source/UIBase.cs'))
    shared = [record(ROOT / 'unity/Assets/Game/Runtime/Official' / path) for path in (
        'ProfessionUI/UIBase.cs', 'ProfessionUI/PlayerInfoUI.cs', 'ProfessionUI/PlayerIconMe.cs',
        'ProfessionUI/PlayerInfoData.cs', 'ProfessionUI/ProfessionPlayerInfoBindings.cs',
        'ProfessionUI/ProfessionImageResourceBridge.cs', 'WorldUI/WorldStateSwitcher.cs')]
    checks = [record(ROOT / 'unity/Assets/Game/Editor' / path) for path in (
        'OfficialMap12PlayModeAcceptance.TeamIcons.cs', 'OfficialMap12PlayModeAcceptance.TeamIcons.cs.meta',
        'OfficialMap12PlayModeAcceptance.TeamControls.cs', 'ProfessionUI/ProfessionPlayerInfoValidation.cs')]
    editor_definition_path = ROOT / 'unity/Assets/Game/Editor/Zjcs.Editor.asmdef'
    editor_definition = json.loads(editor_definition_path.read_bytes())
    editor_definition['references'] = sorted(editor_definition['references'])
    editor_definition_hash = hashlib.sha256(json.dumps(editor_definition, sort_keys=True,
        separators=(',', ':')).encode()).hexdigest()
    value = dict(format=1, manifest=selected, mapClassId=12, assemblies=assemblies,
        inputs=[record(base / 'code-manifest.json'), record(source_path)], originalCode=original,
        outputs=[record(p) for p in output_files], sharedPorts=shared, validation=checks,
        editorAssembly=dict(path=editor_definition_path.relative_to(ROOT).as_posix(),
            semantic_sha256=editor_definition_hash,
            normalization='Parse JSON; sort object keys and unordered assembly references; compact UTF-8 JSON.'),
        adapters=[record(Path(__file__).resolve())],
        originalStatusReferences=[dict(identity=r['identity'], field='myIconStatus', value=r['fields']['myIconStatus'])
                                  for r in status_rows],
        operation='Migrate PlayerIcon/PlayerIconOther, NpcIconUI and TeamMemberIcon using the existing original '
                  'UIBase, PlayerIconMe and PlayerInfoUI ports. Preserve serialized field names, generic NPC '
                  'member branch, initialization/enable order, async first/latest requests, nine-second update '
                  'boundary, cancellation, portrait-click guards, brief-info await, online/default-online flags '
                  'and matching event lifetime. Supply manager, data and resource boundaries through local interfaces.',
        limitations=['Full native team prefab binding, real player/NPC info services, roster/formation persistence '
                     'and formal map battle remain pending.',
                     'The three original status pointers are null. ITeamIconStatus is a consumed manager/component '
                     'contract, not a replacement serialized component or an implementation of PlayerIconStatus.',
                     'NpcIconUI parser/combat-rating and resource services must be bound before its real native '
                     'window is usable; isolated component tests do not establish full-window visuals.'])
    raw = (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + '\n').encode()
    if write:
        OUTPUT.write_bytes(raw)
    elif not OUTPUT.is_file() or OUTPUT.read_bytes() != raw:
        raise ValueError('Original team portrait source/port drift')
    print('Original team portrait/member ports verified: four component ports, exact source and null-status scope.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
