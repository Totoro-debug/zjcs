using UnityEngine;
using System;

namespace Zjcs.Presentation.Home
{
    // Original DailyDungeonEquipAward show/animation-event order. Item details
    // remain owned by the separately migrated shared item module.
    public sealed class DailyDungeonResultAwardSlotAdapter : MonoBehaviour
    {
        public Action PlayOpenEffect;
        public void ShowAward()
        {
            transform.Find("Content").gameObject.SetActive(true);
            GetComponent<Animation>().Play();
        }
        public void OpenAward() => PlayOpenEffect?.Invoke();
    }
}
