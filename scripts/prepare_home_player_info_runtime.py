"""Port the selected original player-name, level and combat-rating observers."""
import hashlib
import json
from pathlib import Path

from prepare_home_experience_runtime import CODE, ROOT, substitute

DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomePlayerInfoUI'
HEADER = '// Selected original player-info implementation; only service boundaries and C# decompilation repairs differ.\n'


def metadata(path, folder=False):
    target = Path(str(path)+'.meta')
    if not target.exists():
        key = hashlib.md5(('zjcs:home-player-info:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        target.write_text('fileFormatVersion: 2\nguid: '+key+'\n'+('folderAsset: yes\nDefaultImporter:\n' if folder else
            'AssemblyDefinitionImporter:\n' if path.suffix == '.asmdef' else 'MonoImporter:\n  serializedVersion: 2\n')+'  externalObjects: {}\n', 'utf8', newline='\n')
    return target


def build():
    DEST.mkdir(parents=True, exist_ok=True)
    sources, outputs = {}, {}
    for original in ('PlayerNameText', 'PlayerLevelText', 'PlayerCombatRatingText', 'IntroductionPopBtn', 'NumberWithSizeArg'):
        source = CODE / ('framework-firstpass-source/Framework' if original == 'NumberWithSizeArg' else 'client-source') / (original+'.cs')
        raw = source.read_bytes(); sources[source.relative_to(ROOT).as_posix()] = hashlib.sha256(raw).hexdigest()
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        if original == 'NumberWithSizeArg':
            text = substitute(text, 'namespace Framework;\n', '', 1)
        else:
            for using in ('using Framework;\n', 'using Common;\n'):
                text = text.replace(using, '')
            text = text.replace('MeManager.MainPlayer', 'InfoBindings.MainPlayer')
            if original == 'IntroductionPopBtn':
                position = text.index('{')+1
                text = text[:position]+'''
    private IHomePlayerInfoBindings InfoBindings;
    public void Configure(IHomePlayerInfoBindings bindings)
    {
        if (gameObject.activeInHierarchy) throw new InvalidOperationException("Bind the inactive original introduction button.");
        InfoBindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
    }
'''+text[position:]
                text = substitute(text, 'LanguageUtils.TextKeyArgWrap', 'HomeInfoTextKey')
                text = substitute(text, '((UnityEvent)mOpenBtn.onClick)', 'mOpenBtn.onClick')
                before = '''WindowManager.Instance.Popup(this, (PopTipMutilDesc go) =>
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			go.Init(mTitleTextArgs, mContent, Input.mousePosition);
		});'''
                text = substitute(text, before, 'InfoBindings.PopupIntroduction(this, mTitleTextArgs, mContent, InfoBindings.PointerPosition);', 1)
            else:
                text = substitute(text, ' : PlayerUIBase', ' : HomePlayerInfoUIBase', 1)
            if original == 'PlayerLevelText':
                for before, after in (
                    ('SubRank subRank', 'int subRank'), ('PlayerSeasonRecordDataWrap dataWrap', 'object dataWrap'),
                    ('StateSwitcher<bool>', 'WorldStateSwitcher<bool>'),
                    ('LanguageUtils.TextKeyArgWrap', 'HomeInfoTextKey'),
                    ('LanguageManager.Instance.GetText', 'InfoBindings.Text'),
                    ('FeatureCompatibility.IsSupported(FeatureType.TMPEffectFontSizeIssueFixed)', 'InfoBindings.TMPEffectFontSizeIssueFixed'),
                    ('TryGetComponent<TMPEffect>(ref val)', 'TryGetComponent<TMPEffect>(out val)')):
                    text = substitute(text, before, after)
        # Keep MonoScript identity explicit while sharing the existing faithful
        # UI/language/TMP implementations; serialized field names are unchanged.
        target = 'Home'+original if original != 'NumberWithSizeArg' else 'HomeNumberWithSizeArg'
        for before, after in (('PlayerNameText','HomePlayerNameText'), ('PlayerLevelText','HomePlayerLevelText'),
                              ('PlayerCombatRatingText','HomePlayerCombatRatingText'), ('IntroductionPopBtn','HomeIntroductionPopBtn'),
                              ('NumberWithSizeArg','HomeNumberWithSizeArg')):
            text = text.replace(before, after)
        text = HEADER+'using System;\nusing Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;\nusing Zjcs.Official.World.NativeLabelUI.Framework;\nnamespace Zjcs.Official.HomePlayerInfoUI\n{\nusing Object = UnityEngine.Object;\nusing MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\n'+text+'\n}\n'
        path = DEST / (target+'.cs')
        if path.exists() and not path.read_text('utf8').startswith(HEADER):
            raise ValueError('Preserve unrelated player-info source: '+str(path))
        path.write_text(text,'utf8',newline='\n')
        metadata(path)
    for path in (DEST, *sorted(DEST.iterdir())):
        if path.suffix == '.meta': continue
        meta = metadata(path, path.is_dir())
        for file in ((meta,) if path.is_dir() else (path, meta)):
            outputs[file.relative_to(ROOT).as_posix()] = hashlib.sha256(file.read_bytes()).hexdigest()
    return dict(sources=sources, outputs=outputs)


if __name__ == '__main__':
    print(json.dumps(build(), ensure_ascii=False, indent=2))
