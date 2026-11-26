"""Bind the complete original score-reward popup to the shared regular controllers."""
import argparse
import hashlib
import json
from pathlib import Path
from home_native_assets import install_staged_assets, write_staged_assets
from home_native_cache_verification import verify_native_cache
from official_source import OfficialSource
from prepare_current_partner_native import PartnerNativeRecovery
from prepare_markable_confirm_native import OfficialPopupBinding
from prepare_partner_card_native import ROOT, guid, sha
from prepare_shared_achievement_panel import build as shared_source
from validation_workspace import validation_directory

BUNDLE = 'defaultpackage_assets_prefabs_ui_gachapanel.bundle'
PREFAB = 'Prefabs/UI/GachaPanel/ScoreRewardDialog.prefab'
DEST = ROOT / 'unity/Assets/Official/Home/PetGacha/ScoreRewardDialog'
CATALOG = ROOT / 'unity/Assets/Game/Resources/PetGacha/OfficialScoreRewardDialogAssetCatalog.asset'
RECEIPT = ROOT / 'assets/reference/score-reward-dialog-native-source-manifest.json'


class ScoreRewardDialogBinding(OfficialPopupBinding):
    panel_name, bundle, prefab, identity = 'ScoreRewardDialog', BUNDLE, PREFAB, 'score-reward-dialog'
    literal_keys = ('{0}', '{0}/{1}')
    supplementary_scripts = dict(ScoreRewardDialog='GachaUI/ScoreRewardDialog.cs',
        AchievementPanel='ProfessionUI/AchievementPanel.cs', AchievementPanelBase='ProfessionUI/AchievementPanelBase.cs',
        AchievementPanelItem='ProfessionUI/AchievementPanelItem.cs',
        PanelTitle='ProfessionUI/PanelTitle.cs',
        ClampingScrollRect='WorldUI/WorldClampingScrollRect.cs', SlicedFilledImage='WorldUI/SlicedFilledImage.cs')
    required_keys = ('ui_pet_gacha_score_rule_text', 'ui_achievement_title_PetGachaScoreCount',
        'ui_achievement_content_PetGachaScoreCount', 'AchievementPanel.CanNotGetReward', 'AchievementPanel.AlreadyGotReward',
        'ui_ItemIcon_count_key')
    source_paths = ('client-source/ScoreRewardDialog.cs', 'client-source/AchievementPanel.cs',
        'client-source/AchievementPanelBase.cs', 'client-source/AchievementPanelItem.cs',
        'client-source/AchievementContentHandler.cs', 'framework-firstpass-source/UIBase.cs',
        'common-source/Common/AchievementUtils.cs')
    runtime_paths = ('Official/GachaUI/ScoreRewardDialog.cs', 'Official/GachaUI/ScoreRewardDialog.cs.meta',
        'Official/GachaUI/ScoreRewardServices.cs', 'Official/GachaUI/Zjcs.Official.GachaUI.asmdef',
        'Offline/GachaUI/OfficialScoreRewardDialogAssetCatalog.cs', 'Offline/GachaUI/OfficialScoreRewardDialogAssetCatalog.cs.meta',
        'Official/ProfessionUI/AchievementPanelData.cs', 'Official/ProfessionUI/ProfessionAchievementPanelSource.cs')

    def build(self):
        shared_source(check=True)
        folder, receipt = super().build()
        paths = [Path(__file__), ROOT / 'assets/reference/shared-achievement-panel-source-manifest.json']
        receipt['inputs'].update({path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in paths})
        controls = ROOT / 'assets/reference/pet-gacha-controls-native-source-manifest.json'
        reward = json.loads(controls.read_bytes())
        receipt['inputs'][controls.relative_to(ROOT).as_posix()] = sha(controls.read_bytes())
        receipt['reward_images'] = reward['score_reward_sprites']
        for relative, expected in reward['outputs'].items():
            if sha((ROOT / relative).read_bytes()) != expected:
                raise ValueError('Preserve original score reward image closure: ' + relative)
        return folder, receipt


def install(work, folder, receipt):
    previous = json.loads(RECEIPT.read_bytes()).get('outputs', {}) if RECEIPT.is_file() else {}
    receipt['outputs'] = install_staged_assets(work, folder, receipt['outputs'], DEST,
        'score-reward-dialog', previous_outputs=previous)
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/GachaUI/OfficialScoreRewardDialogAssetCatalog.cs'
    fields = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
        m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(Path(str(script) + '.meta')), type=3),
        m_Name=CATALOG.stem, m_EditorClassIdentifier='', prefab=receipt['roots']['ScoreRewardDialog'], texts=receipt['texts'],
        rewardImages=[dict(path=row['path'], assetGuid=row['asset_guid'], sprite=row['pointer'])
            for row in receipt['reward_images'].values()])
    staged = work / 'score-dialog-catalog'
    outputs = write_staged_assets([(Path(CATALOG.name), hashlib.md5(b'zjcs:score-reward-dialog:catalog').hexdigest(),
        {11400000: {'MonoBehaviour': fields}})], staged)
    receipt['outputs'].update(install_staged_assets(work, staged, outputs, CATALOG.parent,
        'score-reward-dialog-catalog', previous_outputs=previous))
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


def check():
    shared_source(check=True)
    receipt = json.loads(RECEIPT.read_bytes())
    for section in ('inputs', 'outputs', 'runtime_bindings'):
        for relative, expected in receipt[section].items():
            if sha((ROOT / relative).read_bytes()) != expected:
                raise ValueError('Original score detail source differs: ' + relative)
    for row in receipt['script_bindings'].values():
        path = ROOT / row['path']
        if guid(Path(str(path) + '.meta')) != row['pointer']['guid']:
            raise ValueError('Score detail script identity differs: ' + row['path'])
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Original score detail script binding differs: ' + row['path'])
    print('Original score reward dialog source and native closure verified')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    if args.check:
        check()
    else:
        source = OfficialSource()
        cache = ROOT / '.tools/source-packages/current' / source.version / 'home-score-reward-dialog-native'
        verify_native_cache(cache, [PREFAB], source.version)
        recovery = PartnerNativeRecovery(native_cache=cache, export_name='native-export', bundle=BUNDLE)
        try:
            work = validation_directory()
            folder, receipt = ScoreRewardDialogBinding(work, recovery=recovery).build()
            if args.install:
                install(work, folder, receipt)
            (work / RECEIPT.name).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        finally:
            recovery.apk.reader.close()
