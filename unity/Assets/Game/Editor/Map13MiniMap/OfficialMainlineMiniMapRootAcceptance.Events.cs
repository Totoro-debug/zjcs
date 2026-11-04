using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World.MainlineMiniMap;
using Zjcs.Official.World.Map13MiniMap;
using Status = Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
        private static IEnumerator VerifyEventScopes(GameObject prefab, MainlineMiniMapBindings bindings,
            MainlineMiniMapRootStateFixture state)
        {
            int baseline = state.StatusSubscribers, borrowed = 0, shared = 0;
            Action<int,int,Status,Status> borrowedListener = (map,region,previous,value) => borrowed++;
            Action<int,int,Status,Status> sharedListener = (map,region,previous,value) => shared++;
            state.OnMapStatusUpdate += borrowedListener;
            bindings.Entities.Events.OnMapStatusUpdate += sharedListener;
            MainlineMiniMap first = null, second = null;
            try
            {
                first = MainlineMiniMap.Create(prefab, null, bindings);
                second = MainlineMiniMap.Create(prefab, null, bindings);
                yield return null;
                var roots = UnityEngine.Object.FindObjectsOfType<MainlineNativeMiniMapRoot>().OrderBy(root => root.GetInstanceID()).ToArray();
                Require(roots.Length == 2 && !ReferenceEquals(roots[0].ManagedEvents, roots[1].ManagedEvents),
                    "Two original roots retain independent player-status event scopes");
                int[] received = { 0, 0 };
                for (int index = 0; index < roots.Length; index++)
                {
                    int current = index;
                    roots[index].ManagedEvents.OnMapStatusUpdate += (map,region,previous,value) =>
                    {
                        Require(map == 13 && region == 38 && previous != value, "Original status payload survives root binding");
                        received[current]++;
                    };
                    Require(ReferenceEquals(roots[index].ManagedEvents.AddEntityEvent, bindings.Entities.Events.AddEntityEvent)
                        && ReferenceEquals(roots[index].ManagedEvents.RemoveEntityEvent, bindings.Entities.Events.RemoveEntityEvent)
                        && ReferenceEquals(roots[index].ManagedEvents.ReplaceEntityEvent, bindings.Entities.Events.ReplaceEntityEvent),
                        "Per-root status scopes still share the exact original entity delegates");
                }
                state.EmitStatus(13, 38, Status.Private, Status.Public);
                Require(received.All(count => count == 1) && borrowed == 1 && shared == 0,
                    "One player transition reaches each root once without rebroadcasting through the shared container source");
                first.Dispose(); first.Dispose(); yield return null;
                state.EmitStatus(13, 38, Status.Public, Status.Private);
                Require(received.Sum() == 3 && received.Count(count => count == 1) == 1 && borrowed == 2 && shared == 0,
                    "Disposing one root detaches only its player-status forwarder");
                second.Dispose(); second.Dispose(); yield return null;
                state.EmitStatus(13, 38, Status.Private, Status.Public);
                Require(received.Sum() == 3 && borrowed == 3 && shared == 0 && state.StatusSubscribers == baseline + 1,
                    "All root listeners are released while the borrowed player listener survives");
                var templateHost = new GameObject("IncompleteNativeRootTemplate");
                templateHost.SetActive(false);
                try
                {
                    var incomplete = UnityEngine.Object.Instantiate(prefab, templateHost.transform, false);
                    incomplete.GetComponent<MainlineNativeMiniMapRoot>().Character = null;
                    bool rejected = false;
                    try { MainlineMiniMap.Create(incomplete, null, bindings); }
                    catch (System.IO.InvalidDataException) { rejected = true; }
                    Require(rejected && state.StatusSubscribers == baseline + 1,
                        "A failure after status binding releases its owned player subscription");
                }
                finally { UnityEngine.Object.Destroy(templateHost); }
                yield return null;
            }
            finally
            {
                first?.Dispose(); second?.Dispose();
                state.OnMapStatusUpdate -= borrowedListener;
                bindings.Entities.Events.OnMapStatusUpdate -= sharedListener;
            }
            Require(state.StatusSubscribers == baseline, "Original player subscription count restored");
            Debug.Log("MAINLINE_MINIMAP_EVENT_SCOPES_PASS: two full original roots share entity delegates; player region transitions delivered once per root; independent teardown; borrowed listener retained.");
        }
    }
}
