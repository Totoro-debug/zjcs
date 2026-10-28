"""Migrate the three original team target widgets without changing their UI order."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NAMES = ('TeamPanelTarget', 'TeamPanelTargetItem', 'TeamPanelTargetSubItem')
OUTPUT = ROOT / 'assets/reference/official-map/map12-team-targets-source-manifest.json'
PORTS = ROOT / 'unity/Assets/Game/Runtime/Official/TeamUI'


def converted(name, source):
    source = re.sub(r'^\s*//IL_.*\n', '', source, flags=re.M)
    source = source.replace('using Common;', 'using Zjcs.Official.ProfessionUI;\n'
        'using Zjcs.Official.WorldUI;\nusing ITeamTarget = Zjcs.Official.TeamUI.TeamTargetData;')
    source = source.replace('MultiLanguageText', 'WorldMultiLanguageText')
    source = source.replace('StateSwitcher<', 'WorldStateSwitcher<')
    source = source.replace('public class ' + name, 'namespace Zjcs.Official.TeamUI\n{\npublic class ' + name)
    injection = ('\n\tprivate ITeamTargetServices services;\n'
                 '\tpublic void Configure(ITeamTargetServices value) { services = value; }\n')
    marker = 'public class ' + name
    at = source.index('\n{', source.index(marker)) + 2
    source = source[:at] + injection + source[at:]
    if name == 'TeamPanelTarget':
        source = source.replace('BroadcastManager.ShowErrText(1252, null, false);', 'services.ShowError(1252, false);')
        source = source.replace('\t\t\tTeamTargetInfoParser parser = Parsers.Instance.TeamTargetInfoParser;\n', '')
        source = source.replace('parser.Get(a).Sort - parser.Get(b).Sort', 'services.CategorySort(a) - services.CategorySort(b)')
        source = source.replace('TeamTargetDelegateTaskCompare.Default.Compare', 'services.CompareDelegateTargets')
        source = source.replace('\t\t\tteamPanelTargetItem.Init(', '\t\t\tteamPanelTargetItem.Configure(services);\n\t\t\tteamPanelTargetItem.Init(')
    elif name == 'TeamPanelTargetItem':
        source = source.replace('Parsers.Instance.TeamTargetInfoParser.Get(targetCatalog).MultiTarget', 'services.HasMultipleTargets(targetCatalog)')
        source = source.replace('LanguageUtils.GetEnumName(mTeamTargetCatalog)', 'services.EnumName(mTeamTargetCatalog)')
        source = source.replace('\t\t\tteamPanelTargetSubItem.Init(', '\t\t\tteamPanelTargetSubItem.Configure(services);\n\t\t\tteamPanelTargetSubItem.Init(')
    return ('// Mechanically adapted original client component; see map12 team targets receipt.\n' + source.rstrip() + '\n}\n').encode()


def build(write=False):
    selected = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    if selected != '871_156598':
        raise ValueError('Recheck original target source selection')
    base = ROOT / '.tools/source-packages/current' / selected
    code = base / 'official-code'
    manifest = json.loads((base / 'code-manifest.json').read_bytes())

    def record(path):
        return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())

    assemblies = []
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        row = record(code / 'managed/decoded' / name)
        if row['sha256'] != manifest['assemblies'][name]['decoded_sha256']:
            raise ValueError('Official assembly drift: ' + name)
        assemblies.append(row)
    outputs = []
    for name in NAMES:
        path = PORTS / (name + '.cs')
        raw = converted(name, (code / 'client-source' / (name + '.cs')).read_text(encoding='utf-8'))
        meta = path.with_suffix('.cs.meta')
        meta_raw = ('fileFormatVersion: 2\nguid: ' + uuid.uuid5(uuid.NAMESPACE_URL,
            'zjcs:official:map12:team:' + name).hex + '\n').encode()
        for output, contents in ((path, raw), (meta, meta_raw)):
            if write:
                output.write_bytes(contents)
            elif not output.is_file() or output.read_bytes() != contents:
                raise ValueError('Original team target port drift: ' + output.name)
            outputs.append(record(output))
    for name in ('TeamTargetServices', 'TeamSteleTargetData'):
        path = PORTS / (name + '.cs')
        meta = path.with_suffix('.cs.meta')
        raw = ('fileFormatVersion: 2\nguid: ' + uuid.uuid5(uuid.NAMESPACE_URL,
            'zjcs:official:map12:team:' + name).hex + '\n').encode()
        if write:
            meta.write_bytes(raw)
        elif not meta.is_file() or meta.read_bytes() != raw:
            raise ValueError('Team target metadata drift')
        outputs.extend((record(path), record(meta)))
    original = [record(code / 'client-source' / (name + '.cs')) for name in (*NAMES, 'TeamTargetDelegateTaskCompare')]
    original.extend(record(code / 'common-source/Common' / (name + '.cs')) for name in
                    ('ITeamTarget', 'TeamTargetBattleStele', 'LanguageUtils'))
    checks = [record(ROOT / 'unity/Assets/Game/Editor' / name) for name in
              ('OfficialMap12PlayModeAcceptance.TeamTargets.cs', 'OfficialMap12PlayModeAcceptance.TeamTargets.cs.meta',
               'OfficialMap12PlayModeAcceptance.TeamSpriteImport.cs', 'OfficialMap12PlayModeAcceptance.TeamSpriteImport.cs.meta')]
    value = dict(format=1, manifest=selected, mapClassId=12, assemblies=assemblies,
        originalCode=original, inputs=[record(base / 'code-manifest.json')], outputs=outputs,
        validation=checks,
        adapters=[record(Path(__file__).resolve())],
        operation='Mechanically migrate the three original target UI components. Reuse original UIBase, '
        'object pools, localized text and state switchers. Bind parser/localization/error boundaries through '
        'ITeamTargetServices. Preserve dictionary insertion display order despite the separately sorted '
        'Catalogs list, delegate-only comparator, folding, pooled item lifecycle, selected-key equality '
        'and confirmation callback clearing. Preserve BattleStele stage/group value identity and inherited '
        'catalog-only UI key.',
        limitations=['Real native prefab bindings, parser/localization services and formal map host remain pending.',
                     'TeamTargetData is the consumed Common.ITeamTarget projection; it does not implement transport, '
                     'server checks, or unrelated gameplay target types. The host supplies the original delegate '
                     'comparator when that catalog is exposed.'])
    raw = (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode()
    if write:
        OUTPUT.write_bytes(raw)
    elif not OUTPUT.is_file() or OUTPUT.read_bytes() != raw:
        raise ValueError('Original team target source receipt drift')
    print('Original team target widgets verified: three components and consumed target boundaries.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
