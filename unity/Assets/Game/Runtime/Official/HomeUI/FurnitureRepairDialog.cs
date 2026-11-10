using System;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.HomeUI
{
    // Resource/configuration and the successful server response are supplied by
    // the local host. The original window's keys, field names and order remain.
    public interface IFurnitureRepairDialogServices
    {
        string GetFurnitureTypeNameKey(EFurniture furniture);
        FurnitureIconWidget GetFurnitureIconPrefab(EFurniture furniture);
        bool HasRepairCost(EFurniture furniture);
        void SystemMessage(string key);
        void Repair(EFurniture furniture, Action onSuccess);
        void ShowRepairedNotify(EFurniture furniture);
        void Close(GameObject window);
    }

    public sealed class FurnitureRepairDialog : MonoBehaviour
    {
        public WorldMultiLanguageText SubTitleName;
        public WorldMultiLanguageText Name;
        public WorldMultiLanguageText Desc;
        public Transform ImageSlot;
        public Button RepairBtn;
        public WorldMultiLanguageText RepairBtnTxt;

        public IFurnitureRepairDialogServices Services { private get; set; }
        public EFurniture Furniture { get; private set; }
        public GameObject Operator => gameObject;

        private void Awake() => RepairBtn.onClick.AddListener(OnRepairBtnClick);

        public void Init(EFurniture furniture, bool isBuild)
        {
            Furniture = furniture;
            Name.SetKey("item_unrepair_" + furniture);
            Desc.SetKey("item_unrepair_desc_" + furniture);
            SubTitleName.SetKey(Services.GetFurnitureTypeNameKey(Furniture));
            RepairBtnTxt.SetKey(isBuild ? "ui_build_text" : "ui_fix_text");
            foreach (Transform child in ImageSlot) Destroy(child.gameObject);
            Instantiate(Services.GetFurnitureIconPrefab(Furniture), ImageSlot).SetState(true);
        }

        private void OnRepairBtnClick()
        {
            if (!Services.HasRepairCost(Furniture))
            {
                Services.SystemMessage("ui_item_not_enough");
                return;
            }
            Services.Repair(Furniture, () =>
            {
                Services.ShowRepairedNotify(Furniture);
                Services.Close(gameObject);
            });
        }
    }
}
