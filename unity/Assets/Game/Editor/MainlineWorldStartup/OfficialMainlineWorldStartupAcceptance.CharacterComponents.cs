using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Offline.World.NativeMap14ClientComponents;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;

namespace Zjcs.Editor.MainlineWorldStartup
{
    public static partial class OfficialMainlineWorldStartupAcceptance
    {
        // Original MyEntity lifecycle with real, already-started shared characters.
        // This is a component fixture, not the restored visual character prefab.
        private sealed class CharacterClientComponents : IDisposable
        {
            private readonly NativeMap14SharedViewCache views;
            private readonly NativeMap14ClientComponentBindings bindings;
            private readonly GameObject host;
            private readonly MyEntity entity;
            private NativeMap14SharedViewEntity previousView;
            private object[] previousComponents;

            public CharacterClientComponents(MainlineSharedWorld world)
            {
                bindings = new NativeMap14ClientComponentBindings(world.Assemblies);
                views = new NativeMap14SharedViewCache(world.Assemblies, bindings.Bind);
                host = new GameObject("Original character component binding fixture");
                host.SetActive(false);
                entity = host.AddComponent<MyEntity>();
                host.AddComponent<MyLevel>();
                host.AddComponent<MyProp>();
                host.AddComponent<MyMapRegionStatus>();
                host.AddComponent<MyMapStatusAgent>();
            }

            public void Verify(MainlineSharedWorld world)
            {
                var view = views.Wrap(world.ClientCharacter);
                if (previousView != null)
                {
                    entity.End(null); entity.Remove(null); entity.OnGameObjectPoolReset();
                    if (!ReferenceEquals(view, previousView))
                        throw new InvalidDataException("Character transition replaced the retained original adapter.");
                }
                entity.Bind(null, view, previousView == null);
                if (entity.Components.Count != 4)
                    throw new InvalidDataException("Original character components are absent.");
                var names = new[] { "LevelComponent", "PropComponent", "MapRegionStatusComponent", "MapStatusAgentComponent" };
                var components = entity.Components.ToArray();
                var current = new object[4];
                for (int i = 0; i < names.Length; i++)
                {
                    var component = components[i];
                    var data = component.GetEcComponent() as EntityClientComponentData;
                    var original = world.ClientCharacter.GetType().GetMethod("GetComponent", new[] { typeof(Type) })
                        .Invoke(world.ClientCharacter, new object[] { world.Assemblies.CommonType(names[i]) });
                    if (original == null || data == null || !ReferenceEquals(data.Original, original)
                        || !(component is IEmptyInfoMyComponent) || data.ComponentInfo != null)
                        throw new InvalidDataException("Character binding lost the original component or its empty-info contract: " + names[i]);
                    current[i] = data;
                    if (previousComponents != null && !ReferenceEquals(previousComponents[i], data))
                        throw new InvalidDataException("Character lifecycle rebind changed a retained original component adapter.");
                }
                bindings.Bind(view);
                entity.MyStart();
                if (!entity.myStarted || !world.IsReady)
                    throw new InvalidDataException("Character component lifecycle did not preserve the running shared world.");
                previousView = view; previousComponents = current;
            }

            public void Dispose()
            {
                try
                {
                    if (previousView != null) { entity.End(null); entity.Remove(null); entity.OnGameObjectPoolReset(); }
                }
                finally { views.Dispose(); UnityEngine.Object.Destroy(host); }
            }
        }
    }
}
