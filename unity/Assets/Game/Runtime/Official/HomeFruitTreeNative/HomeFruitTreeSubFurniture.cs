// Original shared fruit-tree sub-furniture controller.
using System;
using Zjcs.Official.HomeFurniture;

namespace Zjcs.Official.HomeFruitTreeNative
{
    public abstract class HomeFruitTreeSubFurniture : FurnitureBase
    {
        public Action OnStateChange;
        public Func<HomeFruitTreeSubFurniture, bool> GetStateIsLock;

        protected override void OnClick() { }

        public override void UpdateState()
        {
            EFurnitureState previous = mFurnitureState;
            base.UpdateState();
            if (previous != mFurnitureState) OnStateChange?.Invoke();
        }

        public override EFurnitureState GetState()
        {
            if (GetStateIsLock != null && GetStateIsLock(this)) return EFurnitureState.Locked;
            return base.GetState();
        }

        public abstract bool TryCallFunction();
        public virtual bool TryCallTip() => false;
    }
}
