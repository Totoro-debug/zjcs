"""Recover official score reward configuration and verify the local source boundary."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_unity_map13_sources import meta


OUTPUT = ROOT / 'unity/Assets/Game/Resources/PetGacha/score-reward-ui.json'
RECEIPT = ROOT / 'assets/reference/pet-score-reward-source-manifest.json'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    apk = Apk.current()
    try:
        code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        constructors = Constructors(code)
        tables = []

        def table(name, kind):
            obj = apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            tables.append(dict(name=name, source=apk.provenance(obj),
                               object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
                               payload_sha256=hashlib.sha256(raw).hexdigest()))
            return constructors.table(raw, kind)

        settings = table('game_settings', 'TbGameSettings')
        functions = table('function_limit', 'TbFunctionLimitInfo')
        data = dict(batchAchievementTypes=settings['BatchGetAchievementTypes'],
                    panelShowCount=settings['AchievementPanelShowCount'],
                    sameTypeAchievementTypes=settings['AchievementTypeListForShowAllInfosInSameType'],
                    topAchievementTypes=settings['AchievementTopTypeList'],
                    groupFunction=next(row for row in functions if row['Type'] == 136 and row['Key0'] == row['Key1'] == 0),
                    batchFunction=next(row for row in functions if row['Type'] == 189 and row['Key0'] == row['Key1'] == 0))
        raw = (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode()
        if check:
            if OUTPUT.read_bytes() != raw:
                raise ValueError('Original score reward configuration differs')
        else:
            if OUTPUT.exists() and (not RECEIPT.exists() or sha(OUTPUT) != json.loads(RECEIPT.read_bytes())['outputs'][OUTPUT.relative_to(ROOT).as_posix()]):
                raise ValueError('Preserve another score reward configuration edit')
            OUTPUT.write_bytes(raw)
            meta(OUTPUT)
        paths = [Path(__file__), ROOT / 'scripts/official_tables.py', ROOT / 'scripts/official_apk.py',
                 ROOT / 'unity/Packages/manifest.json',
                 ROOT / 'unity/Assets/Official/Zjcs.Official.ConvertedSourceDescriptors.asmdef',
                 ROOT / 'unity/Assets/Official/Zjcs.Official.ConvertedSourceDescriptors.asmdef.meta',
                 ROOT / 'assets/reference/pet-gacha-controls-native-source-manifest.json',
                 ROOT / 'assets/reference/partner-gift-controller-source-manifest.json',
                 ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerProfileAssetCatalog.asset',
                 ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfflinePetGachaService.cs',
                 ROOT / 'unity/Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.PetGachaControls.cs',
                 ROOT / 'scripts/test_home_fruit_tree_weather.ps1']
        paths += [ROOT / 'scripts/test_pet_gacha_ui_mesh.ps1', ROOT / 'scripts/test_unity_compressed_mesh.py',
                  ROOT / 'unity/Assets/Game/Editor/Home/OfficialUiMeshValidation.cs',
                  ROOT / 'unity/Assets/Game/Editor/Home/OfficialUiMeshValidation.cs.meta']
        paths += [code / path for path in ['client-source/ScoreRewardUI.cs', 'client-source/AdditionInfoTip.cs',
                  'common-source/Common/AchievementUtils.cs', 'common-source/Common/PlayerAchievementRecordDataWrap.cs',
                  'common-source/Common/LanguageUtils.cs', 'common-source/Common/FunctionType.cs', 'common-source/Common/Player.cs',
                  'common-source/Common/AchievementTaskPetGachaScoreCount.cs',
                  'common-source/Common/AchievementTaskBlackboardCountTypeBase.cs',
                  'common-source/Common/AchievementDataPetGachaScoreCount.cs',
                  'common-source/Common/AchievementDataPetGacha.cs',
                  'common-source/Common/BlackboardDataType.cs', 'common-source/Common/BlackboardDataKey.cs',
                  'client-source/ItemIconManager.cs', 'client-source/ItemIconManagerConfig.cs', 'client-source/IconConfigPool.cs',
                  'client-source/GachaPanelUtils.cs', 'client-source/GachaBaseSetting.cs',
                  'common-source/Common/TimeUtils.cs']]
        paths += [code / 'common-source/Common' / (name + '.cs') for name in constructors.sources if
                  (code / 'common-source/Common' / (name + '.cs')).is_file()]
        paths += [path for folder in ['Official/GachaUI', 'Offline/GachaUI'] for path in
                  (ROOT / 'unity/Assets/Game/Runtime' / folder).iterdir() if path.is_file()]
        proof = dict(manifest=apk.version, tables=tables, inputs={p.relative_to(ROOT).as_posix(): sha(p) for p in paths},
                     outputs={p.relative_to(ROOT).as_posix(): sha(p) for p in (OUTPUT, Path(str(OUTPUT) + '.meta'))},
                     adaptation='Original score UI branches and configuration; local single and atomic available-group claims. Full detail window and panel remain separate work.')
        if check:
            if json.loads(RECEIPT.read_bytes()) != proof:
                raise ValueError('Score reward source boundary differs')
            print('Original score reward source and local binding verified')
        else:
            RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    finally:
        apk.reader.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
