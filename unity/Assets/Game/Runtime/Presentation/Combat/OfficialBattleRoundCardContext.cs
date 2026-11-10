using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleRoundUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // Adapts the battle host's already resolved head photograph/icon and faction
    // image to the native card. Appearance generation remains with the host.
    internal sealed class OfficialBattleRoundCardContext : IBattleRoundCardBindings, ICharacterPortraitManager
    {
        private readonly Sprite icon;
        private readonly GameEffectData effect;
        private readonly BattleRoundPortraitAssets resources;
        private readonly BattleRoundPlayer player;
        private readonly Dictionary<long, CharacterPortraitCache> captures = new Dictionary<long, CharacterPortraitCache>();
        public BattleRoundRole Role { get; }
        public BattleRoundPlayer MainPlayer { get; }
        public Color MimicsMonsterColor => Zjcs.Offline.ProfessionUI.ProfessionNativePortraitStudioResources
            .LoadDefault().AppearanceConfig.MimicsMonsterColor;

        public OfficialBattleRoundCardContext(FightBattleUnitState unit, Sprite portrait, Sprite faction,
            BattleRoundPortraitAssets assets)
        {
            icon = portrait != null ? portrait : throw new InvalidOperationException("Missing original round portrait: " + unit.Id);
            resources = assets;
            var effects = new[] {assets.Configuration.DefaultEffect, assets.Configuration.CharacterMeEffect,
                assets.Configuration.SpactatedCharacterEffect}.Concat(assets.Configuration.FactionEffectDatas);
            effect = faction == null ? assets.Configuration.DefaultEffect : effects.FirstOrDefault(data =>
            {
                string name = System.IO.Path.GetFileName(data.FactionRoundImgPath);
                return faction.name == name || faction.name.StartsWith(name + "-", StringComparison.Ordinal);
            });
            if (effect == null) throw new InvalidOperationException("Unbound original round faction: " + faction.name);
            // These local request identities address completed photographs, not
            // online players. The cache never generates a model or persists data.
            player = new BattleRoundPlayer { Id = unit.Id, Profession = unit.Profession, AppearanceData = portrait };
            MainPlayer = unit.Id == 1 ? player : new BattleRoundPlayer { Id = 1 };
            Role = new BattleRoundRole { MonsterId = unit.MonsterId };
            if (unit.NpcClassId > 0) Role.NpcClassId = unit.NpcClassId;
            else if (unit.Id == 1 || unit.CharacterType == 1) Role.PlayerId = unit.Id;
            else if (unit.CharacterType == 7) Role.PetEntityClassId = unit.MonsterId;
        }
        public GameEffectData GetFaction(BattleRoundRole role) => effect;
        public bool TryGetNpcIcon(int id, out string path) { path = icon.name; return Role.NpcClassId == id; }
        public bool TryGetMonsterIcon(int id, out BattleRoundMonsterIcon result)
        {
            result = new BattleRoundMonsterIcon { Type = Role.PetEntityClassId.HasValue ? 3 : 1, Path = icon.name };
            return id == Role.MonsterId;
        }
        public Sprite GetSprite(string path) => path == icon.name
            || !Role.PlayerId.HasValue && path == "ItemIcon/GameHeadIcon/Monster/G_" + Role.MonsterId
            || Role.PetEntityClassId.HasValue && path == "ItemIcon/GameHeadIcon/Pet/G_" + Role.PetEntityClassId.Value
            ? icon : resources.LoadSprite(path);
        public IBattleRoundPlayerRequest CreatePlayerRequest(Action<BattleRoundPlayer> onResult) => new PlayerRequest(player, onResult);
        private sealed class PlayerRequest : IBattleRoundPlayerRequest
        {
            private readonly BattleRoundPlayer player;
            private Action<BattleRoundPlayer> callback;
            public PlayerRequest(BattleRoundPlayer value, Action<BattleRoundPlayer> onResult) {player=value;callback=onResult;}
            public void Request(long id)
            {
                if(id!=player.Id) throw new InvalidOperationException("Unknown local round portrait identity: "+id);
                callback?.Invoke(player);
            }
            public void Dispose() => callback=null;
        }
        public CharacterPortraitCache GetCharacterPortrait(ICharacterPortraitRequestor request)
        {
            long key=request.GetRequestId();
            if(!captures.TryGetValue(key,out var cache))
            {
                if(!ReferenceEquals(request.GetAppearanceData(),icon) || icon.texture.width!=100 || icon.texture.height!=120
                    || !icon.texture.isReadable || icon.rect!=new Rect(0,0,100,120))
                    throw new InvalidOperationException("The native round head requires the completed original 100x120 photograph.");
                cache=new CharacterPortraitCache();
                cache.UpdateTexture(new CharacterPortraitTexture2D(icon.texture,100,120,true),key,false,out _);
                captures.Add(key,cache);
            }
            return cache;
        }
        public void ClearAllCharacterPortraitPersistenceData() => throw new InvalidOperationException("Round photographs are owned by the battle host.");
    }
}
