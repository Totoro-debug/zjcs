using System;
using System.Collections;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Presentation.World.NativeMap14;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfficialMap14NativeWorldView nativeMap14World;
        private float nativeMap14RefreshAt;
        private MainlineSharedWorld mainlineSharedWorld;
        private IEnumerator mainlineSharedLoad;
        private Coroutine mainlineSharedLoading;

        private void InitializeNativeMap14()
        {
            if (mapClassId != 14 || avatar == null || worldCamera == null)
                throw new InvalidOperationException("The native Map14 world requires its initialized player and camera.");
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            mainlineSharedWorld = new MainlineSharedWorld(catalog, mapClassId, mapClassId);
            mainlineSharedLoad = mainlineSharedWorld.Load();
            mainlineSharedLoading = StartCoroutine(LoadNativeMap14World());
            nativeMap14RefreshAt = Time.unscaledTime + 0.25f;
        }

        private IEnumerator LoadNativeMap14World()
        {
            while (mainlineSharedLoad.MoveNext()) yield return mainlineSharedLoad.Current;
            mainlineSharedLoad = null;
            nativeMap14World = OfficialMap14NativeWorldView.Create(mapRoot, worldCamera,
                new MainlineNativeGroundState(this), mainlineSharedWorld);
        }

        private void RefreshNativeMap14State()
        {
            if (nativeMap14World == null || Time.unscaledTime < nativeMap14RefreshAt) return;
            nativeMap14RefreshAt = Time.unscaledTime + 0.25f;
            nativeMap14World.RefreshState();
        }

        private void DisposeNativeMap14()
        {
            if (mainlineSharedLoading != null) StopCoroutine(mainlineSharedLoading);
            mainlineSharedLoading = null;
            try
            {
                nativeMap14World?.Dispose();
                nativeMap14World = null;
            }
            finally
            {
                try { (mainlineSharedLoad as IDisposable)?.Dispose(); }
                finally
                {
                    mainlineSharedLoad = null;
                    mainlineSharedWorld?.Dispose();
                    mainlineSharedWorld = null;
                }
            }
        }
    }
}
