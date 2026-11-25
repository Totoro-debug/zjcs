using System;
using UnityEngine;
using Zjcs.Official.HomeTipDots;

namespace Zjcs.Editor.HomeShopNewMark
{
    public static class PetShopNewFunctionValidation
    {
        sealed class Condition : IHomePetShopUnlockCondition
        {
            sealed class Lease : IDisposable
            {
                readonly Condition owner;
                public Lease(Condition owner) { this.owner = owner; }
                public void Dispose() { owner.Callback = null; owner.Disposals++; }
            }
            public bool IsLimited { get; set; }
            public bool Missing;
            public int Subscriptions, Disposals;
            public Action Callback;
            public IDisposable Subscribe(Action changed)
            {
                Subscriptions++;
                if (Missing) return null;
                Callback = changed;
                return new Lease(this);
            }
            public void Notify() => Callback?.Invoke();
        }

        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        public static void Run()
        {
            var state = new HomeNewFunctionState();
            var unlocked = new Condition { IsLimited = false };
            var alreadyOpen = new HomePetShopNewFunction(state, unlocked);
            alreadyOpen.Init();
            Require(unlocked.Subscriptions == 0 && !state.IsNew(18, 7), "Already-open shops must not acquire a new mark.");
            alreadyOpen.Dispose();

            var condition = new Condition { IsLimited = true };
            var session = new HomePetShopNewFunction(state, condition);
            int additions = 0, removals = 0;
            state.OnNewFunction += (type, arg) => additions++;
            state.OnRemoveNewFunction += (type, arg) => removals++;
            session.Init(); session.Init();
            Require(condition.Subscriptions == 1, "Player initialization must register only once.");
            condition.Notify();
            Require(!state.IsNew(18, 7) && additions == 0 && condition.Disposals == 0,
                "A still-locked shop must keep waiting.");
            condition.IsLimited = false; condition.Notify();
            Require(state.IsNew(18, 7) && additions == 1 && condition.Disposals == 1,
                "Unlock must mark the real state once and release the condition registration.");
            condition.Notify(); session.OnTabChanged(false, true); session.OnTabChanged(true, false);
            Require(state.IsNew(18, 7) && removals == 0, "Only selecting the shop tab clears the mark.");
            session.OnTabChanged(true, true); session.OnTabChanged(true, true);
            Require(!state.IsNew(18, 7) && additions == 1 && removals == 1,
                "Selecting the shop clears the mark once, before tab presentation.");
            session.Dispose();

            var missing = new Condition { IsLimited = true, Missing = true };
            var noCondition = new HomePetShopNewFunction(state, missing);
            noCondition.Init(); noCondition.Dispose();
            Require(missing.Subscriptions == 1 && missing.Disposals == 0 && !state.IsNew(18, 7),
                "A missing official condition must not invent an unlock.");
            Debug.Log("HOME_PET_SHOP_NEW_FUNCTION_VALIDATION_PASSED");
        }
    }
}
