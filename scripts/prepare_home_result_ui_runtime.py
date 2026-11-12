"""Port the selected HomeUI result notifications without inventing fight results."""
import json
from pathlib import Path
import re

from prepare_home_auto_increase_runtime import ROOT, CODE, sha, metadata, method

DEST = ROOT/'unity/Assets/Game/Runtime/Official/HomeResultUI'
PORTS = '''using System;
using System.Collections.Generic;
using UnityEngine;
namespace Zjcs.Official.HomeResultUI
{
    // These projections expose only fields read by the original notification UI.
    // The result payload stays with the fight host and is never synthesized here.
    public interface IHomeFightResult { int StageId { get; } }
    public interface IHomeStoredFightResult
    {
        int StageId { get; }
        bool IsBackground { get; }
        IHomeFightResult Data { get; }
    }
    public interface IHomePlayerGameResult { IHomeStoredFightResult Result { get; } }
    public interface IHomeDemonBackPushResult { int EntryIndex { get; } int EndLayer { get; } }
    public interface IHomeDemonBackPushData
    {
        bool TryGetOlderBackPushResult(out ulong key, out IHomeDemonBackPushResult result);
    }
    public interface IHomeResultPlayer
    {
        event Action<object> OnGameResultsAdd;
        event Action<long> OnGameResultsRemove;
        IEnumerable<KeyValuePair<long, IHomePlayerGameResult>> GameResultManager { get; }
        IHomeDemonBackPushData StageDemonBattlefieldData { get; }
        bool HasGameResultByResultId(long id);
    }
    public interface IHomeResultStage
    {
        int Id { get; }
        StageType Type { get; }
        bool ShowEndPanel { get; }
        T GetPartInfo<T>() where T:class;
    }
    public interface IHomeResultStageParser { IHomeResultStage Get(int id); }
    public interface IHomeResultDemonParser
    {
        int GetMaxLayer(int entryIndex);
        IHomeResultStage GetInfoByIndexAndLayer(int entryIndex, int layer);
    }
    public interface IHomeResultFightComponent { int StageId { get; } }
    public interface IHomeResultFight { IHomeResultFightComponent Component { get; } }
    public interface IHomeResultFightRoot
    {
        bool HasFightRoot { get; }
        bool Initialized { get; }
        bool InFight { get; }
        IHomeResultFight Fight { get; }
        event Action OnInitialized;
    }
    public interface IHomeGameEndPanel { void InitMsg(IHomeFightResult result); }
    public interface IHomeDemonResultPanel { void Init(IHomeDemonBackPushResult result); }
    public interface IHomeResultWindowHandle { event Action<GameObject> OnComplete; }
    public interface IHomeResultWindows
    {
        event Action<GameObject> OnWindowClosed;
        IHomeResultWindowHandle Popup(Type type, Action<GameObject> initialize);
        void PopupDemon(Action<IHomeDemonResultPanel> initialize);
        bool TryGetEndPanel(GameObject go, out IHomeGameEndPanel panel);
        bool IsDemonResultPanel(GameObject go);
    }
    public interface IHomeResultServices
    {
        IHomeResultPlayer Player { get; }
        IHomeResultStageParser StageInfoParser { get; }
        IHomeResultDemonParser StagePartInfoDemonBattlefieldParser { get; }
        IHomeResultWindows Windows { get; }
        string GetText(string key);
        string GetTextFormated(string key, params object[] args);
        Type GetGameEndPanelType(IHomeResultStage stage);
        void OperPlayerReceivedGameAward(long resultId, bool invokeByPlayer);
        void OperPlayerDemonBattlefieldHandleBackPushResult(ulong key, Action<object> success);
    }
    public sealed class StagePartInfoDemonBattlefield
    {
        public int EntryIndex { get; }
        public int Layer { get; }
        public StagePartInfoDemonBattlefield(int entryIndex,int layer){EntryIndex=entryIndex;Layer=layer;}
    }
    public sealed class StagePartInfoVoidFissure
    {
        public int Layer { get; }
        public StagePartInfoVoidFissure(int layer){Layer=layer;}
    }
}
'''


def build():
    inputs, outputs = {}, {}

    def read(path, resolved=False):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path)
        if resolved:
            proof = path.with_suffix('.decompilation.json')
            data = json.loads(proof.read_bytes())
            assert sha(path) == data['sha256'], path
            for name, digest in data['inputs'].items():
                assert sha(ROOT/name) == digest, name
            inputs[proof.relative_to(ROOT).as_posix()] = sha(proof)
        return path.read_text('utf-8-sig')

    def write(name, text):
        path = DEST/name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, 'utf8', newline='\n')
        for item in (path, metadata(path)):
            outputs[item.relative_to(ROOT).as_posix()] = sha(item)

    prefix = '''// Original selected HomeUI result notification; service/type bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeResultUI
{
'''
    for name in ('GameUIBase', 'GameCurrentStageText', 'GameEndNotifyUI',
                 'GameEndUIHandlerInHome', 'FightDemonBattlefieldAutoPushEndNotifyUI'):
        text = read(CODE/'client-source-resolved'/(name+'.cs'), True)
        text = re.sub(r'^using (?:Common|Framework(?:\.DependencyInjection|\.Logging(?:\.InterpolatedStringHandler)?)?);\n', '', text, flags=re.M)
        text = text.replace('StateSwitcher<', 'WorldStateSwitcher<')
        if name == 'GameUIBase':
            text = text.replace('[Inject]\n\tprotected IFightRootProxy mFightRoot { get; set; }',
                'public IHomeResultFightRoot mFightRoot { protected get; set; }')
            text = re.sub(r'\bMyFight\b', 'IHomeResultFight', text)
        else:
            text = text.replace('\n{\n', '\n{\n\tpublic IHomeResultServices Services { private get; set; }\n', 1)
            for before, after in (
                ('MeManager.MainPlayer', 'Services.Player'),
                ('Parsers.Instance.StageInfoParser', 'Services.StageInfoParser'),
                ('Parsers.Instance.StagePartInfoDemonBattlefieldParser', 'Services.StagePartInfoDemonBattlefieldParser'),
                ('LanguageManager.Instance.', 'Services.'),
                ('WindowManager.Instance', 'Services.Windows'),
                ('GameEndPanelFactory.GetGameEndPanelType', 'Services.GetGameEndPanelType'),
                ('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerReceivedGameAward', 'Services.OperPlayerReceivedGameAward'),
                ('go.TryGetComponent<GameEndPanelBase>(out var component)', 'Services.Windows.TryGetEndPanel(go, out var component)'),
                ('go.GetComponent<StageDemonBattlefieldAutoPushResultPanel>() != null', 'Services.Windows.IsDemonResultPanel(go)'),
                ('Services.Windows.Popup((StageDemonBattlefieldAutoPushResultPanel panel)', 'Services.Windows.PopupDemon((IHomeDemonResultPanel panel)'),
            ):
                text = text.replace(before, after)
            for before, after in (
                ('PlayerFightResult', 'IHomeFightResult'), ('PlayerGameResult', 'IHomePlayerGameResult'),
                ('FightProcessResultMsg', 'object'), ('StageInfoParser', 'IHomeResultStageParser'),
                ('StagePartInfoDemonBattlefieldParser', 'IHomeResultDemonParser'),
                ('PlayerDemonBattlefieldBackPushResultWrap', 'IHomeDemonBackPushResult'),
                ('StageInfo', 'IHomeResultStage'),
            ):
                # Whole identifiers only: service property names must stay intact.
                text = re.sub(r'\b'+before+r'\b', after, text)
            # Restore the original parser property names after identifier adaptation.
            text = text.replace('Services.IHomeResultStageParser', 'Services.StageInfoParser')
            text = text.replace('Services.IHomeResultDemonParser', 'Services.StagePartInfoDemonBattlefieldParser')
            if name == 'GameEndNotifyUI':
                start = text.index('\t\t\t\tGLoggerErrorInterpolatedStringHandler')
                end = text.index('\n\t\t\t}', start)
                text = text[:start]+('\t\t\t\tDebug.LogError("[Fight] game end panel component mismatch, stageType:" + stageInfo.Type + ", resultId:" + mResultId + ", panelType:" + type);')+text[end:]
            if name == 'FightDemonBattlefieldAutoPushEndNotifyUI':
                before = '''\t\t\tPlayerDemonBattlefieldHandleBackPushResultRequest playerDemonBattlefieldHandleBackPushResultRequest = new PlayerDemonBattlefieldHandleBackPushResultRequest();
\t\t\tplayerDemonBattlefieldHandleBackPushResultRequest.Key = mResultKey;
\t\t\tServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerDemonBattlefieldHandleBackPushResult(playerDemonBattlefieldHandleBackPushResultRequest).OnSuccess += FightDemonBattlefieldAutoPushEndNotifyUI_OnSuccess;'''
                assert before in text
                text = text.replace(before, '\t\t\tServices.OperPlayerDemonBattlefieldHandleBackPushResult(mResultKey, FightDemonBattlefieldAutoPushEndNotifyUI_OnSuccess);')
        assert not re.search(r'\b(?:Inject|MeManager|Parsers|WindowManager|ServiceProxyBase|GLoggerErrorInterpolatedStringHandler|MyFight)\b', text), name
        assert 'Unknown result type' not in text and 'IAsyncStateMachine' not in text, name
        write(name+'.cs', prefix+text+'}\n')

    text = read(CODE/'common-source/Common/StageType.cs')
    write('StageType.cs', text.replace('namespace Common;', 'namespace Zjcs.Official.HomeResultUI\n{')+'}\n')
    language = read(CODE/'common-source/Common/LanguageUtils.cs')
    body = '\n'.join(method(language, signature) for signature in (
        'public static string GetStageDemonBattlefieldNameKey(int entryIndex)',
        'public static string GetStageNameKey(int stageId)',
        'public static string GetVoidFissureNameKey(int layer)'))
    write('LanguageUtils.cs', 'namespace Zjcs.Official.HomeResultUI\n{\ninternal static class LanguageUtils\n{\n'+body+'}\n}\n')
    for name in ('PlayerGameResult', 'PlayerGameResultPlayerFightResultWrap', 'PlayerFightResult',
                 'PlayerDemonBattlefieldBackPushResultWrap', 'PlayerDemonBattlefieldBackPushResult',
                 'StageInfo', 'StagePartInfoDemonBattlefield', 'StagePartInfoVoidFissure'):
        read(CODE/'common-source/Common'/(name+'.cs'))
    write('HomeResultServices.cs', PORTS)
    write('Zjcs.Official.HomeResultUI.asmdef', json.dumps(dict(name='Zjcs.Official.HomeResultUI', references=[
        'Unity.ugui', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI']), indent=2)+'\n')
    folder = metadata(DEST, True)
    outputs[folder.relative_to(ROOT).as_posix()] = sha(folder)
    receipt = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
        scope='Original HomeUI result notifications and fight-init lifecycle. Host supplies real ordered results, stage projections, result windows and award transactions; no fight or reward data is fabricated.')
    (ROOT/'assets/reference/home-result-ui-runtime-source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Original home result UI:', len(outputs), 'outputs', flush=True)


if __name__ == '__main__':
    build()
