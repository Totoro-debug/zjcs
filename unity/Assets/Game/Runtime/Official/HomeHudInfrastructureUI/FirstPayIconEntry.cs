using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.HomeHudInfrastructureUI
{
    public interface IHomeFirstPayCondition
    {
        bool IsMet();
    }

    // The host supplies the actual player, configured pay-function condition,
    // condition observer and original first-pay window boundary.
    public interface IHomeFirstPayIconServices
    {
        bool HasPlayer { get; }
        bool HasConditionHandler { get; }
        bool IsPayFunctionLimited();
        IHomeFirstPayCondition GetPayFunctionCondition();
        void RegisterCondition(IHomeFirstPayCondition condition, Action callback);
        void DeregisterCondition(IHomeFirstPayCondition condition, Action callback);
        event Action<int> FirstPayAwardUpdated;
        bool FirstPayIsActive();
        bool FirstPayIsTakenAllAward();
        void OpenFirstPayDialogManual();
    }

    // Selected FirstPayIconEntry; external game services are explicit ports.
    public class FirstPayIconEntry : MonoBehaviour
    {
        [SerializeField] private LayoutElement mLayoutElement;
        [SerializeField] private GameObject mDisplay;
        [SerializeField] private Button mEntryBtn;
        private bool mRegisterCondition;
        private IHomeFirstPayCondition mCondition;

        public IHomeFirstPayIconServices Services { private get; set; }

        private void Awake() => mEntryBtn.onClick.AddListener(OnEntryBtnClick);

        private void OnEnable()
        {
            if (Services.IsPayFunctionLimited()) RegisterPayFunctionOpen();
            else Services.FirstPayAwardUpdated += OnFirstPayAwardUpdate;
            Refresh();
        }

        private void OnDisable()
        {
            if (Services.HasPlayer) Services.FirstPayAwardUpdated -= OnFirstPayAwardUpdate;
            UnregisterPayFunctionOpen();
        }

        private void SetShow(bool show)
        {
            mLayoutElement.ignoreLayout = !show;
            mDisplay.SetActive(show);
        }

        private void RegisterPayFunctionOpen()
        {
            if (!mRegisterCondition)
            {
                mRegisterCondition = true;
                mCondition = Services.GetPayFunctionCondition();
                Services.RegisterCondition(mCondition, OnPayFunctionOpen);
            }
        }

        private void UnregisterPayFunctionOpen()
        {
            if (mRegisterCondition)
            {
                if (Services.HasConditionHandler) Services.DeregisterCondition(mCondition, OnPayFunctionOpen);
                mRegisterCondition = false;
            }
        }

        private void OnPayFunctionOpen()
        {
            if (mCondition.IsMet())
            {
                UnregisterPayFunctionOpen();
                Services.FirstPayAwardUpdated += OnFirstPayAwardUpdate;
                Refresh();
            }
        }

        private bool IsShow()
        {
            if (Services.IsPayFunctionLimited()) return false;
            if (Services.FirstPayIsActive() && Services.FirstPayIsTakenAllAward()) return false;
            return true;
        }

        private void Refresh() => SetShow(IsShow());
        private void OnFirstPayAwardUpdate(int index) => Refresh();
        private void OnEntryBtnClick() => Services.OpenFirstPayDialogManual();
    }
}
