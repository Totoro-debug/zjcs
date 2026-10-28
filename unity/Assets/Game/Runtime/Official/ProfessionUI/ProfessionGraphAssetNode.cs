// Ported from the verified official source; only dependency/serialization adapters differ.
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Zjcs.Official.ProfessionUI
{
    // Original native node fields and dictionary serialization. Only graph reading
    // is consumed by profession UI; no graph-editing or story/server execution API.
    public abstract class ProfessionGraphAssetBase : ScriptableObject
    {
        [SerializeField] public List<ProfessionGraphAssetNode> nodes = new List<ProfessionGraphAssetNode>();
    }
    public abstract class ProfessionGraphAssetNode : ScriptableObject
    {
        public enum ConnectionType { Multiple, Override }
        public enum TypeConstraint { None, Inherited, Strict, InheritedInverse, InheritedAny }
        public enum FlowConnectionType { None, Branch, Main }
        [Serializable]
        public class NodePortDictionary : Dictionary<string, ProfessionGraphPort>, ISerializationCallbackReceiver
	{
		[SerializeField]
		private List<string> keys = new List<string>();

		[SerializeField]
		private List<ProfessionGraphPort> values = new List<ProfessionGraphPort>();

		public void OnBeforeSerialize()
		{
			keys.Clear();
			values.Clear();
			keys.Capacity = Count;
			values.Capacity = Count;
			using Enumerator enumerator = GetEnumerator();
			while (enumerator.MoveNext())
			{
				KeyValuePair<string, ProfessionGraphPort> current = enumerator.Current;
				keys.Add(current.Key);
				values.Add(current.Value);
			}
		}

		public void OnAfterDeserialize()
		{
			Clear();
			base.EnsureCapacity(keys.Count);
			if (keys.Count != values.Count)
			{
				throw new Exception("there are " + keys.Count + " keys and " + values.Count + " values after deserialization. Make sure that both key and value types are serializable.");
			}
			for (int i = 0; i < keys.Count; i++)
			{
				Add(keys[i], values[i]);
			}
		}
	}
        [SerializeField] public ProfessionGraphAssetBase graph;
        [SerializeField] public Vector2 position;
        [SerializeField] protected NodePortDictionary ports = new NodePortDictionary();
        [SerializeField] public int NodeId = -1;
        [SerializeField] public string Mono = "";
        [SerializeField] public bool IsMonoShow;
        public ProfessionGraphPort GetPort(string fieldName)
        {
            if (ports.TryGetValue(fieldName, out var value)) return value;
            return null;
        }
        public virtual object GetValue(ProfessionGraphPort port) { return null; }
    }
}
