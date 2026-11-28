using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureDetailUI;

namespace Zjcs.Offline.TreasureUI
{
    public sealed class OfflineTreasureDetailConfiguration
    {
        [Serializable] public sealed class Item:IProfessionItemInfo
        {
            public int ClassId,Quality,ElementType,ItemType;
            public string RawIcon,IconEffect,TypeKey,DescriptionKey,FunctionDescriptionKey;
            public bool CanOverlap,ShowPlayerItemCount;
            int IProfessionItemInfo.ClassId=>ClassId;
            Quality IProfessionItemInfo.Quality=>(Quality)Quality;
            public string Icon=>RawIcon;
            string IProfessionItemInfo.IconEffect=>IconEffect;
            bool IProfessionItemInfo.CanOverlap=>CanOverlap;
            public object GetFakeIcon()=>throw new InvalidOperationException("Bind the original treasure item icon: "+ClassId);
        }
        [Serializable] public sealed class Npc
        {
            public int Id,TreasureClassId;
            public string Icon,PartnerAdditionBg,NameKey;
        }
        [Serializable] public sealed class Friendship { public int Quality;public long Value; }
        [Serializable] public sealed class Text { public string Key,Value; }
        [Serializable] public sealed class PaletteValue { public Color Color;public int EnableGradient;public Zjcs.Official.WorldUI.WorldNativeGradientData GradientColor; }
        [Serializable] public sealed class PaletteList
        { public int ThemeColorType;public int[] TypeList;public PaletteValue[] TextColors; }
        [Serializable] public sealed class PaletteConfig { public PaletteList[] TextColorLists; }
        [Serializable] public sealed class Palette { public PaletteConfig QualityColorConfig; }
        [Serializable] private sealed class Payload
        {
            public Item[] Items;public Npc[] Npcs;public Friendship[] FriendshipQuality;public Text[] Texts;public Palette GlobalColors;
        }
        private readonly Dictionary<int,Item> items;
        private readonly Dictionary<int,Npc> npcs;
        private readonly Dictionary<int,HashSet<int>> byTreasure;
        private readonly Dictionary<int,long> friendship;
        private readonly Dictionary<string,string> texts;
        private readonly Palette colors;
        private readonly Zjcs.Official.World.NativeLabelUI.Framework.WorldTextReplacer textReplacer;
        public IReadOnlyCollection<Item> Items=>items.Values;
        public OfflineTreasureDetailConfiguration(TextAsset asset)
        {
            if(!asset)throw new ArgumentNullException(nameof(asset));
            var data=JsonUtility.FromJson<Payload>(asset.text)??throw new FormatException("Original treasure detail data is invalid.");
            items=data.Items.ToDictionary(row=>row.ClassId);npcs=data.Npcs.ToDictionary(row=>row.Id);
            byTreasure=new Dictionary<int,HashSet<int>>();
            foreach(var row in data.Npcs)
            {
                if(!byTreasure.TryGetValue(row.TreasureClassId,out var group))byTreasure.Add(row.TreasureClassId,group=new HashSet<int>());
                group.Add(row.Id);
            }
            friendship=data.FriendshipQuality.ToDictionary(row=>row.Quality,row=>row.Value);
            texts=data.Texts.ToDictionary(row=>row.Key,row=>row.Value);
            textReplacer=new Zjcs.Official.World.NativeLabelUI.Framework.WorldTextReplacer(TextValue);
            colors=data.GlobalColors??throw new FormatException("Original detail quality colors are absent.");
        }
        public Item GetItem(int id)=>items.TryGetValue(id,out var item)?item:null;
        public TreasureNpcViewInfo GetNpc(int id)=>npcs.TryGetValue(id,out var npc)?new TreasureNpcViewInfo{
            ClassId=id,TreasureClassId=npc.TreasureClassId,Icon=npc.Icon,PartnerAdditionBg=npc.PartnerAdditionBg,NameKey=npc.NameKey}:null;
        public HashSet<int> GetNpcByTreasure(int id)=>byTreasure.TryGetValue(id,out var group)?new HashSet<int>(group):null;
        public double GetFriendshipPercent(int treasureId)
        {
            var item=GetItem(treasureId);if(item==null)return 0;
            friendship.TryGetValue(item.Quality,out long value);
            return Zjcs.Official.TreasureProperties.TreasurePropertyNumber.Long2Double(value);
        }
        public bool ContainsText(string key)=>texts.ContainsKey(key);
        public string TextValue(string key)=>texts.TryGetValue(key,out string value)?textReplacer.ReplaceText(value):key;
        public IEnumerable<Text> Texts=>texts.Select(row=>new Text{Key=row.Key,Value=TextValue(row.Key)});
        public Color QualityTextColor(Quality quality)
            =>QualityColor(quality).Color;
        public PaletteValue QualityColor(Quality quality)
        {
            var list=colors.QualityColorConfig.TextColorLists.Single(row=>row.ThemeColorType==(int)ThemeColorType.Normal);
            return list.TextColors[Array.IndexOf(list.TypeList,(int)quality)];
        }
    }
}
