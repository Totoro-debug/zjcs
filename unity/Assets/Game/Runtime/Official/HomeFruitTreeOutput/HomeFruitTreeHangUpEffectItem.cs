// Original fruit-effect pool component.
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeFruitTreeOutput
{
    public class HomeFruitTreeHangUpEffectItem : MonoBehaviour, IGameObjectPoolItem
    {
        public virtual void Init(int index) { }
        public void OnGameObjectPoolReset() { }
        public void OnGameObjectPoolClear() { }
    }
}
