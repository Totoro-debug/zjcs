using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    public class WorldTabAnimatorTransition : WorldTabTransition
    {
        public Animator Anim;
        public string ToggleOn = "ToggleOn";
        public string ToggleOff = "ToggleOff";
        public float InstantStartTime = 1f;
        public override void PlayEffect(bool value, bool instant = false)
        {
            if (Anim == null) return;
            string state = value ? ToggleOn : ToggleOff;
            if (instant) Anim.Play(state, 0, InstantStartTime);
            else
            {
                Anim.ResetTrigger(ToggleOn);
                Anim.ResetTrigger(ToggleOff);
                Anim.SetTrigger(state);
            }
        }
    }
}
