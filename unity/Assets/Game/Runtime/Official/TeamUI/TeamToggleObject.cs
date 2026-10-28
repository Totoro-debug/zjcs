using UnityEngine;

namespace Zjcs.Official.TeamUI
{
    // Original UIToggleObject.
    public sealed class TeamToggleObject : TeamToggleStatusBase
    {
        public GameObject[] myActiveObj, myDeactiveObj;
        public override void PlayEffect(bool value, bool instant = false)
        {
            if (myActiveObj != null)
                for (int i = 0; i < myActiveObj.Length; i++) myActiveObj[i].SetActive(value);
            if (myDeactiveObj != null)
                for (int i = 0; i < myDeactiveObj.Length; i++) myDeactiveObj[i].SetActive(!value);
        }
    }
}
