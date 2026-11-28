using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.ItemDetailUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Offline.TreasureUI
{
    public sealed class OfflineTreasureItemDetailBindings:IItemDetailBindings,IItemDetailQualityBindings
    {
        private readonly OfflineTreasureDetailConfiguration configuration;
        private readonly OfflineTreasureService cultivation;
        private readonly IProfessionImageResourceLoader images;
        private readonly Func<int,IReadOnlyList<ItemDetailSource>> sources;
        private readonly Func<GameObject,Transform,GameObject> instantiate;
        private readonly Action<GameObject> close;
        public IProfessionItemIconContext Items { get; }
        public OfflineTreasureItemDetailBindings(OfflineTreasureDetailConfiguration configuration,OfflineTreasureService cultivation,
            IProfessionItemIconContext items,IProfessionImageResourceLoader images,Func<int,IReadOnlyList<ItemDetailSource>> sources,
            Func<GameObject,Transform,GameObject> instantiate,Action<GameObject> close)
        {
            this.configuration=configuration??throw new ArgumentNullException(nameof(configuration));
            this.cultivation=cultivation??throw new ArgumentNullException(nameof(cultivation));
            Items=items??throw new ArgumentNullException(nameof(items));this.images=images??throw new ArgumentNullException(nameof(images));
            this.sources=sources??throw new ArgumentNullException(nameof(sources));this.instantiate=instantiate??throw new ArgumentNullException(nameof(instantiate));
            this.close=close??throw new ArgumentNullException(nameof(close));
        }
        public ItemDetailDefinition Detail(int itemId)
        {
            var info=configuration.GetItem(itemId)??throw new InvalidOperationException("Original treasure detail item is absent: "+itemId);
            int treasure=cultivation.GetTreasureIdByPieceId(itemId);
            return new ItemDetailDefinition{Info=info,NameKey="item_"+itemId+"_name",TypeKey=info.TypeKey,
                DescriptionKey=info.DescriptionKey,FunctionDescriptionKey=info.FunctionDescriptionKey,
                DescriptionArguments=new object[]{treasure!=0?new CultureTextArg("item_"+treasure+"_name"):null},
                ShowPlayerItemCount=info.ShowPlayerItemCount};
        }
        public bool ContainsText(string key)=>configuration.ContainsText(key);
        public Sprite LoadSprite(string address)
        {
            using(var operation=images.LoadAsset<Sprite>(address.StartsWith("Assets/",StringComparison.Ordinal)?address:"Assets/Image/"+address))
                return operation.Result??throw new InvalidOperationException("Original detail sprite is unbound: "+address);
        }
        public Sprite QualityBackground(Quality quality,bool small)=>LoadSprite("UI/ItemDetailDialog/Img"+quality+"_PopUps"+(small?"_Small":""));
        public Color QualityTextColor(Quality quality)=>configuration.QualityTextColor(quality);
        public void SetQualityText(Zjcs.Official.WorldUI.WorldMultiLanguageText text,Quality quality)
        {
            text.SetKey("Quality."+quality);
            var color=configuration.QualityColor(quality);
            var gradient=text.GetComponent<Zjcs.Official.World.NativeLabelUI.TMPColorGradient>();
            if(color.EnableGradient!=0)
            {
                if(gradient){gradient.enabled=true;gradient.SetGradientColor(color.GradientColor.Value());}
            }
            else
            {
                if(gradient)gradient.enabled=false;
                text.Color=color.Color;
            }
        }
        public IReadOnlyList<ItemDetailSource> Sources(int id)=>sources(id)??throw new InvalidOperationException("Original item source owner returned no source collection.");
        public GameObject InstantiateUi(GameObject prefab,Transform parent)=>instantiate(prefab,parent);
        public void Close(GameObject window)=>close(window);
    }
}
