using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.Startup
{
    // PlanesInfoItemBase.Init / OnSelectBtnClick and InitRoleInfo's original
    // selection, recent tag, role information and status branches.
    public sealed class PlanesInfoItemWithRoleInfo : MonoBehaviour
    {
        public void Init(string slot, string character, string level, string rating,
            bool readable, bool selected, bool recent, Action select)
        {
            SetSelected(selected);
            transform.Find("Text").GetComponent<TMP_Text>().text = slot;
            transform.Find("NameTxt").GetComponent<TMP_Text>().text = character;
            transform.Find("LevelTxt").GetComponent<TMP_Text>().text = level;
            transform.Find("CombatRatingItemGo/CombatRatingText").GetComponent<TMP_Text>().text = rating;
            transform.Find("CombatRatingItemGo").gameObject.SetActive(!string.IsNullOrEmpty(rating));
            transform.Find("Recently").gameObject.SetActive(recent);
            transform.Find("Status/Recommend").gameObject.SetActive(readable);
            transform.Find("Status/Maintain").gameObject.SetActive(!readable);
            foreach (string name in new[] { "DeleteBtn", "Reset", "VerticalLineIMg", "Countdown1" })
                transform.Find(name).gameObject.SetActive(false);
            Button button = GetComponent<Button>();
            button.onClick.AddListener(() => { SetSelected(true); select(); });
            // The portrait is part of the enclosing selectable row here.
            foreach (Button child in GetComponentsInChildren<Button>(true))
                if (child != button) child.enabled = false;
        }

        private void SetSelected(bool selected)
        {
            transform.Find("On").gameObject.SetActive(selected);
            transform.Find("Off").gameObject.SetActive(!selected);
        }
    }
}
