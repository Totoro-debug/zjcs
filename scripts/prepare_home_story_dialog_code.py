"""Stage the selected original story dialogue panel with explicit local services."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_home_story_option_code import replace, sha, strip_log
from validation_workspace import validation_directory

VERSION = '884_156692'
CODE = ROOT / '.tools/source-packages/current' / VERSION / 'official-code'
SOURCE = CODE / 'client-source'


def port(name, raw):
    text = raw.decode('utf-8-sig').replace('\r\n', '\n')
    text = re.sub(r'^using [^\n]+;\n', '', text, flags=re.M)
    text = re.sub(r'^\[UIWindow\([^\n]+\)\]\n', '', text, flags=re.M)
    text = re.sub(r'\s*//IL_[^\n]*', '', text)
    if name == 'TalkDialog':
        text = text.replace('ITextProcessor', 'IWorldTextProcessor')
        text = text.replace('LanguageUtils.TextKeyArgWrap', 'StoryTextKeyArgWrap')
        text = text.replace('LanguageUtils.TryReplaceTalkPlayerNameText(', 'StoryDialogLanguage.ReplacePlayerName(')
        old = ('\tprivate DisposableBag mDisposableBag;\n\n'
            '\t[Inject]\n\tprivate IMapRootManagerProxy MapRootManagerProxy { get; set; }\n\n'
            '\t[Inject]\n\tprotected IResourceLoader ResourceLoader { get; set; }\n\n'
            '\t[Inject]\n\tprotected IWwiseProxy WwiseProxy { get; set; }\n\n'
            '\t[Inject]\n\tprotected IScriptableObjectConfig<StorySettings> StorySettings { get; set; }\n\n'
            '\t[Inject]\n\tprotected IUIRootManager UIRootManager { get; set; }')
        text = replace(text, old, '\tprotected IStoryDialogServices Services { get; private set; }')
        text = replace(text, '\tprivate void Awake()\n\t{',
            '\tprivate void Awake()\n\t{\n'
            '\t\tvar scope = GetComponentInParent<HomeStoryDialogScope>();\n'
            '\t\tServices = scope != null ? scope.Services : null;\n'
            '\t\tif (Services == null) throw new InvalidOperationException("The original story dialog scope is required.");\n'
            '\t\tmCharacterController.Services = Services;\n'
            '\t\tmTalkContentTxt.Localizer = Services.Localize;\n\t\tmCharacterNameTxt.Localizer = Services.Localize;\n'
            '\t\tmTalkContentTxt.FontProvider = Services.Font;\n\t\tmCharacterNameTxt.FontProvider = Services.Font;')
        text = replace(text, '(MeManager.MainPlayer != null) ? MeManager.MainPlayer.Name : ""', 'Services.PlayerName')
        text = replace(text, '\t\tmDisposableBag = default;\n', '')
        text = replace(text, '\t\tmDisposableBag.Dispose();', '\t\tServices.ReleasePlayableResources();')
        text = replace(text, '((Object)null != (Object)(object)StorySettings.Value)', '(Services.HasStorySettings)')
        text = text.replace('StorySettings.Value.DialogVoiceStopEventKey', 'Services.DialogVoiceStopEventKey')
        text = text.replace('StorySettings.Value.TalkBubblePlayablePath', 'Services.TalkBubblePlayablePath')
        text = text.replace('WwiseProxy.Wwise.PostEvent(', 'Services.PostVoice(')
        text = text.replace('Parsers.Instance.DialogRoleInfoParser.Get(', 'Services.GetDialogRole(')
        text = replace(text, 'StoryUtils.StoryWorldNpcPlayPlayable(MapRootManagerProxy, roleId, playablePath, ResourceLoader, ref mDisposableBag)',
            'Services.PlayWorldNpcPlayable(roleId, playablePath)')
        text = replace(text, 'StoryUtils.StoryWorldNpcPlayPlayable(MapRootManagerProxy, roleId, Services.TalkBubblePlayablePath, ResourceLoader, ref mDisposableBag)',
            'Services.PlayWorldNpcPlayable(roleId, Services.TalkBubblePlayablePath)')
        text = replace(text, 'DialogInfoUtils.GetRoleName(roleId)',
            'StoryDialogLanguage.RoleName(roleId, Services.PlayerRoleId, Services.PlayerName)')
        text = replace(text, 'LanguageManager.Instance.ContainsKey(', 'Services.ContainsText(')
        start = text.index('\tprotected Color GetBgImageColor()')
        end = text.index('\tprotected virtual void OnNextBtnClick()', start)
        text = text[:start] + ('\tprotected Color GetBgImageColor()\n\t{\n'
            '\t\tPopupWindow lastWindowWithIgnore = Services.LastWindowIgnoringTips();\n'
            '\t\tif (lastWindowWithIgnore != null)\n\t\t{\n'
            '\t\t\tif (lastWindowWithIgnore != PopupWindow) return mBlackBgColorDark;\n'
            '\t\t\treturn ((Graphic)mBlackBgImg).color;\n\t\t}\n'
            '\t\treturn Services.ActivePanelHasSceneGraphic() ? mBlackBgColorDark : mBlackBgColorDarkLess;\n\t}\n\n') + text[end:]
    else:
        start = text.index('\n\t[StructLayout(')
        end = text.index('\n\t[SerializeField]\n\tprivate OptionSlot OptionSlot;', start)
        text = text[:start] + text[end:]
        text, count = strip_log(text, name)
        if count != 1: raise ValueError('Review original dialog logging branch.')
        text = replace(text, '\t\tOptionSlot.Reset();',
            '\t\tforeach (OptionBaseItem item in OptionSlot.OptionItems)\n'
            '\t\t\tif (item is OptionTalkDialogItem option) option.Services = Services;\n\t\tOptionSlot.Reset();')
        text = text.replace('Parsers.Instance.DialogInfoParser.GetDialogInfosById(', 'Services.GetDialogInfos(')
        text = text.replace('Parsers.Instance.DialogRoleInfoParser.Get(', 'Services.GetDialogRole(')
        text = text.replace('LanguageManager.Instance.GetText(', 'Services.Localize(')
        text = text.replace('LanguageUtils.GetDialogTextKey(', 'StoryDialogLanguage.DialogTextKey(')
        text = text.replace('DialogInfoUtils.GetVoiceEventKey(', 'StoryDialogLanguage.VoiceEventKey(')
        text = text.replace('WindowManager.Instance.Close(', 'Services.CloseWindow(')
        text = text.replace('LanguageUtils.TextKeyArgWrap', 'StoryTextKeyArgWrap')
        start = text.index('\t[AsyncStateMachine(')
        end = text.index('\tprivate void PlayNext()', start)
        text = text[:start] + ('\tprivate async UniTask WaitAutoPlayDialog(CancellationToken cancellationToken)\n\t{\n'
            '\t\tawait UniTask.WaitForSeconds(mAutoPlayConfig.AutoPlayWaitTime, false, (PlayerLoopTiming)8, cancellationToken, false);\n'
            '\t\twhile (!mIsTalkFinish) await AutoPlaying(cancellationToken);\n\t}\n\n'
            '\tprivate async UniTask AutoPlaying(CancellationToken cancellationToken)\n\t{\n'
            '\t\tPlayNext();\n'
            '\t\tawait UniTask.WaitForSeconds(mAutoPlayConfig.AutoPlayDura, false, (PlayerLoopTiming)8, cancellationToken, false);\n\t}\n\n') + text[end:]
    for token in ('GLogger', '[Inject]', 'Parsers.', 'MeManager.', 'LanguageManager.', 'LanguageUtils.',
                  'StorySettings.', 'WindowManager.', 'ResourceLoader', 'DisposableBag', 'MapRootManagerProxy',
                  'WwiseProxy', 'StoryUtils.', 'UIRootManager', 'DialogInfoUtils.'):
        if token in text: raise ValueError('Unbound original dialogue dependency: ' + name + '/' + token)
    header = ('// Adapted from the selected original client; see home-story-dialog source manifest.\n'
        'namespace Zjcs.Official.HomeStoryDialog\n{\nusing System;\nusing System.Collections.Generic;\nusing System.Threading;\n'
        'using Cysharp.Threading.Tasks;\nusing UnityEngine;\nusing UnityEngine.Events;\nusing UnityEngine.UI;\n'
        'using Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;\nusing Zjcs.Official.HomeStoryUI;\n'
        'using Zjcs.Official.HomeStoryCharacter;\nusing Zjcs.Official.StoryBubbles;\nusing Zjcs.Official.StoryPlayables;\n'
        'using Object = UnityEngine.Object;\nusing PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;\n'
        'using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\n'
        'using DialogInfo = StoryDialogInfo;\nusing DialogRoleInfo = StoryDialogRoleInfo;\n')
    return '\n'.join(line.rstrip() for line in (header + text + '\n}\n').splitlines()) + '\n'


def main():
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file(): raise ValueError('An owned validation workspace is required.')
    folder = work / 'home-story-dialog-code'
    folder.mkdir(exist_ok=True)
    inputs, outputs = {}, {}
    for name in ('TalkDialog', 'OptionTalkDialogPanel'):
        source = SOURCE / (name + '.cs')
        raw = source.read_bytes()
        inputs[source.relative_to(ROOT).as_posix()] = sha(raw)
        target = folder / source.name
        target.write_text(port(name, raw), 'utf8', newline='\n')
        key = hashlib.md5(('zjcs:home-story-dialog:' + name).encode()).hexdigest()
        Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + key + '\nMonoImporter:\n'
            '  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n',
            'utf8', newline='\n')
        for path in (target, Path(str(target) + '.meta')): outputs[path.name] = sha(path.read_bytes())
    templates = [('home_story_dialog_services.cs', 'HomeStoryDialogServices'),
                 ('home_story_dialog_scope.cs', 'HomeStoryDialogScope')]
    for template_name, _ in templates:
        template = ROOT / 'scripts/templates' / template_name
        inputs[template.relative_to(ROOT).as_posix()] = sha(template.read_bytes())
    for path in (CODE / 'common-source/Common/LanguageUtils.cs', CODE / 'common-source/DialogInfoUtils.cs',
                 CODE / 'common-source/Common/DialogType.cs'):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for template_name, name in templates:
        target = folder / (name + '.cs')
        target.write_bytes((ROOT / 'scripts/templates' / template_name).read_bytes())
        key = hashlib.md5(('zjcs:home-story-dialog:' + name).encode()).hexdigest()
        Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + key + '\nMonoImporter:\n'
            '  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n',
            'utf8', newline='\n')
        for path in (target, Path(str(target) + '.meta')): outputs[path.name] = sha(path.read_bytes())
    target = folder / 'Zjcs.Official.HomeStoryDialog.asmdef'
    target.write_text(json.dumps(dict(name='Zjcs.Official.HomeStoryDialog', references=[
        'Launch', 'UniTask', 'Unity.ugui', 'Unity.TextMeshPro', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI',
        'Zjcs.Official.HomeHudUI', 'Zjcs.Official.HomeStoryPortrait', 'Zjcs.Official.HomeStoryOptions',
        'Zjcs.Official.HomeStoryCharacter', 'Zjcs.Official.StoryBubbles',
        'Zjcs.Official.StoryPlayables']), indent=2) + '\n', 'utf8', newline='\n')
    key = hashlib.md5(b'zjcs:home-story-dialog:asmdef').hexdigest()
    Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + key + '\nAssemblyDefinitionImporter:\n'
        '  externalObjects: {}\n', 'utf8', newline='\n')
    for path in (target, Path(str(target) + '.meta')): outputs[path.name] = sha(path.read_bytes())
    (work / 'home-story-dialog-code-source-manifest.json').write_text(json.dumps(dict(
        manifest=VERSION, inputs=inputs, outputs=outputs, generator_sha256=sha(Path(__file__).read_bytes()),
        scope='Original dialogue panel and execution order with explicit local service ports; prefab and host remain to be bound.'),
        ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged original dialogue panel controllers.', flush=True)


if __name__ == '__main__':
    main()
