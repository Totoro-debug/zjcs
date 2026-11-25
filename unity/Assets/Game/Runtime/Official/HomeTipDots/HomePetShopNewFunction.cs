// Selected NewFunctionManager shop registration and PetGachaPanel tab transition.
using System;

namespace Zjcs.Official.HomeTipDots
{
    public interface IHomePetShopUnlockCondition
    {
        bool IsLimited { get; }
        // Like ConditionHandler.Subscribe, registration does not invoke the callback inline.
        // Null represents a missing condition and leaves the function unregistered.
        IDisposable Subscribe(Action changed);
    }

    public sealed class HomePetShopNewFunction : IDisposable
    {
        public const int ShopFunction = 18;
        public const int PetShopId = 7;
        private readonly HomeNewFunctionState state;
        private readonly IHomePetShopUnlockCondition condition;
        private IDisposable registration;
        private bool initialized;

        public HomePetShopNewFunction(HomeNewFunctionState state, IHomePetShopUnlockCondition condition)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public void Init()
        {
            if (initialized) return;
            initialized = true;
            if (condition.IsLimited) registration = condition.Subscribe(OnConditionChanged);
        }

        private void OnConditionChanged()
        {
            if (!condition.IsLimited)
            {
                state.AddNewFunction(ShopFunction, PetShopId);
                registration?.Dispose();
                registration = null;
            }
        }

        public void OnTabChanged(bool isOn, bool isShop)
        {
            if (isOn && isShop && state.IsNew(ShopFunction, PetShopId))
                state.RemoveNewFunction(ShopFunction, PetShopId);
        }

        public void Dispose()
        {
            registration?.Dispose();
            registration = null;
            initialized = false;
        }
    }
}
