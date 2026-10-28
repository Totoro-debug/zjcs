"""Recover the seven placed mountain steles and their selected official rules."""
from __future__ import annotations

import argparse
import hashlib
import json

from official_apk import Apk, ROOT
from official_tables import Constructors
from official_unity_schema import ScriptSchemas
from prepare_unity_map12_sources import encode, sha, source_path

BASE = ROOT / 'unity/Assets/Official/World/Map/12'
OUTPUT = BASE / 'Resources/Map12/steles.json'
NATIVE = BASE / 'Source/stele-operations.json'
RECEIPT = ROOT / 'assets/reference/official-map/map12-steles-source-manifest.json'
PLACEMENTS = {1813: 906, 2530: 908, 3013: 931, 5519: 930, 6406: 905, 6614: 907, 8320: 932}


def metadata(path):
    identity = source_path(path).removeprefix('unity/')
    guid = hashlib.md5(('zjcs-official:' + identity).encode()).hexdigest()
    raw = ('fileFormatVersion: 2\nguid: ' + guid + '\nTextScriptImporter:\n'
           '  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
    target = path.with_name(path.name + '.meta')
    if target.exists() and b'\n'.join(line.rstrip() for line in target.read_bytes().splitlines()) + b'\n' != raw:
        raise ValueError('Refusing to replace unrelated stele metadata: ' + source_path(target))
    target.write_bytes(raw)


def build(update=False):
    apk = Apk.current()
    if apk.version != '871_156598':
        raise ValueError('Map12 stele source version changed')
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    schemas, decoder = ScriptSchemas(code_root=code), Constructors(code)
    inputs = []

    def record(path):
        return dict(path=source_path(path), sha256=sha(path.read_bytes()))

    def read(path):
        inputs.append(record(path))
        return json.loads(path.read_bytes())

    receipt = read(ROOT / 'assets/reference/official-map/map12-source-manifest.json')
    selected = {row['path']: row['sha256'] for row in receipt['outputs']}
    tables, local, templates = [read(BASE / 'Source' / name) for name in ('tables.json', 'entities.json', 'templates.json')]
    for name in ('tables.json', 'entities.json', 'templates.json'):
        path = BASE / 'Source' / name
        if selected[source_path(path)] != sha(path.read_bytes()):
            raise ValueError('Selected Map12 source has changed: ' + name)
    placed = read(BASE / 'map12-entities.json')['entities']
    battles = read(BASE / 'map12-battles.json')

    def table(name, kind):
        obj = apk.asset('config_luban', '/' + name + '.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        inputs.append(dict(table=name, sha256=sha(raw), source=apk.provenance(obj)))
        return decoder.table(raw, kind)

    all_rules = table('reward_rule', 'TbRewardRuleInfo')
    original_stages = table('stage', 'TbStageInfo')
    parts = [row for row in table('stage_battle_stele', 'TbStagePartInfoBattleStele') if row['Id'] in PLACEMENTS.values()]
    modes = [row for row in table('game_play_mode', 'TbGamePlayModeInfo') if 29 in (row['StageTypes'] or [])]
    settings = table('game_settings', 'TbGameSettings')
    if isinstance(settings, list): settings = settings[0]
    if len(parts) != 7 or len(modes) != 1 or modes[0]['TeamMaxPlayer'] != 4:
        raise ValueError('Original battle-stele stage/team closure changed')
    recommend_index = settings['RecommendCombatRatingIndex']
    operations = []
    for identity in (6650, 6655):
        root = apk.asset('defaultpackage_assets_prefabs_map_map_12_entitydisplays_worldgameplay.bundle',
                         '/' + str(identity) + '_12_view.prefab')
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
        if len(drivers) != 1: raise ValueError('Original stele display driver is ambiguous')
        fields = schemas.read(drivers[0])
        refs = {row['rid']: row for row in fields['references']['RefIds']}
        stage = next(row['data'] for row in refs.values() if row['type']['class'] == 'ECWorldStageView')
        provider = refs[stage['mWorldStageCfgProviderData']['ECWorldStageCfgProvider']['rid']]
        if provider['type']['class'] != 'ECWorldStageCfgNormalProvider' or len(provider['data']['mStateCfgs']) != 1:
            raise ValueError('Original stele provider changed')
        operation = refs[provider['data']['mStateCfgs'][0]['InteractionOperation']['InteractionOperation']['rid']]
        if operation['type']['class'] != 'ECWorldStageInteractionOperationWindow' or operation['data'] != dict(
                mIsReport=0, mSDKEventPanelId=0, mWindowName='WorldMap/BattleSteleWorldStageDialog', mClickDatas=[]):
            raise ValueError('Unconverted original stele operation')
        operations.append(dict(EntityClassId=identity, source=apk.provenance(drivers[0]), fields=fields))

    steles, rule_ids = [], set()
    for row in placed:
        if row['entityClassId'] not in (6650, 6655): continue
        placement = row['placementId']
        component = local[str(placement)]
        template = templates[str(row['entityClassId'])]
        stage_id = component['LoStageBehBattleSteleStageComponent']['StageId']
        if PLACEMENTS.get(placement) != stage_id or row['typeId'] != 218:
            raise ValueError('Original placed stele stage binding changed')
        world = template['WorldStageComponent']
        sub = world['StateInfos'][0]['SubStateList'][0]
        if (len(world['StateInfos']) != 1 or world['AutoNextState'] or world['DestroyOnEnd']
                or sub != dict(Behaviour=dict(CompIndex=0, CompType=13),
                    Conditions=[dict(CompIndex=0, CompType=110)], Results=[], Select=7)):
            raise ValueError('Original stele stage/condition execution changed')
        groups = component['LoStageConPlayerPeriodicityInteractionComponent']['PeriodicityGroupIds']
        if groups != component['LoStageResultPeriodicityInteractionComponent']['PeriodicityGroupIds'] or len(groups) != 1:
            raise ValueError('Original stele condition/result group differs')
        periodic = next(v for v in tables['Map/map_periodicity_interaction'] if v['PeriodicityGroupId'] == groups[0])
        if periodic['PeriodicityTime'] != -1 or periodic['PeriodicityMaxCount'] != 1 or periodic['IsIgnoreOwner']:
            raise ValueError('Stele requires a different original period rule')
        region = next(v for v in tables['Map/map_region'] if v['ClassId'] == 12 and v['Region'] == row['region'])
        parameter = next(v for v in tables['Map/entity_extend_parameter'] if v['EntityClassId'] == row['entityClassId'])
        level = component['LoLevelComponent']
        if level != dict(Absolute=False, Level=0.0, SubRank=0) or any(parameter[k] != 1 for k in ('LevelTypeInPrivateMap', 'LevelTypeInPublicMap')):
            raise ValueError('Stele requires a newly confirmed level initialization')
        stage = next(v for v in battles['stages'] if v['Id'] == stage_id)
        original_stage = next(v for v in original_stages if v['Id'] == stage_id)
        if any(stage[k] != original_stage[k] for k in stage if k in original_stage):
            raise ValueError('Projected stele stage differs from selected source')
        if stage['Type'] != 29 or stage['MonsterLevelAdd'] != 7 or stage['SupportTeam'] != (row['entityClassId'] == 6650):
            raise ValueError('Original stele stage branch changed')
        part = next(v for v in parts if v['Id'] == stage_id)
        if not 0 <= recommend_index < len(part['CombatRatingLevelValues']):
            raise ValueError('Original stele recommendation index is invalid')
        rule_ids.update(stage['Awards'])
        steles.append(dict(PlacementId=placement, EntityClassId=row['entityClassId'], StageId=stage_id,
            Region=row['region'], Area=row['area'], X=row['grid'][0], Y=row['grid'][1],
            RegionLevel=region['Level'], RegionSubRank=region['SubRank'], MonsterLevelAdd=stage['MonsterLevelAdd'],
            PeriodicityGroupId=groups[0], SupportTeam=stage['SupportTeam'], RuleIds=stage['Awards'],
            CombatRatingLevelValues=part['CombatRatingLevelValues']))
    if len(steles) != 7 or rule_ids != set(range(1850, 1856)):
        raise ValueError('Map12 stele placement/reward closure changed')
    rules = [v for v in all_rules if v['Id'] in rule_ids]
    empty = sorted(rule_ids - {v['Id'] for v in rules})
    if empty != [1850, 1853] or len(rules) != 52:
        raise ValueError('Original stele reward references changed')
    # These exact selected rules have one integral fixed entry; the condition
    # parser supplies the stele rank, not the main player's current rank.
    for rule in rules:
        condition = rule['Condition']
        if (len(rule['IdList']) != 1 or len(rule['WeightList']) != 1 or rule['WeightList'][0] <= 0
                or rule['Percent'] != 1 or len(rule['CountList']) != 1 or rule['CountList'][0] != int(rule['CountList'][0])
                or rule['LevelCountId'] or rule['SubRankCountId'] or rule['IndexMininum'] or rule['RecordSharedId']
                or condition is not None and (condition['_type'] != 'SubRankCondition' or condition['Op'] != '>=')):
            raise ValueError('Stele needs a different original reward evaluator')
    item_ids = {v['IdList'][0] for v in rules}
    items = [v for v in table('item', 'TbItemInfo') if v['ClassId'] in item_ids]
    packages = [v for v in table('random_reward', 'TbRandomRewardInfo') if v['Id'] in item_ids]
    if len(items) != len(item_ids) or any(v['IsCanAutoExpend'] for v in packages):
        raise ValueError('Stele fixed item closure requires automatic package expansion')
    payload = dict(manifest=apk.version, mapClassId=12, TeamMaxPlayer=modes[0]['TeamMaxPlayer'],
        RecommendCombatRatingIndex=recommend_index, Steles=sorted(steles, key=lambda v: v['PlacementId']),
        RewardRules=rules, Items=items, EmptyRewardRuleIds=empty)
    candidates = {OUTPUT: encode(payload), NATIVE: encode(dict(manifest=apk.version, Displays=operations))}
    for path, raw in candidates.items():
        if path.exists() and path.read_bytes() != raw and not update:
            raise ValueError('Stele source output differs; inspect before --update: ' + source_path(path))
        if update or not path.exists():
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        metadata(path)
    originals = ['client-source/' + n + '.cs' for n in ('BattleSteleWorldStageDialog', 'WorldSteleStageInput',
        'ECWorldStageView', 'ECWorldStageCfgNormalProvider', 'ECWorldStageInteractionOperationWindow', 'UILogicUtils')]
    originals += ['common-source/Common/' + n + '.cs' for n in ('StageBehStageBattleSteleComponent',
        'StageConPlayerPeriodicityInteractionComponent', 'MapPeriodicityInteractionInfoParser',
        'StagePartInfoBattleStele', 'WorldEntityLevelComponent', 'RewardRuleInfoParser', 'FormulaHandler')]
    inputs += [record(code / name) for name in originals]
    adapters = [ROOT / p for p in ('scripts/prepare_unity_map12_steles.py',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineWorldExplorationService.Map12Steles.cs',
        'unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Map12Steles.cs',
        'unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.Steles.cs')]
    proof = dict(manifest=apk.version, mapClassId=12, inputs=inputs, assemblies={
        name: record(code / 'managed/decoded' / name) for name in ('Assembly-CSharp.dll', 'Console.dll')},
        nativeSources=[v['source'] for v in operations], outputs=[record(p) for p in candidates],
        outputMetadata=[record(p.with_name(p.name + '.meta')) for p in candidates],
        adapters=[record(p) for p in adapters],
        emptyRewardRuleIds=empty,
        emptyRuleBehavior='Selected complete reward table lacks these IDs; original RewardRuleInfoParser.Get returns null, so no invented reward is added.',
        offlinePolicy='User confirmed stele entity level/rank inherit its own region; original monster +7 and formation offsets; existing confirmed map reward rules.',
        limitations=['Dedicated native BattleSteleWorldStageDialog and NPC-team input still require migration and runtime acceptance.',
            'Runtime acceptance is paused by the user; this receipt does not prove Play Mode or Player behavior.'])
    raw = encode(proof)
    if RECEIPT.exists() and RECEIPT.read_bytes() != raw and not update:
        raise ValueError('Stele source receipt differs; inspect before --update')
    RECEIPT.write_bytes(raw)
    apk.archive.close()
    print('Map12 steles: seven placements/stages, two original operation graphs, 52 fixed reward branches, two original empty references verified.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--update', action='store_true')
    build(parser.parse_args().update)
