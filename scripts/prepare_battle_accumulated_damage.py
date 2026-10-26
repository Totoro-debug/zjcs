"""Verify the official 24062 -> 13148 -> 13149 accumulated-damage dependency chain."""
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import unpack

WANTED = {24062: 'FightStatusAccDmgAndCastCustomDamageComponent',
          13148: 'FightSkillComponent', 13149: 'FightHitFixedComponent'}


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def build():
    payload_path = ROOT / 'unity/Assets/Official/Combat/battle-rules.json'
    payload = json.loads(payload_path.read_text('utf8'))
    versions = []
    for version in ('854_156383', '871_156598'):
        apk = Apk(manifest=version)
        found, sources, status_ai, damage_process, status_end = {}, [], None, None, None
        for path, pointer in apk.container('config_ec'):
            if path.rsplit('/', 1)[-1] not in ('FightSkill_part157.g.bytes', 'FightSkill_part703.g.bytes'):
                continue
            obj = pointer.deref()
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            try:
                rows = unpack(raw)[0]
            except Exception:
                rows = unpack(raw[2:])[0]
            provenance = apk.provenance(obj)
            provenance.update(file=path, payload_sha256=hashlib.sha256(raw).hexdigest())
            sources.append(provenance)
            for parent, identity, components in rows:
                if identity not in WANTED:
                    continue
                for typ, value in components:
                    if identity == 24062 and typ.removeprefix('Common.') == 'FightStatusComponent':
                        decoded = Formats().component(typ, value)
                        if decoded != payload['entities']['24062']['FightStatusComponent']:
                            raise ValueError('Official status/AI configuration differs from runtime: '+version)
                        status_ai = decoded['AI']
                    if identity == 24062 and typ.removeprefix('Common.') == 'FightStatusDamageProcessComponent':
                        decoded = Formats().component(typ, value)
                        if decoded != payload['entities']['24062']['FightStatusDamageProcessComponent']:
                            raise ValueError('Official damage-process configuration differs from runtime: '+version)
                        damage_process = decoded
                    if identity == 24062 and typ.removeprefix('Common.') == 'StatusEndComponent':
                        decoded = Formats().component(typ, value)
                        if decoded != payload['entities']['24062']['StatusEndComponent']:
                            raise ValueError('Official status-end configuration differs from runtime: '+version)
                        status_end = decoded
                    if typ.removeprefix('Common.') == WANTED[identity]:
                        decoded = Formats().component(typ, value)
                        if decoded != payload['entities'][str(identity)][WANTED[identity]]:
                            raise ValueError(f'Official {version} configuration differs from runtime: {identity}')
                        found[str(identity)] = {'parent': parent, 'component': WANTED[identity], 'value': decoded}
        if len(found) != len(WANTED) or status_ai != {'MoreHit': True} or damage_process is None or status_end is None:
            raise ValueError('Incomplete official accumulated-damage dependency chain: ' + version)
        versions.append({'manifest': version, 'inputs': sources, 'entities': found, 'status_ai': status_ai,
                         'damage_process': damage_process, 'status_end': status_end})
    text_path = ROOT / 'unity/Assets/Official/Combat/battle-texts.json'
    texts = json.loads(text_path.read_text('utf8'))
    description = next(row for row in texts['texts'] if row['key'] == 'entry_24062_desc')
    source_paths = [
        'unity/Assets/Official/Combat/battle-rules.json', 'unity/Assets/Official/Combat/battle-texts.json',
        'unity/Assets/Official/Combat/skill-rules.json',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/FightStatusAccDmgAndCastCustomDamageComponentInfo.cs',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/FightStatusTriggerSkillCfg.cs',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/HitDamageType.cs',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/DamageResultType.cs',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/FightStatusUtilsHandler.cs',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/FightStatusAiCfg.cs',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/FightStatusDamageProcessComponentInfo.cs',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/StatusEndComponentInfo.cs',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/StatusTriggerTarget.cs',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/DamageProcessPhase.cs',
        '.tools/source-packages/current/871_156598/official-code/common-source/Common/BattleFormulaHandler.cs']
    receipt = {'config_manifest': '854_156383', 'code_manifest': '871_156598',
               'language_manifest': texts['language_manifest'], 'versions': versions,
               'runtime_config_identical_in_both_versions': True,
               'description': description,
               'inputs': [{'path': path, 'sha256': sha(ROOT / path)} for path in source_paths],
               'generator_sha256': sha(__file__),
               'runtime': [{'path': 'unity/Assets/Game/Runtime/Official/Combat/'+name,
                            'sha256': sha(ROOT/'unity/Assets/Game/Runtime/Official/Combat'/name)} for name in (
                   'FightStatusAccumulatedDamageRuntime.cs','FightBattleExecutor.AccumulatedDamage.cs',
                   'FightBattleStatistics.cs','FightBattleReportBuilder.cs','FightBattleTargeting.cs','FightBattleExecutor.cs',
                   'FightOfficialCombatCatalog.cs','FightStatusLifecycle.cs','FightBattleState.cs',
                   'FightStatusCreatorDamageBoostRuntime.cs','FightBattleHitExecutor.cs','FightStatusPropertyRuntime.cs')],
               'status_property_inputs': {
                   'entity_prop': next(row for row in json.loads((ROOT/'unity/Assets/Official/Combat/skill-rules.json').read_bytes())['tables']['entity_prop'] if row['EntityId']==24062),
                   'rank_level_example': {'rank':1,'level':1,'sub_rank':1,'percent_231':2000,'fixed_235':4789}},
               'confirmed_offline_rules': {
                   'dead_creator_expiration': 'If the mark creator has died while the battle continues and the marked target remains alive, expiration still triggers the original damage chain and retains the original creator attribution. Do not revive the creator or admit ordinary dead-unit attacks.',
                   'creator_damage_boost': 'For damage from the mark creator, multiply pre-shield damage by one plus resolved status property 231, then directly add fixed property 235. Other actors and creator summons do not receive the bonus; healing is unchanged.',
                   'additional_source_credit': 'Allocate triggered actual HP loss proportionally to original actual HP contributions by source and skill; retain the independent mark hit. Exclude shield and overkill. Use the original DistributeLongProportional rounding/remainder helper.',
                   'more_hit_priority': 'Retain eligible targets and forced taunt/confusion constraints; prefer MoreHit marked attack targets before existing coverage/distance priorities.'},
               'scope': 'Official configuration, descriptions and the integer distribution helper are available. The server allocation, MoreHit comparator and damage-process hook are absent; their wiring follows the user-confirmed offline rules.'}
    target = ROOT / 'assets/reference/battle-rules/accumulated-damage-source-manifest.json'
    target.write_bytes((json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode('utf8'))
    print('Verified official 24062 -> 13148 -> 13149 in both manifests; runtime configuration is identical.')


if __name__ == '__main__':
    build()
