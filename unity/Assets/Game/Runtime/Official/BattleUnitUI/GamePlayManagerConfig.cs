// Original configuration fields; stage enum retains its serialized Int32 representation.
namespace Zjcs.Official.BattleUnitUI
{
using System.Collections.Generic;
using UnityEngine;

public class GamePlayManagerConfig : ScriptableObject
{
	[Tooltip("是否启用玩家头像ETC2合图功能")]
	public bool EnablePlayerPortraitETC2;

	[Tooltip("是否启用玩家头像ETC2合图功能  场景化")]
	public bool EnablePlayerPortraitETC2_Scene;

	[Tooltip("找不到数据时的血条效果数据")]
	public GameEffectData DefaultEffect;

	[Tooltip("自己的血条效果数据")]
	public GameEffectData CharacterMeEffect;

	[Tooltip("观战者")]
	public GameEffectData SpactatedCharacterEffect;

	[Tooltip("不同阵营血条效果数据")]
	public List<GameEffectData> FactionEffectDatas;

	[Tooltip("回放自动开始播放的黑名单")]
	public List<int> AutoStartWhenPlaybackBlackType;

	[Tooltip("Fill/Out特效预创建个数")]
	public int PreCreateEffectCount = 20;
}

}
