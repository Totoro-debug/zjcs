using UnityEngine;

namespace Zjcs.Official.World.NativeMap14Labels
{
    // Original MapUIBase map-initialized subscription and InitWrapper order.
    public class MapUIBase : UIBase
    {
        protected override void Start()
        {
            base.Start();
            if (Services.IsMapInitialized) Instance_OnMapInited();
            Services.Initialized += Instance_OnMapInited;
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Services != null) Services.Initialized -= Instance_OnMapInited;
        }
        private void Instance_OnMapInited()
        {
            if (Services.IsMapInitialized) InitWrapper(InitSub);
        }
        protected virtual void InitSub() { }
    }
}
