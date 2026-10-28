// Selected official Common implementation; namespace / C# syntax adaptation only.
using System.Collections.Generic;

namespace Zjcs.Official.PlayerPlay
{

public static class PlayerPlayStateMutexConfig
{
	private static Dictionary<(PlayerPlayConfig<PlayerPlayStateType>, PlayerPlayConfig<PlayerPlayActionType>), PlayerPlayStateMutexType> sMutexTable = BuildMutexTable();

	public static Dictionary<int, int> sGameMatchMutexFightFlags;

	public static PlayerPlayStateMutexType GetMutexType(IPlayerPlayAction action, IEnumerable<IPlayerPlayState> curState)
	{
		PlayerPlayStateMutexType playerPlayStateMutexType = PlayerPlayStateMutexType.NotMutex;
		PlayerPlayConfig<PlayerPlayActionType> item = new PlayerPlayConfig<PlayerPlayActionType>(action.Type, action.Param);
		foreach (IPlayerPlayState item3 in curState)
		{
			PlayerPlayConfig<PlayerPlayStateType> item2 = new PlayerPlayConfig<PlayerPlayStateType>(item3.Type, item3.Param);
			if (sMutexTable.TryGetValue((item2, item), out var value) && value > playerPlayStateMutexType)
			{
				playerPlayStateMutexType = value;
				if (playerPlayStateMutexType == PlayerPlayStateMutexType.HardMutex)
				{
					break;
				}
			}
		}
		return playerPlayStateMutexType;
	}

	public static IEnumerable<IPlayerPlayState> FilterStateByMutexType(PlayerPlayStateMutexType mutexType, IPlayerPlayAction action, IEnumerable<IPlayerPlayState> states)
	{
		PlayerPlayConfig<PlayerPlayActionType> actionCfg = new PlayerPlayConfig<PlayerPlayActionType>(action.Type, action.Param);
		foreach (IPlayerPlayState state in states)
		{
			PlayerPlayConfig<PlayerPlayStateType> item = new PlayerPlayConfig<PlayerPlayStateType>(state.Type, state.Param);
			if (sMutexTable.TryGetValue((item, actionCfg), out var value) && value == mutexType)
			{
				yield return state;
			}
		}
	}

	private static Dictionary<(PlayerPlayConfig<PlayerPlayStateType>, PlayerPlayConfig<PlayerPlayActionType>), PlayerPlayStateMutexType> BuildMutexTable()
	{
		InitGameMatchLimitedFightFlags();
		Dictionary<(PlayerPlayConfig<PlayerPlayStateType>, PlayerPlayConfig<PlayerPlayActionType>), PlayerPlayStateMutexType> mutexTable = new Dictionary<(PlayerPlayConfig<PlayerPlayStateType>, PlayerPlayConfig<PlayerPlayActionType>), PlayerPlayStateMutexType>();
		BuildGameMatchStateMutexTable(ref mutexTable);
		BuildFightStateMutexTable(ref mutexTable);
		BuildRoomMatchStateMutexTable(ref mutexTable);
		BuildRoomAndTeamStateMutexTable(ref mutexTable);
		BuildDreamDanceStateMutexTable(ref mutexTable);
		BuildActivitySessionStateMutexTable(ref mutexTable);
		BuildChatActionStateMutexTable(ref mutexTable);
		return mutexTable;
	}

	private static void BuildRoomAndTeamStateMutexTable(ref Dictionary<(PlayerPlayConfig<PlayerPlayStateType>, PlayerPlayConfig<PlayerPlayActionType>), PlayerPlayStateMutexType> mutexTable)
	{
		PlayerPlayConfig<PlayerPlayStateType> item = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.Room, default(PlayerPlayActionNoneParam));
		PlayerPlayConfig<PlayerPlayActionType> item2 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Team, default(PlayerPlayActionNoneParam));
		PlayerPlayConfig<PlayerPlayStateType> item3 = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.Team, default(PlayerPlayActionNoneParam));
		PlayerPlayConfig<PlayerPlayActionType> item4 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Room, default(PlayerPlayActionNoneParam));
		mutexTable[(item, item2)] = PlayerPlayStateMutexType.SoftMutex;
		mutexTable[(item3, item4)] = PlayerPlayStateMutexType.SoftMutex;
		PlayerPlayConfig<PlayerPlayActionType> item5 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.RoomInvited, default(PlayerPlayActionNoneParam));
		mutexTable[(item3, item5)] = PlayerPlayStateMutexType.HardMutex;
		PlayerPlayConfig<PlayerPlayActionType> item6 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.TeamInvited, default(PlayerPlayActionNoneParam));
		mutexTable[(item, item6)] = PlayerPlayStateMutexType.HardMutex;
	}

	private static void BuildRoomMatchStateMutexTable(ref Dictionary<(PlayerPlayConfig<PlayerPlayStateType>, PlayerPlayConfig<PlayerPlayActionType>), PlayerPlayStateMutexType> mutexTable)
	{
		PlayerPlayConfig<PlayerPlayStateType> item = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.RoomMatch, new PlayerPlayRoomMatchStateParam(isMatchDone: false));
		PlayerPlayConfig<PlayerPlayStateType> item2 = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.RoomMatch, new PlayerPlayRoomMatchStateParam(isMatchDone: true));
		PlayerPlayConfig<PlayerPlayActionType> item3 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.RoomMatch, new PlayerPlayRoomMatchStateParam(isMatchDone: false));
		PlayerPlayConfig<PlayerPlayActionType> item4 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.GameMatch, new PlayerPlayMatchStateParam(isMatchDone: false));
		mutexTable[(item, item3)] = PlayerPlayStateMutexType.SoftMutex;
		mutexTable[(item2, item3)] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, item4)] = PlayerPlayStateMutexType.SoftMutex;
		mutexTable[(item2, item4)] = PlayerPlayStateMutexType.HardMutex;
		PlayerPlayConfig<PlayerPlayActionType> item5 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Team, default(PlayerPlayActionNoneParam));
		mutexTable[(item, item5)] = PlayerPlayStateMutexType.SoftMutex;
		mutexTable[(item2, item5)] = PlayerPlayStateMutexType.HardMutex;
		PlayerPlayConfig<PlayerPlayActionType> item6 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Room, default(PlayerPlayActionNoneParam));
		mutexTable[(item, item6)] = PlayerPlayStateMutexType.SoftMutex;
		mutexTable[(item2, item6)] = PlayerPlayStateMutexType.HardMutex;
		PlayerPlayConfig<PlayerPlayActionType> item7 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.CommonPlay, default(PlayerPlayActionNoneParam));
		mutexTable[(item, item7)] = PlayerPlayStateMutexType.SoftMutex;
		mutexTable[(item2, item7)] = PlayerPlayStateMutexType.HardMutex;
		int num = 4;
		int num2 = 16;
		for (int i = 0; i < 31; i++)
		{
			if ((i & num) != num && (i & num2) != num2)
			{
				PlayerPlayConfig<PlayerPlayActionType> item8 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Fight, new PlayerPlayFightStateParam(i));
				mutexTable[(item2, item8)] = PlayerPlayStateMutexType.HardMutex;
				if (IsFightParamFlagMatch(i, sGameMatchMutexFightFlags))
				{
					mutexTable[(item, item8)] = PlayerPlayStateMutexType.SoftMutex;
				}
			}
		}
	}

	private static void BuildGameMatchStateMutexTable(ref Dictionary<(PlayerPlayConfig<PlayerPlayStateType>, PlayerPlayConfig<PlayerPlayActionType>), PlayerPlayStateMutexType> mutexTable)
	{
		PlayerPlayConfig<PlayerPlayStateType> item = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.GameMatch, new PlayerPlayMatchStateParam(isMatchDone: false));
		PlayerPlayConfig<PlayerPlayStateType> item2 = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.GameMatch, new PlayerPlayMatchStateParam(isMatchDone: true));
		PlayerPlayConfig<PlayerPlayActionType> item3 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.GameMatch, new PlayerPlayMatchStateParam(isMatchDone: false));
		mutexTable[(item, item3)] = PlayerPlayStateMutexType.SoftMutex;
		mutexTable[(item2, item3)] = PlayerPlayStateMutexType.HardMutex;
		PlayerPlayConfig<PlayerPlayActionType> item4 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Team, default(PlayerPlayActionNoneParam));
		mutexTable[(item, item4)] = PlayerPlayStateMutexType.SoftMutex;
		mutexTable[(item2, item4)] = PlayerPlayStateMutexType.HardMutex;
		PlayerPlayConfig<PlayerPlayActionType> item5 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Room, default(PlayerPlayActionNoneParam));
		mutexTable[(item, item5)] = PlayerPlayStateMutexType.SoftMutex;
		mutexTable[(item2, item5)] = PlayerPlayStateMutexType.HardMutex;
		PlayerPlayConfig<PlayerPlayActionType> item6 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.CommonPlay, default(PlayerPlayActionNoneParam));
		mutexTable[(item, item6)] = PlayerPlayStateMutexType.SoftMutex;
		mutexTable[(item2, item6)] = PlayerPlayStateMutexType.HardMutex;
		int num = 4;
		int num2 = 16;
		for (int i = 0; i < 31; i++)
		{
			if ((i & num) != num && (i & num2) != num2)
			{
				PlayerPlayConfig<PlayerPlayActionType> item7 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Fight, new PlayerPlayFightStateParam(i));
				mutexTable[(item2, item7)] = PlayerPlayStateMutexType.HardMutex;
				if (IsFightParamFlagMatch(i, sGameMatchMutexFightFlags))
				{
					mutexTable[(item, item7)] = PlayerPlayStateMutexType.SoftMutex;
				}
			}
		}
	}

	private static void BuildFightStateMutexTable(ref Dictionary<(PlayerPlayConfig<PlayerPlayStateType>, PlayerPlayConfig<PlayerPlayActionType>), PlayerPlayStateMutexType> mutexTable)
	{
		PlayerPlayConfig<PlayerPlayActionType> item = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.GameMatch, new PlayerPlayMatchStateParam(isMatchDone: false));
		PlayerPlayConfig<PlayerPlayActionType> item2 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.AcceptGameMatch, default(PlayerPlayActionNoneParam));
		PlayerPlayConfig<PlayerPlayActionType> item3 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.AcceptRoomMatch, default(PlayerPlayActionNoneParam));
		int num = 3;
		int num2 = 0;
		int num3 = 4;
		int num4 = 16;
		for (int i = 0; i < 31; i++)
		{
			if ((i & num3) != num3 && (i & num4) != num4)
			{
				PlayerPlayConfig<PlayerPlayStateType> item4 = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.Fight, new PlayerPlayFightStateParam(i));
				if (IsFightParamFlagMatch(i, sGameMatchMutexFightFlags))
				{
					mutexTable[(item4, item)] = PlayerPlayStateMutexType.HardMutex;
					mutexTable[(item4, item2)] = PlayerPlayStateMutexType.HardMutex;
					mutexTable[(item4, item3)] = PlayerPlayStateMutexType.HardMutex;
				}
				else if ((i & 2) == 2)
				{
					mutexTable[(item4, item2)] = PlayerPlayStateMutexType.SoftMutex;
					mutexTable[(item4, item3)] = PlayerPlayStateMutexType.SoftMutex;
				}
				else if ((i & num) == num2)
				{
					mutexTable[(item4, item2)] = PlayerPlayStateMutexType.SoftMutex;
					mutexTable[(item4, item3)] = PlayerPlayStateMutexType.SoftMutex;
				}
			}
		}
	}

	public static bool IsFightParamFlagMatch(int flag, Dictionary<int, int> checkFlags)
	{
		foreach (KeyValuePair<int, int> checkFlag in checkFlags)
		{
			if ((flag & checkFlag.Key) == checkFlag.Value)
			{
				return true;
			}
		}
		return false;
	}

	private static void BuildDreamDanceStateMutexTable(ref Dictionary<(PlayerPlayConfig<PlayerPlayStateType>, PlayerPlayConfig<PlayerPlayActionType>), PlayerPlayStateMutexType> mutexTable)
	{
		PlayerPlayConfig<PlayerPlayStateType> item = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.DreamDance, default(PlayerPlayActionNoneParam));
		PlayerPlayConfig<PlayerPlayActionType> item2 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.DreamDance, default(PlayerPlayActionNoneParam));
		PlayerPlayConfig<PlayerPlayStateType> item3 = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.GameMatch, new PlayerPlayMatchStateParam(isMatchDone: false));
		PlayerPlayConfig<PlayerPlayStateType> item4 = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.GameMatch, new PlayerPlayMatchStateParam(isMatchDone: true));
		PlayerPlayConfig<PlayerPlayActionType> item5 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.GameMatch, new PlayerPlayMatchStateParam(isMatchDone: false));
		PlayerPlayConfig<PlayerPlayActionType> item6 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.AcceptGameMatch, default(PlayerPlayActionNoneParam));
		mutexTable[(item, item5)] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, item6)] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item3, item2)] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item4, item2)] = PlayerPlayStateMutexType.HardMutex;
		PlayerPlayConfig<PlayerPlayStateType> item7 = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.RoomMatch, new PlayerPlayRoomMatchStateParam(isMatchDone: false));
		PlayerPlayConfig<PlayerPlayStateType> item8 = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.RoomMatch, new PlayerPlayRoomMatchStateParam(isMatchDone: true));
		mutexTable[(item7, item2)] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item8, item2)] = PlayerPlayStateMutexType.HardMutex;
		PlayerPlayConfig<PlayerPlayActionType> item9 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.RoomMatch, new PlayerPlayRoomMatchStateParam(isMatchDone: false));
		mutexTable[(item, item9)] = PlayerPlayStateMutexType.HardMutex;
		PlayerPlayConfig<PlayerPlayStateType> item10 = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.Team, default(PlayerPlayActionNoneParam));
		PlayerPlayConfig<PlayerPlayActionType> item11 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Team, default(PlayerPlayActionNoneParam));
		PlayerPlayConfig<PlayerPlayActionType> item12 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.TeamInvited, default(PlayerPlayActionNoneParam));
		mutexTable[(item, item11)] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, item12)] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item10, item2)] = PlayerPlayStateMutexType.HardMutex;
	}

	private static void BuildActivitySessionStateMutexTable(ref Dictionary<(PlayerPlayConfig<PlayerPlayStateType>, PlayerPlayConfig<PlayerPlayActionType>), PlayerPlayStateMutexType> mutexTable)
	{
		PlayerPlayConfig<PlayerPlayStateType> item = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.ActivitySession, default(PlayerPlayActionNoneParam));
		PlayerPlayActionNoneParam playerPlayActionNoneParam = default;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Team, playerPlayActionNoneParam))] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.TeamInvited, playerPlayActionNoneParam))] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Room, playerPlayActionNoneParam))] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.RoomInvited, playerPlayActionNoneParam))] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.GameMatch, new PlayerPlayMatchStateParam(isMatchDone: false)))] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.RoomMatch, new PlayerPlayRoomMatchStateParam(isMatchDone: false)))] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.CommonPlay, playerPlayActionNoneParam))] = PlayerPlayStateMutexType.HardMutex;
	}

	private static void BuildChatActionStateMutexTable(ref Dictionary<(PlayerPlayConfig<PlayerPlayStateType>, PlayerPlayConfig<PlayerPlayActionType>), PlayerPlayStateMutexType> mutexTable)
	{
		PlayerPlayActionNoneParam playerPlayActionNoneParam = default;
		PlayerPlayConfig<PlayerPlayStateType> item = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.ChatAction, playerPlayActionNoneParam);
		PlayerPlayConfig<PlayerPlayActionType> item2 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.ChatAction, playerPlayActionNoneParam);
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.GameMatch, new PlayerPlayMatchStateParam(isMatchDone: false)))] = PlayerPlayStateMutexType.SoftMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Team, playerPlayActionNoneParam))] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.TeamInvited, playerPlayActionNoneParam))] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Story, playerPlayActionNoneParam))] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.CommonPlay, playerPlayActionNoneParam))] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.AcceptGameMatch, playerPlayActionNoneParam))] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Room, playerPlayActionNoneParam))] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.RoomMatch, new PlayerPlayMatchStateParam(isMatchDone: false)))] = PlayerPlayStateMutexType.SoftMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.RoomInvited, playerPlayActionNoneParam))] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(item, new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.DreamDance, playerPlayActionNoneParam))] = PlayerPlayStateMutexType.HardMutex;
		for (int i = 0; i <= 31; i++)
		{
			PlayerPlayConfig<PlayerPlayActionType> item3 = new PlayerPlayConfig<PlayerPlayActionType>(PlayerPlayActionType.Fight, new PlayerPlayFightStateParam(i));
			mutexTable[(item, item3)] = PlayerPlayStateMutexType.HardMutex;
		}
		mutexTable[(new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.GameMatch, new PlayerPlayMatchStateParam(isMatchDone: false)), item2)] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.GameMatch, new PlayerPlayMatchStateParam(isMatchDone: true)), item2)] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.RoomMatch, new PlayerPlayRoomMatchStateParam(isMatchDone: false)), item2)] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.RoomMatch, new PlayerPlayRoomMatchStateParam(isMatchDone: true)), item2)] = PlayerPlayStateMutexType.HardMutex;
		for (int j = 0; j <= 31; j++)
		{
			if ((j & 4) == 0)
			{
				PlayerPlayConfig<PlayerPlayStateType> item4 = new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.Fight, new PlayerPlayFightStateParam(j));
				mutexTable[(item4, item2)] = PlayerPlayStateMutexType.HardMutex;
			}
		}
		mutexTable[(new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.Team, playerPlayActionNoneParam), item2)] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.Room, playerPlayActionNoneParam), item2)] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.Story, playerPlayActionNoneParam), item2)] = PlayerPlayStateMutexType.HardMutex;
		mutexTable[(new PlayerPlayConfig<PlayerPlayStateType>(PlayerPlayStateType.DreamDance, playerPlayActionNoneParam), item2)] = PlayerPlayStateMutexType.HardMutex;
	}

	private static void InitGameMatchLimitedFightFlags()
	{
		int key = 3;
		int value = 1;
		int key2 = 10;
		int value2 = 0;
		int key3 = 9;
		int value3 = 0;
		sGameMatchMutexFightFlags = new Dictionary<int, int>
		{
			{ key, value },
			{ key2, value2 },
			{ key3, value3 }
		};
	}
}
}
