"""Bind the selected-version placed chests to native reward rules and regions."""
import json
from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_unity_map12_sources import encode, sha, source_path, write, meta

SOURCE = ROOT / 'unity/Assets/Official/World/Map/12/Source'
OUT = ROOT / 'unity/Assets/Official/World/Map/12/Resources/Map12/chests.json'
RECEIPT = ROOT / 'assets/reference/official-map/map12-chests-source-manifest.json'


def build():
    inputs = []
    def read(path):
        raw = path.read_bytes()
        inputs.append(dict(path=source_path(path), sha256=sha(raw)))
        return json.loads(raw)
    source_manifest = read(ROOT / 'assets/reference/official-map/map12-source-manifest.json')
    if source_manifest['manifest'] != '871_156598':
        raise ValueError('Map12 chest source version changed')
    tables, local, templates = [read(SOURCE / name) for name in ('tables.json', 'entities.json', 'templates.json')]
    placed = read(ROOT / 'unity/Assets/Official/World/Map/12/map12-entities.json')['entities']
    shared = read(ROOT / 'unity/Assets/Game/Resources/World/Mainline/exploration.json')
    source_hashes = {r['path']: r['sha256'] for r in source_manifest['outputs']}
    for path in (SOURCE / name for name in ('tables.json', 'entities.json', 'templates.json')):
        if source_hashes[source_path(path)] != sha(path.read_bytes()):
            raise ValueError('Decoded chest source changed: ' + str(path))
    chests, ids = [], set()
    for row in placed:
        if row['entityClassId'] not in (8200, 8201, 8202, 8203):
            continue
        comp, template = local[str(row['placementId'])], templates[str(row['entityClassId'])]
        stage = template['WorldStageComponent']
        if (stage['StateInfos'] != [{'EnterResults': [], 'SubStateList': [{'Behaviour': {'CompIndex': 0, 'CompType': 2},
                'Conditions': [], 'Results': [{'CompIndex': 0, 'CompType': 200}], 'Select': 7}]}]
                or not stage['AutoNextState'] or not stage['DestroyOnEnd'] or row['state'] != 0):
            raise ValueError('Unconverted placed chest stage: ' + str(row['placementId']))
        award = template['StageResultAwardComponent']['AwardInfos'][0]
        if award['AdditionPropType'] != 0:
            raise ValueError('Chest addition property requires its native evaluator')
        rules = list(award['RuleIds'])
        overrides = comp.get('LoStageResultAwardComponent', {}).get('AwardInfos', [])
        if overrides:
            if len(overrides) != 1:
                raise ValueError('Unexpected placed chest award index')
            extra = overrides[0]
            rules = list(extra['RuleIds']) if extra['IsReplace'] else rules + extra['RuleIds']
        region = next(v for v in tables['Map/map_region'] if v['ClassId'] == 12 and v['Region'] == row['region'])
        parameter = next(v for v in tables['Map/entity_extend_parameter'] if v['EntityClassId'] == row['entityClassId'])
        level = comp['LoLevelComponent']
        if level['Level'] != int(level['Level']) or parameter['LevelTypeInPrivateMap'] != 1 or parameter['LevelTypeInPublicMap'] != 2:
            raise ValueError('Unconverted placed chest level branch')
        chests.append(dict(PlacementId=row['placementId'], EntityClassId=row['entityClassId'], Region=row['region'],
            Area=row['area'], X=row['grid'][0], Y=row['grid'][1], Level=int(level['Level']), Absolute=level['Absolute'],
            SubRank=level['SubRank'], RegionLevel=region['Level'], RegionSubRank=region['SubRank'], DropItems=region['DropItems'],
            PrivateLevelType=parameter['LevelTypeInPrivateMap'], PublicLevelType=parameter['LevelTypeInPublicMap'],
            CompleteState=parameter['CompleteState'], RuleIds=rules, LogicTag=comp['LoEntityComponent']['Tag'] or ''))
        ids.update(rules)
    rules = [v for v in tables['reward_rule'] if v['Id'] in ids]
    missing = ids - {v['Id'] for v in rules}
    if missing != {711, 721, 722, 732, 735, 742, 745} or len(chests) != 83:
        raise ValueError('Placed chest closure changed')
    # Existing reward execution is reused only after selected-source equality,
    # including every condition branch and automatic package expansion.
    for key in ids - {782}:
        if [v for v in rules if v['Id'] == key] != [v for v in shared['rewardRules'] if v['Id'] == key]:
            raise ValueError('Shared reward rule differs from current source: ' + str(key))
    if any(v['Condition'] is not None for v in rules if v['Id'] == 782):
        raise ValueError('New chest reward condition requires its own closure')
    apk = Apk.current()
    decoder = Constructors(ROOT / '.tools/source-packages/current/871_156598/official-code')
    def table(name, kind):
        obj = apk.asset('config_luban', '/' + name + '.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        inputs.append(dict(table=name, sha256=sha(raw), source=apk.provenance(obj)))
        return decoder.table(raw, kind)
    level_ids = {i for r in rules for key in ('LevelCountId', 'SubRankCountId') for i in (r[key] or []) if i}
    numbers = [v for v in table('level_number', 'TbLevelNumber') if v['Id'] in level_ids]
    if numbers != [v for v in shared['rewardLevelNumbers'] if v['Id'] in level_ids]:
        raise ValueError('Shared reward scale differs from current source')
    item_ids = {i for r in rules for i in r['IdList']}
    packages = [v for v in table('random_reward', 'TbRandomRewardInfo') if v['Id'] in item_ids]
    if packages != [v for v in shared['randomRewards'] if v['Id'] in item_ids]:
        raise ValueError('Shared automatic reward package differs from current source')
    # Composite condition lookups stay in the existing proven evaluator.
    condition_ids = set()
    def collect(condition):
        if condition and condition['_type'] in ('CompositeAndCondition', 'CompositeOrCondition'):
            for key in condition['Ids']:
                if key in condition_ids:
                    continue
                condition_ids.add(key)
                current = next(v for v in tables['conditions'] if v['ClassId'] == key)
                if current != next(v for v in shared['conditions'] if v['ClassId'] == key):
                    raise ValueError('Shared chest condition differs from selected source')
                collect(current['Condition'])
    for rule in rules:
        collect(rule['Condition'])
    map_info = next(v for v in tables['Map/map'] if v['ClassId'] == 12)
    payload = dict(manifest='871_156598', MapGroupId=map_info['MapGroupId'], chests=chests,
        rewards=dict(rewardRules=rules, rewardLevelNumbers=numbers))
    write(OUT, encode(payload)); meta(OUT)
    code = ROOT / '.tools/source-packages/current/871_156598/official-code'
    functions = ['common-source/Common/' + name + '.cs' for name in ('WorldEntityLevelComponent', 'LoLevelComponentInfo',
        'StageResultAwardComponent', 'StageResultAwardComponentInfo', 'LoStageSingleAwardInfo', 'RewardRuleInfoParser')]
    for name in functions:
        p = code / name
        inputs.append(dict(path=source_path(p), sha256=sha(p.read_bytes())))
    adapters = [ROOT / ('unity/Assets/Game/' + name) for name in (
        'Runtime/Offline/Progression/OfflineWorldExplorationService.Map12Chests.cs',
        'Runtime/Offline/Progression/OfflineWorldExplorationService.Rewards.cs',
        'Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Map12Chests.cs',
        'Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Interactions.cs',
        'Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Persistence.cs',
        'Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.cs',
        'Editor/OfficialMap12PlayModeAcceptance.Chests.cs',
        'Editor/OfficialMap12PlayModeAcceptance.Runtime.cs',
        'Editor/OfficialMap12PlayModeAcceptance.cs')]
    write(RECEIPT, encode(dict(manifest='871_156598', generator=source_path(__import__('pathlib').Path(__file__)),
        inputs=inputs, output=dict(path=source_path(OUT), sha256=sha(OUT.read_bytes())),
        outputMeta=dict(path=source_path(OUT.with_name(OUT.name + '.meta')),
            sha256=sha(OUT.with_name(OUT.name + '.meta').read_bytes())), chests=len(chests),
        emptyRewardRuleIds=sorted(missing), offlineSubRankZero='inherit placed chest region; nonzero uses explicit value',
        adapters=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in adapters],
        runtime='OfflineWorldExplorationService.Map12Chests.cs', sharedRewardEquality=True)))
    apk.archive.close()
    print('Map12: bound 83 placed chests, current reward rules, original scales and local reward closure')


if __name__ == '__main__':
    build()
