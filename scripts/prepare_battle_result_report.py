"""Restore native result report buttons and the report's original actor identity branches."""
from pathlib import Path
import hashlib
import json
import uuid

from UnityPy.classes import PPtr
from official_apk import Apk
from current_component_schemas import CurrentSchemas
from prepare_dungeon_ui_bindings import relative_transform_path
from prepare_dungeon_matching_panels import file_record, native_hierarchy, state_records
from prepare_shared_battle_report import verify_native_layout
from restore_battle_report_tabs import write

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'unity/Assets/Game/Resources/Combat/BattleReport'


def build():
    apk = Apk.current()
    schemas = CurrentSchemas()
    records = []
    outputs = []

    def prefab(bundle, name, native):
        original = next(pointer.deref() for key, pointer in apk.container(bundle)
                        if key.endswith('/' + name + '.prefab'))
        nodes = list(relative_transform_path(original.read()))
        paths = {'/'.join(map(str, path)): list(names) for _, path, names, _ in nodes}
        if paths != native_hierarchy(native):
            raise ValueError('Original result/report hierarchy differs: ' + name)
        layout = verify_native_layout(native, nodes)
        by_id = {go.object_reader.path_id: (path, names) for go, path, names, _ in nodes}
        record = dict(name=name, source=apk.provenance(original), native=file_record(native),
                      hierarchy_nodes=len(nodes), verified_layout_fields=layout, components=[])
        records.append(record)

        def target(owner, pointer):
            if not pointer['m_PathID']:
                return None
            obj = PPtr(**pointer, assetsfile=owner.assets_file).deref()
            identity = obj.path_id if obj.type.name == 'GameObject' else (
                obj.read().m_GameObject.path_id if obj.type.name in ('Transform', 'RectTransform')
                else schemas.read(obj)['m_GameObject']['m_PathID'])
            return '/'.join(map(str, by_id[identity][0]))

        def components(names):
            for go, path, _, _ in nodes:
                for pair in go.m_Component:
                    obj = pair.component.deref()
                    if obj.type.name != 'MonoBehaviour':
                        continue
                    cls = obj.read(check_read=False).m_Script.read().m_ClassName
                    if cls not in names:
                        continue
                    fields = schemas.read(obj)
                    record['components'].append(dict(type=cls, source=apk.provenance(obj), fields=fields,
                        payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest()))
                    yield obj, cls, fields, '/'.join(map(str, path))
        return components, target, by_id

    def output(name, data):
        path = OUT / (name + '.json')
        write(path, (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode())
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/battle-result-report/' + name).hex
        write(Path(str(path) + '.meta'), ('fileFormatVersion: 2\nguid: ' + guid +
            '\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode())
        outputs.extend((file_record(path), file_record(Path(str(path) + '.meta'))))

    result_windows = []
    for name in ('GameEndPanelActivityDomainBoss', 'GameEndPanelActivityFunnyBoss'):
        bundle = 'defaultpackage_assets_prefabs_ui_fight_' + name.lower() + '.bundle'
        components, target, _ = prefab(bundle, name,
            ROOT / ('unity/Assets/Game/Resources/DailyChallenges/' + name + '.prefab'))
        base, = [row for row in components(('GameEndPanelBase',))]
        obj, _, fields, _ = base
        result_windows.append(dict(resource='DailyChallenges/' + name, **{
            key: target(obj, fields[field]) for key, field in (
                ('report', 'mSkillItemReportBtn'), ('share', 'mGamePlaybackShareBtn'),
                ('save', 'mGameSaveRecordBtn'), ('thumbsUp', 'mThumbUpReportSwitchBtn'))}))
    icon_receipt = ROOT / 'assets/reference/partner-npc-icons-source-manifest.json'
    icons = json.loads(icon_receipt.read_text(encoding='utf-8'))
    if icons['manifest'] != apk.version:
        raise ValueError('Report NPC/profession icon version differs')
    professions = [dict(profession=consumer['profession'], address=row['address'])
        for row in icons['sprites'] for consumer in row['consumers']
        if consumer.get('field') == 'Icon1']
    output('result-report-bindings', dict(windows=result_windows, professionIcons=professions))

    name = 'FightEndReportPlayerTabViewItem'
    components, target, by_id = prefab(
        'defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_' + name.lower() + '.bundle',
        name, OUT / (name + '.prefab'))
    rows = list(components(('PlayerInfoUI', 'HeadCharacterPortrait')))
    info, = [row for row in rows if row[1] == 'PlayerInfoUI']
    head, = [row for row in rows if row[1] == 'HeadCharacterPortrait']
    obj, _, fields, prefix = info
    if fields['NpcAppearanceSlot']['m_PathID'] or fields['PlayerFigure']['m_PathID']:
        raise ValueError('The report requires an additional original NPC appearance binding')
    def relative(path):
        if path == prefix: return ''
        if path is None or not path.startswith(prefix + '/'):
            raise ValueError('Original report identity reference leaves its root: ' + str(path))
        return path[len(prefix) + 1:]
    def states(field):
        return [dict(state=row['state'], objects=[relative(path) for path in row['objects']])
                for row in state_records(fields, field, by_id)]
    data = {key: relative(target(obj, fields[field])) for key, field in (
        ('name', 'NameText'), ('professionGo', 'ProfessionIconGo'), ('profession', 'ProfessionIcon'),
        ('npcIcon', 'PlayerIcon'), ('collection', 'CollectionImg'))}
    head_obj, _, head_fields, _ = head
    pointer = head_fields['mRequestor']['mRawImage']
    raw = PPtr(**pointer, assetsfile=head_obj.assets_file).deref()
    raw_fields = schemas.read(raw)
    records[-1]['components'].append(dict(type='RawImage', source=apk.provenance(raw), fields=raw_fields,
        payload_sha256=hashlib.sha256(raw.get_raw_data()).hexdigest()))
    uv = raw_fields['m_UVRect']
    data.update(portrait=relative(target(head_obj, pointer)),
        portraitUv=[uv[key] for key in ('x', 'y', 'width', 'height')],
        portraitColor=raw_fields['m_Color'], portraitRaycastTarget=bool(raw_fields['m_RaycastTarget']),
        actorStates=states('mStateSwitcher'), photographedStates=states('mIsMyselfSwitcher'))
    output('player-identity-bindings', data)

    client = ROOT / '.tools/source-packages/current' / apk.version / 'official-code/client-source'
    runtime = [
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleResultReport.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleReportIdentityView.cs',
        'unity/Assets/Game/Runtime/Presentation/Character/CharacterSkillPanel.BattleReport.cs',
        'unity/Assets/Game/Runtime/Presentation/Character/CharacterSkillPanel.DamageTestResult.cs',
        'unity/Assets/Game/Runtime/Presentation/Character/CharacterSkillPanel.Auxiliary.cs',
        'unity/Assets/Game/Runtime/Presentation/Character/CharacterSkillPanel.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.BattleReports.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.DailyChallenges.cs']
    receipt = dict(manifest=apk.version, apk_manifest=apk.source.apk_version,
        apk_sha256=apk.source.apk_sha256, native=records, outputs=outputs,
        official_code=[file_record(client / (name + '.cs')) for name in (
            'GameEndPanelBase', 'GameEndPanelActivityDomainBoss', 'GameEndPanelActivityFunnyBoss',
            'PlayerIcon', 'PlayerIconOther', 'PlayerInfoUI', 'HeadCharacterPortrait')],
        profession_parser=file_record(client.parent / 'common-source/Common/ProfessionBaseInfoParser.cs'),
        retained_inputs=[file_record(ROOT / path) for path in (
            'assets/reference/battle-hud/report-panels-source-manifest.json',
            'assets/reference/partner-npc-icons-source-manifest.json',
            'unity/Assets/Official/Skills/skill-ui.json',
            'unity/Assets/Official/Partners/partner-rules.json')],
        runtime=[file_record(ROOT / path) for path in runtime],
        generator=file_record(Path(__file__).resolve()))
    receipt_path = ROOT / 'assets/reference/battle-hud/result-report-source-manifest.json'
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Restored native result report controls and player/NPC identity bindings.', flush=True)


if __name__ == '__main__':
    build()
