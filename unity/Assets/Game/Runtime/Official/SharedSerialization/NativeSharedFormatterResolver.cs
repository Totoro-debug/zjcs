using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using UnityEngine;

namespace Zjcs.Official.SharedSerialization
{
    // Constructor selections come from unchanged Console generated resolver IL.
    // Resolve them lazily so unrelated online types need not be initialized.
    public sealed class NativeSharedFormatterResolver : IFormatterResolver
    {
        [Serializable] private sealed class TypeRow
        {
            public string Name;
            public string Assembly;
            public TypeRow[] Arguments;
            public TypeRow Array;
            public int Rank;
        }
        [Serializable] private sealed class Entry { public string Key; public TypeRow Type; }
        [Serializable] private sealed class Lookup { public Entry[] Closed; public Entry[] Open; }

        private readonly Assembly shared;
        private readonly Dictionary<string, TypeRow> closed = new Dictionary<string, TypeRow>();
        private readonly Dictionary<string, TypeRow> open = new Dictionary<string, TypeRow>();
        private readonly Dictionary<Type, object> cache = new Dictionary<Type, object>();

        public NativeSharedFormatterResolver(Assembly shared, string originalLookupJson)
        {
            this.shared = shared ?? throw new ArgumentNullException(nameof(shared));
            var lookup = Newtonsoft.Json.JsonConvert.DeserializeObject<Lookup>(originalLookupJson);
            foreach (var entry in lookup.Closed) closed.Add(entry.Key, entry.Type);
            foreach (var entry in lookup.Open) open.Add(entry.Key, entry.Type);
        }

        private static string Key(Type type)
        {
            if (type.IsArray)
                return Key(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            if (type.IsGenericType)
                return type.GetGenericTypeDefinition().FullName.Replace('+', '/') + "<" + string.Join(",", type.GetGenericArguments().Select(Key)) + ">";
            return type.FullName.Replace('+', '/');
        }

        private Type Resolve(TypeRow row)
        {
            if (row.Array != null)
                return row.Rank == 1 ? Resolve(row.Array).MakeArrayType() : Resolve(row.Array).MakeArrayType(row.Rank);
            Type type;
            if (row.Assembly == "Console" || row.Assembly == "Console.dll")
                type = shared.GetType(row.Name.Replace('/', '+'), true);
            else if (row.Assembly == "GFrameCore")
            {
                Assembly assembly = row.Name.StartsWith("MessagePack.Unity.", StringComparison.Ordinal)
                    ? typeof(NativeSharedFormatterResolver).Assembly : typeof(MessagePackSerializer).Assembly;
                type = assembly.GetType(row.Name, true);
            }
            else type = Type.GetType(row.Name + ", " + row.Assembly, true);
            return row.Arguments != null && row.Arguments.Length > 0
                ? type.MakeGenericType(row.Arguments.Select(Resolve).ToArray()) : type;
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            lock (cache)
            {
                Type type = typeof(T);
                if (cache.TryGetValue(type, out var found)) return (IMessagePackFormatter<T>)found;
                object formatter;
                if (type == typeof(Vector3)) formatter = new MessagePack.Unity.Vector3Formatter();
                else if (type == typeof(Quaternion)) formatter = new MessagePack.Unity.QuaternionFormatter();
                else if (closed.TryGetValue(Key(type), out var row)) formatter = Activator.CreateInstance(Resolve(row));
                else if (type.IsGenericType && open.TryGetValue(type.GetGenericTypeDefinition().FullName.Replace('+', '/'), out row))
                    formatter = Activator.CreateInstance(Resolve(row).MakeGenericType(type.GetGenericArguments()));
                else formatter = BuiltinResolver.Instance.GetFormatter<T>()
                    ?? MessagePack.Unity.UnityResolver.Instance.GetFormatter<T>()
                    ?? DynamicGenericResolver.Instance.GetFormatter<T>()
                    ?? DynamicEnumResolver.Instance.GetFormatter<T>()
                    ?? PrimitiveObjectResolver.Instance.GetFormatter<T>();
                cache[type] = formatter;
                return (IMessagePackFormatter<T>)formatter;
            }
        }
    }
}
