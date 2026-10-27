"""Recover the two mountain buildings' actual EC world-stage operation graphs."""
import json
from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from prepare_unity_map12_sources import encode, sha, source_path, write, meta


def build():
    apk = Apk.current()
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    schemas = ScriptSchemas(code_root=code)
    source_root = ROOT / 'unity/Assets/Official/World/Map/12/Source'
    tables = json.loads((source_root / 'tables.json').read_bytes())
    templates = json.loads((source_root / 'templates.json').read_bytes())
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
        buildings.append(dict(EntityClassId=identity, PlacementId=entities[0]['placementId'], States=states,
                              Operations=list(nodes.values()),
                              CanInteractAfterMove=bool(stage['mCanInteractAfterMove']),
                              CompleteState=parameter['CompleteState'], PublicDefaultState=parameter['PublicDefaultState']))
        original.append(dict(EntityClassId=identity, source=apk.provenance(driver), fields=fields))
    conditions = [v for v in tables['conditions'] if v['ClassId'] in condition_ids]
    if condition_ids != set(range(4102, 4107)) or len(conditions) != len(condition_ids):
        raise ValueError('Building conditions require a new closure')
    out = ROOT / 'unity/Assets/Official/World/Map/12/Resources/Map12/building-operations.json'
    source = source_root / 'building-operations.json'
    write(source, encode(original)); meta(source)
    write(out, encode(dict(manifest=apk.version, mapClassId=12, Buildings=buildings, Conditions=conditions))); meta(out)
    names = ('ECWorldStageView', 'ECWorldStageCfgNormalProvider', 'ECWorldStageInteractionOperationBase', 'ECWorldStageInteractionOperationCondition',
             'ECWorldStageInteractionOperationSequence', 'ECWorldStageInteractionOperationClick',
             'ECWorldStageInteractionOperationOnlyStory', 'ECWorldStageInteractionOperationStoryBase',
             'ECWorldStageInteractionOperationWindow', 'StageDialog', 'StageTouchUI', 'StageStageUI',
             'WorldCommonStageUI', 'WorldDialogEntityContent', 'WorldStageOperationWindowClickUi', 'WorldStageUiClickData')
    inputs = [code / 'client-source' / (v + '.cs') for v in names]
    inputs += [source_root / v for v in ('tables.json', 'templates.json')]
    inputs += [ROOT / v for v in (
        'scripts/prepare_unity_map12_building_operations.py',
        'unity/Assets/Game/Runtime/Official/World/WorldStageInteractionOperation.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/StageDialog.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/StageTouchUI.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldCommonStageUI.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldDialogEntityContent.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldStageOperationWindowClickUi.cs',
        'unity/Assets/Game/Runtime/Presentation/World/OfficialMap12BuildingData.cs',
        'unity/Assets/Game/Editor/OfficialMap11UiTools.Stage.cs',
        'unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.Buildings.cs')]
    record = lambda p: dict(path=source_path(p), sha256=sha(p.read_bytes()), bytes=p.stat().st_size)
    receipt = dict(manifest=apk.version, mapClassId=12,
                   operation='Decode original ECViewDriver managed references; preserve conditions, synchronous sequence order and first matching window-click operation.',
                   inputs=[record(p) for p in inputs], outputs=[record(p) for p in (source, out)],
                   nativeSources=[v['source'] for v in original],
                   limitations=['Source graphs do not establish server stage execution or complete building UI migration.'])
    write(ROOT / 'assets/reference/official-map/map12-building-operations-source-manifest.json', encode(receipt))
    print('Map12 original building operations: 2 buildings, 7 states, 5 conditions', flush=True)


if __name__ == '__main__':
    build()
