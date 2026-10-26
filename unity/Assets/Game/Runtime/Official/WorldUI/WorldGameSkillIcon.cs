using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    /// <summary>GameSkillIcon state enum/CD display and StateGameObjectConfig.SetState, from the official client.</summary>
    public sealed class WorldGameSkillIcon : MonoBehaviour
    {
        public enum State { None, Ready, Applied, InCD, Ended, LimitedTimes, ProfessionLimit, Disabled }

        public sealed class StateObjects
        {
            public State State;
            public List<GameObject> GameObjects;
        }

        private IReadOnlyList<StateObjects> states;
        private TMP_Text cdText;
        public Image Icon { get; private set; }
        public int SkillIndex { get; private set; }
        public Image QualityImg { get; private set; }

        public void Initialize(int skillIndex, IReadOnlyList<StateObjects> states, Image icon, Image quality, TMP_Text cdText)
        {
            SkillIndex = skillIndex;
            this.states = states ?? throw new ArgumentNullException(nameof(states));
            Icon = icon ?? throw new ArgumentNullException(nameof(icon));
            QualityImg = quality ?? throw new ArgumentNullException(nameof(quality));
            this.cdText = cdText;
        }

        public void SetUIState(State state)
        {
            // Preserve official iteration order and shared-object handling. An absent state
            // disables the objects of every other state; it does not synthesize a new binding.
            List<GameObject> selected = null;
            foreach (StateObjects item in states)
                if (item.State == state) { selected = item.GameObjects; break; }
            foreach (StateObjects item in states)
            {
                if (item.State == state) continue;
                foreach (GameObject gameObject in item.GameObjects)
                {
                    if (gameObject == null) throw new InvalidOperationException("Official skill state has a null prefab reference.");
                    if (selected == null || !selected.Contains(gameObject)) gameObject.SetActive(false);
                }
            }
            if (selected == null) return;
            foreach (GameObject gameObject in selected)
            {
                if (gameObject == null) throw new InvalidOperationException("Official skill state has a null prefab reference.");
                gameObject.SetActive(true);
            }
        }

        public void SetSkillCd(long cdLeft)
        {
            if (cdLeft > 0)
            {
                SetUIState(State.InCD);
                if (cdText != null) cdText.text = cdLeft.ToString();
            }
            else SetUIState(State.Ready);
        }
    }
}
