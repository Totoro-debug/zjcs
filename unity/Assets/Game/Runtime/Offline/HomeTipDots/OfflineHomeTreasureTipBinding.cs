using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using ElementType=Zjcs.Official.BattleSkillPlayables.ElementType;
using H=Zjcs.Official.HomeTipDots.TreasureRedDotEnum;
using T=Zjcs.Official.TreasureCultivation.TreasureRedDotEnum;
using S=Zjcs.Official.TreasureSlots.ElementType;

namespace Zjcs.Offline.HomeTipDots
{
    // Scene owner bridges committed save changes (including other windows' sessions) into the original graph events.
    public sealed class OfflineHomeTreasureTipBinding:IDisposable
    {
        private readonly OfflineHomeTipDotServices services;
        private readonly OfflineTreasureTipService model;
        private readonly Func<OfflinePlayerProgress> player;
        private readonly Func<ElementType,List<H>,bool,bool,bool> byElement;
        private readonly Func<List<H>,bool,bool,bool> any;
        private readonly Func<ElementType,bool> pieces;
        private readonly Func<int,bool> promotion;
        private Dictionary<int,(long Count,bool Parameter,bool Active,int Star,int Hit)> previous;
        private string previousSlots,previousGrowth;
        private bool disposed;
        public OfflineHomeTreasureTipBinding(OfflineHomeTipDotServices services,OfflineTreasureTipService model,Func<OfflinePlayerProgress> player)
        {
            this.services=services??throw new ArgumentNullException(nameof(services));this.model=model??throw new ArgumentNullException(nameof(model));
            this.player=player??throw new ArgumentNullException(nameof(player));
            if(services.TreasureRedDotEvaluator!=null||services.AnyTreasureRedDotEvaluator!=null||services.TreasurePieceRedDotEvaluator!=null||services.IsItemEffectTreasurePromoteEvaluator!=null)
                throw new InvalidOperationException("A treasure graph owner is already bound.");
            byElement=(element,tips,auto,awake)=>model.Query().TreasureRedDot((S)(int)element,Convert(tips),auto,awake);
            any=(tips,auto,awake)=>model.Query().AnyTreasureRedDot(Convert(tips),auto,awake);
            pieces=element=>model.Query().TreasurePieceRedDot((S)(int)element);promotion=model.IsPromotionItem;
            services.ObserveItemCounts(model.ObservedItemIds);
            previous=Capture();previousSlots=Slots();previousGrowth=Growth();
            services.TreasureRedDotEvaluator=byElement;services.AnyTreasureRedDotEvaluator=any;
            services.TreasurePieceRedDotEvaluator=pieces;services.IsItemEffectTreasurePromoteEvaluator=promotion;
            services.AutoMergeWhenLevelUp=model.AutoMergeWhenLevelUp;
            services.AwakeAutoMergeEnable=player().CharacterProperties.TreasureSlots.AwakeAutoMergeEnable;
        }
        private static List<T> Convert(List<H> tips)=>tips?.Select(tip=>(T)(int)tip).ToList();
        private Dictionary<int,(long Count,bool Parameter,bool Active,int Star,int Hit)> Capture()
        {
            var saved=player();var states=saved.CharacterProperties.Treasures.ToDictionary(row=>row.ClassId);
            return saved.Inventory.Where(row=>model.Catalog.Get(row.ItemId)?.Type==305).ToDictionary(row=>row.ItemId,row=>{
                states.TryGetValue(row.ItemId,out var state);
                return (row.Count,state!=null,state?.Active??false,state?.Star??0,state?.HitCount??0);
            });
        }
        private string Slots()=>JsonUtility.ToJson(player().CharacterProperties.TreasureSlots);
        private string Growth()
        {
            var p=player();return $"{p.SubRank}:{p.Level}:{p.Experience}:{p.BlessingExperience}:{p.SeasonRank}:{p.HasActiveSeasonRecord}:{p.CharacterProperties.TreasurePlaneRank}";
        }
        public void Synchronize()
        {
            if(disposed)return;
            var next=Capture();string slots=Slots(),growth=Growth();bool all=slots!=previousSlots||growth!=previousGrowth;
            var changed=previous.Keys.Concat(next.Keys).Distinct().Where(id=>all||!previous.TryGetValue(id,out var old)
                ||!next.TryGetValue(id,out var current)||!old.Equals(current)).ToArray();
            var oldIds=new HashSet<int>(previous.Keys);previous=next;previousSlots=slots;previousGrowth=growth;
            services.AutoMergeWhenLevelUp=model.AutoMergeWhenLevelUp;
            services.AwakeAutoMergeEnable=player().CharacterProperties.TreasureSlots.AwakeAutoMergeEnable;
            foreach(int id in changed)services.NotifyTreasureRefresh(id,!oldIds.Contains(id));
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(services.TreasureRedDotEvaluator==byElement)services.TreasureRedDotEvaluator=null;
            if(services.AnyTreasureRedDotEvaluator==any)services.AnyTreasureRedDotEvaluator=null;
            if(services.TreasurePieceRedDotEvaluator==pieces)services.TreasurePieceRedDotEvaluator=null;
            if(services.IsItemEffectTreasurePromoteEvaluator==promotion)services.IsItemEffectTreasurePromoteEvaluator=null;
        }
    }
}
