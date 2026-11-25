using System;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeFurniture
{
    public interface IFunctionUnlockTipRegistration : IDisposable
    {
        bool IsMet();
    }
    public interface IFunctionUnlockTipServices
    {
        IFunctionUnlockTipRegistration Subscribe(int type, int key0, int key1, Action changed);
    }

    [Serializable]
    public sealed class TipDotFunctionUnlockConditionNode : TipDotConditionNodeBase
    {
        public int Type, Key0, Key1;
        [NonSerialized] public IFunctionUnlockTipServices Services;
        private IFunctionUnlockTipRegistration registration;

        public override void Init()
        {
            base.Init();
            registration = Services?.Subscribe(Type, Key0, Key1, SetDirty);
            if (registration == null) UnityEngine.Debug.LogError("Function condition unavailable: " + Type + ", " + Key0 + ", " + Key1);
        }
        public override void Release()
        {
            registration?.Dispose();
            registration = null;
            base.Release();
        }
        public override bool CheckShowTip() => registration != null && registration.IsMet();
        public override TipDotNodeBase Clone() => new TipDotFunctionUnlockConditionNode { Type = Type, Key0 = Key0, Key1 = Key1 };
    }
}
