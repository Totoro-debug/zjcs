using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    /// <summary>
    /// Offline adaptation of StageTouchUI, GoddessTouchDialog, LostGoddessTouchDialog,
    /// ConditionItem and Framework.StateGameObjectConfig from the pinned client.
    /// Serialized references and state memberships are restored from the original prefab.
    /// </summary>
    public sealed class GoddessTouchDialog : MonoBehaviour
    {
        [Serializable]
        public sealed class ObjectState
        {
            public int state;
            public GameObject[] objects;
        }

        public sealed class TreasurePreview
        {
            public int itemId;
            public GameObject prefab;
            public Sprite icon;
            public Sprite quality;
        }

        public Button myTouchBtn;
        public Button CannotActiveBtn;
        public Button mTransferBtn;
        public TMP_Text myStageName;
        public TMP_Text mConditionDesc;
        public TMP_Text mBlessingLevelTxt;
        public Transform mPropPrefab;
        public Transform mPropPrefabSlot;
        public Transform TreasureAwardSlot;
        public GameObject TreasureContent;
        public Transform DialogIconSlot;
        public GameObject DialogIconPrefab;
        public ObjectState[] ActivedStateSwitcher;
        public ObjectState[] ConditionStateSwitcher;
        private Action startStage;
        private Action transfer;
        private Action cannotActivate;
        private Action closed;
        private Action<int> treasureClicked;
        private bool canActivate;
        private readonly List<GameObject> propItems = new List<GameObject>();
        private readonly List<GameObject> treasureItems = new List<GameObject>();

        private void Awake()
        {
            myTouchBtn.onClick.AddListener(HandleTouchBtnClicked);
            CannotActiveBtn.onClick.AddListener(HandleCannotActiveBtnClick);
            mTransferBtn.onClick.AddListener(HandleTransferBtnClick);
        }

        public void DoInit(bool isActived, bool canActiveGoddess, string name, string condition,
            string blessing, string[] propDescriptions, Action activate, Action doTransfer,
            Action showCannotActivate, Action onClosed) =>
            DoInit(isActived, canActiveGoddess, name, condition, blessing, propDescriptions,
                Array.Empty<TreasurePreview>(), activate, doTransfer, showCannotActivate, onClosed);

        public void DoInit(bool isActived, bool canActiveGoddess, string name, string condition,
            string blessing, string[] propDescriptions, TreasurePreview[] treasurePreviews, Action activate, Action doTransfer,
            Action showCannotActivate, Action onClosed)
        {
            DoInit(isActived, canActiveGoddess, name, condition, blessing, propDescriptions, treasurePreviews,
                activate, doTransfer, showCannotActivate, onClosed, null);
        }

        public void DoInit(bool isActived, bool canActiveGoddess, string name, string condition,
            string blessing, string[] propDescriptions, TreasurePreview[] treasurePreviews, Action activate, Action doTransfer,
            Action showCannotActivate, Action onClosed, Action<int> onTreasureClicked)
        {
            startStage = activate;
            transfer = doTransfer;
            cannotActivate = showCannotActivate;
            closed = onClosed;
            treasureClicked = onTreasureClicked;
            canActivate = canActiveGoddess && !isActived;
            myStageName.text = name;
            mConditionDesc.text = condition;
            if (mBlessingLevelTxt != null) mBlessingLevelTxt.text = blessing;
            SetState(ConditionStateSwitcher, canActiveGoddess ? 1 : 0);
            SetState(ActivedStateSwitcher, isActived ? 2 : canActiveGoddess ? 1 : 0);
            // The confirmed offline transfer uses this original, serialized button.
            mTransferBtn.gameObject.SetActive(isActived);
            if (DialogIconSlot != null && DialogIconPrefab != null)
            {
                GameObject icon = Instantiate(DialogIconPrefab, DialogIconSlot, false);
                icon.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                foreach (GoddessDialogIcon stateIcon in icon.GetComponentsInChildren<GoddessDialogIcon>(true))
                    stateIcon.UpdateState(isActived);
            }
            foreach (GameObject item in propItems) Destroy(item);
            propItems.Clear();
            if (mPropPrefab != null)
            {
                mPropPrefab.gameObject.SetActive(false);
                foreach (string description in propDescriptions ?? Array.Empty<string>())
                {
                    GameObject item = Instantiate(mPropPrefab.gameObject, mPropPrefabSlot, false);
                    item.GetComponentInChildren<TMP_Text>(true).text = description;
                    item.SetActive(true);
                    propItems.Add(item);
                }
            }
            foreach (GameObject item in treasureItems) Destroy(item);
            treasureItems.Clear();
            bool hasTreasurePreviews = treasurePreviews != null && treasurePreviews.Length > 0;
            if (hasTreasurePreviews && (TreasureContent == null || TreasureAwardSlot == null))
                throw new InvalidOperationException("Official goddess treasure preview bindings are missing.");
            if (TreasureContent != null) TreasureContent.SetActive(hasTreasurePreviews);
            if (treasurePreviews != null && treasurePreviews.Length > 0)
            {
                foreach (TreasurePreview preview in treasurePreviews)
                {
                    if (preview?.prefab == null || preview.icon == null || preview.quality == null)
                        throw new InvalidOperationException("Official goddess item preview data is incomplete.");
                    WorldItemIcon icon = Instantiate(preview.prefab, TreasureAwardSlot, false).GetComponent<WorldItemIcon>();
                    if (icon == null) throw new InvalidOperationException("Official goddess item icon binding is missing.");
                    icon.Init(preview.icon, preview.quality, string.Empty);
                    if (icon.myTextGo != null) icon.myTextGo.SetActive(false);
                    if (icon.BottomGo != null) icon.BottomGo.SetActive(false);
                    Button button = icon.GetComponent<Button>();
                    if (button != null && treasureClicked != null)
                    {
                        button.enabled = true;
                        int itemId = preview.itemId;
                        button.onClick.AddListener(() => treasureClicked(itemId));
                    }
                    treasureItems.Add(icon.gameObject);
                }
            }
            GetComponent<WorldPopupWindow>().Show();
        }

        private void HandleTouchBtnClicked()
        {
            if (canActivate) startStage?.Invoke();
            Close();
        }

        private void HandleCannotActiveBtnClick() => cannotActivate?.Invoke();

        private void HandleTransferBtnClick()
        {
            transfer?.Invoke();
            Close();
        }

        public void Close()
        {
            GetComponent<WorldPopupWindow>().Hide(() =>
            {
                closed?.Invoke();
                Destroy(gameObject);
            });
        }

        // Preserve the source order: disable objects absent from the selected list, then enable
        // the selected list. An object shared by multiple states stays active.
        public static void SetState(ObjectState[] configs, int state)
        {
            GameObject[] selected = configs.FirstOrDefault(value => value.state == state)?.objects;
            foreach (ObjectState config in configs)
            {
                if (config.state == state) continue;
                foreach (GameObject item in config.objects)
                    if (selected == null || !selected.Contains(item)) item.SetActive(false);
            }
            if (selected == null) return;
            foreach (GameObject item in selected) item.SetActive(true);
        }
    }
}
