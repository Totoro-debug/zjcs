"""Restore the original player report view fields and record confirmed local grouping rules."""
import argparse
import json
import re
import sys
from pathlib import Path
from restore_battle_report_dropdown import ROOT, WORLD, RESOURCE, sha, write, guid, metadata

RECEIPT = ROOT / 'assets/reference/battle-hud/report-player-view-source-manifest.json'
PREFAB = RESOURCE + 'FightEndReportPlayerTabView.prefab'
VIEW = WORLD + 'WorldBattleReportPlayerView.cs'
PATHS = [VIEW, 'unity/Assets/Game/Runtime/Official/Combat/FightBattleReportGrouping.cs',
         'unity/Assets/Game/Runtime/Official/Combat/FightBattleReportBuilder.cs',
         'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleReportPlayerRowBinder.cs',
         'unity/Assets/Game/Editor/Combat/OfficialBattleReportPlayerRowAcceptance.cs',
         'scripts/test_battle_report_grouping.ps1']


def build(source_root):
    sys.path.insert(0, str(source_root / 'scripts'))
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from refresh_current_ui_graphics import object_path
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    from UnityPy.classes.PPtr import PPtr
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
    Dumper.add_representer(dict, lambda d, v: d.represent_mapping('tag:yaml.org,2002:map', v.items(), flow_style='fileID' in v))

    def read_prefab(name):
        raw = (ROOT / name).read_bytes().replace(b'\r\n', b'\n')
        receipts = [json.loads(p.read_bytes()) for p in (ROOT / 'assets/reference/battle-hud').glob('report-*-source-manifest.json')]
        assert sha(raw) in {x['sha256'] for d in receipts for x in d.get('outputs', []) if x['path'] == name}, 'Unexpected prefab input: ' + name
        blocks = re.split(r'(?m)^--- ', raw.decode())
        docs = {}
        for index, block in enumerate(blocks[1:], 1):
            match = re.match(r'!u!(\d+) &(-?\d+)\n', block)
            docs[int(match[2])] = index, int(match[1]), next(iter(yaml.safe_load(block[match.end():]).values()))
        return blocks, docs

    blocks, docs = read_prefab(PREFAB)
    transforms = {v['m_GameObject']['fileID']: v for _, k, v in docs.values() if k == 224}
    def hierarchy(go):
        value = docs[go][2]
        parent = transforms[go]['m_Father']['fileID']
        return (hierarchy(docs[parent][2]['m_GameObject']['fileID']) if parent else '') + '/' + value['m_Name']
    native = {hierarchy(go): go for go, (_, k, _) in docs.items() if k == 1}
    objects = list(apk.objects('defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportplayertabview.bundle'))
    local = {}
    for obj in objects:
        if obj.type.name != 'GameObject': continue
        go = native[object_path(obj)[0]]
        old, new = obj.read().m_Component, docs[go][2]['m_Component']
        assert len(old) == len(new)
        local[obj.path_id] = go
        for source, target in zip(old, new):
            target_id = target['component']['fileID']
            assert source.component.type.value == docs[target_id][1]
            local[source.component.path_id] = target_id
    row_path = RESOURCE + 'FightEndReportPlayerTabViewItem.prefab'
    _, row_docs = read_prefab(row_path)
    row_root = next(go for go, (_, kind, value) in row_docs.items()
                    if kind == 1 and value['m_Name'] == 'FightEndReportPlayerTabViewItem')
    def convert(value, owner):
        if isinstance(value, list): return [convert(x, owner) for x in value]
        if not isinstance(value, dict): return value
        if set(value) == {'m_FileID', 'm_PathID'}:
            if not value['m_PathID']: return dict(fileID=0)
            if not value['m_FileID']: return dict(fileID=local[value['m_PathID']])
            target = PPtr(**value, assetsfile=owner.assets_file).deref()
            assert target.type.name == 'GameObject' and target.read().m_Name == 'FightEndReportPlayerTabViewItem'
            return dict(fileID=row_root, guid=guid(ROOT / row_path), type=3)
        return {k: convert(v, owner) for k, v in value.items()}
    obj = next(o for o in objects if o.type.name == 'MonoBehaviour' and o.read(check_read=False).m_Script.read().m_ClassName == 'FightEndReportPlayerTabView')
    fields = {k: convert(v, obj) for k, v in schemas.read(obj).items() if k not in ['m_GameObject', 'm_Enabled', 'm_Script', 'm_Name']}
    assert set(fields) == {'PlayerSlot', 'PrefabObj'}
    for name in PATHS:
        if name.endswith('.cs'): metadata(ROOT / name, 'MonoImporter')
    file_id = local[obj.path_id]
    index, _, value = docs[file_id]
    value['m_Script'] = dict(fileID=11500000, guid=guid(ROOT / VIEW), type=3)
    value.update(fields)
    blocks[index] = '!u!114 &' + str(file_id) + '\n' + yaml.dump({'MonoBehaviour': value}, Dumper=Dumper, allow_unicode=True, sort_keys=False, width=10000)
    result = '--- '.join(blocks).encode()
    write(ROOT / PREFAB, result)
    official = source_root / '.tools/source-packages/official-code'
    names = ['client-source/FightEndReportPlayerTabView.cs', 'common-source/Common/FightReportData.cs',
             'common-source/Common/FightRoleNormalReport.cs', 'common-source/Common/GameCharacterType.cs',
             'common-source/Common/BattleReportDistributor.cs', 'common-source/Common/FightReportComponent.cs']
    record = dict(format=1, manifest=apk.version,
                  official_code={name: sha((official / name).read_bytes()) for name in names},
                  binding=dict(prefab=PREFAB, file_id=str(file_id), original_path_id=str(obj.path_id),
                               object_sha256=sha(obj.get_raw_data()), source=apk.provenance(obj), fields=fields),
                  outputs=[dict(path=PREFAB, sha256=sha(result), bytes=len(result))],
                  confirmed=['Each NPC teammate occupies its own row.', 'Summons of summons belong to the final owner summon category.', 'Pet-created summons inherit the final owner pet report category.'],
                  local_adaptation='Use recorded report MasterId and CharacterType. Player/NPC rows keep roster order. Dead/ended units remain included. Unresolved/cyclic/cross-side ownership is retained explicitly and cannot create a partial team ratio.',
                  pending=['PetEntity and other proxy category rules.', 'Portrait/detail provider and formal report-window entry.'])
    write(RECEIPT, (json.dumps(record, ensure_ascii=False, indent=2) + '\n').encode())
    record_runtime()
    print('Restored original player view:', fields)


def record_runtime():
    data = json.loads(RECEIPT.read_bytes())
    data['confirmed'] = ['Each NPC teammate occupies its own row.', 'Summons of summons belong to the final owner summon category.', 'Pet-created summons inherit the final owner pet report category.']
    data['pending'] = ['PetEntity and other proxy category rules.', 'Portrait/detail provider and formal report-window entry.']
    assert all(sha((ROOT / row['path']).read_bytes().replace(b'\r\n', b'\n')) == row['sha256'] for row in data['outputs'])
    paths = [p for name in PATHS for p in ([name, name + '.meta'] if name.endswith('.cs') else [name])]
    data['runtime'] = {p: sha((ROOT / p).read_bytes()) for p in paths}
    data['generator_sha256'] = sha(Path(__file__).read_bytes())
    write(RECEIPT, (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    parser.add_argument('--record-runtime', action='store_true')
    args = parser.parse_args()
    record_runtime() if args.record_runtime else build(args.source_root)
