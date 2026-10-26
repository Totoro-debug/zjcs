"""Restore the official skill report row fields and original CN number-format methods."""
import argparse
import json
import re
import sys
from pathlib import Path
from restore_battle_report_dropdown import ROOT, WORLD, sha, write, guid, metadata

RECEIPT = ROOT / 'assets/reference/battle-hud/report-skill-row-source-manifest.json'
PREFAB = 'unity/Assets/Game/Resources/Combat/BattleReport/FightEndReportSkillTabViewItem.prefab'
ROW = WORLD + 'WorldBattleReportSkillRow.cs'
FORMAT = WORLD + 'WorldBattleReportNumberFormat.cs'
BINDER = 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleReportSkillRowBinder.cs'


def restore_headers(apk, schemas, yaml, dumper):
    records = json.loads((ROOT / 'assets/reference/battle-hud/report-scroll-source-manifest.json').read_bytes())['bindings']
    from refresh_current_ui_graphics import object_path
    results, bindings = [], []
    for record in records:
        path = ROOT / record['prefab']
        raw = path.read_bytes().replace(b'\r\n', b'\n')
        receipts = [json.loads(p.read_bytes()) for p in (ROOT / 'assets/reference/battle-hud').glob('report-*-source-manifest.json')]
        assert sha(raw) in {x['sha256'] for d in receipts for x in d.get('outputs', []) if x['path'] == record['prefab']}
        blocks = re.split(r'(?m)^--- ', raw.decode())
        docs = {}
        for index, block in enumerate(blocks[1:], 1):
            match = re.match(r'!u!(\d+) &(-?\d+)\n', block)
            docs[int(match[2])] = index, int(match[1]), next(iter(yaml.safe_load(block[match.end():]).values()))
        transforms = {v['m_GameObject']['fileID']: v for _, k, v in docs.values() if k == 224}
        def hierarchy(go):
            value = docs[go][2]
            parent = transforms[go]['m_Father']['fileID']
            repeated = 0
            if parent:
                for child in docs[parent][2]['m_Children']:
                    child_go = docs[child['fileID']][2]['m_GameObject']['fileID']
                    if child_go == go: break
                    repeated += docs[child_go][2]['m_Name'] == value['m_Name']
            return (hierarchy(docs[parent][2]['m_GameObject']['fileID']) if parent else '') + '/' + value['m_Name'] + (f' ({repeated})' if repeated else '')
        native = {hierarchy(go): go for go, (_, kind, _) in docs.items() if kind == 1}
        objects = list(apk.objects('defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_' + path.stem.lower() + '.bundle'))
        original = next(obj for obj in objects if obj.path_id == int(record['original_path_id']))
        assert sha(original.get_raw_data()) == record['object_sha256']
        originals = {obj.path_id: obj for obj in objects}
        def convert(value):
            if isinstance(value, list): return [convert(x) for x in value]
            if not isinstance(value, dict): return value
            if set(value) == {'m_FileID', 'm_PathID'}:
                if value['m_PathID'] == 0: return dict(fileID=0)
                assert value['m_FileID'] == 0
                target = originals[value['m_PathID']]
                assert target.type.name == 'GameObject'
                return dict(fileID=native[object_path(target)[0]])
            return {k: convert(v) for k, v in value.items()}
        data = schemas.read(original)
        state = convert(data['mGMStateSwitcher'])
        selected = {x['rid'] for x in state['mStateConfigs']}
        refs = [convert(x) for x in data['references']['RefIds'] if x['rid'] in selected]
        assert len(refs) == len(selected)
        for reference in refs:
            assert reference['type']['class'] == 'StateGameObjectConfig'
            reference['type'] = dict(reference['type'], **{'class': 'WorldStateGameObjectConfig', 'ns': 'Zjcs.Official.WorldUI', 'asm': 'Zjcs.Official.WorldUI'})
        index, kind, component = docs[int(record['file_id'])]
        assert kind == 114 and component['m_Script']['guid'] == guid(ROOT / WORLD / 'WorldBattleReportSkillScrollSync.cs')
        component['mGMStateSwitcher'] = state
        existing = component.get('references', dict(version=2, RefIds=[]))
        existing['RefIds'] = [x for x in existing['RefIds'] if x['rid'] not in selected] + refs
        component['references'] = existing
        blocks[index] = '!u!114 &' + record['file_id'] + '\n' + yaml.dump({'MonoBehaviour': component}, Dumper=dumper, allow_unicode=True, sort_keys=False, width=10000)
        result = '--- '.join(blocks).encode()
        results.append((path, result))
        bindings.append(dict(prefab=record['prefab'], path=record['path'], fields=dict(mGMStateSwitcher=state, references=dict(version=2, RefIds=refs)),
                             original_path_id=record['original_path_id'], object_sha256=record['object_sha256'], source=apk.provenance(original)))
    return results, bindings


def build(source_root):
    sys.path.insert(0, str(source_root / 'scripts'))
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from refresh_current_ui_graphics import object_path
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    import yaml
    apk = Apk(manifest='854_156383', reuse_current_cache=True)
    schemas = ScriptSchemas()
    schemas.generator = TypeTreeGenerator('2022.3.57f1', generator='AssetsTools')
    editor = Path('D:/Tool/Unity/Editor/Data')
    schemas.generator.load_local_dll_folder(str(editor / 'Managed/UnityEngine'))
    for path in sorted((source_root / '.tools/source-packages/official-code/managed/decoded').glob('*.dll')):
        if not path.stem.startswith('UnityEngine') and path.stem != 'Unity.TextMeshPro':
            schemas.generator.load_dll(path.read_bytes())
    for path in [ROOT / ('unity/Library/ScriptAssemblies/' + name + '.dll') for name in ['UnityEngine.UI', 'Unity.TextMeshPro']] + [
            editor / 'NetStandard/ref/2.1.0/netstandard.dll', editor / 'MonoBleedingEdge/lib/mono/4.5/mscorlib.dll']:
        schemas.generator.load_dll(path.read_bytes())
    class Dumper(yaml.SafeDumper):
        pass
    Dumper.add_representer(dict, lambda dumper, value: dumper.represent_mapping('tag:yaml.org,2002:map', value.items(), flow_style='fileID' in value))
    header_results, header_bindings = restore_headers(apk, schemas, yaml, Dumper)
    path = ROOT / PREFAB
    raw = path.read_bytes().replace(b'\r\n', b'\n')
    receipts = [json.loads(p.read_bytes()) for p in (ROOT / 'assets/reference/battle-hud').glob('report-*-source-manifest.json')]
    assert sha(raw) in {x['sha256'] for d in receipts for x in d.get('outputs', []) if x['path'] == PREFAB}, 'Unexpected skill row input.'
    blocks = re.split(r'(?m)^--- ', raw.decode())
    docs = {}
    for index, block in enumerate(blocks[1:], 1):
        match = re.match(r'!u!(\d+) &(-?\d+)\n', block)
        docs[int(match[2])] = index, int(match[1]), next(iter(yaml.safe_load(block[match.end():]).values()))
    transforms = {v['m_GameObject']['fileID']: v for _, k, v in docs.values() if k == 224}
    def hierarchy(go):
        value = docs[go][2]
        parent = transforms[go]['m_Father']['fileID']
        repeated = 0
        if parent:
            for child in docs[parent][2]['m_Children']:
                child_go = docs[child['fileID']][2]['m_GameObject']['fileID']
                if child_go == go: break
                repeated += docs[child_go][2]['m_Name'] == value['m_Name']
        return (hierarchy(docs[parent][2]['m_GameObject']['fileID']) if parent else '') + '/' + value['m_Name'] + (f' ({repeated})' if repeated else '')
    native = {hierarchy(go): go for go, (_, k, _) in docs.items() if k == 1}
    objects = list(apk.objects('defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportskilltabviewitem.bundle'))
    local = {}
    for obj in objects:
        if obj.type.name != 'GameObject': continue
        go = native[object_path(obj)[0]]
        old, new = obj.read().m_Component, docs[go][2]['m_Component']
        assert len(old) == len(new), 'Original skill row component order changed.'
        local[obj.path_id] = go
        for source, target in zip(old, new):
            target_id = target['component']['fileID']
            assert source.component.type.value == docs[target_id][1], 'Original skill row component type changed.'
            local[source.component.path_id] = target_id
    def convert(value):
        if isinstance(value, list): return [convert(x) for x in value]
        if not isinstance(value, dict): return value
        if set(value) == {'m_FileID', 'm_PathID'}:
            if value['m_PathID'] == 0: return dict(fileID=0)
            assert value['m_FileID'] == 0, 'Unexpected external skill row reference.'
            return dict(fileID=local[value['m_PathID']])
        return {k: convert(v) for k, v in value.items()}
    obj = next(o for o in objects if o.type.name == 'MonoBehaviour' and o.read(check_read=False).m_Script.read().m_ClassName == 'FightEndReportSkillTabViewItem')
    fields = {k: convert(v) for k, v in schemas.read(obj).items() if k not in ['m_GameObject', 'm_Enabled', 'm_Script', 'm_Name']}
    for reference in fields['references']['RefIds']:
        assert reference['type']['class'] == 'StateGameObjectConfig'
        reference['type'] = dict(reference['type'], **{'class': 'WorldStateGameObjectConfig', 'ns': 'Zjcs.Official.WorldUI', 'asm': 'Zjcs.Official.WorldUI'})
    metadata(ROOT / ROW, 'MonoImporter')
    file_id = local[obj.path_id]
    index, _, value = docs[file_id]
    value['m_Script'] = dict(fileID=11500000, guid=guid(ROOT / ROW), type=3)
    value.update(fields)
    blocks[index] = '!u!114 &' + str(file_id) + '\n' + yaml.dump({'MonoBehaviour': value}, Dumper=Dumper, allow_unicode=True, sort_keys=False, width=10000)
    references = {row['rid']: row for row in fields['references']['RefIds']}
    checks = []
    def check(value, key):
        if isinstance(value, dict):
            if 'fileID' in value: checks.append(dict(path=key, kind='reference', reference=value))
            elif set(value) == {'rid'}:
                reference = references[value['rid']]
                t = reference['type']
                checks.append(dict(path=key, kind='managed', text=t['asm'] + ' ' + t['ns'] + '.' + t['class']))
                check(reference['data'], key)
            else:
                for k, v in value.items(): check(v, key + '.' + k)
        elif isinstance(value, list):
            checks.append(dict(path=key, kind='array', count=len(value)))
            for i, v in enumerate(value): check(v, key + '.Array.data[' + str(i) + ']')
        else: checks.append(dict(path=key, **(dict(kind='string', text=value) if isinstance(value, str) else dict(kind='number', number=value))))
    for key, value in fields.items():
        if key != 'references': check(value, key)
    result = '--- '.join(blocks).encode()
    # Copy the original numeric methods verbatim, adapting only namespace/class and supplying CN constants.
    official = source_root / '.tools/source-packages/official-code'
    helper = official / 'framework-firstpass-source/Framework/NumberFormatHelper.cs'
    source = helper.read_text(encoding='utf8')
    methods = []
    for method in ['Divide', 'TruncateBySignificantDigits', 'ApplyDecimalSeparator', 'ComposeFormattedNumber', 'FormatStandardSignificantPercent']:
        start = re.search(r'\tpublic static[^\n]*\b' + method + r'\(', source).start()
        opening = source.index('{', start)
        depth, end = 1, opening + 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}')
            end += 1
        methods.append(source[start:end])
    wrapper = '''using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Official.WorldUI
{
    // Original NumberFormatHelper methods and CultureNumberFormatterBase/CN compact/percent path.
    public static class WorldBattleReportNumberFormat
    {
        private static readonly string[] Symbols = { "", "万", "亿", "兆", "京" };
        public static string Compact(long value)
        {
            var (number, index) = Divide(value, 10000, Symbols.Length);
            return ComposeFormattedNumber(TruncateBySignificantDigits(number, 3), index, Symbols, '.');
        }
        public static string Significant(double value) => value.ToString("G3", CultureInfo.InvariantCulture);
        public static string Percent(double value) => FormatStandardSignificantPercent(value, "%", '.', 3);
'''
    write(ROOT / FORMAT, (wrapper + '\n\n'.join(methods) + '\n    }\n}\n').encode())
    for name in [FORMAT, BINDER, 'unity/Assets/Game/Editor/Combat/OfficialBattleReportSkillRowAcceptance.cs']:
        if (ROOT / name).exists(): metadata(ROOT / name, 'MonoImporter')
    write(path, result)
    for header_path, header_result in header_results: write(header_path, header_result)
    names = ['client-source/FightEndReportSkillTabViewItem.cs', 'client-source/FightEndReportSkillTabView.cs', 'common-source/Common/PlayerFightSkillReport.cs',
             'framework-firstpass-source/Framework/NumberFormatHelper.cs', 'framework-firstpass-source/Framework/CultureNumberFormatterBase.cs',
             'framework-firstpass-source/Framework/NumberCultureFormatter_CN.cs']
    data = dict(format=1, manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()),
                official_code={name: sha((official / name).read_bytes()) for name in names},
                binding=dict(prefab=PREFAB, path='/FightEndReportSkillTabViewItem', file_id=str(file_id), original_path_id=str(obj.path_id),
                             object_sha256=sha(obj.get_raw_data()), fields=fields, checks=checks, source=apk.provenance(obj)),
                header_bindings=header_bindings,
                outputs=[dict(path=PREFAB, sha256=sha(result), bytes=len(result))] + [dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(v), bytes=len(v)) for p, v in header_results],
                copied_number_methods=['Divide', 'TruncateBySignificantDigits', 'ApplyDecimalSeparator', 'ComposeFormattedNumber', 'FormatStandardSignificantPercent'],
                adaptations=['Original numeric Refresh order, fields, local references, GM state and zero colors; icon/detail providers remain pending.',
                             'User-confirmed average integer display uses Mathf.RoundToInt; raw checkpoint ratios remain unchanged.',
                             'Missing counters/history are blank and are never populated using unconfirmed counter aliases or estimated zeros.'],
                user_confirmed=['DamageCount equals confirmed damage-hit count, including immunity/full shielding and excluding dodge.',
                                'CureCount counts landed cure-target hits, including full HP with zero effective healing.',
                                'Each target accepted status application counts once; stack/refresh counts once, multiple stacks still once, discard/probability failure zero. Debuff includes Debuff and AbnormalDebuff.'],
                pending=['Original item icon manager, report snapshots/detail popup, formal report window/provider and full passive root-source mapping.'])
    write(RECEIPT, (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode())
    record_runtime()
    print('Restored original skill row:', len(checks), 'fields/references.')


def record_runtime():
    data = json.loads(RECEIPT.read_bytes())
    assert all(sha((ROOT / row['path']).read_bytes().replace(b'\r\n', b'\n')) == row['sha256'] for row in data['outputs'])
    names = [ROW, FORMAT, BINDER, 'unity/Assets/Game/Editor/Combat/OfficialBattleReportSkillRowAcceptance.cs']
    paths = [p for n in names for p in [n, n + '.meta']] + ['unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs',
            WORLD + 'WorldBattleReportSkillScrollSync.cs']
    data['runtime'] = {p: sha((ROOT / p).read_bytes()) for p in paths if (ROOT / p).exists()}
    data['generator_sha256'] = sha(Path(__file__).read_bytes())
    write(RECEIPT, (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    parser.add_argument('--record-runtime', action='store_true')
    args = parser.parse_args()
    record_runtime() if args.record_runtime else build(args.source_root)
