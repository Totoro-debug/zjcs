"""Port selected original PlayerUIBase/PlayerExpSlider without changing formulas."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeHudUI'


def substitute(text, before, after, expected=None):
    count = text.count(before)
    if not count or (expected is not None and count != expected):
        raise ValueError((before, count, expected))
    return text.replace(before, after)


def build():
    sources = {}
    outputs = {}
    for original, target in (('PlayerUIBase', 'HomeExperiencePlayerUIBase'), ('PlayerExpSlider', 'HomePlayerExpSlider')):
        source = CODE / 'client-source' / (original+'.cs')
        raw = source.read_bytes(); sources[source.relative_to(ROOT).as_posix()] = hashlib.sha256(raw).hexdigest()
        text = raw.decode('utf-8-sig').replace('\r\n','\n')
        if original == 'PlayerUIBase':
            text = substitute(text, 'public class PlayerUIBase : UIBase', 'public class HomeExperiencePlayerUIBase : UIBase', 1)
            position = text.index('{') + 1
            text = text[:position] + '''
    protected IHomeExperienceServices Bindings { get; private set; }
    public void Configure(IHomeExperienceServices bindings)
    {
        if (gameObject.activeInHierarchy) throw new InvalidOperationException("Bind the inactive original experience widget.");
        Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
    }
''' + text[position:]
            text = substitute(text, 'MeManager.MainPlayer', 'Bindings.MainPlayer')
            text = substitute(text, 'MeManager.Instance != null', 'Bindings != null && Bindings.ManagerAvailable')
            text = substitute(text, 'MeManager.Instance.OnPlayerUpdate', 'Bindings.OnPlayerUpdate')
        else:
            for line in ('using Common;\n', 'using Framework;\n', 'using Framework.DependencyInjection;\n'):
                text = substitute(text, line, '', 1)
            text = substitute(text, 'public class PlayerExpSlider : PlayerUIBase',
                'public class HomePlayerExpSlider : HomeExperiencePlayerUIBase', 1)
            text = substitute(text, '\t[Inject]\n\tprivate IPlayerExpRewardHandler PlayerExpRewardHandler { get; set; }',
                '\tprivate IHomeExperienceRewards PlayerExpRewardHandler => Bindings.Rewards;', 1)
            for before, after in (
                ('MeManager.MainPlayer', 'Bindings.MainPlayer'),
                ('Parsers.Instance.LevelExpParser', 'Bindings.LevelExpParser'),
                ('Parsers.Instance.LevelBlessInfoParser', 'Bindings.LevelBlessInfoParser'),
                ('Parsers.GameSettings.PlayerExpItemId', 'Bindings.PlayerExpItemId'),
                ('Parsers.Instance.PlayerSubRankInfoParser.Get', 'Bindings.ResolveSubRank'),
                ('PlayerBlessLevelUtils.GetExpRequestByBlessLevel', 'Bindings.LevelBlessInfoParser.GetExpRequestByBlessLevel'),
                ('LanguageUtils.GetSeasonNameByRank', 'Bindings.GetSeasonNameByRank'),
                ('PlanesInfoUtils.PlanesRank', 'Bindings.PlanesRank'),
                ('CultureNumberArg.Compact', 'Bindings.Compact'),
                ('MathUtils.Lerp(preExp, postExp, totalTime / mExpSliderPlayTime)', 'OriginalLerp(preExp, postExp, totalTime / mExpSliderPlayTime)'),
                ('Player mainPlayer', 'IHomeExperiencePlayer mainPlayer'),
                ('PlayerItemDataWrap itemData', 'HomeExperienceItem itemData'),
                ('AwardSourceData sourceData', 'object sourceData'),
                ('PlayerSeasonRecordDataWrap dataWrap', 'object dataWrap'),
                ('SubRank subRank', 'int subRank'),
                ('Rank rank', 'int rank'),
                ('LevelExpParser levelExpParser', 'IHomeLevelExpParser levelExpParser'),
                ('LevelBlessInfoParser levelBlessInfoParser', 'IHomeLevelBlessParser levelBlessInfoParser'),
                ('LevelBlessInfo levelBlessInfo', 'HomeLevelBlessInfo levelBlessInfo')):
                text = substitute(text, before, after)
            text = substitute(text,
                'mainPlayer.OnBlessLevelDataUpdate = (Action)Delegate.Combine(mainPlayer.OnBlessLevelDataUpdate, new Action(MainPlayer_OnBlessDataUpdate));',
                'mainPlayer.OnBlessLevelDataUpdate += MainPlayer_OnBlessDataUpdate;', 1)
            text = substitute(text,
                'mainPlayer.OnBlessLevelDataUpdate = (Action)Delegate.Remove(mainPlayer.OnBlessLevelDataUpdate, new Action(MainPlayer_OnBlessDataUpdate));',
                'mainPlayer.OnBlessLevelDataUpdate -= MainPlayer_OnBlessDataUpdate;', 1)
            # Verbatim long interpolation from Common.MathUtils.Lerp; retain the
            # decimal multiplication, cast-before-add and original subtraction.
            math = CODE / 'common-source/Common/MathUtils.cs'
            math_raw = math.read_bytes(); sources[math.relative_to(ROOT).as_posix()] = hashlib.sha256(math_raw).hexdigest()
            formula = 'return (long)((decimal)(end - start) * (decimal)Clamp(t, 0f, 1f)) + start;'
            assert formula in math_raw.decode('utf-8-sig')
            position = text.rfind('}')
            text = text[:position] + '\n    private static long OriginalLerp(long start, long end, float t)\n    {\n        '+formula.replace('Clamp(', 'Mathf.Clamp(')+'\n    }\n' + text[position:]
        text = ('// Original selected client implementation; only player/parser/reward ports differ.\n'
            'using System;\nusing Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;\n'
            'namespace Zjcs.Official.HomeHudUI\n{\n'
            'using Object = UnityEngine.Object;\nusing MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\n'+text+'\n}\n')
        path = DEST / (target+'.cs')
        if path.exists() and not path.read_text('utf8').startswith('// Original selected client implementation; only player/parser/reward ports differ.'):
            raise ValueError('Preserve existing runtime implementation: '+str(path))
        path.write_text(text,'utf8',newline='\n')
        meta = Path(str(path)+'.meta')
        if not meta.exists():
            guid = hashlib.md5(('zjcs:home-experience:'+target).encode()).hexdigest()
            meta.write_text('fileFormatVersion: 2\nguid: '+guid+'\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n','utf8',newline='\n')
        for file in (path,meta): outputs[file.relative_to(ROOT).as_posix()] = hashlib.sha256(file.read_bytes()).hexdigest()
    interface=DEST/'IHomeExperienceServices.cs'
    meta=Path(str(interface)+'.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(b'zjcs:home-experience:services').hexdigest()
            +'\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n','utf8',newline='\n')
    for file in (interface,meta): outputs[file.relative_to(ROOT).as_posix()] = hashlib.sha256(file.read_bytes()).hexdigest()
    return dict(sources=sources,outputs=outputs)


if __name__ == '__main__':
    print(json.dumps(build(),ensure_ascii=False,indent=2))
