using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // Restores the original client prefab configuration. This is not a server
    // entity factory: no player state, rewards or initial server state is made up.
    public sealed class NativeMap14SharedPrefabs
    {
        [Serializable] public sealed class Payload { public Entity[] Entities; }
        [Serializable] public sealed class Entity { public int ClassId; public Component[] Components; }
        [Serializable] public sealed class Component { public string Type; public Field[] Fields; }
        [Serializable] public sealed class Field
        {
            public int Parent;
            public string Name;
            public int Kind;
            public string Value;
        }

        private readonly NativeMap14SharedAssemblies assemblies;
        private readonly Dictionary<int, Entity> sources = new Dictionary<int, Entity>();

        public NativeMap14SharedPrefabs(NativeMap14SharedAssemblies assemblies, NativeMap14SharedAssemblyCatalog catalog)
        {
            this.assemblies = assemblies ?? throw new ArgumentNullException(nameof(assemblies));
            if (!catalog || !catalog.Prefabs) throw new InvalidOperationException("Original shared prefabs are not bound.");
            NativeMap14SharedAssemblies.Verify(catalog.Prefabs.bytes, catalog.PrefabsSha256);
            var payload = JsonUtility.FromJson<Payload>(catalog.Prefabs.text);
            foreach (var source in payload.Entities) sources.Add(source.ClassId, source);
        }

        public object CreateClientPrefab(int classId)
        {
            if (!sources.TryGetValue(classId, out var source))
                throw new InvalidOperationException("Original shared prefab has not been migrated: " + classId);
            var entity = Activator.CreateInstance(assemblies.CommonType("EcEntity"));
            Set(entity, "ClassId", classId);
            var components = (IList)Get(entity, "ComponentList");
            // EcDataCenterParser.Load: class, component construction, original
            // info.Init, _Info, Entity, then append in source order.
            foreach (var input in source.Components)
            {
                var component = Activator.CreateInstance(assemblies.CommonType(input.Type));
                Type infoType = null;
                for (var type = component.GetType(); type != null; type = type.BaseType)
                    if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == "Common.EcComponent`1")
                    { infoType = type.GetGenericArguments()[0]; break; }
                if (infoType != null)
                {
                    var info = Restore(input.Fields, infoType);
                    Call(info, "Init");
                    Set(component, "_Info", info);
                }
                Set(component, "Entity", entity);
                components.Add(component);
            }
            return entity;
        }

        private static object Restore(Field[] fields, Type type)
        {
            if (fields == null || fields.Length == 0 || fields[0].Parent != -1)
                throw new InvalidOperationException("Original shared field root is absent.");
            var children = new List<int>[fields.Length];
            for (var i = 0; i < children.Length; i++) children[i] = new List<int>();
            for (var i = 1; i < fields.Length; i++)
            {
                if (fields[i].Parent < 0 || fields[i].Parent >= i)
                    throw new InvalidOperationException("Original shared field order differs.");
                children[fields[i].Parent].Add(i);
            }
            return RestoreValue(fields, children, 0, type);
        }

        private static object RestoreValue(Field[] fields, List<int>[] children, int index, Type type)
        {
            var field = fields[index];
            switch (field.Kind)
            {
                case 5:
                    if (type.IsValueType && Nullable.GetUnderlyingType(type) == null)
                        throw new InvalidOperationException("Null original value type: " + type.FullName);
                    return null;
                case 2:
                    if (type != typeof(string)) throw new InvalidOperationException("Original string type differs.");
                    return field.Value;
                case 3:
                    if (type != typeof(bool)) throw new InvalidOperationException("Original bool type differs.");
                    return bool.Parse(field.Value);
                case 4:
                    return type.IsEnum
                        ? Enum.ToObject(type, Convert.ChangeType(field.Value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture))
                        : Convert.ChangeType(field.Value, type, CultureInfo.InvariantCulture);
                case 1:
                    if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(List<>))
                        throw new InvalidOperationException("Original shared list type is not supported: " + type.FullName);
                    var list = (IList)Activator.CreateInstance(type);
                    var element = type.GetGenericArguments()[0];
                    foreach (var child in children[index]) list.Add(RestoreValue(fields, children, child, element));
                    return list;
                case 0:
                    var result = Activator.CreateInstance(type);
                    foreach (var child in children[index])
                    {
                        var name = fields[child].Name;
                        // Current original generated SubStageStateInfoFormatter
                        // consumes field 3 without assigning Select. Keep that
                        // behavior rather than treating raw server data as a rule.
                        if (type.FullName == "Common.SubStageStateInfo" && name == "Select") continue;
                        var target = FindField(type, name);
                        target.SetValue(result, RestoreValue(fields, children, child, target.FieldType));
                    }
                    return result;
                default: throw new InvalidOperationException("Unknown original field kind: " + field.Kind);
            }
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new InvalidOperationException("Original shared field is absent: " + type.FullName + "." + name);
        }

        public static object Get(object owner, string name)
        {
            var field = owner.GetType().GetField(name);
            return field != null ? field.GetValue(owner) : owner.GetType().GetProperty(name).GetValue(owner);
        }

        public static void Set(object owner, string name, object value)
        {
            var field = owner.GetType().GetField(name);
            if (field != null) field.SetValue(owner, value);
            else owner.GetType().GetProperty(name).SetValue(owner, value);
        }

        public static object Call(object owner, string name) => owner.GetType().GetMethod(name, Type.EmptyTypes).Invoke(owner, null);
    }
}
