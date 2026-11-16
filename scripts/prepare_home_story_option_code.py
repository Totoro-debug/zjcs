"""Adapt selected original story option controllers to explicit offline services."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from validation_workspace import validation_directory

VERSION = '884_156692'
SOURCE = ROOT / '.tools/source-packages/current' / VERSION / 'official-code/client-source'
NAMES = ('OptionBaseItem', 'OptionSlot', 'OptionTalkDialogItem', 'OptionTalkItemBaseText',
         'OptionTalkItemNormalText', 'OptionTalkItemRemainTimeText', 'OptionTalkItemDot',
         'DialogOptionDataBase', 'DialogOptionData', 'DialogOptionRemainTimeData',
         'IOptionTalkDotContext', 'EOptionTalkItemDotType', 'EOptionTalkItemTextPresentType')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def replace(text, old, new, count=1):
    if text.count(old) != count:
        raise ValueError('Review changed original option controller: ' + old[:100])
    return text.replace(old, new)


def strip_log(text, name):
    pattern = (r'(?m)^(\t+)(?:GLogger\w+InterpolatedStringHandler )?message = new '
               r'GLogger\w+InterpolatedStringHandler\([^\n]*\);\n.*?^\1Log\.Error\([^\n]*\);')
    return re.subn(pattern, lambda m: m[1] + 'Debug.LogError("' + name + ': original error branch.");',
                   text, flags=re.S)


def port(name, raw):
    text = raw.decode('utf-8-sig').replace('\r\n', '\n')
    text = re.sub(r'^using (?:Common|Framework(?:\.[\w.]+)?|R3);\n', '', text, flags=re.M)
    text = text.replace('using UnityEngine;\n', '')
    text = re.sub(r'\s*//IL_[^\n]*', '', text)
    text = re.sub(r'^\s*\[Rename\([^\n]*\)\]\n', '\n', text, flags=re.M)
    text, count = strip_log(text, name)
    expected_logs = {'OptionSlot': 1, 'DialogOptionData': 2}.get(name, 0)
    if count != expected_logs:
        raise ValueError('Review original logging branches: ' + name + '/' + str(count))
    text = text.replace('LanguageUtils.TextKeyArgWrap', 'StoryTextKeyArgWrap')
    text = text.replace('MultiLanguageText', 'WorldMultiLanguageText')
    text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
    text = text.replace('CountRedDotImage', 'WorldCountRedDotImage')
    text = text.replace('ShowRemainTimeInfo', 'WorldShowRemainTimeInfo')
    text = text.replace('ETimeSpanFormatType', 'WorldTimeSpanFormatType')
    if name == 'OptionBaseItem':
        text = text.replace(' : UIBase, IGameObjectPoolItem', ' : UIBase')
    if name == 'OptionTalkDialogItem':
        text = replace(text, '\tprivate DisposableBag mDisposableBag;\n\n\t[Inject]\n\tprivate IResourceLoader ResourceLoader { get; set; }',
            '\tprivate IStoryOptionSpriteLease mSpriteLease;\n\tpublic IStoryOptionServices Services { get; set; }')
        text = replace(text, '\t\tmDisposableBag = default;\n', '')
        text = replace(text, '\t\tmDisposableBag.Dispose();',
            '\t\tmSpriteLease?.Dispose();\n\t\tmSpriteLease = null;')
        text = replace(text, 'Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(iconPath), ref mDisposableBag).Result',
            '(mSpriteLease = Services.LoadSprite(iconPath)).Result')
        text = replace(text, '\t\tOptionTalkItemBaseText optionTalkItemTextCtrl = GetOptionTalkItemTextCtrl(mData);',
            '\t\tif (NormalText != null) NormalText.Services = Services;\n'
            '\t\tif (RemainTimeText != null) RemainTimeText.Services = Services;\n'
            '\t\tOptionTalkItemBaseText optionTalkItemTextCtrl = GetOptionTalkItemTextCtrl(mData);')
    if name == 'OptionTalkItemBaseText':
        text = replace(text, 'public class OptionTalkItemBaseText : MonoBehaviour\n{',
            'public class OptionTalkItemBaseText : MonoBehaviour\n{\n\tpublic IStoryOptionServices Services { get; set; }')
        text = replace(text, 'LanguageManager.Instance.ContainsKey(data.TipWrap.Key)', 'Services.ContainsText(data.TipWrap.Key)')
        text = replace(text, '\t\t\tNameText.SetKey(data.TextWrap.Key, data.TextWrap.Args);',
            '\t\t\tNameText.Localizer = Services.Localize;\n\t\t\tTipText.Localizer = Services.Localize;\n'
            '\t\t\tNameText.FontProvider = Services.Font;\n\t\t\tTipText.FontProvider = Services.Font;\n'
            '\t\t\tNameText.SetKey(data.TextWrap.Key, data.TextWrap.Args);')
    if name == 'OptionTalkItemRemainTimeText':
        text = replace(text, '\t\tDialogOptionRemainTimeData dialogOptionRemainTimeData = data as DialogOptionRemainTimeData;',
            '\t\tDialogOptionRemainTimeData dialogOptionRemainTimeData = data as DialogOptionRemainTimeData;\n'
            '\t\tShowRemainTimeCtrl.Clock = Services.Now;')
        text = replace(text, 'timeSpanFormat: dialogOptionRemainTimeData.TimeSpanFormat, customKey: dialogOptionRemainTimeData.CustomKey, callBack: OnTimeEnd',
            'timeFormat: dialogOptionRemainTimeData.TimeSpanFormat, key: dialogOptionRemainTimeData.CustomKey, onEnd: OnTimeEnd')
    if name == 'DialogOptionData':
        text = replace(text, '\tprivate string mStoryName = "";',
            '\tprivate readonly IStoryOptionServices mServices;\n\tprivate string mStoryName = "";')
        text = replace(text, 'public DialogOptionData(string storyName, int id, params object[] textWrapArgs)',
            'public DialogOptionData(IStoryOptionServices services, string storyName, int id, params object[] textWrapArgs)')
        text = replace(text, '\t\tSetParameter(storyName, id, textWrapArgs);',
            '\t\tmServices = services ?? throw new ArgumentNullException(nameof(services));\n\t\tSetParameter(storyName, id, textWrapArgs);')
        text = text.replace('StoryOptionInfo', 'StoryOptionInfo')
        text = text.replace('StoryUtils.GetStoryOptionInfo(', 'mServices.GetStoryOptionInfo(')
        text = text.replace('LanguageUtils.GetStoryOptionTextKey(', 'StoryOptionLanguage.GetStoryOptionTextKey(')
        text = text.replace('LanguageUtils.GetStoryOptionMinirTextKey(', 'StoryOptionLanguage.GetStoryOptionMinirTextKey(')
        text = replace(text, 'ConditionFactory.Create(storyOptionInfo.ConditionType, storyOptionInfo.ConditionParams).IsMet(MeManager.MainPlayer)',
            'mServices.IsConditionMet(storyOptionInfo.ConditionType, storyOptionInfo.ConditionParams)')
    if name == 'DialogOptionRemainTimeData':
        text = replace(text, 'public DialogOptionRemainTimeData(string storyName, int id, DateTime endTime)',
            'public DialogOptionRemainTimeData(IStoryOptionServices services, string storyName, int id, DateTime endTime)')
        text = replace(text, ': base(storyName, id)', ': base(services, storyName, id)')
    for token in ('GLogger', '[Inject]', 'ResourceLoader.', 'LanguageManager.', 'StoryUtils.',
                  'ConditionFactory.', 'MeManager.', 'DisposableBag', 'AssetOperationHandle', 'LanguageUtils.'):
        if token in text:
            raise ValueError('Unadapted original option controller boundary: ' + name + '/' + token)
    header = ('// Adapted from the selected original client; see home-story-option-code source manifest.\n'
        'namespace Zjcs.Official.HomeStoryUI\n{\n'
        'using UnityEngine;\nusing Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;\n'
        'using Object = UnityEngine.Object;\n')
    return '\n'.join(line.rstrip() for line in (header + text + '\n}\n').splitlines()) + '\n'


def main():
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required')
    folder = work / 'home-story-option-code'
    folder.mkdir(exist_ok=True)
    inputs, outputs = {}, {}
    for name in NAMES:
        source = SOURCE / (name + '.cs')
        raw = source.read_bytes()
        inputs[source.relative_to(ROOT).as_posix()] = sha(raw)
        target = folder / source.name
        target.write_text(port(name, raw), 'utf8', newline='\n')
        guid = hashlib.md5(('zjcs:home-story-option:' + name).encode()).hexdigest()
        Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + guid + '\nMonoImporter:\n'
            '  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n',
            'utf8', newline='\n')
        for path in (target, Path(str(target) + '.meta')):
            outputs[path.name] = sha(path.read_bytes())
    service = ROOT / 'scripts/templates/home_story_option_services.cs'
    language = ROOT / '.tools/source-packages/current' / VERSION / 'official-code/common-source/Common/LanguageUtils.cs'
    inputs[service.relative_to(ROOT).as_posix()] = sha(service.read_bytes())
    inputs[language.relative_to(ROOT).as_posix()] = sha(language.read_bytes())
    target = folder / 'StoryOptionServices.cs'
    target.write_bytes(service.read_bytes())
    guid = hashlib.md5(b'zjcs:home-story-option:StoryOptionServices').hexdigest()
    Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + guid + '\nMonoImporter:\n'
        '  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n',
        'utf8', newline='\n')
    for path in (target, Path(str(target) + '.meta')):
        outputs[path.name] = sha(path.read_bytes())
    target = folder / 'Zjcs.Official.HomeStoryOptions.asmdef'
    target.write_text(json.dumps(dict(name='Zjcs.Official.HomeStoryOptions',
        references=['Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI', 'Unity.ugui', 'Unity.TextMeshPro']),
        indent=2) + '\n', 'utf8', newline='\n')
    guid = hashlib.md5(b'zjcs:home-story-option:asmdef').hexdigest()
    Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + guid + '\nAssemblyDefinitionImporter:\n'
        '  externalObjects: {}\n', 'utf8', newline='\n')
    for path in (target, Path(str(target) + '.meta')):
        outputs[path.name] = sha(path.read_bytes())
    (work / 'home-story-option-code-source-manifest.json').write_text(json.dumps(dict(
        manifest=VERSION, inputs=inputs, outputs=outputs, generator_sha256=sha(Path(__file__).read_bytes()),
        scope='Original option component methods; native prefab and services remain to be bound.'),
        ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged original story option controllers:', len(NAMES), flush=True)


if __name__ == '__main__':
    main()
