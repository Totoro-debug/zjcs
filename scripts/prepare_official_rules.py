"""Export only fully consumed APK tables, with source byte and constructor hashes."""
import hashlib
import json
from pathlib import Path
import re
from official_apk import ROOT
from official_tables import Constructors, SOURCE

TABLES = ['item', 'item_piece_merge', 'skill', 'skill_rank', 'skill_plan', 'skill_skin', 'score_level_group',
          'profession_base', 'prop_cfg', 'pet', 'pet_food', 'pet_evolution', 'pet_ability', 'pet_potential',
          'treasure', 'treasure_levelup_material', 'cooking', 'furniture', 'furniture_skin',
          'home_level', 'home_theme_skin', 'home_environment_skin', 'home_decoration',
          'furniture_level_rule', 'hang_up', 'hang_up_reward', 'hang_up_upgrade_material',
          'fast_hang_up', 'level_exp', 'level_number', 'level_bless', 'conditions', 'function_limit',
          'chest', 'reward_rule', 'random_reward', 'gacha', 'gacha_pity_probability', 'ai_priority',
          'shop', 'shop_goods', 'shop_goods_group', 'shop_goods_price_off', 'home_visit_shop', 'home_visit_shop_goods', 'legion_donate', 'game_settings',
          'equip', 'equip_type', 'equip_rank', 'equip_upgrade_material', 'equip_upgrade_bless',
          'equip_piece_merge', 'equip_attributes', 'equip_attributes_props',
          'player_subrank', 'profession_level_cost', 'level_prop_default_value',
          'pet_battle', 'pet_upgrade_bless', 'pet_ability_prop', 'pet_ability_prop_scale_factor',
          'treasure_bless', 'treasure_upgrade_material', 'task', 'achievement',
          'stage', 'stage_type', 'stage_entry', 'stage_monster', 'gacha_additional_awards', 'treasure_gacha',
          'direct_pay','periodic_card_pay','money_cost','daily_pay_gift','weekly_pay_gift',
          'activity_sign_days','activity_entry','activity_entry_group',
          'stage_map_explore_boss','activity_festival_boss','activity_festival_boss_layer','activity_festival_boss_npc',
          'fight_stage_status', 'player_robot_invite', 'npc_level_detail', 'npc_profession_detail',
          'character_skin_color','character_skin_dye','npc',
          'npc_gift','npc_friendship_level','npc_friendship_tag','npc_talk','npc_unlock_item',
          'item_merge','gem','gem_merge']

def build():
    c = Constructors()
    mapping = {m[2]:m[1] for m in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', c.source('Tables'))}
    output, records, missing = {}, {}, {}
    for name in TABLES:
        path = ROOT/'.tools/source-packages/official-data'/(name+'.g.bytes')
        if name not in mapping or not path.exists():
            missing[name] = 'Not in packaged table set'
            continue
        raw = path.read_bytes()
        output[name] = c.table(raw, mapping[name])
        source = (SOURCE/(mapping[name]+'.cs')).read_bytes()
        records[name] = {'text_asset': name+'.g', 'bytes': len(raw),
            'sha256': hashlib.sha256(raw).hexdigest(), 'consumed_bytes': len(raw),
            'constructor': 'Console.dll/Common/'+mapping[name]+'.cs',
            'constructor_sha256': hashlib.sha256(source).hexdigest()}
    art = ROOT/'assets/official'
    art.mkdir(exist_ok=True)
    (art/'rules.json').write_text(json.dumps(output,ensure_ascii=False,separators=(',',':')),encoding='utf8')
    reference = ROOT/'assets/reference/official-city'
    reference.mkdir(exist_ok=True)
    code_root = ROOT/'.tools/source-packages/official-code'
    code_files = ['client-source/ExploreBossTeamDialog.cs',
        'client-source/ActivityFestivalBossPanelBattleContent.cs',
        'client-source/ActivityFestivalBossPanelDetailContent.cs',
        'client-source/TeamInviteRobotPanel.cs',
        'client-source/TeamInviteRobotItem.cs',
        'client-source/TeamInviteRobotComparer.cs',
        'common-source/TeamInvitePlayerInfoData.cs',
        'common-source/Common/NpcFightData.cs',
        'common-source/Common/ActivityFestivalBossUtils.cs',
        'common-source/Common/PlayerActivityFestivalBossDataExt.cs',
        'client-source/RoadblockTeamPropStageTouchUI.cs',
        'client-source/RoadblockPropStageTouchUI.cs',
        'common-source/Common/FightStageStatusInfoParser.cs',
        'client-source/NpcAdventureSelectNpcDialog.cs']
    evidence = {'apk_sha256':'ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9',
        'bundle':'defaultpackage_assets_config_luban.bundle',
        'tables':records,'missing':missing,
        'code':{name:hashlib.sha256((code_root/name).read_bytes()).hexdigest() for name in code_files},
        'rules_sha256':hashlib.sha256((art/'rules.json').read_bytes()).hexdigest()}
    (reference/'rules-manifest.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2),encoding='utf8')
    source_manifest_path = reference/'source-manifest.json'
    source_manifest = json.loads(source_manifest_path.read_text(encoding='utf8'))
    source_manifest.setdefault('data_sha256', {})['rules.json'] = evidence['rules_sha256']
    source_manifest['explicit_data_resources'] = sorted(set(source_manifest.get('explicit_data_resources', [])) | {'rules.json'})
    source_manifest_path.write_text(json.dumps(source_manifest,ensure_ascii=False,separators=(',',':')),encoding='utf8')
    print('Verified runtime tables:',len(output),'missing:',missing)

if __name__=='__main__':build()
