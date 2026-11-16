using System;
using System.Collections;
using UnityEngine;

namespace Zjcs.Presentation.Home
{
    // FurnitureBase.RepairEffect: activate, switch state after ShowTime, then
    // deactivate after the remainder of FullTime. WaitForSeconds uses game time.
    public sealed class HomeRepairEffectPlayer : MonoBehaviour
    {
        public void Play(GameObject effect, float showTime, float fullTime, Action updateState)
        {
            if (!effect || updateState == null || showTime < 0 || fullTime < showTime)
                throw new ArgumentException("Invalid original furniture repair effect binding.");
            StartCoroutine(RepairEffect(effect, showTime, fullTime, updateState));
        }

        private IEnumerator RepairEffect(GameObject effect, float showTime, float fullTime, Action updateState)
        {
            effect.SetActive(true);
            yield return new WaitForSeconds(showTime);
            updateState();
            yield return new WaitForSeconds(fullTime - showTime);
            effect.SetActive(false);
            Destroy(effect.transform.parent.gameObject);
        }
    }
}
