// Selected original home binding implementation; see the source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using Newtonsoft.Json;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityBindings
{
using Object = UnityEngine.Object;
public readonly struct HomeUnitPointKey : IEquatable<HomeUnitPointKey>
{
	public HomeUnitPointKey(string unitInstanceId, string pointId)
	{
		UnitInstanceId = unitInstanceId;
		PointId = pointId;
	}

	public readonly string UnitInstanceId;

	public readonly string PointId;

	public bool IsValid()
	{
		if (!string.IsNullOrEmpty(UnitInstanceId))
		{
			return !string.IsNullOrEmpty(PointId);
		}
		return false;
	}

	public bool Equals(HomeUnitPointKey other)
	{
		if (string.Equals(UnitInstanceId, other.UnitInstanceId, StringComparison.Ordinal))
		{
			return string.Equals(PointId, other.PointId, StringComparison.Ordinal);
		}
		return false;
	}

	public override bool Equals(object obj)
	{
		if (obj is HomeUnitPointKey other)
		{
			return Equals(other);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return (17 * 31 + StringComparer.Ordinal.GetHashCode(UnitInstanceId ?? string.Empty)) * 31 + StringComparer.Ordinal.GetHashCode(PointId ?? string.Empty);
	}

	public override string ToString()
	{
		return UnitInstanceId + "/" + PointId;
	}
}
}
