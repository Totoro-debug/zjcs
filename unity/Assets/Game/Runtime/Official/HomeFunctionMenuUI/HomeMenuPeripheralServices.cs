using System;
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
