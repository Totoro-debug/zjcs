using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ActivityCategoryType = Zjcs.Official.HomeFunctionMenuUI.HomeMenuConfigurationRules.ActivityCategoryType;

namespace Zjcs.Official.HomeHudInfrastructureUI
{
    // The selected ActivityEntryBtn reads the activity manager and opens its
    // panel; the offline owner supplies the actual local records and window.
    public interface IHomeActivityEntryButtonServices
    {
        event Action ActivitiesUpdated;
        IEnumerable<ActivityCategoryType> ActivityCategories { get; }
        void OpenActivityEntryPanel();
    }

    public class ActivityEntryBtn : MonoBehaviour
    {
        [SerializeField] private Button mEntryBtn;
        [SerializeField] private ActivityCategoryType mActivityCategoryType;

        public IHomeActivityEntryButtonServices Services { private get; set; }

        private void Awake()
        {
            Services.ActivitiesUpdated += OnActivitiesUpdate;
            mEntryBtn.onClick.AddListener(OnEntryBtnClick);
        }

        private void OnEnable() => OnActivitiesUpdate();

        private void OnDestroy()
        {
            Services.ActivitiesUpdated -= OnActivitiesUpdate;
            mEntryBtn.onClick.RemoveListener(OnEntryBtnClick);
        }

        private void OnActivitiesUpdate()
        {
            int count = 0;
            foreach (ActivityCategoryType category in Services.ActivityCategories)
                if (mActivityCategoryType == category) count++;
            gameObject.SetActive(count != 0);
        }

        private void OnEntryBtnClick() => Services.OpenActivityEntryPanel();
    }
}
