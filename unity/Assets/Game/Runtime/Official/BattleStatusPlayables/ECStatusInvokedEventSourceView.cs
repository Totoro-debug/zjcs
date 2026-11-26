using System;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Official.BattleStatusPlayables
{
    [Serializable]
    public sealed class StatusInvokedArgs : FightViewEventArgs
    {
        public MyEntity StatusOwner, Status;
    }

    [Serializable]
    public sealed class ECStatusInvokedEventSourceView : ECFightViewEventSourceBase<StatusInvokedArgs>
    {
        private IFightStatusPlayableState mStatus;
        private MyFightStatus mStatusShell;
        private IDisposable mSubscription;

        protected override bool TryBind(out string error)
        {
            mStatus = GetComponent<IFightStatusPlayableState>();
            mStatusShell = mOwner.GetComponentInParent<MyFightStatus>();
            if (mStatus == null || !mStatusShell)
            { error = "FightStatusComponent not found on entity"; return false; }
            mStatus.OnInvoked += OnInvoked;
            mSubscription = new CurrentStatusViewSubscription(() => mStatus.OnInvoked -= OnInvoked);
            error = null;
            return true;
        }

        protected override void Unbind()
        {
            mSubscription?.Dispose();
            mSubscription = null; mStatus = null; mStatusShell = null;
        }

        private void OnInvoked(IFightStatusPlayableState status)
        {
            var owner = mStatusShell.Context?.Owner;
            if (!owner || mStatusShell.Context.Agent == null) return;
            mSharedArgs.StatusOwner = owner.GetComponent<MyEntity>();
            mSharedArgs.Status = mOwner.GetComponentInParent<MyEntity>();
            Dispatch();
        }

        protected override void Reset(StatusInvokedArgs args) { args.StatusOwner = null; args.Status = null; }
    }
}
