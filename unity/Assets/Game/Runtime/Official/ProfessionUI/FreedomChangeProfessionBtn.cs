using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionFreedomButtonData
    {
        IProfessionTimeService Time { get; }
        bool FunctionLimited { get; }
        bool IsChangingProfession { get; }
        bool FreedomChangeConditionMet { get; }
        int CurrentProfessionRank { get; }
        string FunctionLimitKey { get; }
        object[] FunctionLimitArguments { get; }
        string FunctionUnlockDescription { get; }
        bool CanFreeChangeProfession(DateTime now);
        DateTime NextFreeChangeProfessionTime { get; }
        Sprite TutorBodyImage { get; }
        int ShortTimeSpanFormat { get; }
        void ShowMessage(string key, params object[] arguments);
        void JumpToHighestUnlockedTutor(string confirmKey);
    }
    [Serializable]
    public sealed class ProfessionIntDictionary
    {
        [SerializeField] private List<WorldMonoKeyValuePair<int, int>> mSerializeBuffer;
        public bool TryGetValue(int key, out int value)
        {
            foreach (var pair in mSerializeBuffer)
                if (pair.Key == key) { value = pair.Value; return true; }
            value = default;
            return false;
        }
    }
    public sealed class FreedomChangeProfessionBtn : MonoBehaviour
    {
        public enum BtnStatus { FreeChange, ChangeProfessioning, FreeChangeCold }
        [SerializeField] private Button mFreedomChangeProfessionBtn;
        [SerializeField] private WorldStateSwitcher<BtnStatus> mStateSwitcher;
        [SerializeField] private ShowRemainTimeInfo mRemainTimeInfo;
        [SerializeField] private WorldMultiLanguageText mBtnDesc;
        [SerializeField] private Image mTutorHeadImg;
        [SerializeField] private string mConfirmKey;
        [SerializeField] private ProfessionIntDictionary mNpcOffset;
        [SerializeField] private Transform mTransform;
        [SerializeField] private WorldMultiLanguageText mLockConditonText;
        [SerializeField] private WorldStateSwitcher<bool> mIsUnlockStateSwitcher;
        private int mPreOffsetY;
        private IProfessionFreedomButtonData data;
        private void Awake() => mFreedomChangeProfessionBtn.onClick.AddListener(OnJumpBtnClick);
        public void Init(IProfessionFreedomButtonData value)
        {
            data = value ?? throw new ArgumentNullException(nameof(value));
            mRemainTimeInfo.Bind(data.Time);
            string key = data.FunctionLimitKey == "function_limit_level" ? "ui_profession_function_limit_level" : data.FunctionLimitKey;
            mLockConditonText.SetKey(key, data.FunctionLimitArguments);
            if (isActiveAndEnabled) Activate();
        }
        private void OnEnable() { if (data != null) Activate(); }
        private void Activate()
        {
            DateTime now = data.Time.Now;
            if (!data.CanFreeChangeProfession(now))
                mRemainTimeInfo.Init(data.NextFreeChangeProfessionTime, 1f,
                    "ui_freedom_change_profession_free_count_time_desc", data.ShortTimeSpanFormat, Refresh);
            mIsUnlockStateSwitcher.SetState(!data.FunctionLimited);
            Refresh();
        }
        public void Refresh()
        {
            if (mTutorHeadImg != null)
            {
                mTutorHeadImg.sprite = data.TutorBodyImage;
                CustomNpcTransform(data.CurrentProfessionRank);
            }
            DateTime now = data.Time.Now;
            if (data.IsChangingProfession)
            {
                mBtnDesc.SetKey("ui_freedom_change_profession_processing");
                mStateSwitcher.SetState(BtnStatus.ChangeProfessioning);
            }
            else if (!data.CanFreeChangeProfession(now))
            {
                mBtnDesc.SetKey("ui_goto_change_profession");
                mStateSwitcher.SetState(BtnStatus.FreeChangeCold);
            }
            else
            {
                mRemainTimeInfo.Stop();
                mBtnDesc.SetKey("ui_goto_change_profession");
                mStateSwitcher.SetState(BtnStatus.FreeChange);
            }
        }
        private void OnJumpBtnClick()
        {
            if (data.FunctionLimited)
                data.ShowMessage("ui_freedom_change_profession_conditon_desc", data.FunctionUnlockDescription);
            else if (!data.FreedomChangeConditionMet)
                data.ShowMessage("ui_freedom_change_profession_condition_ummit");
            else data.JumpToHighestUnlockedTutor(mConfirmKey);
        }
        public void CustomNpcTransform(int professionRank)
        {
            Vector3 position = mTransform.localPosition;
            position.y -= mPreOffsetY;
            if (mNpcOffset.TryGetValue(professionRank, out int offset))
            { position.y += offset; mPreOffsetY = offset; }
            else mPreOffsetY = 0;
            mTransform.localPosition = position;
        }
    }
}
