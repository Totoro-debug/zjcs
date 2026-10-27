// Ported from the verified official source; only dependency/serialization adapters differ.
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public class ProfessionGraphPort
{
	public enum IO
	{
		Input,
		Output
	}

	[Serializable]
	private class PortConnection
	{
		[SerializeField]
		public string fieldName;

		[SerializeField]
		public ProfessionGraphAssetNode node;

		[NonSerialized]
		private ProfessionGraphPort port;

		[SerializeField]
		public List<Vector2> reroutePoints = new List<Vector2>();

		public int NodeId
		{
			get
			{
				if (!((Object)(object)node == (Object)null))
				{
					return node.NodeId;
				}
				return 0;
			}
		}

		public ProfessionGraphPort Port
		{
			get
			{
				if (port == null)
				{
					return port = GetPort();
				}
				return port;
			}
		}

		public PortConnection()
		{
			port = null;
			node = null;
		}

		public PortConnection(ProfessionGraphPort port)
		{
			this.port = port;
			node = port.node;
			fieldName = port.fieldName;
		}

		public void BindNode(ProfessionGraphPort port, ProfessionGraphAssetNode peerNode)
		{
			this.port = port;
			node = peerNode;
			fieldName = port.fieldName;
		}

		private ProfessionGraphPort GetPort()
		{
			if ((Object)(object)node == (Object)null || string.IsNullOrEmpty(fieldName))
			{
				return null;
			}
			return node.GetPort(fieldName);
		}
	}

	private Type valueType;

	[SerializeField]
	private string _fieldName;

	[SerializeField]
	private ProfessionGraphAssetNode _node;

	[SerializeField]
	private string _typeQualifiedName;

	[SerializeField]
	private List<PortConnection> connections = new List<PortConnection>();

	[SerializeField]
	private IO _direction;

	[SerializeField]
	private ProfessionGraphAssetNode.ConnectionType _connectionType;

	[SerializeField]
	private ProfessionGraphAssetNode.TypeConstraint _typeConstraint;

	[SerializeField]
	private ProfessionGraphAssetNode.FlowConnectionType _flowConnectionType;

	[SerializeField]
	private bool _dynamic;


public ProfessionGraphAssetNode node => _node;
public string fieldName => _fieldName;
public List<ProfessionGraphPort> GetConnections()
	{
		List<ProfessionGraphPort> list = new List<ProfessionGraphPort>();
		for (int i = 0; i < connections.Count; i++)
		{
			ProfessionGraphPort connection = GetConnection(i);
			if (connection != null)
			{
				list.Add(connection);
			}
		}
		return list;
	}
public ProfessionGraphPort GetConnection(int i)
	{
		if ((Object)(object)connections[i].node == (Object)null || string.IsNullOrEmpty(connections[i].fieldName))
		{
			connections.RemoveAt(i);
			return null;
		}
		ProfessionGraphPort port = connections[i].node.GetPort(connections[i].fieldName);
		if (port == null)
		{
			connections.RemoveAt(i);
			return null;
		}
		return port;
	}
}
}
