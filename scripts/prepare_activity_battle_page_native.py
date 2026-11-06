"""Recover the complete original domain-boss page through the common UI binder."""
import json
from pathlib import Path

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_activity_battle_rank_native import RankBinding
from prepare_activity_battle_team_native import build, install
from prepare_partner_card_native import guid, sha, script_id
from validation_workspace import validation_directory


class PageBinding(RankBinding):
    resource_group = 'Activity'

    def replace(self, value):
        if isinstance(value, dict) and set(value) == {'class', 'ns', 'asm'}:
            if value['class'] in ('TipDotActivityDomainBossDailyChallengeConditionNode',
                                  'TipDotActivityDomainBossHasKillAwardConditionNode') and value['ns'] == '' and value['asm'] == 'Assembly-CSharp':
                return dict(**{'class': value['class'], 'ns': 'Zjcs.Official.ActivityBattleUI', 'asm': 'Zjcs.Official.ActivityBattleUI'})
            if value['class'] in ('TipDotActivityDomainBossDailyChallengeConditionNode',
                                  'TipDotActivityDomainBossHasKillAwardConditionNode') and value['ns'] == 'Zjcs.Official.ActivityBattleUI' and value['asm'] == 'Zjcs.Official.ActivityBattleUI':
                return value
        return super().replace(value)

    def register_scripts(self):
        super().register_scripts()
        for name in ('ActivityDomainBossPanel', 'ActivityDomainBossPanelDetailContent',
                     'ActivityDomainBossPanelBattleContent', 'ActivityInfoUI', 'ActivityDomainBossPreviewContent',
                     'MultiFontTextMaterial', 'NavigateBtn', 'SecretAreaBattleButtons', 'MonsterUI',
                     'CountdownText', 'CountdownView'):
            path = ROOT / 'unity/Assets/Game/Runtime/Official/ActivityBattleUI' / (name + '.cs')
            pointer = self.script_pointer(path)
            self.scripts[name] = pointer
            self.class_ids[script_id('', name)] = pointer
        for namespace, name, relative in (
            ('', 'AttachmentFollower', 'Official/BattleSpinePlayables/AttachmentFollower.cs'),
            ('', 'RotateAnimation', 'Official/WorldUI/WorldRotateAnimation.cs'),
            ('', 'ShowRemainTimeInfo', 'Official/WorldUI/WorldShowRemainTimeInfo.cs'),
            ('Coffee.UIExtensions', 'UIParticle', 'Presentation/Opening/OfficialCoffeeUIParticle/UIParticle.cs'),
        ):
            path = ROOT / 'unity/Assets/Game/Runtime' / relative
            pointer = self.script_pointer(path)
            self.scripts['.'.join(filter(None, (namespace, name)))] = pointer
            self.class_ids[script_id(namespace, name)] = pointer
        for name in ('ActivityNodeInstanceInfo', 'TipDotActivityDomainBossDailyChallengeConditionNode',
                     'TipDotActivityDomainBossHasKillAwardConditionNode'):
            path = ROOT / 'unity/Assets/Game/Runtime/Official/ActivityBattleUI' / (name + '.cs')
            for source in (path, Path(str(path) + '.meta')):
                self.inputs[source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())

    def complete_weapon_parts(self, selection):
        super().complete_weapon_parts(selection)
        path = 'Assets/UIEffect/FontMaterial/FZY4K/Christmas/FZY4K - ChristmasGuai.g.mat'
        records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        entry, = [row for row in records if row['path'] == path]
        original = self.recovery.apk.asset(entry['bundle'], path)
        source_guid = guid(Path(str(self.native / path.removeprefix('Assets/')) + '.meta'))
        self.objects[source_guid] = original
        material = self.load(source_guid)
        catalogs = [data for relative, _, data in self.queue
                    if relative.as_posix() == 'Resources/DailyChallenges/' + self.resource_group + '/PartAssets.asset']
        catalog, = catalogs
        fields, = [node['MonoBehaviour'] for node in catalog.values() if 'MonoBehaviour' in node]
        fields['assets'].append(dict(Path='FZY4K - ChristmasGuai.g', Asset=material))
        selection['language_font_material'] = dict(path=path, pointer=material)


def main():
    binding = PageBinding(validation_directory())
    try:
        result = build(binding, prefab_name='ActivityDomainBossPanel', group=binding.resource_group,
            complete_template=dict(name='ActivityDomainBossPanel',
                bundle='defaultpackage_assets_prefabs_ui_activitydomainboss.bundle',
                native_relative='Prefabs/UI/ActivityDomainBoss/ActivityDomainBossPanel.prefab'),
            literal_text_keys=('ui_null',),
            extra_text_keys=('SecretAreeRankPanel_no_join', 'SecretAreeRankPanel_first_rank',
                'SecretAreeRankPanel_wait_desc', 'SecretAreeRankItem_damage_desc',
                'TopRangeType.Planes', 'TopRangeType.Domain', 'TopRangeType.Global',
                'activity_name_ActivityDomainBoss_209', 'activity_name_ActivityDomainBoss_212',
                'activity_name_ActivityDomainBoss_226'))
        binding.complete_rank_resources(result[2], result[1])
        binding.script_guids.update(value for _, value, _ in binding.queue)
        for _, _, data in binding.queue:
            if isinstance(data, dict):
                resolved = binding.replace(data)
                data.clear(); data.update(resolved)
        install(binding, *result, group=binding.resource_group,
            receipt_name='activity-page-native-source-manifest.json',
            extra_helpers=('prepare_activity_battle_page_native.py', 'prepare_activity_battle_rank_native.py'))
        binding.record_actor_receipt(ROOT / 'assets/reference/daily-challenges/activity-page-native-source-manifest.json',
            'Complete original activity page hierarchy and local native resource closure recovered; production host and Play Mode validation pending.')
    finally:
        binding.recovery.apk.archive.close()


if __name__ == '__main__': main()
