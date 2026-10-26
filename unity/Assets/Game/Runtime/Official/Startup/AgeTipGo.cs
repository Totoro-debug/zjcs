using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.Startup
{
    // Original Launch/AgeTipGo flags and button ordering. WindowManager route
    // is bound to the local instance of the original AgeTipsDialog prefab.
    public sealed class AgeTipGo : MonoBehaviour
    {
        [SerializeField] private Button mAgeTipBtn;
        [SerializeField] private GameObject mHelathHinit, mIsbnGo, mLowPerformanceTip, mAgeTopKr;
        public Action OpenDialog;
        private void Awake()
        {
            mLowPerformanceTip.SetActive(false);
            gameObject.SetActive(true);
            mHelathHinit.SetActive(true);
            mIsbnGo.SetActive(true);
            mAgeTopKr.SetActive(false);
        }
        private void Start() => mAgeTipBtn.onClick.AddListener(OnAgeTipBtnClick);
        private void OnDestroy() => mAgeTipBtn.onClick.RemoveListener(OnAgeTipBtnClick);
        private void OnAgeTipBtnClick() => OpenDialog?.Invoke();
    }
}
