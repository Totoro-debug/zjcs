// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using System;

namespace Zjcs.Official.StoryPlayables
{

[Serializable]
public struct EcEntityIdentifier : IComparable<EcEntityIdentifier>, IEquatable<EcEntityIdentifier>
{
	public static readonly EcEntityIdentifier sDefault;
	public long ContainerId { get; set; }
	public int EntityId { get; set; }

	public EcEntityIdentifier(long contId, int entIdx)
	{
		ContainerId = contId;
		EntityId = entIdx;
	}

	public bool IsValid()
	{
		if (ContainerId > 0)
		{
			return EntityId > 0;
		}
		return false;
	}

	public bool IsInvalid()
	{
		if (ContainerId > 0)
		{
			return EntityId <= 0;
		}
		return true;
	}

	public override string ToString()
	{
		return $"[ContainerId:{ContainerId}, EntityId:{EntityId}]";
	}

	public override bool Equals(object obj)
	{
		if (obj is EcEntityIdentifier ecEntityIdentifier)
		{
			if (ContainerId == ecEntityIdentifier.ContainerId)
			{
				return EntityId == ecEntityIdentifier.EntityId;
			}
			return false;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return (17 * 23 + ContainerId.GetHashCode()) * 23 + EntityId.GetHashCode();
	}

	public int CompareTo(EcEntityIdentifier other)
	{
		if (ContainerId != other.ContainerId)
		{
			return ContainerId.CompareTo(other.ContainerId);
		}
		return EntityId.CompareTo(other.EntityId);
	}

	public bool Equals(EcEntityIdentifier other)
	{
		return this == other;
	}

	public static bool operator ==(EcEntityIdentifier lId, EcEntityIdentifier rId)
	{
		if (lId.EntityId == rId.EntityId)
		{
			return lId.ContainerId == rId.ContainerId;
		}
		return false;
	}

	public static bool operator !=(EcEntityIdentifier lId, EcEntityIdentifier rId)
	{
		if (lId.EntityId == rId.EntityId)
		{
			return lId.ContainerId != rId.ContainerId;
		}
		return true;
	}
}

}
