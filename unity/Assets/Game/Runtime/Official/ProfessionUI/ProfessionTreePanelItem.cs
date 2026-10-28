using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ProfessionUI
{
    public sealed class ProfessionTreeCondition
    {
        public Func<bool> IsMet;
        public string DescriptionKey;
        public object[] DescriptionArguments;
        public IWorldTextProcessor DescriptionProcessor;
    }
    public interface IProfessionTreeData
    {
        int CurrentProfession { get; }
        int TotalProfessionLevel { get; }
        int HighestUnlockedRank { get; }
        int ProfessionLevel(int profession);
        bool IsUnlocked(int profession);
        int PreProfession(int profession);
        bool HasPromoteCondition(int profession);
        bool PromoteConditionMet(int profession);
        IReadOnlyList<ProfessionTreeCondition> SeparatedConditions(int profession);
        int TreeItemConditionCountLimit { get; }
        Sprite FirstIcon(int profession);
        Color OverviewBackgroundColor(int profession);
        string NameKey(int profession);
        string UnknownProfessionNameKey { get; }
        void OpenDetail(int profession);
        void ConfirmJumpToTutor(int profession);
        void InitializeTip(Component linker, int profession);
        void RefreshAppearance(ProfessionTreePanelItem item, Component portrait,
            GameObject playerAnimation, Transform animationSlot, bool selected, bool silhouette);
        event Action Changed;
    }

    // Original ProfessionTreePanelItem state order, conditions and UI update order.
    // Offline resource/window/map/appearance services are supplied by the host.
    public sealed class ProfessionTreePanelItem : MonoBehaviour
    {
        public enum ProfessionStatus { Unknown, LockedUnmet, LockedMet, Unlocked, Selected }
        public int Profession;
        [SerializeField] private Component mPortrait;
        [SerializeField] private Transform mAnimSlot;
        [SerializeField] private GameObject mPlayerAnimation;
        [SerializeField] private Button mTutorJumpButton;
        [SerializeField] private Component mTipDotLinker;
        public Image Icon;
        public Button MyButton;
        public WorldMultiLanguageText Name, Lv;
        public Image BackGround;
        public GameObject ConditionPrefab;
        public Transform ConditionTrans;
        [SerializeField] private WorldStateSwitcher<ProfessionStatus> mStatusSwitcher;
        private readonly List<WorldMultiLanguageText> mConditionItems = new List<WorldMultiLanguageText>();
        private IProfessionTreeData data;
        private bool subscribed;
        private ProfessionStatus mStatus;
        public ProfessionStatus Status => mStatus;
        private void Awake()
        {
            MyButton.onClick.AddListener(OnClick);
            mTutorJumpButton.onClick.AddListener(JumpToTutor);
        }
        public void Init(IProfessionTreeData context)
        {
            Unsubscribe();
            data = context ?? throw new ArgumentNullException(nameof(context));
            data.InitializeTip(mTipDotLinker, Profession);
            if (isActiveAndEnabled) { Subscribe(); Refresh(); }
        }
        private void OnEnable() { if (data != null) { Subscribe(); Refresh(); } }
        private void Subscribe()
        {
            if (subscribed) return;
            data.Changed += Refresh;
            subscribed = true;
        }
        private void Unsubscribe()
        {
            if (subscribed && data != null) data.Changed -= Refresh;
            subscribed = false;
        }
        private void OnDisable() => Unsubscribe();
        private void Refresh()
        {
            SetStatus();
            mStatusSwitcher.SetState(mStatus);
            if (mStatus != ProfessionStatus.Unknown)
            {
                Icon.sprite = data.FirstIcon(Profession);
                BackGround.color *= data.OverviewBackgroundColor(Profession);
            }
            data.RefreshAppearance(this, mPortrait, mPlayerAnimation, mAnimSlot,
                Profession == data.CurrentProfession, mStatus == ProfessionStatus.Unknown);
            Name.SetKey(mStatus == ProfessionStatus.Unknown ? data.UnknownProfessionNameKey : data.NameKey(Profession));
            bool unlocked = mStatus == ProfessionStatus.Unlocked || mStatus == ProfessionStatus.Selected;
            if (unlocked) Lv.Format(data.ProfessionLevel(Profession));
            MyButton.interactable = unlocked;
            if (mStatus == ProfessionStatus.LockedUnmet) ShowUnlockConditions();
        }
        private void SetStatus()
        {
            mStatus = ProfessionStatus.Unknown;
            if (Profession == data.CurrentProfession) mStatus = ProfessionStatus.Selected;
            else if (data.IsUnlocked(Profession)) mStatus = ProfessionStatus.Unlocked;
            else if (data.PreProfession(Profession) != 0 && data.IsUnlocked(data.PreProfession(Profession)))
                mStatus = data.HasPromoteCondition(Profession) && data.PromoteConditionMet(Profession)
                    ? ProfessionStatus.LockedMet : ProfessionStatus.LockedUnmet;
        }
        private void OnClick() => data.OpenDetail(Profession);
        private void JumpToTutor() => data.ConfirmJumpToTutor(Profession);
        private void ShowUnlockConditions()
        {
            if (!data.HasPromoteCondition(Profession)) return;
            IReadOnlyList<ProfessionTreeCondition> conditions = data.SeparatedConditions(Profession);
            int count = 0, limit = data.TreeItemConditionCountLimit;
            if (limit <= 0) throw new InvalidOperationException("Original profession tree condition count limit is invalid.");
            for (int i = 0; i < conditions.Count; i += limit)
            {
                bool met = true;
                for (int j = 0; j < limit; j++)
                    if (!conditions[i + j].IsMet()) { met = false; break; }
                if (met) continue;
                for (int k = 0; k < limit; k++)
                {
                    int index = i + k;
                    if (index >= conditions.Count) break;
                    WorldMultiLanguageText label;
                    if (mConditionItems.Count < index + 1)
                    {
                        label = Instantiate(ConditionPrefab, ConditionTrans, false).GetComponent<WorldMultiLanguageText>();
                        mConditionItems.Add(label);
                    }
                    else label = mConditionItems[index];
                    label.gameObject.SetActive(true);
                    ProfessionTreeCondition condition = conditions[index];
                    label.SetKey(condition.DescriptionKey, condition.DescriptionProcessor, null, condition.DescriptionArguments);
                    count++;
                }
                break;
            }
            for (int i = count; i < mConditionItems.Count; i++) mConditionItems[i].gameObject.SetActive(false);
        }
    }
}
