using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using GConfig;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14SharedDataAcceptance
    {
        public static IEnumerator Verify(NativeMap14SharedAssemblies shared, NativeMap14SharedConfiguration configuration)
        {
            using (var data = new NativeMap14SharedData(shared, configuration))
            {
                yield return data.LoadAll();
                Require(data.Loaded, "map load did not finish");
                Require((int)Get(data.Navigation, "MapClassId") == 14, "navigation map identity differs");
                Require(((IDictionary)Get(data.Placement, "RegionInfos")).Count == 111, "region count differs");
                string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
                var expected = JObject.Parse(File.ReadAllText(Path.Combine(repository, "unity/Assets/Official/World/Map/14/Source/entities.json")));
                int entityCount = 0, componentCount = 0;
                foreach (var row in expected.Properties())
                {
                    int id = int.Parse(row.Name, CultureInfo.InvariantCulture);
                    object entity = data.GetContainerPrefab(id);
                    Require(entity != null, "original placement is absent: " + id);
                    Require((int)Get(entity, "ClassId") == id, "original placement class identity differs");
                    var components = (IList)Get(entity, "ComponentList");
                    var componentRows = (JObject)row.Value;
                    Require(components.Count == componentRows.Count, "component count differs: " + id);
                    int index = 0;
                    foreach (var componentRow in componentRows.Properties())
                    {
                        object component = components[index++];
                        Require(component.GetType().FullName == "Common." + componentRow.Name, "component order/type differs: " + id);
                        object info = Get(component, "_Info");
                        if (info != null || ((JObject)componentRow.Value).Count != 0)
                            Compare(componentRow.Value, info, row.Name + "." + componentRow.Name);
                        componentCount++;
                    }
                    entityCount++;
                    if ((entityCount & 31) == 0) yield return null;
                }
                Require(entityCount == 2961 && componentCount == 22979, "map entity/component totals differ");
                var root = data.GetCommonPrefab(14);
                Require(root != null, "original map root prefab is absent");
                var ground = ((IList)Get(root, "ComponentList")).Cast<object>().Single(component => component.GetType().Name == "GridGroundComponent");
                var groundInfo = Get(ground, "Info");
                object size = Get(groundInfo, "Size");
                Require((int)Get(size, "X") == 280 && (int)Get(size, "Y") == 280, "original grid dimensions differ");
                var origin = (Vector3)Get(groundInfo, "OriginPos");
                Require(origin == new Vector3(-134.5f, -134.5f, 0), "original grid origin differs");
                var positionType = shared.CommonType("Pos2d");
                var gridToPos = ground.GetType().GetMethod("GridToPos", new[] { positionType });
                var posToGrid = ground.GetType().GetMethod("PosToGrid", new[] { typeof(Vector3) });
                var gridType = groundInfo.GetType().GetMethod("GetGridGroundType");
                var cells = (Array)Get(data.Navigation, "PosDataArray");
                for (int y = 0; y < 280; y++)
                {
                    var cellsRow = (Array)cells.GetValue(y);
                    for (int x = 0; x < 280; x++)
                    {
                        object position = Activator.CreateInstance(positionType, x, y);
                        object cell = cellsRow.GetValue(x);
                        Require(Convert.ToInt32(gridType.Invoke(groundInfo, new[] { position })) == Convert.ToInt32(Get(cell, "GroundType")), "full/lite walkability differs");
                        var world = (Vector3)gridToPos.Invoke(ground, new[] { position });
                        object roundTrip = posToGrid.Invoke(ground, new object[] { world });
                        Require((int)Get(roundTrip, "X") == x && (int)Get(roundTrip, "Y") == y, "original coordinate round trip differs");
                    }
                    if ((y & 7) == 0) yield return null;
                }
                yield return OfficialMap14SharedRootAcceptance.Verify(shared, configuration, data,
                    expected.Properties().Select(row => int.Parse(row.Name, CultureInfo.InvariantCulture)));
            }
            // The map scope must restore the earlier settings-only loader.
            var settings = configuration.Manager.GetParser(shared.CommonType("GameSettingsParser"));
            ConfigRequest request = settings.Parse((ParsePhase)7, false);
            configuration.Manager.Wait(new[] { request });
            Require(request.Success, "settings loader was not restored after map scope disposal");
            Debug.Log("Map14 original shared data Play Mode passed: 2961 entities / 22979 components in original order, source fields, original template/root loading, 111 regions, all 78400 full/lite ground cells and coordinate round trips, and settings loader restoration. Formal map views, input, gameplay, save and Player remain pending.");
        }

        private static object Get(object value, string name)
        {
            for (Type type = value.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(value);
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null) return property.GetValue(value);
            }
            throw new InvalidDataException("Original field is absent: " + value.GetType().FullName + "." + name);
        }

        private static void Compare(JToken expected, object actual, string path)
        {
            if (expected.Type == JTokenType.Null) { Require(actual == null, "null differs: " + path); return; }
            Require(actual != null, "value is absent: " + path);
            if (expected is JObject obj)
            {
                if (actual is IDictionary dictionary)
                {
                    Type keyType = actual.GetType().GetGenericArguments()[0];
                    Require(dictionary.Count == obj.Count, "dictionary count differs: " + path);
                    foreach (var property in obj.Properties())
                        Compare(property.Value, dictionary[Convert.ChangeType(property.Name, keyType, CultureInfo.InvariantCulture)], path + "." + property.Name);
                }
                else foreach (var property in obj.Properties()) Compare(property.Value, Get(actual, property.Name), path + "." + property.Name);
                return;
            }
            if (expected is JArray array)
            {
                var list = (IList)actual;
                Require(list.Count == array.Count, "array count differs: " + path);
                for (int i = 0; i < array.Count; i++) Compare(array[i], list[i], path + "[" + i + "]");
                return;
            }
            var value = ((JValue)expected).Value;
            if (actual is float single)
                Require(BitConverter.SingleToInt32Bits(single) == BitConverter.SingleToInt32Bits(Convert.ToSingle(value, CultureInfo.InvariantCulture)), "float bits differ: " + path);
            else if (actual.GetType().IsEnum)
                Require(Convert.ToInt64(actual) == Convert.ToInt64(value), "enum value differs: " + path);
            else Require(Equals(actual, Convert.ChangeType(value, actual.GetType(), CultureInfo.InvariantCulture)), "field differs: " + path);
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidDataException("Original Map14 shared data: " + message);
        }
    }
}
