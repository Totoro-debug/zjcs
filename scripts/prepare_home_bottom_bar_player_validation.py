"""Stage real local-player queries in the existing native bottom-bar fixture."""
import hashlib
import json
import shutil
from pathlib import Path

from official_apk import ROOT
from validation_workspace import validation_directory, require_temporary_unity_project


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare(work=None):
    work = work or validation_directory()
    project = require_temporary_unity_project(work / 'unity')
    runtime = 'unity/Assets/Game/Runtime/Offline/HomeUI/OfflineHomeBottomBarPlayer.cs'
    for relative in (runtime, runtime + '.meta'):
        source = ROOT / relative
        target = work / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
    fixture = 'scripts/fixtures/home_bottom_bar_player_validation.cs'
    original_fixture = 'scripts/fixtures/home_bottom_bar_native_validation.cs'
    folder = project / 'Assets/HomeBottomBarPlayerValidation'
    folder.mkdir(exist_ok=True)
    source = (ROOT / original_fixture).read_text('utf8')
    replacements = {
        'HomeBottomBarNativeValidation': 'HomeBottomBarPlayerValidation',
        'HomeBottomBarNativeRunner': 'HomeBottomBarPlayerRunner',
        'public sealed class HomeBottomBarPlayerRunner': 'public sealed partial class HomeBottomBarPlayerRunner',
        'EditorSceneManager.NewScene(': 'Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");\n        EditorSceneManager.NewScene(',
        'AudioListener.volume=0;': 'Application.runInBackground=true;AudioListener.volume=0;',
        'var font=TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("HomeValidationFont"));':
            'var fontAssets=Zjcs.Offline.HomeHudUI.OfflineHomeHudAssets.LoadDefault();\n'
            '        playerFonts=fontAssets.CreateFontService();playerFonts.Initialize();\n'
            '        var font=fontAssets.defaultFont;',
        'var run=Check();': 'var steps=new Stack<IEnumerator>();steps.Push(Check());',
        'while(true){object current;try{if(!run.MoveNext())break;current=run.Current;}':
            'while(steps.Count>0){object current;try{if(!steps.Peek().MoveNext()){steps.Pop();continue;}current=steps.Peek().Current;}',
        '            yield return current;}':
            '            if(current is IEnumerator nested){steps.Push(nested);continue;}\n            yield return current;}',
        'view?.Dispose();HomeBottomBarPlayerValidation.Finish(error);':
            'view?.Dispose();playerFonts?.Dispose();HomeBottomBarPlayerValidation.Finish(error);',
        'view.Dispose();yield return null;\n        HomeBottomBarPlayerValidation.Finish':
            'view.Dispose();yield return null;playerFonts?.Dispose();\n        HomeBottomBarPlayerValidation.Finish',
        'Require(PublicBottomBar.Instance==null,"native singleton released");':
            'Require(PublicBottomBar.Instance==null,"native singleton released");\n        yield return CheckLocalPlayer(canvas.transform,font,assets);',
    }
    for before, after in replacements.items():
        if before not in source:
            raise ValueError('Original native fixture changed; review real-player composition: ' + before)
        source = source.replace(before, after)
    (folder / 'HomeBottomBarPlayerRunner.cs').write_text('#if UNITY_EDITOR\n' + source + '\n#endif\n', 'utf8', newline='\n')
    (folder / 'PlayerChecks.cs').write_text('#if UNITY_EDITOR\n' + (ROOT / fixture).read_text('utf8') + '\n#endif\n', 'utf8', newline='\n')
    references = ['Unity.ugui', 'Unity.TextMeshPro', 'Zjcs.Official.HomeHudUI',
                  'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI', 'Zjcs.Offline.HomeUI',
                  'Zjcs.Offline.Progression', 'Zjcs.Offline.CharacterCreation', 'Zjcs.Offline.HomeHudUI',
                  'Zjcs.Offline.State']
    (folder / 'Zjcs.HomeBottomBarPlayerValidation.asmdef').write_text(json.dumps(dict(
        name='Zjcs.HomeBottomBarPlayerValidation', references=references), indent=2) + '\n', 'utf8', newline='\n')
    code = '.tools/source-packages/current/884_156692/official-code/'
    sources = [code + suffix for suffix in (
        'client-source/PublicBottomBar.cs', 'client-source/PublicBottomBarCharacterBtn.cs',
        'common-source/Common/PlayerBlessLevelUtils.cs',
        'common-source/Common/SystemLevelLimitInfoParser.cs',
        'common-source/Common/PlayerSubRankInfoParser.cs', 'common-source/Common/Sex.cs')]
    sources += ['assets/reference/home-rules-current-source-manifest.json',
                'unity/Assets/Game/Runtime/Official/ProfessionUI/FontAssetFontService.cs',
                'unity/Assets/Game/Runtime/Offline/HomeHudUI/OfflineHomeHudAssets.cs',
                'unity/Assets/Official/Home/home-rules-current.json',
                'scripts/prepare_home_bottom_bar_player_validation.py',
                original_fixture, fixture]
    receipt = dict(manifest='884_156692', inputs={name: sha(ROOT / name) for name in sources},
        runtime={runtime: sha(ROOT / runtime), runtime + '.meta': sha(ROOT / (runtime + '.meta'))},
        original_methods=['PlayerBlessLevelUtils.CheckSubRankUpgradeShow',
                          'SystemLevelLimitInfoParser.SetPlayerMaxLevelLimit',
                          'SystemLevelLimitInfoParser.GetPlayerBaseLevelLimit'],
        offline_policy='Remove only the confirmed PlanesInfo.PlanesRank gate; retain the next configured '
                       'sub-rank, current personal level and strictly increasing base-level cap. No promotion '
                       'cost check belongs to this original display predicate.',
        scope='Real local player queries and explicit owner notifications in the native bottom-bar '
              'fixture. Full entry/tip services and production HomeFeatureRuntime integration remain pending.')
    (ROOT / 'assets/reference/home-bottom-bar-player-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged native bottom-bar validation with real local-player queries', flush=True)


if __name__ == '__main__':
    prepare()
