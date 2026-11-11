"""Dependency adapters for the original time, Wiki and predownload menu components."""
import re

from prepare_home_auto_increase_runtime import CODE

PORTS='''using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerPanelUI;

namespace Zjcs.Official.HomeFunctionMenuUI
{
    public interface IHomeMenuWikiPlayer:IHomeMenuPlayer
    {
        string GetShuShuAccountId(); string Account { get; }
        long ServerId { get; } long Id { get; } string Name { get; } int Level { get; }
    }
    public sealed class HomeMenuWikiInfo { public int LimitConditionId; }
    public interface IHomeMenuWikiServices
    {
        IHomeMenuWikiPlayer Player { get; }
        bool CheckWikiDisabled();
        HomeMenuWikiInfo GetWikiInfo(string type);
        string GetUrl(string type); string GetTitleKey(string type);
        IHomeHudCondition GetCondition(int id); bool IsMet(IHomeHudCondition condition);
        string GetText(string key); void SystemMessage(string text); void TrySystemMessage(string text);
        void PublishNewTipClick();
        UniTask<string> OpenHtml(string url,string style,string orientation,string close,object parameter);
        Dictionary<string,object> CreateEventData(string name,string accountId,string account);
        void ReportEvent(string name,Dictionary<string,object> values);
    }
    public struct HomeMenuPredownloadStateUpdate { }
    public interface IHomeMenuPredownloadServices
    {
        PredownloadState CurrentDownloadState { get; }
        long TotalDownloadedBytes { get; } long CurrentDownloadedBytes { get; }
        bool TimeSynced { get; }
        IDisposable SubscribeStateUpdates(Action<HomeMenuPredownloadStateUpdate> callback);
        void PopupTipDialog(); void PopupProgressDialog(Action<IHomeMenuPanel> initialize);
        void ClickEntry(bool clearNeedTip);
    }
    public interface IHomeMenuTimeServices
    {
        IReadOnlyCollection<int> DisplayServerTimeRegionList { get; }
        int RegionCode { get; } int TimeZone { get; }
        DateTime GetDateTime(); DateTime GetClientLocalDateTime();
        DateTime GetDateTimeWithTimeZone(DateTime value);
        string GetServerTimeWithZoneTip(int timeZone);
    }
    internal static class HomeMenuSubscriptions
    {
        // The original R3 subscription owns this exact UnityEvent listener.
        public static IDisposable SubscribeClick(Button button,Action callback)
        {
            UnityAction listener=()=>callback();button.onClick.AddListener(listener);
            return new ClickSubscription(button,listener);
        }
        public static T AddTo<T>(this T item,ref HomePanelDisposableBag bag)where T:IDisposable { bag.Add(item);return item; }
        sealed class ClickSubscription:IDisposable
        {
            Button button;UnityAction listener;
            public ClickSubscription(Button button,UnityAction listener){this.button=button;this.listener=listener;}
            public void Dispose(){if(button && listener!=null)button.onClick.RemoveListener(listener);button=null;listener=null;}
        }
    }
}
'''


def strip_logging(text):
    # Each removed block contains only the decompiler's interpolated logger
    # handler writes. Conditions and returns surrounding those blocks stay put.
    matches=list(re.finditer(r'\n(?P<indent>\t+)if \(flag\)\n(?P=indent)\{',text))
    for match in reversed(matches):
        start=match.start();brace=text.index('{',match.start());depth=1;end=brace+1
        while depth:
            depth+=(text[end]=='{')-(text[end]=='}');end+=1
        block=text[brace+1:end-1]
        if not all(not line.strip() or re.match(r'message\d*\.Append',line.strip()) for line in block.splitlines()):
            continue
        text=text[:start]+text[end:]
    lines=[]
    for line in text.splitlines():
        value=line.strip()
        if re.match(r'(?:Microsoft.Extensions.Logging.ILogger\s+logger\d*\b|logger\d*\s*=\s*Logger;|ZLogger\w+InterpolatedStringHandler\s+message\d*\b|message\d*\s*=\s*new ZLogger|logger\d*\.(?:Info|Warn|Debug)\(|bool flag;)',value):continue
        lines.append(line)
    return '\n'.join(lines)+'\n'


def prepare_support(read,write):
    common_imports='using System.Linq;\nusing Zjcs.Official.HomeHudUI;\nusing Zjcs.Official.WorldUI;\nusing Zjcs.Official.HomePlayerPanelUI;\nusing MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;\n'
    for name,service in [('WikiEntry','IHomeMenuWikiServices'),('PredownloadEntry','IHomeMenuPredownloadServices'),('TimeInfoUI','IHomeMenuTimeServices')]:
        text=read(CODE/'client-source-resolved'/(name+'.cs'),True)
        for using in ('Common','Framework','Framework.DependencyInjection','GFrameCore.Extensions.ZLogger','MessagePipe','Microsoft.Extensions.Logging','Predownload','R3','SDK','ZLogger'):
            text=text.replace('using '+using+';\n','')
        text=re.sub(r'\t\[Inject\]\n\tprivate [^\n]+ \{ get; set; \}\n','',text)
        text=strip_logging(text)
        at=text.index('\n{',text.index('public class'))+2
        text=text[:at]+'\n\tpublic '+service+' Services { private get; set; }\n'+text[at:]
        text=text.replace('R3.DisposableBag','HomePanelDisposableBag').replace('CancellationDisposable','HomePanelCancellationDisposable')
        text=text.replace('StateSwitcher<bool>','WorldStateSwitcher<bool>')
        if name=='WikiEntry':
            text=text.replace('mButton.OnClickAsObservable().Subscribe(OnClick)','HomeMenuSubscriptions.SubscribeClick(mButton,OnClick)')
            text=text.replace('OnClick(Unit _)','OnClick()').replace('WikiClickPublisher.Publish(default)','Services.PublishNewTipClick()')
            text=text.replace('ServerInfoManager.Instance.CheckFunctionDisabled(null, ServerFunctionType.Wiki)','Services.CheckWikiDisabled()')
            text=text.replace('MeManager.MainPlayer','Services.Player').replace('WikiInfo wikiInfo','HomeMenuWikiInfo wikiInfo')
            for original,target in [('GetURL','GetUrl'),('GetTitleKey','GetTitleKey'),('GetWikiInfo','GetWikiInfo')]:
                text=text.replace('Parsers.Instance.WikiInfoParser.'+original,'Services.'+target)
            text=text.replace('ICondition condition = Parsers.Instance.ConditionInfoParser.GetCondition','IHomeHudCondition condition = Services.GetCondition')
            text=text.replace('condition.IsMet(Services.Player)','Services.IsMet(condition)')
            text=text.replace('LanguageManager.Instance.GetText','Services.GetText')
            text=text.replace('BroadcastManager.Instance.SystemMsg','Services.SystemMessage').replace('BroadcastManager.TrySystemMsg','Services.TrySystemMessage')
            text=text.replace('\t\tOpenHtmlArgs args = OpenHtmlArgs.Create(url, "2", "2", "1", (OpenHtmlParam)null);\n','')
            text=text.replace('SDKService.OpenHtml(args)','Services.OpenHtml(url, "2", "2", "1", null)')
            text=text.replace('EventReporter.CreateEventData','Services.CreateEventData').replace('EventReporter.ReportEvent','Services.ReportEvent')
        elif name=='PredownloadEntry':
            text=text.replace('mEntryBtn.onClick.AsObservable().Subscribe(OnEntryBtnClick)','HomeMenuSubscriptions.SubscribeClick(mEntryBtn,OnEntryBtnClick)')
            text=text.replace('StateSubscriber.Subscribe(OnPredownloadStateUpdate)','Services.SubscribeStateUpdates(OnPredownloadStateUpdate)')
            text=text.replace('OnEntryBtnClick(Unit unuse)','OnEntryBtnClick()')
            text=text.replace('PredownloadStateUpdateEvent _','HomeMenuPredownloadStateUpdate _')
            text=text.replace('PredownloadHandler.','Services.').replace('TimeManager.Instance.TimeSynced','Services.TimeSynced')
            text=text.replace('WindowManager.Instance.Popup<PredownloadTipDialog>()','Services.PopupTipDialog()')
            text=text.replace('WindowManager.Instance.Popup((PredownloadProgressDialog dialog)','Services.PopupProgressDialog((IHomeMenuPanel dialog)')
        else:
            text=text.replace('Parsers.GameSettings.DisplayServerTimeRegionList','Services.DisplayServerTimeRegionList')
            text=text.replace('ServerInfoManager.Instance.','Services.')
            text=text.replace('TimeManager.Instance.GetDateTime()','Services.GetDateTime()')
            text=text.replace('TimeManager instance = TimeManager.Instance','IHomeMenuTimeServices instance = Services')
            text=text.replace('TimeUtils.GetDateTimeWithTimeZone','Services.GetDateTimeWithTimeZone')
            text=text.replace('LanguageUtils.GetServerTimeWithZoneTip','Services.GetServerTimeWithZoneTip')
        assert not re.search(r'\b(?:Logger|MeManager|Parsers|WindowManager|TimeManager|ServerInfoManager|Inject|R3|SDKService)\b',text),name
        write(name+'.cs','// Original menu peripheral component; local service and observable bindings.\n'+common_imports+
            'namespace Zjcs.Official.HomeFunctionMenuUI\n{\n'+text+'}\n')
    text=read(CODE/'common-source/Predownload/PredownloadState.cs')
    text=text.replace('namespace Predownload;','namespace Zjcs.Official.HomeFunctionMenuUI\n{')
    write('PredownloadState.cs','// Original shared predownload state values.\n'+text+'}\n')
    read(CODE/'common-source/Common/GameSettings.cs')
    write('HomeMenuPeripheralServices.cs',PORTS)
