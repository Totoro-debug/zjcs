// Original alternating decoration states for Christmas fruits.
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.HomeFruitTreeOutput
{
    public class HomeFruitTreeChristmasHangUpEffectItem : HomeFruitTreeHangUpEffectItem
    {
        public WorldStateSwitcher<int> StateSwitcher;
        public override void Init(int index)
        {
            base.Init(index);
            StateSwitcher.SetState(index % 2);
        }
    }
}
