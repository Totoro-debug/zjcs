using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14SharedRootAcceptance
    {
        public static IEnumerator Verify(NativeMap14SharedAssemblies shared, NativeMap14SharedConfiguration configuration,
            NativeMap14SharedData data, IEnumerable<int> placements)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                var root = new NativeMap14SharedRoot(shared, configuration, data, 14 + pass);
                try
                {
                    var components = (IList)Get(root.Root, "ComponentList");
                    Require(components.Count == 9, "root component coverage differs");
                    Require(ReferenceEquals(Get(root.Container, "RootEntity"), root.Root), "original root lookup differs");
                    Require(ReferenceEquals(Get(root.Ground, "AreaAgent"), null), "root awakened before start");
                    var order = new List<string>();
                    var observations = new List<bool>();
                    using (Subscribe(shared, root.Container, "StartEntityEvent", "IEcContainerStartEntityEventReceiver", args =>
                    {
                        order.Add("entity-start");
                        observations.Add(ReferenceEquals(args[0], root.Root) && !(bool)args[1] &&
                            (bool)Get(root.Root, "Awaked") && (bool)Get(root.Root, "Started") &&
                            components.Cast<object>().All(component => (bool)Get(component, "Awaked") && (bool)Get(component, "Started")));
                    }))
                    using (Subscribe(shared, root.Container, "StartEvent", "IEcContainerStartEventReceiver", args => order.Add("container-start")))
                    using (Subscribe(shared, root.Container, "RemoveEntityEvent", "IEcContainerRemoveEntityEventReceiver", args =>
                    {
                        order.Add("remove");
                        observations.Add(ReferenceEquals(args[0], root.Root) && (bool)Get(root.Root, "Removing") &&
                            !(bool)Get(root.Root, "Ended") && ((IDictionary)Get(root.Container, "EntityDict")).Count == 0);
                    }))
                    using (Subscribe(shared, root.Container, "DestroyEntityEvent", "IEcContainerDestroyEntityEventReceiver", args =>
                    {
                        order.Add("destroy");
                        observations.Add(ReferenceEquals(args[0], root.Root) && (bool)Get(root.Root, "Ended") &&
                            components.Cast<object>().All(component => (bool)Get(component, "Ended")));
                    }))
                    {
                        root.Start();
                        Require(string.Join(",", order) == "entity-start,container-start" && observations.All(value => value), "root startup ordering differs");
                        Require(ReferenceEquals(Get(root.Ground, "AreaAgent"), root.AreaAgent), "original area binding differs");
                        object positions = Get(data.Navigation, "PosDataArray");
                        var mask = root.Ground.GetType().GetMethod("GetGridMask");
                        var area = root.AreaAgent.GetType().GetMethod("GridToArea");
                        for (int y = 0; y < 280; y++)
                        {
                            var row = (Array)((Array)positions).GetValue(y);
                            for (int x = 0; x < 280; x++)
                            {
                                object pos = Activator.CreateInstance(shared.CommonType("Pos2d"), x, y);
                                object cell = row.GetValue(x);
                                Require((int)mask.Invoke(root.Ground, new[] { pos }) == 1 << Convert.ToInt32(Get(cell, "GroundType")), "initialized ground mask differs");
                                Require((int)area.Invoke(root.AreaAgent, new[] { pos }) == (int)Get(cell, "Area"), "initialized original area index differs");
                            }
                            if ((y & 7) == 0) yield return null;
                        }
                        if (pass == 0)
                        {
                            int count = 0;
                            foreach (int id in placements)
                            {
                                object prefab = data.GetContainerPrefab(id);
                                object entity = root.Instantiate(prefab);
                                var source = (IList)Get(prefab, "ComponentList");
                                var actual = (IList)Get(entity, "ComponentList");
                                Require(!ReferenceEquals(entity, prefab) && (int)Get(entity, "ClassId") == (int)Get(prefab, "ClassId") &&
                                    actual.Count == source.Count && !(bool)Get(entity, "Awaked") && Get(entity, "Container") == null,
                                    "fresh original entity initialization differs");
                                for (int index = 0; index < actual.Count; index++)
                                    Require(!ReferenceEquals(actual[index], source[index]) && actual[index].GetType() == source[index].GetType() &&
                                        ReferenceEquals(Get(actual[index], "_Info"), Get(source[index], "_Info")) &&
                                        ReferenceEquals(Get(actual[index], "Entity"), entity), "original component identity/binding/order differs");
                                if ((++count & 31) == 0) yield return null;
                            }
                            Require(count == 2961, "fresh map placement count differs");
                        }
                        root.Dispose();
                        root.Dispose();
                        Require(string.Join(",", order) == "entity-start,container-start,remove,destroy" && observations.Count == 3 &&
                            observations.All(value => value), "root teardown ordering differs");
                        Require(ReceiverCount(Get(root.Container, "AddEntityEvent")) == 0 &&
                            ReceiverCount(Get(root.Container, "StartEntityEvent")) == 1 &&
                            ReceiverCount(Get(root.Container, "RemoveEntityEvent")) == 1, "original root receivers were not removed");
                    }
                    Require(ReceiverCount(Get(root.Container, "StartEntityEvent")) == 0 &&
                        ReceiverCount(Get(root.Container, "RemoveEntityEvent")) == 0 &&
                        ReceiverCount(Get(root.Container, "DestroyEntityEvent")) == 0 &&
                        ReceiverCount(Get(root.Container, "StartEvent")) == 0, "local receiver scope was not removed");
                }
                finally { root.Dispose(); }
            }
            Debug.Log("Map14 original root Play Mode passed: complete nine-component root startup/removal/recreation; 78400 original masks and area indices on each pass; fresh original instantiation of all 2961 placements; event ordering and receiver teardown. World spawning, visibility state, formal view/input/save and Player remain pending.");
        }

        private static NativeMap14SharedEventSubscription Subscribe(NativeMap14SharedAssemblies shared, object container,
            string dispatcher, string receiver, Action<object[]> callback) =>
            new NativeMap14SharedEventSubscription(Get(container, dispatcher), shared.CommonType(receiver), callback);

        private static int ReceiverCount(object dispatcher) => ((IList)Get(dispatcher, "mRecordEventReceivers")).Count;

        private static object Get(object value, string name)
        {
            for (Type type = value.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(value);
                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (property != null) return property.GetValue(value);
            }
            throw new MissingMemberException(value.GetType().FullName, name);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException("Original Map14 root: " + message);
        }
    }
}
