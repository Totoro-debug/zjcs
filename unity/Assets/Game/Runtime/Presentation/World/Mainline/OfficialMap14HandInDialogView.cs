using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Inventory;

namespace Zjcs.Presentation.World
{
    // DialogHandInItemDialog's original refresh/cancel/confirm/close sequence;
    // inventory and localization are supplied by the offline story host.
    public sealed class OfficialMap14HandInDialogView : MonoBehaviour
    {
        public Transform mSubmitItemSlot;
        public Button mCancelBtn, mComfirmBtn;
        public Material grayImageMaterial;
        private Action confirmed, canceled, interrupted, hidden;
        private bool resolved;

        public void Initialize(OfficialInventoryAssetCatalog inventoryAssets, int itemId, int quality,
            long requiredCount, long ownedCount, Func<string, string> localize,
            Action onConfirm, Action onCancel, Action onInterrupt, Action onHidden = null)
        {
            if (inventoryAssets == null) throw new ArgumentNullException(nameof(inventoryAssets));
            if (itemId <= 0 || requiredCount <= 0 || ownedCount < 0) throw new ArgumentOutOfRangeException(nameof(itemId));
            if (mSubmitItemSlot == null || mCancelBtn == null || mComfirmBtn == null)
                throw new InvalidDataException("Original hand-in serialized component references are missing.");
            confirmed = onConfirm; canceled = onCancel; interrupted = onInterrupt; hidden = onHidden;
            foreach (var text in GetComponentsInChildren<WorldMultiLanguageText>(true))
            {
                text.Localizer = localize;
                text.Refresh();
            }
            SetConfirmationGray(ownedCount < requiredCount);
            mComfirmBtn.interactable = ownedCount >= requiredCount;
            mCancelBtn.onClick.AddListener(HandleCancel);
            mComfirmBtn.onClick.AddListener(HandleConfirm);

            var iconPrefab = Resources.Load<GameObject>("Map14/UI/HandIn/ItemIconDefault");
            if (iconPrefab == null) throw new InvalidDataException("Pinned official hand-in item prefab is missing.");
            var icon = Instantiate(iconPrefab, mSubmitItemSlot, false).GetComponent<WorldItemIcon>();
            Sprite sprite = inventoryAssets.FindItemSprite(itemId);
            Sprite qualitySprite = inventoryAssets.FindQualityBackground(quality);
            if (icon == null || sprite == null || qualitySprite == null)
                throw new InvalidDataException("Official hand-in item art is unbound.");
            foreach (var text in icon.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = localize;
            icon.Init(sprite, qualitySprite, string.Empty);
            ShowRequireCount(icon, ownedCount, requiredCount);
            foreach (var sound in GetComponentsInChildren<WorldButtonSound>(true)) sound.Post = Map14HandInUiAudio.Post;
            foreach (var sound in GetComponentsInChildren<WorldPopupWindowSound>(true)) sound.Post = Map14HandInUiAudio.Post;
            GetComponent<WorldPopupWindow>().Show();
        }

        // client-source/UIUtils.SetUIGray, applied to the original hand-in button tree.
        private void SetConfirmationGray(bool gray)
        {
            if (grayImageMaterial == null) throw new InvalidDataException("Official hand-in gray material is unbound.");
            foreach (Graphic graphic in mComfirmBtn.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is TextMeshProUGUI)
                {
                    var effect = graphic.GetComponent<TMPEffect>();
                    if (gray) { if (effect == null) effect = graphic.gameObject.AddComponent<TMPEffect>(); effect.SetGray(true); }
                    else if (effect != null) effect.SetGray(false);
                }
                else if (graphic is Image || graphic is RawImage) graphic.material = gray ? grayImageMaterial : null;
            }
        }

        private static void ShowRequireCount(WorldItemIcon icon, long owned, long required)
        {
            var progress=icon.GetComponentInChildren<WorldItemProgress>(true);
            if (progress==null) throw new InvalidDataException("Original item progress component is missing.");
            progress.Compact=OfficialPropFormat.Compact;
            progress.gameObject.SetActive(true);
            progress.Init(owned,required,WorldItemProgress.ProgressType.Require);
            var document=(Dictionary<string,object>)OfficialEquipmentJson.Parse(Resources.Load<TextAsset>("Map14/UI/HandIn/catalog").text,true);
            Color ColorOf(string name)
            {
                var values=(List<object>)document[name];
                return new Color(Convert.ToSingle(values[0]),Convert.ToSingle(values[1]),Convert.ToSingle(values[2]),Convert.ToSingle(values[3]));
            }
            progress.SetColor(ColorOf("itemProgressEnoughColor"),ColorOf("itemProgressLackColor"));
            icon.myTextGo.SetActive(false);
        }

        private void Awake() => GetComponent<WorldPopupWindow>().OnHide += OnClosed;
        private void OnClosed(WorldPopupWindow window)
        {
            if (resolved) return;
            resolved=true;
            interrupted?.Invoke();
            Destroy(gameObject);
        }

        private void HandleConfirm() => Resolve(confirmed);
        private void HandleCancel() => Resolve(canceled);
        private void Resolve(Action callback)
        {
            if (resolved) return;
            resolved = true;
            RemoveListeners();
            callback?.Invoke();
            GetComponent<WorldPopupWindow>().Hide(() => Destroy(gameObject));
        }
        private void RemoveListeners()
        {
            if (mCancelBtn != null) mCancelBtn.onClick.RemoveListener(HandleCancel);
            if (mComfirmBtn != null) mComfirmBtn.onClick.RemoveListener(HandleConfirm);
        }
        private void OnDestroy()
        {
            var popup=GetComponent<WorldPopupWindow>();
            if (popup!=null) popup.OnHide-=OnClosed;
            RemoveListeners();
            if (!resolved) { resolved = true; interrupted?.Invoke(); }
            hidden?.Invoke();
        }
    }
}
