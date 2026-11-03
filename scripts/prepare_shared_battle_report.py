"""Bind the selected official report window to the retained, verified native hierarchy."""
from pathlib import Path
import hashlib
import json
import math
import re
import uuid

from current_component_schemas import CurrentSchemas
from official_apk import Apk
from prepare_dungeon_matching_panels import file_record, native_hierarchy, state_records
from prepare_dungeon_ui_bindings import relative_transform_path
from restore_battle_report_tabs import write
from UnityPy.classes import PPtr

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'unity/Assets/Game/Resources/Combat/BattleReport/report-window-bindings.json'
RECEIPT = ROOT / 'assets/reference/battle-hud/report-window-source-manifest.json'


def verify_native_layout(native, nodes):
    blocks = {int(identity): (int(kind), body) for kind, identity, body in re.findall(
        r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=\n--- !u!|\Z)', native.read_text(encoding='utf-8'), re.S)}
    transforms = {identity: body for identity, (kind, body) in blocks.items() if kind in (4, 224)}
    native_nodes = {}
    def visit(identity, path=()):
        body = transforms[identity]
        native_nodes[path] = body
        children = re.search(r'  m_Children:(.*?)(?=\n  \w)', body, re.S)[1]
        for index, child in enumerate(re.findall(r'fileID: (-?\d+)', children)):
            visit(int(child), path + (index,))
    visit(next(identity for identity, body in transforms.items() if 'm_Father: {fileID: 0}' in body))
    count = 0
    for _, path, names, transform in nodes:
        for field in ('m_LocalPosition', 'm_LocalRotation', 'm_LocalScale', 'm_AnchorMin', 'm_AnchorMax',
                      'm_AnchoredPosition', 'm_SizeDelta', 'm_Pivot'):
            if not hasattr(transform, field): continue
            raw = re.search(r'  ' + field + r': \{([^}]+)\}', native_nodes[path])
            if raw is None: raise ValueError('Missing native transform field: ' + '/'.join(names) + ':' + field)
            value = getattr(transform, field)
            for axis, number in re.findall(r'(\w+): ([^,}]+)', raw[1]):
                if not math.isclose(float(number), float(getattr(value, axis)), rel_tol=1e-6, abs_tol=1e-5):
                    raise ValueError('Report layout differs: ' + '/'.join(names) + ':' + field + '.' + axis)
            count += 1
    return count


def build():
    apk = Apk.current()
    schemas = CurrentSchemas()
    name = 'FightEndReportPanel'
    bundle = 'defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendreportpanel.bundle'
    prefab = next(pointer.deref() for key, pointer in apk.container(bundle)
                  if key.endswith('/' + name + '.prefab'))
    nodes = list(relative_transform_path(prefab.read()))
    paths = {'/'.join(map(str, path)): list(names) for _, path, names, _ in nodes}
    native = ROOT / ('unity/Assets/Game/Resources/Combat/BattleReport/' + name + '.prefab')
    if paths != native_hierarchy(native):
        raise ValueError('Report native hierarchy differs from the selected official source')
    layout_fields = verify_native_layout(native, nodes)
    by_id = {go.object_reader.path_id: (path, names) for go, path, names, _ in nodes}
    sources = []
    popup = None
    result = dict(window=None, player=None, skill=None, panelTabs=[], skillTabs=[])

    def record(obj, fields):
        sources.append(dict(source=apk.provenance(obj),
            payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(), fields=fields))

    def target(owner, pointer):
        obj = PPtr(**pointer, assetsfile=owner.assets_file).deref()
        if obj.type.name == 'GameObject':
            identity = obj.path_id
        elif obj.type.name in ('Transform', 'RectTransform'):
            identity = obj.read().m_GameObject.path_id
        else:
            identity = schemas.read(obj)['m_GameObject']['m_PathID']
        return '/'.join(map(str, by_id[identity][0]))

    for go, path, _, _ in nodes:
        for pair in go.m_Component:
            obj = pair.component.deref()
            if obj.type.name != 'MonoBehaviour':
                continue
            cls = obj.read(check_read=False).m_Script.read().m_ClassName
            if cls not in ('FightEndReportPanel', 'FightEndReportPlayerTabView',
                           'FightEndReportSkillTabView', 'FightEndReportPanelTabItem',
                           'FightEndReportSkillTabItem', 'PopupWindow'):
                continue
            fields = schemas.read(obj)
            record(obj, fields)
            row = dict(path='/'.join(map(str, path)))
            if cls == 'PopupWindow':
                if path: continue
                if any(fields[key]['m_PathID'] for key in ('mShowClip', 'mHideClip', 'mLoopClip')):
                    raise ValueError('Report popup animation bindings require migration')
                popup = dict(maskType=fields['mMaskType'], closeByMask=bool(fields['mCloseByClickMask']))
            elif cls == 'FightEndReportPanel':
                row.update({key: target(obj, fields[field]) for key, field in (
                    ('tabVisual', 'TabVisualObj'), ('tabs', 'TabGroup'),
                    ('close', 'CloseBtn'), ('round', 'RoundCountText'),
                    ('player', 'PlayerTabView'), ('skill', 'SkillTabView'))})
                row['states'] = state_records(fields, 'TabTypeSwitcher', by_id)
                result['window'] = row
            elif cls == 'FightEndReportPlayerTabView':
                row['slot'] = target(obj, fields['PlayerSlot'])
                result['player'] = row
            elif cls == 'FightEndReportSkillTabView':
                row.update({key: target(obj, fields[field]) for key, field in (
                    ('titleScroll', 'mTitleScrollRect'), ('itemScroll', 'mItemScrollRect'),
                    ('tabGroupGo', 'mTabGroupGO'), ('tabs', 'mTabGroup'))})
                row['gmStates'] = state_records(fields, 'mGMStateSwitcher', by_id)
                if fields['mShowEquipTermReportSwitcher']['mStateConfigs']:
                    row['equipmentStates'] = state_records(fields, 'mShowEquipTermReportSwitcher', by_id)
                else:
                    row['equipmentStates'] = []
                result['skill'] = row
            else:
                row['type'] = fields['mTabType']
                result['panelTabs' if cls == 'FightEndReportPanelTabItem' else 'skillTabs'].append(row)
    if any(result[key] is None for key in ('window', 'player', 'skill')):
        raise ValueError('The original report window bindings are incomplete')
    if popup is None or popup['maskType'] != 3:
        raise ValueError('Original report blur mask binding is missing')
    # Reuse the retained 854 native WorldCanvas mask, keeping its original provenance.
    mask_document = ROOT / 'unity/Assets/Official/Skills/skill-ui.json'
    mask = json.loads(mask_document.read_text(encoding='utf-8'))['popup_masks'][str(popup['maskType'])]
    prefix = '/WorldCanvas/' + mask['prefab_path']
    def relative(field):
        path = mask['fields'][field]
        if not path.startswith(prefix): raise ValueError('Original mask binding left its native root')
        return path[len(prefix):].lstrip('/')
    popup['mask'] = dict(prefabResource=mask['prefab_resource'], prefabPath=mask['prefab_path'],
        blurMaterial=mask['blur_material'], buttonPath=relative('mBtn'), imagePath=relative('mImage'),
        uvRect=mask['raw_image']['m_UVRect'])
    result['window'].update(popup)
    write(OUTPUT, (json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode())
    identity = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/shared-battle-report/' + OUTPUT.relative_to(ROOT).as_posix()).hex
    write(OUTPUT.with_suffix(OUTPUT.suffix + '.meta'), (
        'fileFormatVersion: 2\nguid: ' + identity + '\nTextScriptImporter:\n'
        '  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode())
    receipt = dict(manifest=apk.version, apk_manifest=apk.source.apk_version,
                   apk_sha256=apk.source.apk_sha256, native=file_record(native),
                   prefab_source=apk.provenance(prefab),
                   retained_mask=dict(document=file_record(mask_document), binding=mask, resources=[file_record(ROOT / path) for path in (
                       'unity/Assets/Game/Resources/Map11/UI/WorldCanvas.prefab',
                       'unity/Assets/Game/Resources/World/Mainline/ScreenBlur.mat',
                       'unity/Assets/Game/Runtime/Presentation/World/Shaders/ScreenBlur.shader')]),
                   hierarchy_nodes=len(paths), verified_layout_fields=layout_fields,
                   sources=sources, output=file_record(OUTPUT),
                   official_code=[file_record(ROOT / '.tools/source-packages/current' / apk.version /
                       'official-code/client-source' / (cls + '.cs')) for cls in (
                       'FightEndReportPanelBase', 'FightEndReportPanel', 'FightEndReportSkillTabView',
                       'FightEndReportPlayerTabView', 'FightEndReportPanelTabItem', 'FightEndReportSkillTabItem')],
                   generator=file_record(Path(__file__).resolve()))
    receipt['runtime'] = [file_record(ROOT / path) for path in (
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleReportView.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleReportView.cs.meta',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattlePopupMask.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattlePopupMask.cs.meta',
        'unity/Assets/Game/Runtime/Presentation/Character/OfficialSkillPopupMask.cs',
        'unity/Assets/Game/Runtime/Presentation/Character/CharacterSkillPanel.BattleReport.cs',
        'unity/Assets/Game/Runtime/Official/Combat/FightOfficialSkillItemCatalog.cs')]
    receipt['equipment_user_type'] = file_record(ROOT / '.tools/source-packages/current' / apk.version /
        'official-code/common-source/Common/SkillUserType.cs')
    receipt['output_meta'] = file_record(OUTPUT.with_suffix(OUTPUT.suffix + '.meta'))
    write(RECEIPT, (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode())
    print('Verified shared native report bindings:', len(paths), 'nodes,', len(sources), 'components')


if __name__ == '__main__':
    build()
