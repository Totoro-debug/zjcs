"""Stage original arena tables and prerequisites without publishing resources."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
from official_apk import ROOT, Apk
from official_tables import Constructors

WANTED = {'arena_guard_template': 'TbArenaGuardTemplateInfo',
          'arena_rank_template': 'TbArenaRankTemplateInfo',
          'arena_match_rule': 'TbArenaMatchRuleInfo',
          'arena_match_strategy': 'TbArenaMatchStrategyInfo',
          'arena_score_ratio': 'TbArenaScoreRatioInfo',
          'arena_daily_award': 'TbArenaDailyAwardInfo',
          'arena_season_award': 'TbArenaSeasonAwardInfo',
          'item_count_limit': 'TbItemCountLimitInfo'}


def build(output, verify_apk=False, manifest_output=None):
    constructors = Constructors()
    cache = ROOT/'.tools/source-packages/official-data'
    tables, inputs, raws = {}, {}, {}
    for name, typ in WANTED.items():
        path = cache/(name+'.g.bytes')
        raw = path.read_bytes()
        raws[name] = raw
        tables[name] = constructors.table(raw, typ)
        inputs[name] = dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw),
                            sha256=hashlib.sha256(raw).hexdigest(), constructor=typ,
                            consumed_bytes=len(raw), apk_verified_this_run=False)
    if verify_apk:
        apk = Apk()
        found = set()
        for path, pointer in apk.container('config_luban'):
            name = path.rsplit('/', 1)[-1].removesuffix('.g.bytes')
            if name not in WANTED:continue
            obj = pointer.deref()
            if obj.read().m_Script.encode('utf8', 'surrogateescape') != raws[name]:
                raise ValueError('Arena table cache differs from pinned APK: '+name)
            inputs[name].update(apk.provenance(obj), asset=path, apk_verified_this_run=True)
            found.add(name)
        if found != set(WANTED):raise ValueError('Arena tables missing from APK: '+str(set(WANTED)-found))
    rules_path = ROOT/'assets/official/rules.json'
    rules = json.loads(rules_path.read_text('utf8'))
    setting = rules['game_settings']['ArenaSetting']
    battle_stages_path = ROOT/'assets/official/battle-stages.json'
    battle_stages = json.loads(battle_stages_path.read_text('utf8'))
    stage = next((row for row in rules['stage'] if int(row['Id']) == int(setting['StageId'])), None)
    if stage is None:
        raise ValueError('Arena stage is missing from official stage configuration')
    game_id = int(stage['GameId'])
    scene_data = battle_stages.get('stages', {}).get(str(game_id))
    if scene_data is None:
        raise ValueError('Official arena battle scene is missing: '+str(game_id))
    grid = scene_data['FightGridGroundComponent']
    fight = scene_data['FightComponent']
    arena_scene = dict(
        stageId=int(setting['StageId']), gameId=game_id, sceneName=fight['SceneName'],
        gridWidth=grid['Size']['X'], gridHeight=grid['Size']['Y'], gridOrigin=grid['OriginPos'],
        cells=[dict(x=value['Pos']['X'], y=value['Pos']['Y'], groundType=value['GroundType'], height=value['Height'])
               for value in grid['PosInfos']],
        factions=[dict(id=value['Faction'],
                       entryPositions=[[pos['X'], pos['Y']] for pos in value['EntryPosList']],
                       entryDirections=value['EntryDirList']) for value in fight['FactionInfoList']])
    factions = {row['id']: row for row in arena_scene['factions']}
    if arena_scene['gridWidth'] != 21 or arena_scene['gridHeight'] != 20 \
            or 0 not in factions or 1 not in factions:
        raise ValueError('Official arena scene grid or faction entries are invalid')
    runtime_tables = tables
    refresh_costs = setting.get('BuyRefreshCountCost') or {}
    runtime = dict(
        guards=[dict(id=row['Id'], nameKey=row['NameKey'], profession=row['Profession'],
                     monsterId=row['MonsterId'], monsterGroupId=row['MonsterGroupId'],
                     level=row['Level'], subRank=row['SubRank'],
                     appendAppearances=[dict(sex=sex, skins=[dict(skinType=skin_type, skinId=skin_id)
                         for skin_type, skin_id in sorted((skins or {}).items())])
                         for sex, skins in sorted((row['AppendAppearances'] or {}).items())],
                     activeSkills=[dict(skillId=skill['SkillId'], level=skill['Level'], rank=skill['Rank'])
                                   for skill in row['ActiveSkill']],
                     passiveSkills=[dict(skillId=skill['SkillId'], level=skill['Level'], rank=skill['Rank'])
                                    for skill in row['PassiveSkill']])
                for row in runtime_tables['arena_guard_template']],
        rankTemplates=[dict(lastRank=row['LastRank'], weights=[dict(templateId=int(key), weight=value)
                            for key, value in (row['TemplateWeight'] or {}).items()])
                       for row in runtime_tables['arena_rank_template']],
        matchRules=[dict(id=row['Id'], type=row['Type'], isAhead=row['IsAhead'],
                         percent=row['Percent'] or [], range=row['Range'] or [])
                    for row in runtime_tables['arena_match_rule']],
        matchStrategies=[dict(rank=row['Rank'], rules=[dict(ruleId=int(key), count=value)
                              for key, value in (row['RuleInfo'] or {}).items()])
                         for row in runtime_tables['arena_match_strategy']],
        scoreRatios=[dict(lastRank=row['LastRank'], scoreRatio=row['ScoreRatio'])
                     for row in runtime_tables['arena_score_ratio']],
        refreshCostItemId=int(next(iter(refresh_costs), 0)),
        refreshCost=int(next(iter(refresh_costs.values()), 0)),
        dailyAwards=[dict(lastRank=row['LastRank'], awards=[dict(itemId=int(key), count=value)
                           for key, value in (row['Award'] or {}).items()])
                     for row in runtime_tables['arena_daily_award']])
    items = [row for row in rules['item'] if int(row['ClassId']) in [int(setting['ChallengeCountId']), int(setting['RefreshCountId'])]]
    limits = [row for row in rules['function_limit'] if int(row['Type']) == 58]
    output.mkdir(parents=True, exist_ok=True)
    target = output/'arena-rules.json'
    target.write_text(json.dumps(dict(tables=tables, runtime=runtime, settings=setting, items=items,
                                      function_limits=limits, arena_scene=arena_scene),
                                 ensure_ascii=False, indent=2)+'\n', 'utf8')
    source = ROOT/'.tools/source-packages/official-code/common-source/Common'
    code = {name+'.cs': hashlib.sha256((source/(name+'.cs')).read_bytes()).hexdigest() for name in constructors.sources}
    record = dict(status='runtime_resource' if manifest_output else 'staged_only', manifest='854_156383', tables=inputs, original_constructor_code_sha256=code,
                  rules_sha256=hashlib.sha256(rules_path.read_bytes()).hexdigest(),
                  battle_stages_sha256=hashlib.sha256(battle_stages_path.read_bytes()).hexdigest(),
                  metadata_sha256=hashlib.sha256(constructors.metadata.data).hexdigest(),
                  output_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                  operation='Interpret original generated constructors with complete payload consumption; use the pinned StageId scene for local guard challenges; static guard templates and rank weights do not imply server opponent records or season schedule.')
    manifest_path = manifest_output or output/'source-manifest.json'
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(record, ensure_ascii=False, indent=2)+'\n', 'utf8')
    return record


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT/'.tools/gameplay-fixtures/combat/arena-staged')
    parser.add_argument('--verify-apk', action='store_true')
    parser.add_argument('--runtime', action='store_true', help='Write the runtime table to assets/official and its source manifest to assets/reference.')
    args = parser.parse_args()
    output = ROOT/'assets/official' if args.runtime else args.output
    manifest = ROOT/'assets/reference/arena-source-manifest.json' if args.runtime else None
    record = build(output, args.verify_apk, manifest)
    data = json.loads((output/'arena-rules.json').read_text('utf8'))
    if args.runtime:
        resource_dir = ROOT/'unity/Assets/Game/Resources/Dungeons'
        resource_dir.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(output/'arena-rules.json', resource_dir/'arena-rules.json')
        resource_manifest = resource_dir/'arena-source-manifest.json'
        shutil.copyfile(manifest, resource_manifest)
    print('Staged arena rows:', {key: len(rows) for key, rows in data['tables'].items()})
    print('APK verified this run:', all(row['apk_verified_this_run'] for row in record['tables'].values()))


if __name__ == '__main__':main()
