"""Recover the two mountain buildings' actual EC world-stage operation graphs."""
import json
import struct
import xxhash
import re
from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from official_tables import Constructors
from prepare_unity_map12_sources import encode, sha, source_path, write, meta


def build():
    apk = Apk.current()
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    schemas = ScriptSchemas(code_root=code)
    source_root = ROOT / 'unity/Assets/Official/World/Map/12/Source'
    tables = json.loads((source_root / 'tables.json').read_bytes())
    templates = json.loads((source_root / 'templates.json').read_bytes())
    local = json.loads((source_root / 'entities.json').read_bytes())
    placed = json.loads((ROOT / 'unity/Assets/Official/World/Map/12/map12-entities.json').read_bytes())['entities']
    condition_ids, original, buildings = set(), [], []
    for identity in (6103, 6302):
        root = apk.asset('defaultpackage_assets_prefabs_map_map_12_entitydisplays_worldbuilding.bundle',
                         '/' + str(identity) + '_view.prefab')
        drivers = []
        def visit(obj):
            for entry in obj.read().m_Component:
                component = entry.component.deref()
                if component.type.name in ('Transform', 'RectTransform'):
                    for child in component.read().m_Children:
                        visit(child.read().m_GameObject.deref())
                elif component.type.name == 'MonoBehaviour' and component.read(check_read=False).m_Script.read().m_ClassName == 'ECViewDriver':
                    drivers.append(component)
        visit(root)
        if len(drivers) != 1:
            raise ValueError('Expected the building native ECViewDriver')
        driver = drivers[0]
        fields = schemas.read(driver)
        refs = {v['rid']: v for v in fields['references']['RefIds']}
        def deref(pointer):
            return refs[pointer['rid']] if pointer['rid'] >= 0 else None
        stage = next(v['data'] for v in refs.values() if v['type']['class'] == 'ECWorldStageView')
        provider = deref(stage['mWorldStageCfgProviderData']['ECWorldStageCfgProvider'])
        if provider['type']['class'] != 'ECWorldStageCfgNormalProvider':
            raise ValueError('Building requires a different stage provider')
        nodes = {}
        def operation(wrapper):
            ref = deref(wrapper['InteractionOperation'])
            if ref is None:
                return -2
            if ref['rid'] in nodes:
                return ref['rid']
            kind, data = ref['type']['class'], ref['data']
            out = dict(Id=ref['rid'], Type=kind)
            nodes[ref['rid']] = out
            if kind == 'ECWorldStageInteractionOperationCondition':
                condition_ids.update(data['mConditionIds'])
                out.update(ConditionIds=data['mConditionIds'], NeedAllMeet=bool(data['mNeedAllMeet']),
                           MeetId=operation(data['mMeetConditionOper']), NoMeetId=operation(data['mNoMeetConditionOper']))
            elif kind == 'ECWorldStageInteractionOperationSequence':
                out['OperationIds'] = [operation(v) for v in data['mOperDatas']]
            elif kind == 'ECWorldStageInteractionOperationOnlyStory':
                out.update(Scenario=data['mNpcTalkScenario'], EndOperationId=operation(data['StoryEndOperation']))
            elif kind == 'ECWorldStageInteractionOperationWindow':
                out.update(WindowName=data['mWindowName'], IsReport=bool(data['mIsReport']), PanelId=data['mSDKEventPanelId'],
                           Clicks=[dict(Type=v['Type'], OperationId=operation(v['OperationData'])) for v in data['mClickDatas']])
            elif kind != 'ECWorldStageInteractionOperationClick':
                raise ValueError('Unconverted original building operation: ' + kind)
            return ref['rid']
        states = [dict(OperationId=operation(v['InteractionOperation'])) for v in provider['data']['mStateCfgs']]
        entities = [v for v in placed if v['entityClassId'] == identity]
        if len(entities) != 1 or len(states) != len(templates[str(identity)]['WorldStageComponent']['StateInfos']):
            raise ValueError('Building placement/stage closure changed')
        parameter = next(v for v in tables['Map/entity_extend_parameter'] if v['EntityClassId'] == identity)
        entity = entities[0]
        region = next(v for v in tables['Map/map_region'] if v['ClassId'] == 12 and v['Region'] == entity['region'])
        level = local[str(entity['placementId'])]['LoLevelComponent']
        stage_add = 0.0
        if identity == 6302:
            stage_add = templates[str(identity)]['StageBehStageComponent']['StageInfoList'][0]['LevelAdd']
            additions = local[str(entity['placementId'])]['LoStageBehStageComponent']['LevelAdds']
            if additions: stage_add += additions[0]
        buildings.append(dict(EntityClassId=identity, PlacementId=entities[0]['placementId'], States=states,
                              Operations=list(nodes.values()),
                              CanInteractAfterMove=bool(stage['mCanInteractAfterMove']),
                              CompleteState=parameter['CompleteState'], PublicDefaultState=parameter['PublicDefaultState'],
                              StageWorldType=templates[str(identity)]['WorldStageComponent']['StageWorldType'],
                              Level=int(level['Level']), Absolute=level['Absolute'], SubRank=level['SubRank'],
                              StageLevelAdd=stage_add,
                              RegionLevel=region['Level'], RegionSubRank=region['SubRank'],
                              PrivateLevelType=parameter['LevelTypeInPrivateMap'], PublicLevelType=parameter['LevelTypeInPublicMap']))
        original.append(dict(EntityClassId=identity, source=apk.provenance(driver), fields=fields))
    conditions = [v for v in tables['conditions'] if v['ClassId'] in condition_ids]
    if condition_ids != set(range(4102, 4107)) or len(conditions) != len(condition_ids):
        raise ValueError('Building conditions require a new closure')
    decoder = Constructors(code)
    table_inputs = []
    def table(name, kind):
        obj = apk.asset('config_luban', '/' + name + '.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        table_inputs.append(dict(table=name, sha256=sha(raw), source=apk.provenance(obj)))
        return decoder.table(raw, kind)
    rules = [v for v in tables['reward_rule'] if v['Id'] in (570, 571)]
    if len(rules) != 2 or any(v['Condition'] is not None or v['Percent'] != 1 or len(v['IdList']) != 1
        or v['WeightList'] != [100] or v['LevelCountId'] is not None for v in rules):
        raise ValueError('The original fixed MountLift award closure changed')
    numbers = [v for v in table('level_number', 'TbLevelNumber') if v['Id'] == 6]
    items = [v for v in table('item', 'TbItemInfo') if v['ClassId'] in (1, 110)]
    settings = table('game_settings', 'TbGameSettings')
    if any(v['ClassId'] in settings['IgnoreShowAwardId'] or not v['CanDropWhenExist'] for v in items):
        raise ValueError('MountLift preview requires its original item availability filter')
    stage_enum = code/'common-source/Common/MapEntityWorldStageType.cs'
    stage_names = {int(number): name for name, number in re.findall(r'(\w+)\s*=\s*(\d+)', stage_enum.read_text('utf8'))}
    end_tip_keys = ['entity_'+str(v['EntityClassId'])+'_end_tip' for v in buildings]
    end_tip_keys += ['entity_'+stage_names[v['StageWorldType']]+'_end_tip' for v in buildings]
    wanted = {xxhash.xxh64_intdigest(key.encode()): key for key in end_tip_keys}
    texts = {}
    languages = json.loads((ROOT/'assets/reference/official-map/localization-source-manifest.json').read_bytes())['inputs']
    for value in languages.values():
        raw = apk.archive.read(value['source']['path'])
        if sha(raw) != value['sha256']: raise ValueError('Official APK language hash changed')
        count = struct.unpack_from('<i', raw)[0]
        start = 4 + count * 12
        for i in range(count):
            identity, offset = struct.unpack_from('<Qi', raw, 4+i*12)
            if identity not in wanted: continue
            pos = start + offset
            size = struct.unpack_from('<H', raw, pos)[0]
            texts[wanted[identity]] = raw[pos+2:pos+2+size].decode('utf8')
    for building in buildings:
        primary = 'entity_'+str(building['EntityClassId'])+'_end_tip'
        fallback = 'entity_'+stage_names[building['StageWorldType']]+'_end_tip'
        building['EndTipKey'] = primary if primary in texts else fallback
        if building['EndTipKey'] not in texts: raise ValueError('Original StageEndTipUI text lookup has no result')
    battle = json.loads((ROOT/'unity/Assets/Official/World/Map/12/Resources/Map12/battle-runtime.json').read_bytes())
    fight = next(v for v in battle['stages'] if v['Id'] == 102)
    if fight['Awards'] != [570, 571] or len(numbers) != 1 or len(items) != 2:
        raise ValueError('The original MountLift stage award references changed')
    out = ROOT / 'unity/Assets/Official/World/Map/12/Resources/Map12/building-operations.json'
    source = source_root / 'building-operations.json'
    write(source, encode(original)); meta(source)
    write(out, encode(dict(manifest=apk.version, mapClassId=12, Buildings=buildings, Conditions=conditions,
        MountLiftAwards=rules, RewardNumbers=numbers, RewardItems=items,
        Texts=[dict(key=k,value=v) for k,v in sorted(texts.items())]))); meta(out)
    names = ('ECWorldStageView', 'ECWorldStageCfgNormalProvider', 'ECWorldStageInteractionOperationBase', 'ECWorldStageInteractionOperationCondition',
             'ECWorldStageInteractionOperationSequence', 'ECWorldStageInteractionOperationClick',
             'ECWorldStageInteractionOperationOnlyStory', 'ECWorldStageInteractionOperationStoryBase',
             'ECWorldStageInteractionOperationWindow', 'StageDialog', 'StageTouchUI', 'StageStageUI',
             'WorldCommonStageUI', 'WorldDialogEntityContent', 'WorldStageOperationWindowClickUi', 'WorldStageUiClickData')
    inputs = [code / 'client-source' / (v + '.cs') for v in names]
    inputs += [source_root / v for v in ('tables.json', 'templates.json', 'entities.json')]
    inputs += [ROOT/'unity/Assets/Official/World/Map/12/Resources/Map12/battle-runtime.json']
    inputs += [code/'common-source/Common'/ (name+'.cs') for name in
        ('StoryStatusCondition', 'WorldEntityLevelComponent', 'LoLevelComponentInfo', 'RewardRuleInfoParser', 'LevelNumberParser', 'StageBehStageComponent')]
    inputs += [code/'client-source'/ (name+'.cs') for name in
        ('RemoveMapExclusiveNpcNode', 'RemoveMapEntitiesByCfgNode')]
    inputs += [code/'common-source/RewardPreviewItemComparer.cs', code/'client-source/UILogicUtils.cs']
    inputs += [stage_enum, code/'client-source/StageEndTipUI.cs', code/'common-source/Common/LanguageUtils.cs',
        code/'client-source/AwardUtils.cs', code/'common-source/Common/PlayerItemManagerExt.cs']
    inputs += [ROOT / v for v in (
        'scripts/prepare_unity_map12_building_operations.py',
        'unity/Assets/Game/Runtime/Official/World/WorldStageInteractionOperation.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/StageDialog.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/StageTouchUI.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldCommonStageUI.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldDialogEntityContent.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldStageOperationWindowClickUi.cs',
        'unity/Assets/Game/Runtime/Presentation/World/OfficialMap12BuildingData.cs',
        'unity/Assets/Game/Runtime/Presentation/World/OfficialMap12BuildingRewardView.cs',
        'unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Map12Buildings.cs',
        'unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.cs',
        'unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Story.cs',
        'unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Map10.cs',
        'unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Interactions.cs',
        'unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Dialogue.cs',
        'unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Puzzles.cs',
        'unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Battle.cs',
        'unity/Assets/Game/Editor/OfficialMap11UiTools.Stage.cs',
        'unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.BuildingRuntime.cs',
        'unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.Runtime.cs',
        'unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.cs',
        'unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.Buildings.cs')]
    record = lambda p: dict(path=source_path(p), sha256=sha(p.read_bytes()), bytes=p.stat().st_size)
    receipt = dict(manifest=apk.version, mapClassId=12,
                   operation='Decode original ECViewDriver managed references; preserve conditions, synchronous sequence order and first matching window-click operation.',
                   inputs=[record(p) for p in inputs] + table_inputs, outputs=[record(p) for p in (source, out)],
                   nativeSources=[v['source'] for v in original], languageSources=languages,
                   offlineBuildingLevelPolicy='User confirmed: private building SubRank 0 inherits region; stage 102 monster level uses building level plus original offsets, subrank uses building subrank.',
                   limitations=['Source graphs do not establish server stage execution or complete building UI migration.'])
    write(ROOT / 'assets/reference/official-map/map12-building-operations-source-manifest.json', encode(receipt))
    print('Map12 original building operations: 2 buildings, 7 states, 5 conditions', flush=True)


if __name__ == '__main__':
    build()
