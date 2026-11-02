"""Port the selected original dialogue address-book controllers and comparer."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT

SOURCE = ROOT / '.tools/source-packages/official-code'
TARGET = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Roster'
RECEIPT = ROOT / 'assets/reference/partner-dialogue-list-ui-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def meta(path):
    result = Path(str(path) + '.meta')
    if not result.exists():
        key = hashlib.md5(('zjcs:partner-dialogue-list:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        result.write_text('fileFormatVersion: 2\nguid: ' + key + '\nMonoImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
    return result


def port(name):
    source = SOURCE / 'client-source' / (name + '.cs')
    text = source.read_text('utf-8-sig')
    text = re.sub(r'^using (?:Common|Framework(?:\.[\w.]+)?);\n', '', text, flags=re.M)
    text = re.sub(r'\s*//IL_[^\n]*', '', text)
    text = text.replace('PlayerNpcDialogueDataWrap', 'PartnerDialogueData').replace('PlayerNpcDataWrap', 'PartnerDialogueNpcData')
    text = text.replace('StateSwitcher<', 'WorldStateSwitcher<').replace('DialogueStatus', 'PartnerDialogueStatus')
    # Undo the property rename: the original data member is still DialogueStatus.
    text = text.replace('.PartnerDialogueStatus', '.DialogueStatus')
    text = text.replace('ITextProcessor', 'IWorldTextProcessor')
    text = text.replace('MeManager.MainPlayer.OnNpcDialogueAdd', 'services.DialogueAdded')
    text = text.replace('MeManager.MainPlayer.OnNpcDialogueRemove', 'services.DialogueRemoved')
    text = text.replace('MeManager.MainPlayer.OnNpcDialogueUpdate', 'services.DialogueUpdated')
    text = text.replace('MeManager.MainPlayer.NpcDialogueManager', 'services.Dialogues')
    text = text.replace('MeManager.MainPlayer.NpcManager', 'services.Npcs')
    text = text.replace('MeManager.MainPlayer != null', 'services.MainPlayerAvailable')
    text = text.replace('new PlayerNameTextProcessor(MeManager.MainPlayer?.Name ?? "")', 'new PartnerDialoguePlayerNameProcessor(services.PlayerName ?? "")')
    text = text.replace('NpcDialogueInfoParser.sDialogueStartNodeId', 'services.DialogueStartNodeId')
    text = text.replace('LanguageUtils.GetNpcDialogueTextKey', 'PartnerDialogueListRules.GetNpcDialogueTextKey')
    text = text.replace('NpcDialogueComparer.sDialogue.DefaultComparer', 'PartnerDialogueListRules.CompareDialogues')
    text = text.replace('BroadcastManager.ShowErrText(1506, null, false)', 'services.ShowError(1506)')
    text = text.replace('((Object)(object)', '((UnityEngine.Object)(object)').replace('(Object)null', '(UnityEngine.Object)null')
    text = text.replace('((Object)mFadeInClip)', '((UnityEngine.Object)mFadeInClip)')
    # Both original UIUtils overloads retain their distinct transform-reset flag.
    text = text.replace('UIUtils.Instantiate(mItemPrefab, mRoot, EUIInstantiateFlag.Transform)', 'services.Instantiate(mItemPrefab, mRoot, true)')
    text = text.replace('UIUtils.Instantiate<NpcPanelDialogueListDialogueItem>(mDialogueItemPrefab, mDialogueRoot)', 'services.Instantiate(mDialogueItemPrefab, mDialogueRoot, false).GetComponent<NpcPanelDialogueListDialogueItem>()')
    text = text.replace('mDialogueRoot.DestroyActivedChildren();', '''for (int index = mDialogueRoot.childCount - 1; index >= 0; index--)
        {
            Transform child = mDialogueRoot.GetChild(index);
            if (child.gameObject.activeSelf) UnityEngine.Object.Destroy(child.gameObject);
        }''')
    text = text.replace('int redDotCount = mPlayerNpcDialogues.GetRedDotCount();', 'int redDotCount = PartnerDialogueListRules.GetRedDotCount(mPlayerNpcDialogues, services);')
    text = text.replace('bool active = mNpcDialogueDataWrap.HasRedDot();', 'bool active = PartnerDialogueListRules.HasRedDot(mNpcDialogueDataWrap, services);')
    if name == 'NpcPanelDialogueList':
        text = text.replace('services.Npcs.TryGetValue(x', 'mNpcPanelDialogueList.services.Npcs.TryGetValue(x')
        text = text.replace('services.Npcs.TryGetValue(y', 'mNpcPanelDialogueList.services.Npcs.TryGetValue(y')
        text = text.replace('ItemComparerBase.ComparerResult(score, score2, priority.orderType)', 'PartnerDialogueListRules.Compare(score, score2, priority.orderType)')
        # Logging adaptation only: replace unavailable interpolated log handlers.
        start = text.index('\t\t\t\tGLoggerErrorInterpolatedStringHandler')
        end = text.index('\n\t\t\t\treturn 0;', start)
        text = text[:start] + '\t\t\t\tDebug.LogError($"没有找到 NPC 数据！NpcId: {x}/{y}");' + text[end:]
        start = text.index('\t\t\tGLoggerErrorInterpolatedStringHandler')
        end = text.index('\n\t\t}', start)
        text = text[:start] + '\t\t\tDebug.LogError($"NpcPanelDialogueListNpcItem 数据有误！有 NPC:{npcId} 没有对话信息！");' + text[end:]
    elif name == 'NpcPanelDialogueListNpcItem':
        text = text.replace('private PlayerIcon mIcon;', 'private Component mIcon;')
        start = text.index('\t\t\tNpcTargetData npcTargetData')
        end = text.index('\n\t\t\tTryRefreshDialogues', start)
        text = text[:start] + '\t\t\tservices.InitializeNpcIcon(mIcon, mNpcData);' + text[end:]
    else:
        start = text.index('\t\tWindowManager.Instance.Popup')
        end = text.index('\n\t}', start)
        text = text[:start] + '\t\tservices.ShowDialogue(mNpcDialogueDataWrap);' + text[end:]
        # Awake runs at instantiation, so defer only the injected name processor
        # until Configure, before any original Init/Refresh call.
        line = '\t\tmTextProcessor = new PartnerDialoguePlayerNameProcessor(services.PlayerName ?? "");'
        assert line in text
        text = text.replace(line, '')
    declaration = 'public class ' + name
    index = text.index('{', text.index(declaration)) + 1
    configure = '\n    private IPartnerDialogueUiServices services;\n    public void Configure(IPartnerDialogueUiServices value)\n    {\n        services = value ?? throw new System.ArgumentNullException(nameof(value));\n'
    if name == 'NpcPanelDialogueListDialogueItem':
        configure += '        mTextProcessor = new PartnerDialoguePlayerNameProcessor(services.PlayerName ?? "");\n'
    configure += '    }\n'
    text = text[:index] + configure + text[index:]
    assert 'MeManager' not in text and 'GLogger' not in text and 'WindowManager' not in text
    result = ('// Ported original controller; see partner-dialogue-list-ui-source-manifest.json.\n'
        + 'namespace Zjcs.Official.PartnersUI\n{\nusing Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;\n'
        + 'using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\n' + text + '\n}\n')
    return source, '\n'.join(line.rstrip() for line in result.splitlines()) + '\n'


def main():
    from official_source import OfficialSource
    source = OfficialSource()
    inputs, outputs = {}, {}
    for name in ('NpcPanelDialogueList', 'NpcPanelDialogueListNpcItem', 'NpcPanelDialogueListDialogueItem'):
        original, content = port(name)
        selected = ROOT / '.tools/source-packages/current' / source.version / 'official-code/client-source' / original.name
        if original.read_bytes() != selected.read_bytes():
            raise ValueError('Controller source differs from selected version: ' + name)
        inputs[original.relative_to(ROOT).as_posix()] = sha(original.read_bytes())
        path = TARGET / (name + '.cs')
        path.write_text(content, 'utf8', newline='\n')
        for result in (path, meta(path)):
            outputs[result.relative_to(ROOT).as_posix()] = sha(result.read_bytes())
    for relative in ('common-source/NpcDialogueComparer.cs', 'client-source/NpcPanelDialogueListDialogueItemUtils.cs',
                     'client-source/NpcPanelDialogueListNpcItemUtils.cs', 'common-source/Common/PlayerNpcDialogueDataWrap.cs',
                     'common-source/Common/DialogueStatus.cs', 'common-source/Common/LanguageUtils.cs',
                     'common-source/ItemComparerBase.cs', 'framework-source/Framework/TransformExtensions.cs',
                     'common-source/Common/PlayerNameTextProcessor.cs'):
        path = SOURCE / relative
        if path.read_bytes() != (ROOT / '.tools/source-packages/current' / source.version / 'official-code' / relative).read_bytes():
            raise ValueError('Dialogue source differs from selected version: ' + relative)
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for name in ('PartnerDialogueUiServices', 'PartnerDialogueListRules', 'PartnerDialoguePlayerNameProcessor'):
        path = TARGET / (name + '.cs')
        for result in (path, meta(path)):
            outputs[result.relative_to(ROOT).as_posix()] = sha(result.read_bytes())
    RECEIPT.write_text(json.dumps(dict(manifest=source.version, inputs=inputs, outputs=outputs,
        generator_sha256=sha(Path(__file__).read_bytes()),
        adaptation='Original controllers/comparer; explicit local player, icon, window, condition, injection and text services. No layout generated.'),
        ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    main()
