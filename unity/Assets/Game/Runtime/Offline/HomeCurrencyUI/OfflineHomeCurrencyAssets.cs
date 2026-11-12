using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Official.HomeCurrencyWindowsUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.HomeCurrencyUI
{
    // The selected raw tables remain the authority; this catalog only projects
    // decoded fields into the original window and item-icon interfaces.
    public sealed class OfflineHomeCurrencyAssets : ScriptableObject
    {
        public GameObject quickBuyPrefab, payChangePrefab, itemIconPrefab;
        public TextAsset configuration, texts;
        public ItemIconConfig iconConfig;
        public ItemIconAttachmentConfig attachmentConfig;
        public OfflineProfessionImageCatalog images;
        [Serializable] public sealed class SoundBinding { public uint eventId; public AudioClip clip; }
        public SoundBinding[] sounds;
        JObject data;
        Dictionary<int, JObject> itemRows, quickRows, numbers;
        Dictionary<int, QuickBuyInfo> quickBuys;
        Dictionary<string, string> language;

        public static OfflineHomeCurrencyAssets LoadDefault()
            => Resources.Load<OfflineHomeCurrencyAssets>("Home/OfflineHomeCurrencyAssets")
                ?? throw new InvalidOperationException("Original home currency windows are unbound.");

        public int MoneyId => (int)Settings["MoneyId"];
        public int DiamonId => (int)Settings["DiamonId"];
        public int FlareStarId => (int)Settings["FlareStarId"];
        JObject Settings { get { Load(); return (JObject)data["tables"]["game_settings"]; } }
        public HomeCurrencyPaySettings Pay => new HomeCurrencyPaySettings {
            DiamonChangeScale=(long)Settings["Pay"]["DiamonChangeScale"],
            DiamonChangeMin=(long)Settings["Pay"]["DiamonChangeMin"],
            DiamonChangeMax=(long)Settings["Pay"]["DiamonChangeMax"] };

        public string Text(string key)
        {
            Load(); return language.TryGetValue(key, out var value) ? value : key;
        }
        public bool ContainsText(string key) { Load(); return language.ContainsKey(key); }
        public AudioClip Sound(uint id) => sounds?.FirstOrDefault(row=>row.eventId==id)?.clip;
        public JObject ItemRow(int id) { Load(); return itemRows[id]; }
        public JObject QuickBuyRow(int id) { Load(); return quickRows.TryGetValue(id,out var row)?row:null; }
        public QuickBuyInfo QuickBuy(int id) { Load(); return quickBuys.TryGetValue(id,out var info)?info:null; }
        // The original exchange window requests FlareStar in addition to the
        // three HomeUI currencies. The caller owns this runtime catalog copy.
        public OfficialHomeTopBarAssetCatalog CreateTopBarCatalog(OfficialHomeTopBarAssetCatalog template=null)
        {
            var result=Instantiate(template?template:OfficialHomeTopBarAssetCatalog.LoadDefault());
            var existing=result.currencies.FirstOrDefault(row=>row.type==PlayerItemType.FlareStar);
            if(existing!=null) {
                if(existing.itemId!=FlareStarId) { Destroy(result); throw new InvalidOperationException("Original FlareStar mapping differs."); }
                return result;
            }
            var sprite=images.Entries.Single(row=>row.Path=="Assets/Image/"+Item(FlareStarId).Icon).Asset as Sprite;
            if(!sprite) { Destroy(result); throw new InvalidOperationException("Original FlareStar icon is unbound."); }
            result.currencies=result.currencies.Concat(new[]{new OfficialHomeTopBarAssetCatalog.Currency{
                type=PlayerItemType.FlareStar,itemId=FlareStarId,icon=sprite}}).ToArray();
            return result;
        }
        public HomeCurrencyWindowItemInfo Item(int id)
        {
            Load();
            if(!itemRows.TryGetValue(id,out var row)) return null;
            var limit=((JArray)data["tables"]["item_count_limit"]).FirstOrDefault(value=>(int)value["ClassId"]==id);
            return new HomeCurrencyWindowItemInfo {
                Id=id, Icon=(string)row["RawIcon"], Name=Text("item_"+id+"_name"),
                ItemCountLimitInfo=limit==null?null:new HomeCurrencyWindowItemCountLimitInfo{MaxCount=(long)limit["MaxCount"]},
                HasAutoIncrease=row["Effects"] is JArray effects && effects.Any(effect=>(string)effect["_type"]=="ItemEffectAutoIncrease") };
        }
        public long CountLimit(int id)
        {
            Load(); return (long)((JArray)data["tables"]["item_count_limit"]).Single(row=>(int)row["ClassId"]==id)["MaxCount"];
        }
        public Color White { get { Load(); return ColorValue(data["globalColors"]["White"]); } }
        public Color Green { get { Load(); return ColorValue(data["globalColors"]["Green"]); } }
        public Color LightRed => TextColor(ThemeColorType.Light, ColorType.Red);
        public Color TextColor(ThemeColorType theme, ColorType type)
        {
            Load();
            var row=((JArray)data["globalColors"]["TextColorConfig"]["TextColorLists"])
                .First(value=>(int)value["ThemeColorType"]==(int)theme);
            var types=(JArray)row["TypeList"];
            for(int i=0;i<types.Count;i++) if((int)types[i]==(int)type)
                return ColorValue(row["TextColors"][i]["Color"]);
            throw new InvalidOperationException("Original text color is unavailable: "+theme+"/"+type);
        }
        static Color ColorValue(JToken value) => new Color((float)value["r"],(float)value["g"],(float)value["b"],(float)value["a"]);

        // Selected Common.LevelNumberParser.GetNumberDouble, with table lookup
        // supplied by the decoded local configuration and diagnostics adapted.
        public double GetNumberDouble(int id, int level)
        {
            Load();
            if(!numbers.TryGetValue(id,out var row)) {
                Debug.LogError("无法获取Id为"+id+"的等级系数"); return 0.0;
            }
            var nums=(JArray)row["Nums"];
            if(level<0) return (double)nums[0];
            if(level>=nums.Count) return (double)nums[nums.Count-1];
            return (double)nums[level];
        }
        void Load()
        {
            if(data!=null)return;
            if(!configuration||!texts)throw new InvalidOperationException("Original currency configuration is absent.");
            var parsed=JObject.Parse(configuration.text);
            if((string)parsed["manifest"]!="884_156692")throw new FormatException("Unexpected currency configuration version.");
            itemRows=((JArray)parsed["tables"]["item"]).Cast<JObject>().ToDictionary(row=>(int)row["ClassId"]);
            quickRows=((JArray)parsed["tables"]["quick_buy"]).Cast<JObject>().ToDictionary(row=>(int)row["ItemId"]);
            numbers=((JArray)parsed["tables"]["level_number"]).Cast<JObject>().ToDictionary(row=>(int)row["Id"]);
            language=((JArray)JObject.Parse(texts.text)["texts"]).ToDictionary(row=>(string)row["key"],row=>(string)row["text"]);
            quickBuys=quickRows.ToDictionary(pair=>pair.Key,pair=>new QuickBuyInfo {
                ItemId=pair.Key,CurrencyId=(int)pair.Value["CurrencyId"],RefreshType=(EQuickBuyRefreshType)(int)pair.Value["RefreshType"],
                PriceList=pair.Value["PriceList"].Values<long>().ToList(),PriceBuyCount=pair.Value["PriceBuyCount"].Values<long>().ToList(),
                BaseCount=(long)pair.Value["BaseCount"],SubRankScaleId=(int)pair.Value["SubRankScaleId"],LevelScaleId=(int)pair.Value["LevelScaleId"],
                GetNumberDouble=GetNumberDouble });
            // Selected Common.QuickBuyInfoParser.PostParse, preserving negative
            // tier handling and summation order.
            foreach(QuickBuyInfo value in quickBuys.Values) {
                long num=0L;
                foreach(long item in value.PriceBuyCount) if(num!=-1) num=item>=0?num+item:-1;
                value.MaxBuyCount=num;
            }
            data=parsed;
        }
    }
}
