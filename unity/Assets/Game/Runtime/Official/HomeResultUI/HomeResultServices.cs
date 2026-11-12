using System;
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
