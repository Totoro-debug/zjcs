using System.Collections.Generic;
using Zjcs.Official.EntityLoading;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.ProfessionUI;
// Complete original behaviour; see battle-hud/damage-ui-runtime-source-manifest.json.
using System;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using AttachToObjUI = Zjcs.Official.StoryBubbles.AttachToObjUI;
namespace Zjcs.Official.BattleDamageUI
{
public class CharacterDamageUIManager : UIBase
{
	private Dictionary<EcEntityIdentifier, List<CharacterDamageUI>> myCharacterToDamageUI;

	private Dictionary<EffectUIType, GameObjectPoolEmbed<CharacterDamageUI>> mEffectUITypeToPool;

	private Dictionary<EffectUIType, GameObject> mEffectUIPrefabDic;





	public CharacterDamageUIContext Context { private get; set; }

	public void Init()
	{
		InitDataOnStart();
		InitWrapper(null);
	}

	public void Release()
	{
		ReleaseEffectUIPrefabs();
	}

	private void InitDataOnStart()
	{
		myCharacterToDamageUI = new Dictionary<EcEntityIdentifier, List<CharacterDamageUI>>();
		InitEffectUIPrefabs();
		InitDamageUIPool();
	}

	private void InitDamageUIPool()
	{


		Transform damageFloatsRoot = Context.DamageFloatsRoot;
		if (!Context.UseGameObjectPool)
		{
			return;
		}
		mEffectUITypeToPool = new Dictionary<EffectUIType, GameObjectPoolEmbed<CharacterDamageUI>>();
		int i = 0;
		for (int count = Context.Settings.EffectUIPoolPresents.Count; i < count; i++)
		{
			EffectUIType effectUIType = (EffectUIType)i;
			CharacterDamageUIPoolPresent characterDamageUIPoolPresent = Context.Settings.EffectUIPoolPresents[i];
			mEffectUIPrefabDic.TryGetValue(effectUIType, out var value);
			if ((Object)(object)value != (Object)null)
			{
				GameObjectPoolEmbed<CharacterDamageUI> gameObjectPoolEmbed = new GameObjectPoolEmbed<CharacterDamageUI>(damageFloatsRoot, value);
				gameObjectPoolEmbed.Extend(GetEffectUIPoolInitSize(effectUIType, characterDamageUIPoolPresent.PoolInitSize));
				mEffectUITypeToPool.Add(effectUIType, gameObjectPoolEmbed);
				continue;
			}
			Debug.LogError("CreateEffectUIPool;effectUIPrefab != null, i= " + i);
		}
	}

	private int GetEffectUIPoolInitSize(EffectUIType effectUIType, int defaultSize)
	{
		if (!Context.StageType.HasValue)
		{
			return defaultSize;
		}
		int stageType = Context.StageType.Value;
		CharacterDamageUIStageWarmup fightWarmupSetting = Context.WarmupSettings.FightWarmupSettings.Find((CharacterDamageUIStageWarmup s) => s.StageType == stageType);
		if (fightWarmupSetting == null)
		{
			return defaultSize;
		}
		return fightWarmupSetting.DamageEffectUIs.Find((CharacterDamageUIPresetCount s) => s.EffectUIType == effectUIType)?.PresetCount ?? defaultSize;
	}

	private void InitEffectUIPrefabs()
	{

		mEffectUIPrefabDic = new Dictionary<EffectUIType, GameObject>();
		int i = 0;
		for (int count = Context.Settings.EffectUIPoolPresents.Count; i < count; i++)
		{
			EffectUIType key = (EffectUIType)i;
			CharacterDamageUIPoolPresent characterDamageUIPoolPresent = Context.Settings.EffectUIPoolPresents[i];
			if (characterDamageUIPoolPresent.EffectUIPrefabProxy != null)
			{
				GameObject result = Context.LoadPrefab(characterDamageUIPoolPresent.EffectUIPrefabProxy.AssetGuid);
				mEffectUIPrefabDic.Add(key, result);
			}
		}
	}

	private void ReleaseEffectUIPrefabs()
	{
		Context.ReleasePrefabs();
		mEffectUIPrefabDic.Clear();
	}

	protected override void OnEnableAndInited()
    {
        base.OnEnableAndInited();
        Context.OnCharacterAdd += MyGame_OnCharacterAdd;
        Context.OnCharacterRemove += MyGame_OnCharacterRemove;
        Context.OnSkillReplay += OnSkillReplay;
        foreach (CharacterDamageUIRole character in Context.Characters) MyGame_OnCharacterAdd(character);
    }

	protected override void OnDisableAndInited()
    {
        Context.OnSkillReplay -= OnSkillReplay;
        Context.OnCharacterAdd -= MyGame_OnCharacterAdd;
        Context.OnCharacterRemove -= MyGame_OnCharacterRemove;
        foreach (CharacterDamageUIRole character in Context.Characters) MyGame_OnCharacterRemove(character);
        base.OnDisableAndInited();
    }

	private void MyGame_OnCharacterRemove(CharacterDamageUIRole character)
    {
        MyFightRoleFight component = character.Fight;
        if (component != null) component.OnDamage -= MyBattle_OnDamage;
        character.OnSkillCostHp -= OnSkillCostHp;
        character.OnDisperseStatus -= OnDisperseStatus;
        character.OnStatusDamage -= FightStatusAgent_OnStatusDamage;
        character.OnStatusTriggerCustomAction -= FightStatusAgent_OnStatusTriggerCustomAction;
    }


	private void MyGame_OnCharacterAdd(CharacterDamageUIRole character)
    {
        MyFightRoleFight component = character.Fight;
        if (component != null) component.OnDamage += MyBattle_OnDamage;
        character.OnSkillCostHp += OnSkillCostHp;
        character.OnDisperseStatus += OnDisperseStatus;
        character.OnStatusDamage += FightStatusAgent_OnStatusDamage;
        character.OnStatusTriggerCustomAction += FightStatusAgent_OnStatusTriggerCustomAction;
    }

	private void OnSkillReplay(CharacterDamageUISkillReplay replay)
    {
        if (replay.IsExtra) return;
        MyEntity entity = Context.GetEntity(replay.SkillId);
        if (entity != null)
        {
            MyFightSkill skill = entity.GetComponent<MyFightSkill>();
            if (CanShowSkillReplay(skill)) ShowResetSkillCDUI(replay.CharacterId);
        }
        else
        {
            Context.GetSkillPrefabAsync(replay.SkillId, prefabSkill =>
            {
                if (prefabSkill && prefabSkill.ShowSkillReplay) ShowResetSkillCDUI(replay.CharacterId);
            });
        }
    }


	private void ShowResetSkillCDUI(EcEntityIdentifier characterEntityId)
	{
		if (Context != null)
		{
			MyEntity entity = Context.GetEntity(characterEntityId);
			CharacterDamageUI characterDamageUI = GetCharacterDamageUI(EffectUIType.ResetSkillCDUI);
			AddDamageInfo(characterEntityId, characterDamageUI);
			characterDamageUI.Init(entity, OnCharacterDamageUIEnd);
			UpdateDamageUIOffset(characterEntityId);
		}
	}

	private void FightStatusAgent_OnStatusDamage(EcEntityIdentifier entityId, long damageValue, bool isCure)
	{
		DamageColor damageColor = (isCure ? Context.Settings.StatusCureColor : Context.Settings.StatusDamageColor);
		ShowDamage(entityId, damageValue, damageColor);
	}

	private void FightStatusAgent_OnStatusTriggerCustomAction(EcEntityIdentifier id, EStatusCustomAction customAction)
	{
		ShowStatusCustomAction(id, customAction, EffectUIType.CustomActionUI);
	}

	public void ShowStatusCustomAction(EcEntityIdentifier id, EStatusCustomAction customAction, EffectUIType uiType)
	{
		MyEntity entity = Context.GetEntity(id);
		if (!((Object)(object)entity == (Object)null))
		{
			CharacterDamageUI characterDamageUI = GetCharacterDamageUI(uiType);
			AddDamageInfo(id, characterDamageUI);
			characterDamageUI.Init(entity, OnCharacterDamageUIEnd);
			characterDamageUI.UpdateText("EStatusCustomAction." + customAction);
			DamageCustomActionUI component = ((Component)characterDamageUI).GetComponent<DamageCustomActionUI>();
			if ((Object)(object)component != (Object)null)
			{
				component.Init(customAction);
			}
			UpdateDamageUIOffset(id);
		}
	}

	private void OnSkillCostHp(CharacterDamageUIRole battle, EcEntityIdentifier skillId, long preHp, long hp)
	{
		long damageValue = hp - preHp;
		ShowDamage(battle.Id, damageValue, Context.Settings.SkillCostDamageColor);
	}

	private void OnDisperseStatus(CharacterDamageUIRole comp)
	{
		ShowDisperseUI(comp.Id);
	}

	private void MyBattle_OnDamage(MyFightRoleFight battle, MyFightDamage myDamage)
	{
		DamageResultParameter damageResult = myDamage.Component.DamageResult;
		long num = damageResult.DamageValue + -damageResult.ShieldValue;
		if (CanShowDamage(battle, myDamage, num))
		{
			ShowDamage(battle.myEntity.Id, num, GetDamageColor(myDamage.Component), myDamage);
		}
	}

	private void ShowDisperseUI(EcEntityIdentifier entityId)
	{
		MyEntity entity = Context.GetEntity(entityId);
		if (!((Object)(object)entity == (Object)null))
		{
			CharacterDamageUI characterDamageUI = GetCharacterDamageUI(EffectUIType.DisperseDamageUI);
			AddDamageInfo(entityId, characterDamageUI);
			characterDamageUI.Init(entity, OnCharacterDamageUIEnd);
			UpdateDamageUIOffset(entityId);
		}
	}

	private void OnCharacterDamageUIEnd(CharacterDamageUI ui)
	{
		if ((Object)(object)ui != (Object)null)
		{
			RemoveDamageInfo(ui.EntityId, ui);
			ReturnCharacterDamageUI(ui.Type, ui);
		}
	}

	private void ShowDamage(EcEntityIdentifier entityId, long damageValue, DamageColor damageColor, MyFightDamage myDamage = null)
	{
		CharacterDamageUI characterDamageUI = GetCharacterDamageUI(myDamage);
		AddDamageInfo(entityId, characterDamageUI);
		MyEntity entity = Context.GetEntity(entityId);
		bool flag = (Object)(object)myDamage != (Object)null;
		bool isElementAffect = false;
		float elementConfrontVal = 0f;
		bool isBreakResilience = false;
		bool trueDamage = false;
		if (flag)
		{
			DamageResultParameter damageResult = myDamage.Component.DamageResult;
			elementConfrontVal = damageResult.ElementConfrontation;
			isElementAffect = damageResult.IsElementAffect;
			isBreakResilience = damageResult.DamageTypeData.IsBreakResilience;
			trueDamage = damageResult.DamageTypeData.IsTrueDamage;
		}
		characterDamageUI.Init(entity, damageValue, damageColor, OnCharacterDamageUIEnd, isBreakResilience, isElementAffect, elementConfrontVal, trueDamage);
		UpdateDamageUIOffset(entityId);
	}

	private void UpdateDamageUIOffset(EcEntityIdentifier entityId)
	{







		if (myCharacterToDamageUI.TryGetValue(entityId, out var value))
		{
			Vector3 val = Vector3.zero;
			for (int num = value.Count - 2; num >= 0; num--)
			{
				CharacterDamageUI characterDamageUI = value[num + 1];
				CharacterDamageUI characterDamageUI2 = value[num];
				val.y += characterDamageUI.Height;
				val += Context.Settings.DamageUISpacing;
				characterDamageUI2.UpdateOffset(val);
			}
		}
	}

	private bool CanShowDamage(MyFightRoleFight battle, MyFightDamage dmg, long finalDmg)
	{
		if (dmg.myHideDamage)
		{
			return false;
		}
		if (dmg.Component.DamageResult.IsElementAffect)
		{
			return true;
		}
		if (finalDmg != 0L)
		{
			return true;
		}
		ICustomDamageUIFilter component = ((Component)battle).GetComponent<ICustomDamageUIFilter>();
		if (component != null && component.IsFilter(dmg))
		{
			return false;
		}
		return dmg.myShowZeroDamage;
	}

	private bool CanShowSkillReplay(MyFightSkill skill)
	{
		if ((Object)(object)skill != (Object)null)
		{
			return skill.ShowSkillReplay;
		}
		return false;
	}

	private CharacterDamageUI GetCharacterDamageUI(MyFightDamage dmg)
	{
		EffectUIType effectUITypeByDamage = GetEffectUITypeByDamage(dmg);
		return GetCharacterDamageUI(effectUITypeByDamage);
	}

	private CharacterDamageUI GetCharacterDamageUI(EffectUIType effectUIType)
	{


		Transform damageFloatsRoot = Context.DamageFloatsRoot;
		CharacterDamageUI characterDamageUI = null;
		if (Context.UseGameObjectPool)
		{
			GameObjectPoolEmbed<CharacterDamageUI> gameObjectPoolEmbed = mEffectUITypeToPool[effectUIType];
			if (gameObjectPoolEmbed == null)
			{
				Debug.LogError("[Battle] CharacterUIManager EffectUIType(" + effectUIType + ") is not support type!");
				return null;
			}
			characterDamageUI = gameObjectPoolEmbed.Borrow(damageFloatsRoot);
		}
		else
		{
			mEffectUIPrefabDic.TryGetValue(effectUIType, out var value);
			if ((Object)(object)value == (Object)null)
			{
				return null;
			}
			GameObject val2 = StoryPlayableInstantiation.Instantiate(damageFloatsRoot, value, damageFloatsRoot, false);
			characterDamageUI = val2.GetComponent<CharacterDamageUI>();
			val2.SetActive(true);
		}
		((Component)characterDamageUI).transform.SetAsLastSibling();
		return characterDamageUI;
	}

	private void ReturnCharacterDamageUI(EffectUIType effectUIType, CharacterDamageUI effectUI)
	{
		if (Context.UseGameObjectPool)
		{
			mEffectUITypeToPool[effectUIType].Return(effectUI);
		}
		else
		{
			Object.Destroy((Object)(object)((Component)effectUI).gameObject);
		}
	}

	private void AddDamageInfo(EcEntityIdentifier id, CharacterDamageUI value)
	{
		if (!myCharacterToDamageUI.TryGetValue(id, out var value2))
		{
			value2 = new List<CharacterDamageUI>();
			myCharacterToDamageUI.Add(id, value2);
		}
		value2.Add(value);
	}

	private void RemoveDamageInfo(EcEntityIdentifier id, CharacterDamageUI value)
	{
		if (myCharacterToDamageUI.TryGetValue(id, out var value2))
		{
			value2.Remove(value);
		}
	}

	private DamageColor GetDamageColor(FightDamageComponent damage)
	{
		DamageResultParameter damageResult = damage.DamageResult;
		if (damageResult.IsCure)
		{
			return Context.Settings.ElementCureColor;
		}
		foreach (ElementDamageColor elementDamageColor in Context.Settings.ElementDamageColors)
		{
			if (elementDamageColor.ElementType == damageResult.SkillElementType)
			{
				if (damageResult.DamageTypeData.IsBlock)
				{
					return elementDamageColor.Block;
				}
				if (damageResult.DamageTypeData.IsCrit)
				{
					return elementDamageColor.Crit;
				}
				return elementDamageColor.Normal;
			}
		}
		return Context.Settings.DefaultColor;
	}

	private EffectUIType GetEffectUITypeByDamage(MyFightDamage dmg)
	{
		EffectUIType result = EffectUIType.NormalDamageUI;
		if ((Object)(object)dmg != (Object)null)
		{
			FightDamageComponent component = dmg.Component;
			DamageResultParameter damageResult = component.DamageResult;
			bool flag = Context.GetSkillElementType(dmg) == ElementType.Abyss;
			if (flag)
			{
				result = EffectUIType.AbyssNormalDamageUI;
			}
			if (damageResult.DamageValue > 0)
			{
				result = EffectUIType.CureUI;
			}
			else if (damageResult.DamageTypeData.IsImmunity)
			{
				result = EffectUIType.ImmunityDamageUI;
			}
			else if (damageResult.DamageTypeData.IsDodge)
			{
				result = EffectUIType.DodgeDamageUI;
			}
			else if (damageResult.DamageTypeData.IsBlinding)
			{
				result = EffectUIType.BlindingDamageUI;
			}
			else if (damageResult.DamageTypeData.IsCrit)
			{
				result = ((!flag) ? EffectUIType.CritDamageUI : EffectUIType.AbyssCritDamageUI);
			}
			else if (damageResult.DamageTypeData.IsBlock)
			{
				result = (flag ? EffectUIType.AbyssBlockDamageUI : EffectUIType.BlockDamageUI);
			}
		}
		return result;
	}
}
}
