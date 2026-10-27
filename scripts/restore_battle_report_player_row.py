"""Restore the original player-report row fields, references and self-highlight state."""
import argparse
import json
import re
import sys
from pathlib import Path
from restore_battle_report_dropdown import ROOT, WORLD, sha, write, guid, metadata

RECEIPT = ROOT / 'assets/reference/battle-hud/report-player-row-source-manifest.json'
PREFAB = 'unity/Assets/Game/Resources/Combat/BattleReport/FightEndReportPlayerTabViewItem.prefab'
ROW = WORLD + 'WorldBattleReportPlayerRow.cs'
BINDER = 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleReportPlayerRowBinder.cs'
ACCEPTANCE = 'unity/Assets/Game/Editor/Combat/OfficialBattleReportPlayerRowAcceptance.cs'


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
    path = ROOT / PREFAB
    raw = path.read_bytes().replace(b'\r\n', b'\n')
    receipts = [json.loads(p.read_bytes()) for p in (ROOT / 'assets/reference/battle-hud').glob('report-*-source-manifest.json')]
    assert sha(raw) in {x['sha256'] for d in receipts for x in d.get('outputs', []) if x['path'] == PREFAB}, 'Unexpected player row input.'
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
    objects = list(apk.objects('defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportplayertabviewitem.bundle'))
    local = {}
    for obj in objects:
        if obj.type.name != 'GameObject': continue
        go = native[object_path(obj)[0]]
        old, new = obj.read().m_Component, docs[go][2]['m_Component']
        assert len(old) == len(new), 'Original player row component order changed.'
        local[obj.path_id] = go
        for source, target in zip(old, new):
            target_id = target['component']['fileID']
            assert source.component.type.value == docs[target_id][1], 'Original player row component type changed.'
            local[source.component.path_id] = target_id
    def convert(value):
        if isinstance(value, list): return [convert(x) for x in value]
        if not isinstance(value, dict): return value
        if set(value) == {'m_FileID', 'm_PathID'}:
            if value['m_PathID'] == 0: return dict(fileID=0)
            assert value['m_FileID'] == 0, 'Unexpected external player row reference.'
            return dict(fileID=local[value['m_PathID']])
        return {k: convert(v) for k, v in value.items()}
    obj = next(o for o in objects if o.type.name == 'MonoBehaviour' and o.read(check_read=False).m_Script.read().m_ClassName == 'FightEndReportPlayerTabViewItem')
    fields = {k: convert(v) for k, v in schemas.read(obj).items() if k not in ['m_GameObject', 'm_Enabled', 'm_Script', 'm_Name']}
    for reference in fields['references']['RefIds']:
        original_class = reference['type']['class']
        assert original_class in ['StateGameObjectConfig', 'StateUIColorConfig'], original_class
        reference['type'] = dict(reference['type'], **{'class': 'World' + original_class, 'ns': 'Zjcs.Official.WorldUI', 'asm': 'Zjcs.Official.WorldUI'})
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
    row_fields, row_checks = fields, checks
    graphics = json.loads((ROOT / 'assets/reference/battle-hud/report-graphics-source-manifest.json').read_bytes())
    sliced = ROOT / WORLD / 'SlicedFilledImage.cs'
    fill_bindings, reused = [], {}
    for image in objects:
        if image.type.name != 'MonoBehaviour': continue
        mb = image.read(check_read=False)
        original_path = object_path(mb.m_GameObject.deref())[0]
        if not original_path.startswith('/FightEndReportPlayerTabViewItem/BattleData/') or mb.m_Script.read().m_ClassName != 'SlicedFilledImage': continue
        data = schemas.read(image)
        def fill_convert(value):
            if isinstance(value, list): return [fill_convert(x) for x in value]
            if not isinstance(value, dict): return value
            if set(value) == {'m_FileID', 'm_PathID'}:
                if not value['m_PathID']: return dict(fileID=0)
                from UnityPy.classes.PPtr import PPtr
                target = PPtr(**value, assetsfile=image.assets_file).deref()
                assert target.type.name == 'Sprite', target.type.name
                digest = sha(target.get_raw_data())
                assert any(x.get('object_sha256') == digest and x['type'] == 'Sprite' for x in graphics['inputs'])
                name = 'unity/Assets/Official/Combat/BattleReport/Graphics/Sprite_' + digest[:16] + '.asset'
                native = ROOT / name
                assert any(x['path'] == name and x['sha256'] == sha(native.read_bytes()) for x in graphics['native_assets'])
                reused[name] = dict(sha256=sha(native.read_bytes()), meta_sha256=sha(native.with_name(native.name + '.meta').read_bytes()),
                                    original_sha256=digest, source=apk.provenance(target))
                return dict(fileID=21300000, guid=guid(native), type=2)
            return {k: fill_convert(v) for k, v in value.items()}
        fill_fields = {k: fill_convert(v) for k, v in data.items() if k not in ['m_GameObject', 'm_Enabled', 'm_Script', 'm_Name']}
        fill_id = local[image.path_id]
        fill_index, _, component = docs[fill_id]
        component['m_Script'] = dict(fileID=11500000, guid=guid(sliced), type=3)
        component.update(fill_fields)
        blocks[fill_index] = '!u!114 &' + str(fill_id) + '\n' + yaml.dump({'MonoBehaviour': component}, Dumper=Dumper, allow_unicode=True, sort_keys=False, width=10000)
        checks = []
        for key, value in fill_fields.items(): check(value, key)
        fill_bindings.append(dict(prefab=PREFAB, path=original_path, file_id=str(fill_id), fields=fill_fields, checks=checks,
                                  original_path_id=str(image.path_id), object_sha256=sha(image.get_raw_data()), source=apk.provenance(image)))
    assert len(fill_bindings) == 4
    fields, checks = row_fields, row_checks
    result = '--- '.join(blocks).encode()
    write(path, result)
    for name in [BINDER, ACCEPTANCE]:
        metadata(ROOT / name, 'MonoImporter')
    official = source_root / '.tools/source-packages/official-code'
    names = ['client-source/FightEndReportPlayerTabViewItem.cs', 'client-source/FightEndReportPlayerTabView.cs',
             'client-source/FightReportUIUtils.cs', 'client-source/PlayerIcon.cs', 'common-source/Common/FightReportData.cs',
             'common-source/Common/FightRoleNormalReport.cs']
    data = dict(format=1, manifest=apk.version,
                official_code={name: sha((official / name).read_bytes()) for name in names},
                binding=dict(prefab=PREFAB, path='/FightEndReportPlayerTabViewItem', file_id=str(file_id), original_path_id=str(obj.path_id),
                             object_sha256=sha(obj.get_raw_data()), fields=fields, checks=checks, source=apk.provenance(obj)),
                fills=fill_bindings, reused=reused,
                sliced_code=dict(original_sha256=sha((official / 'framework-firstpass-source/SlicedFilledImage.cs').read_bytes()),
                                 path=sliced.relative_to(ROOT).as_posix(), canonical_lf_sha256=sha(sliced.read_bytes().replace(b'\r\n', b'\n'))),
                outputs=[dict(path=PREFAB, sha256=sha(result), bytes=len(result))],
                adaptations=['Original player-row refresh order, self state, four values, three-category sums and four team-ratio sliders.',
                             'Unknown historical values remain blank; an unknown team denominator clears its slider.',
                             'The original PlayerIcon reference is retained as a Component adapter endpoint; its portrait/detail implementation remains pending.'],
                pending=['Production player/pet/summon report grouping, player portrait/detail provider, formal report-window entry.'])
    write(RECEIPT, (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode())
    record_runtime()
    print('Restored original player row:', len(checks), 'fields/references.')


def record_runtime():
    data = json.loads(RECEIPT.read_bytes())
    assert all(sha((ROOT / row['path']).read_bytes().replace(b'\r\n', b'\n')) == row['sha256'] for row in data['outputs'])
    paths = [p for n in [ROW, BINDER, ACCEPTANCE] for p in [n, n + '.meta']]
    paths += ['unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs']
    data['runtime'] = {p: sha((ROOT / p).read_bytes()) for p in paths}
    data['generator_sha256'] = sha(Path(__file__).read_bytes())
    write(RECEIPT, (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    parser.add_argument('--record-runtime', action='store_true')
    args = parser.parse_args()
    record_runtime() if args.record_runtime else build(args.source_root)
