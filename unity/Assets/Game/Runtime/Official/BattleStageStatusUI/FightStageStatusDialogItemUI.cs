using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.BattleStageStatusUI
{
    // Official FightStageStatusDialogItemUI: retain serialized fields and the
    // enable/disable subscription lifetime. The host supplies local assets/text.
    public sealed class FightStageStatusDialogItemUI : MonoBehaviour
    {
        [SerializeField] private Image mIconImg;
        [SerializeField] private Image mIconBgImg;
        [SerializeField] private WorldMultiLanguageText mNameText;
        [SerializeField] private WorldMultiLanguageText mContentText;
        private FightStageStatusPresentation presentation;
        private int mStatusId;
        private bool initialized, subscribed;

        public void Init(int statusEntityId, FightStageStatusPresentation source)
        {
            Unsubscribe();
            mStatusId = statusEntityId;
            presentation = source;
            initialized = true;
            if (isActiveAndEnabled) Refresh();
        }

        private void OnEnable() { if (initialized) Refresh(); }
        private void OnDisable() => Unsubscribe();

        private void Refresh()
        {
            Unsubscribe();
            var item = presentation.Resolve(mStatusId);
            if (item.DescriptionUpdatable)
            {
                presentation.DescriptionChanged += OnUpdateStatusDesc;
                subscribed = true;
            }
            mIconImg.sprite = item.Icon;
            mIconBgImg.sprite = item.Background;
            mNameText.SetKey("{0}", item.Name);
            mContentText.SetKey("{0}", item.Description);
        }

        private void Unsubscribe()
        {
            if (subscribed) presentation.DescriptionChanged -= OnUpdateStatusDesc;
            subscribed = false;
        }

        private void OnUpdateStatusDesc()
            => mContentText.SetKey("{0}", presentation.Resolve(mStatusId).Description);
    }
}
