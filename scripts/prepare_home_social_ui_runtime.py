"""Port original HomeUI matching controls; keep offline policy in their host."""
import json
from pathlib import Path
import re

from prepare_home_auto_increase_runtime import ROOT,CODE,sha,metadata

DEST=ROOT/'unity/Assets/Game/Runtime/Official/HomeSocialUiControls'
PORTS='''using System;
namespace Zjcs.Official.HomeSocialUiControls
{
    public interface IHomeMatchTarget
    {
        TeamTargetCatalog TargetCatalog { get; }
        string GetItemName();
    }
    public interface IHomeMatchInfo
    {
        DateTime MatchStartTime { get; }
        DateTime MatchTargetTime { get; }
        int AlreadyWaitSec { get; }
        IHomeMatchTarget MatchTarget { get; }
    }
    public interface IHomeMatchPlayer
    {
        event Action OnGameMatchUpdate;
        bool IsInGameMatch { get; }
        IHomeMatchInfo GameMatchInfo { get; }
    }
    public interface IHomeMatchServices
    {
        IHomeMatchPlayer Player { get; }
        bool IsLeader { get; }
        DateTime GetDateTime();
        string GetText(string key);
        IDisposable SubscribeConfirmStart(Action listener);
        void OperPlayerStopGameMatch(Action<object> onSuccess);
    }
}
'''


def build():
    inputs,outputs={},{}
    def read(path,resolved=False):
        inputs[path.relative_to(ROOT).as_posix()]=sha(path)
        if resolved:
            proof=path.with_suffix('.decompilation.json');data=json.loads(proof.read_bytes())
            assert sha(path)==data['sha256'],path
            for name,digest in data['inputs'].items():assert sha(ROOT/name)==digest,name
            inputs[proof.relative_to(ROOT).as_posix()]=sha(proof)
        return path.read_text('utf-8-sig')
    def write(name,text):
        path=DEST/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_text(text,'utf8',newline='\n')
        for item in (path,metadata(path)):outputs[item.relative_to(ROOT).as_posix()]=sha(item)
    prefix='''using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.WorldUI;
using CultureTextArg=Zjcs.Official.World.NativeLabelUI.Framework.CultureTextArg;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeSocialUiControls
{
'''
    for name in ('GameMatchMiniPanel','GameMatchMiniSwitchPanel'):
        text=read(CODE/'client-source-resolved'/(name+'.cs'),True)
        for using in ('Common','Framework','Framework.DependencyInjection','R3'):
            text=text.replace('using '+using+';\n','')
        text=text.replace('StateSwitcher<','WorldStateSwitcher<')
        if name=='GameMatchMiniPanel':
            text=re.sub(r'\t\[Inject\]\n\tprivate IGameMatchModule GameMatchModule \{ get; set; \}',
                '\tpublic IHomeMatchServices Services { protected get; set; }',text)
            for before,after in [
                ('private DisposableBag','private HomePanelDisposableBag'),
                ('MeManager.MainPlayer','Services.Player'),('MeManager.Instance.IsLeader','Services.IsLeader'),
                ('GameMatchInfoMsg','IHomeMatchInfo'),('Player mainPlayer','IHomeMatchPlayer mainPlayer'),
                ('TimeManager.Instance.GetDateTime()','Services.GetDateTime()'),
                ('LanguageManager.Instance.GetText','Services.GetText'),
                ('GameMatchModule.OnConfirmStart.Subscribe(Instance_OnGameMatchConfirmStart).AddTo(ref mDisposableBag);',
                 'mDisposableBag.Add(Services.SubscribeConfirmStart(Instance_OnGameMatchConfirmStart));'),
                ('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerStopGameMatch().OnSuccess += OnCancelMatch;',
                 'Services.OperPlayerStopGameMatch(OnCancelMatch);')]:
                assert before in text,(name,before);text=text.replace(before,after)
        assert not re.search(r'\b(?:Inject|MeManager|GameMatchModule|TimeManager|LanguageManager|ServiceProxyBase|R3)\b',text),name
        assert 'Unknown result type' not in text and 'IAsyncStateMachine' not in text,name
        # Preserve the original OnDestory spelling, observer order, first
        # 0.1-second delay and DateTime/mmss formatting branches verbatim.
        write(name+'.cs','// Original selected home matching UI; service and namespace bindings only.\n'+prefix+text+'}\n')
    text=read(CODE/'common-source/Common/TeamTargetCatalog.cs').replace('namespace Common;','namespace Zjcs.Official.HomeSocialUiControls\n{')
    write('TeamTargetCatalog.cs',text+'}\n')
    for name in ('GameMatchInfoMsg','ITeamTarget'):
        read(CODE/'common-source/Common'/(name+'.cs'))
    write('HomeMatchServices.cs',PORTS)
    for relative in ('HomePlayerPanelUI/HomePanelDisposableBag.cs','WorldUI/WorldStateSwitcher.cs',
        'World/NativeLabelUI/Framework/CultureTextArg.cs','World/NativeLabelUI/Framework/ArgBase.cs'):
        read(ROOT/'unity/Assets/Game/Runtime/Official'/relative)
    write('Zjcs.Official.HomeSocialUiControls.asmdef',json.dumps(dict(name='Zjcs.Official.HomeSocialUiControls',references=[
        'Unity.ugui','Zjcs.Official.HomePlayerPanelUI','Zjcs.Official.WorldUI','Zjcs.Official.World.NativeLabelUI']),indent=2)+'\n')
    item=metadata(DEST,True);outputs[item.relative_to(ROOT).as_posix()]=sha(item)
    receipt=dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,
        scope='Original HomeUI matching mini-panel lifecycle and switch animation control. Native subtree, local offline host and runtime validation remain required.')
    (ROOT/'assets/reference/home-social-ui-runtime-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original home matching controls:',len(outputs),'outputs',flush=True)


if __name__=='__main__':build()
