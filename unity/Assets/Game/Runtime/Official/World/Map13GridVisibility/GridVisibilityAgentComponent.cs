// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using System.Collections.Generic;



namespace Zjcs.Official.World.Map13GridVisibility
{
public class GridVisibilityAgentComponent : VisibilityComponentSource<GridVisibilityAgentComponentInfo>, IGridTransformPosUpdatedEventReceiver, IEventReceiver, IVisibilitySerializationCallbacks
{
	public delegate void PosVisibilityUpdateDel(Pos2d pos, bool preVi, bool postVi, bool forRegionActive);

	public byte[] ZipedData;


	public GridVisibilityAgentHandle Handle = new GridVisibilityAgentHandle();

	private MapGroundGridBoolData mVisibilityData;

	private MapComponent mMap;


	public int Width { get; private set; }


	public int Height { get; private set; }

	public event PosVisibilityUpdateDel OnPosVisibilityUpdate;

	public event Action<Pos2d, bool> OnOneCellUnlock;

	public override void Start()
	{
		Handle.Init(this);
		mMap = GetComponent<MapComponent>();
		mMap.OnCharacterAdd += Map_OnCharacterAdd;
		mMap.OnCharacterRemove += Map_OnCharacterRemove;
		foreach (KeyValuePair<object, IMapCharacter> item in mMap.MapCharacterDict)
		{
			Map_OnCharacterAdd(item.Value);
		}
		GridGroundComponent component = GetComponent<GridGroundComponent>();
		if (component != null)
		{
			DefaultAStarMovableHandler checkStandHandler = component.CheckStandHandler;
			checkStandHandler.CanStand = (Func<Pos2d, bool>)Delegate.Combine(checkStandHandler.CanStand, new Func<Pos2d, bool>(GetPosVisibility));
		}
	}

	public override void End(VisibilitySerializable userData)
	{
		if (mMap != null)
		{
			mMap.OnCharacterAdd -= Map_OnCharacterAdd;
			mMap.OnCharacterRemove -= Map_OnCharacterRemove;
		}
		GridGroundComponent component = GetComponent<GridGroundComponent>();
		if (component != null)
		{
			DefaultAStarMovableHandler checkStandHandler = component.CheckStandHandler;
			checkStandHandler.CanStand = (Func<Pos2d, bool>)Delegate.Remove(checkStandHandler.CanStand, new Func<Pos2d, bool>(GetPosVisibility));
		}
		Handle.OnDestroy();
	}

	private void Map_OnCharacterRemove(IMapCharacter character)
	{
		if (MapCharacterCheck(character))
		{
			character.Entity.GridTransform.PosUpdatedEvent -= (IGridTransformPosUpdatedEventReceiver)this;
			DoPosLeave(character, character.GridTransform.Pos);
		}
	}

	private void Map_OnCharacterAdd(IMapCharacter character)
	{
		if (MapCharacterCheck(character))
		{
			character.Entity.GridTransform.PosUpdatedEvent += (IGridTransformPosUpdatedEventReceiver)this;
			DoPosEnter(character);
		}
	}

	public void GridTransform_OnPosUpdated(GridTransformComponent transform, Pos2d prePos)
	{
		IMapCharacter component = transform.GetComponent<IMapCharacter>();
		DoPosEnter(component);
		DoPosLeave(component, prePos);
	}

	private void DoPosEnter(IMapCharacter character)
	{
		DoPosEnter(character.GridTransform.GridGround, character.GridTransform.Pos, character.ViewRange);
	}

	public void DoPosEnter(GridGroundComponent gridGround, Pos2d mePos, int viewRange)
	{
		foreach (Pos2d item in Host.CalcAllVisiblePos(gridGround, mePos, viewRange))
		{
			UnlockOneCell(item);
			Handle.CheckUnlockMultiCellsByEntityOnPos(gridGround, item);
		}
	}

	private void DoPosLeave(IMapCharacter character, Pos2d prePos)
	{
	}

	public bool UpdatePosVisibilityAndNotify(Pos2d pos, bool visibility)
	{
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if (!CheckPosInGround(pos))
		{
			return false;
		}
		_ = Width;
		bool posValue = mVisibilityData.GetPosValue(pos.X, pos.Y);
		if (posValue == visibility)
		{
			return false;
		}
		mVisibilityData.SetPosValue(pos.X, pos.Y, visibility);
		try
		{
			OnPosVisibilityUpdate?.Invoke(pos, posValue, visibility, forRegionActive: false);
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogException(ex);
		}
		return true;
	}

	public bool SetPosVisibility(Pos2d pos, bool visibility)
	{
		if (visibility)
		{
			return UnlockOneCell(pos, showAnima: false);
		}
		return UpdatePosVisibilityAndNotify(pos, visibility);
	}

	public bool UnlockOneCell(Pos2d pos, bool showAnima = true)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		if (UpdatePosVisibilityAndNotify(pos, visibility: true))
		{
			try
			{
				OnOneCellUnlock?.Invoke(pos, showAnima);
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogException(ex);
			}
			return true;
		}
		return false;
	}

	public bool CheckPosInGround(int x, int y)
	{
		if (x >= 0 && x < Width && y >= 0)
		{
			return y < Height;
		}
		return false;
	}

	public bool CheckPosInGround(Pos2d pos)
	{
		if (pos.X >= 0 && pos.X < Width && pos.Y >= 0)
		{
			return pos.Y < Height;
		}
		return false;
	}

	public bool GetPosVisibility(Pos2d pos)
	{
		return GetPosVisibility(pos.X, pos.Y);
	}

	public bool GetPosVisibility(int x, int y)
	{
		if (!CheckPosInGround(x, y))
		{
			return false;
		}
		return mVisibilityData.GetPosValue(x, y);
	}

	public void OnBeforeSerialize()
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		if (Entity != null && Width > 0 && Height > 0 && mVisibilityData != null)
		{
			ZipedData = GridVisibilityCompressUtils.DoMyZip(mVisibilityData);
			// Original diagnostic logger adapted; no state mutation.
		}
	}

	public void OnAfterDeserialize()
	{
		if (ZipedData != null)
		{
			MapGroundGridBoolData mapGroundGridBoolData = (mVisibilityData = GridVisibilityCompressUtils.DoMyUnzip(ZipedData));
			Height = mapGroundGridBoolData.Height;
			Width = mapGroundGridBoolData.Width;
			ZipedData = null;
			// Original diagnostic logger adapted; no state mutation.
		}
	}

	public bool MapCharacterCheck(IMapCharacter character)
	{
		return character?.Character?.PlayerId == Host.MainPlayerId;
	}

	public bool IsSameRegionWithOwner(int regionId)
	{
		IMapCharacter mapCharacter = Host.MainMapCharacter;
		if (mapCharacter != null)
		{
			int num = Container.RootEntity.GetComponent<AreaAgentComponent>().GridToRegion(Container.ClassId, mapCharacter.GridTransform.Pos);
			return regionId == num;
		}
		return false;
	}

	public override ushort GetOpCode()
	{
		return 49037;
	}
}

}
