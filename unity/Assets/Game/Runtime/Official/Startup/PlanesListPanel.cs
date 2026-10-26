using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.Startup
{
    // PlanesListPanel.InitShow / OnTabItemChange and TabAnimatorTransition.PlayEffect.
    // The offline host supplies local rows and the create action instead of RPC providers.
    public sealed class PlanesListPanel : MonoBehaviour
    {
        private const string Content = "BgDialog1 (1)/Content/";
        private Transform roleTab, createTab;
        private GameObject roles, other;
        private bool hasRows;
        public bool IsCreatePage { get; private set; }
        public Transform RoleItemSlot => transform.Find(Content + "Content/RoleListContent/ViewPort/Content");
        public TMP_Text MessageText => transform.Find(Content + "Content/OtherPlanesGo/EmptyTip/Text").GetComponent<TMP_Text>();
        public Button CreateButton => transform.Find(Content + "Content/OtherPlanesGo/EmptyTip/CommonBtn_12").GetComponent<Button>();

        public void Init(bool hasProfiles, Action create)
        {
            hasRows = hasProfiles;
            roleTab = transform.Find(Content + "TabGroup/TabGroup/TabGroup/NormalMailDialogTabGroupItem");
            createTab = transform.Find(Content + "TabGroup/TabGroup/TabGroup/GiftMailDialogTabGroupItem");
            roleTab.GetComponentInChildren<TMP_Text>(true).text = "已有存档";
            createTab.GetComponentInChildren<TMP_Text>(true).text = "新建存档";
            BindTab(roleTab, () => OnTabItemChange(false));
            BindTab(createTab, () => OnTabItemChange(true));
            roles = transform.Find(Content + "Content/RoleListContent").gameObject;
            other = transform.Find(Content + "Content/OtherPlanesGo").gameObject;
            other.transform.Find("ViewPort").gameObject.SetActive(false);
            other.transform.Find("SearchGo").gameObject.SetActive(false);
            other.GetComponent<ScrollRect>().enabled = false;
            other.transform.Find("EmptyTip").gameObject.SetActive(true);
            CreateButton.gameObject.SetActive(true);
            CreateButton.GetComponentInChildren<TMP_Text>(true).text = "新建存档";
            CreateButton.onClick.AddListener(() => create());
            transform.Find("BgDialog1 (1)/BgImg/BgTitle/TitleText").GetComponent<TMP_Text>().text = "选择存档";
            transform.Find(Content + "BottomGo").gameObject.SetActive(false);
            Transform bottom = transform.Find(Content + "RoleBottomGo");
            bottom.gameObject.SetActive(true);
            bottom.Find("StateDescGo/Idle/Text (TMP)").GetComponent<TMP_Text>().text = "可读取";
            bottom.Find("StateDescGo/Maintenance/Text (TMP)").GetComponent<TMP_Text>().text = "无法读取";
            OnTabItemChange(false, true);
        }

        public void OnTabItemChange(bool create, bool instant = false)
        {
            IsCreatePage = create;
            PlayTab(roleTab, !create, instant);
            PlayTab(createTab, create, instant);
            roles.SetActive(!create && hasRows);
            other.SetActive(create || !hasRows);
            other.transform.Find("EmptyTip/ImgBg").gameObject.SetActive(!create && !hasRows);
            MessageText.text = create ? "开启一段新的冒险旅程" : "暂无存档，开启你的冒险吧";
        }

        private static void BindTab(Transform tab, Action selected)
        {
            Button button = tab.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => selected());
        }

        private static void PlayTab(Transform tab, bool on, bool instant)
        {
            Animator animator = tab.GetComponent<Animator>();
            string state = on ? "ToggleOn" : "ToggleOff";
            if (instant) { animator.Play(state, 0, 1f); animator.Update(0); }
            else
            {
                animator.ResetTrigger("ToggleOn");
                animator.ResetTrigger("ToggleOff");
                animator.SetTrigger(state);
            }
        }
    }
}
