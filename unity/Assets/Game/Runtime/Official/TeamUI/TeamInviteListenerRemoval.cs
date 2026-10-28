using System;
namespace Zjcs.Official.TeamUI
{
    internal sealed class TeamInviteListenerRemoval : IDisposable
    {
        private Action remove;
        public TeamInviteListenerRemoval(Action value) { remove = value; }
        public void Dispose() { Action action = remove; remove = null; action?.Invoke(); }
    }
}
