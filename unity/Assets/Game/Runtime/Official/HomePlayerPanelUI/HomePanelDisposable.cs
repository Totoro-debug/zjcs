using System;

namespace Zjcs.Official.HomePlayerPanelUI
{
    internal static class HomePanelDisposable
    {
        public static T AddTo<T>(T handle, ref HomePanelDisposableBag bag) where T : IDisposable
        {
            bag.Add(handle);
            return handle;
        }
    }
}
