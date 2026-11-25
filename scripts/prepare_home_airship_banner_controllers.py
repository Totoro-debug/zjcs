"""Record the verified original sources of the airship banner controllers."""
import hashlib
import json
from pathlib import Path
from official_apk import ROOT
from official_source import OfficialSource


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    source = ROOT / '.tools/source-packages/current' / OfficialSource().version / 'official-code'
    originals = [source / 'client-source/AirshipActivityBannerItem.cs',
                 source / 'framework-firstpass-source/MultiLanguageUI.cs',
                 source / 'framework-firstpass-source/MultiLanguageSpriteRenderer.cs',
                 source / 'common-source/Common/LanguageUtils.cs',
                 source / 'common-source/Common/ActivityEntryType.cs',
                 ROOT / 'unity/Assets/Game/Runtime/Official/HomeFunctionMenuUI/ActivityEntryType.cs']
    adapted = [ROOT / 'unity/Assets/Game/Runtime/Official/HomeAirship/AirshipActivityBannerItem.cs',
               ROOT / 'unity/Assets/Game/Runtime/Official/HomeAirship/AirshipActivityBannerItem.cs.meta',
               ROOT / 'unity/Assets/Game/Runtime/Official/HomeAirship/Zjcs.Official.HomeAirship.asmdef',
               ROOT / 'unity/Assets/Game/Runtime/Official/HomeAirship/Zjcs.Official.HomeAirship.asmdef.meta',
               ROOT / 'unity/Assets/Game/Runtime/Official/HomeAirship.meta',
               ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldMultiLanguageSpriteRenderer.cs',
               ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldMultiLanguageSpriteRenderer.cs.meta',
               ROOT / 'scripts/fixtures/home_language_sprite_validation.cs',
               ROOT / 'scripts/fixtures/home_airship_banner_title_validation.cs',
               Path(__file__).resolve()]
    proof = {'manifest': OfficialSource().version,
             'inputs': {path.relative_to(ROOT).as_posix(): sha(path) for path in originals},
             'outputs': {path.relative_to(ROOT).as_posix(): sha(path) for path in adapted},
             'scope': 'Original banner title key and reusable language-sprite lifecycle; local activity and resource owners remain separate.'}
    output = ROOT / 'assets/reference/home-airship-banner-controller-source-manifest.json'
    if output.exists():
        if json.loads(output.read_text(encoding='utf8')) != proof:
            raise ValueError('Preserve existing banner controller receipt')
        print('Verified banner controller receipt')
        return
    output.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
    print('Recorded banner controller source receipt')


if __name__ == '__main__':
    main()
