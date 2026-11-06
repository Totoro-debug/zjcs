"""Bind the exact original activity HP subtree and its Windows resource closure."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_activity_battle_team_native import TeamBinding, build, install
from prepare_partner_card_native import guid, sha, script_id
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory


class HealthBinding(TeamBinding):
    def chinese_font(self, records):
        # The HP preview uses a Japanese font. Load the configured CN asset
        # explicitly before the shared original language-font binding runs.
        config, = [row for row in records if row['path'] ==
            'Assets/TextMesh Pro/Assets/Fonts & Materials/MultiLanguageFontConfig.json']
        obj = self.recovery.apk.asset(config['bundle'], config['path'])
        name = json.loads(obj.read().m_Script)['RegionFontAssetDict']['CN']['Default']
        row, = [row for row in records if row['path'] == name or row['address'] == name]
        obj = self.recovery.apk.asset(row['bundle'], row['path'])
        native = self.native / row['path'].removeprefix('Assets/')
        value = guid(Path(str(native) + '.meta'))
        self.objects[value] = obj
        self.load(value)
        return super().chinese_font(records)

    def register_scripts(self):
        super().register_scripts()
        path = ROOT / 'unity/Assets/Game/Runtime/Official/ActivityBattleUI/ActivityDomainBossHpView.cs'
        pointer = dict(fileID=11500000, guid=guid(metadata(path)), type=3)
        self.scripts['ActivityDomainBossHpView'] = pointer
        self.script_guids.add(pointer['guid'])
        self.script_sources['ActivityDomainBossHpView'] = dict(path=path.relative_to(ROOT).as_posix(),
            pointer=pointer, sha256=sha(path.read_bytes()), meta_sha256=sha(metadata(path).read_bytes()))
        path = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/SlicedFilledImage.cs'
        pointer = dict(fileID=11500000, guid=guid(metadata(path)), type=3)
        self.scripts['SlicedFilledImage'] = pointer
        self.class_ids[script_id('', 'SlicedFilledImage')] = pointer
        self.script_guids.add(pointer['guid'])
        self.script_sources['SlicedFilledImage'] = dict(path=path.relative_to(ROOT).as_posix(),
            pointer=pointer, sha256=sha(path.read_bytes()), meta_sha256=sha(metadata(path).read_bytes()))


def decorate(binding, panel, subtree, selection):
    consumer, = [row for row in panel['scripts'] if row['type'] == 'ActivityDomainBossPanelBattleContent']
    fields = panel['documents'][consumer['nativeFileID']]['MonoBehaviour']
    component_id = 114000000000000001
    if component_id in subtree['documents']: raise ValueError('HP view component identity collision')
    root = subtree['root']
    subtree['documents'][component_id] = dict(MonoBehaviour=dict(m_ObjectHideFlags=0,
        m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=root), m_Enabled=1,
        m_EditorHideFlags=0, m_Script=binding.scripts['ActivityDomainBossHpView'], m_Name='',
        m_EditorClassIdentifier='', mHpSlider=fields['mHpSlider'], mHpText=fields['mHpText']))
    subtree['documents'][root]['GameObject']['m_Component'].append(dict(component=dict(fileID=component_id)))
    selection['hp_fields'] = {key: fields[key] for key in ('mHpSlider', 'mHpText')}
    selection['added_component'] = component_id
    selection['adaptation'] = ('Exact original HpSliderGo subtree, root parent detached only. '
        'Preserve every original object/component/field; add the extracted RefreshContent HP presenter '
        'with original mHpSlider and mHpText references.')
    source = ROOT / '.tools/source-packages/current' / binding.recovery.apk.version / 'official-code/client-source/ActivityDomainBossPanelBattleContent.cs'
    binding.inputs[source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())


def main():
    binding = HealthBinding(validation_directory())
    try:
        result = build(binding, field='mHpSlider', parent_levels=1, decorate_selection=decorate,
            prefab_name='ActivityDomainBossHpView', group='Health')
        install(binding, *result, group='Health', receipt_name='activity-health-native-source-manifest.json',
            extra_helpers=('prepare_activity_battle_health_native.py',))
    finally:
        binding.recovery.apk.archive.close()


if __name__ == '__main__': main()
