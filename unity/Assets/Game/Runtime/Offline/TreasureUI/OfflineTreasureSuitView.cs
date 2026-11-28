using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureDetailUI;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.WorldUI;
using Object=UnityEngine.Object;

namespace Zjcs.Offline.TreasureUI
{
    // The enclosing original detail window owns layout, language, sound and popup lifetime.
    public sealed class OfflineTreasureSuitView : ITreasureSuitViewServices,IDisposable
    {
        private readonly GameObject staging;
        private readonly OfflineTreasurePropertyCatalog tables;
        private readonly Zjcs.Official.TreasureCultivation.ITreasureCultivationTables cultivation;
        private readonly ITreasurePropertyPlayer player;
        private readonly IProfessionPropertyBindings properties;
        private readonly IProfessionItemPlayer items;
        private readonly TMP_FontAsset font;
        private readonly Func<string,string> localize;
        private readonly IProfessionSoundHost sound;
        private readonly Action<int,bool> popupTreasure,popupPiece;
        public GameObject Root { get; }
        public TreasureSuitContent Content { get; }
        public OfflineTreasureSuitView(Transform parent,OfflineTreasureSuitAssets assets,
            OfflineTreasurePropertyCatalog tables,Zjcs.Official.TreasureCultivation.ITreasureCultivationTables cultivation,
            ITreasurePropertyPlayer player,IProfessionItemPlayer items,
            IProfessionPropertyBindings properties,TMP_FontAsset font,Func<string,string> localize,
            IProfessionSoundHost sound,Action<int,bool> popupTreasure,Action<int,bool> popupPiece)
        {
            if(!parent||!assets||!assets.Prefab||!font)throw new ArgumentNullException();
            this.tables=tables??throw new ArgumentNullException(nameof(tables));this.player=player??throw new ArgumentNullException(nameof(player));
            this.cultivation=cultivation??throw new ArgumentNullException(nameof(cultivation));
            this.items=items??throw new ArgumentNullException(nameof(items));this.properties=properties??throw new ArgumentNullException(nameof(properties));
            this.font=font;this.localize=localize??throw new ArgumentNullException(nameof(localize));this.sound=sound??throw new ArgumentNullException(nameof(sound));
            this.popupTreasure=popupTreasure??throw new ArgumentNullException(nameof(popupTreasure));this.popupPiece=popupPiece??throw new ArgumentNullException(nameof(popupPiece));
            staging=new GameObject("Original suit service injection");staging.SetActive(false);
            try
            {
                Root=Object.Instantiate(assets.Prefab,staging.transform,false);Inject(Root);
                Content=Root.GetComponent<TreasureSuitContent>();
                if(!Content)throw new InvalidOperationException("Original suit content controller is unbound.");
                Content.Configure(this);Root.SetActive(false);Root.transform.SetParent(parent,false);
            }
            catch{if(Root)Object.Destroy(Root);Object.Destroy(staging);throw;}
        }
        private void Inject(GameObject root)
        {
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))text.font=font;
            foreach(var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true)){text.Localizer=localize;text.FontProvider=()=>font;}
            foreach(var row in root.GetComponentsInChildren<ProfessionPlayerPropItem>(true))row.Configure(properties);
            foreach(var row in root.GetComponentsInChildren<TreasureSuitPropItem>(true))row.Configure(tables);
            foreach(var click in root.GetComponentsInChildren<UIClickSound>(true))click.AkEvent.InstanceHost=sound;
        }
        public void Show(int treasureClassId,bool canOperation)
        {
            if(!Root)throw new ObjectDisposedException(nameof(OfflineTreasureSuitView));
            var suit=tables.Suits.FirstOrDefault(value=>value.Treasures.Contains(treasureClassId));
            var stars=new Dictionary<int,int>();if(suit!=null)TreasureSuitQueries.GetSuitStartInfo(suit,player,stars);
            Content.Show(treasureClassId,canOperation,suit,stars);
        }
        public bool HasItem(int itemId,long count)=>items.GetItemAmount(itemId)>=count;
        public int GetPieceIdByTreasureId(int id)=>cultivation.GetPieceIdByTreasureId(id);
        public Transform InstantiateSuitMember(Transform prefab,Transform parent)
        {
            if(!staging)throw new ObjectDisposedException(nameof(OfflineTreasureSuitView));
            var clone=Object.Instantiate(prefab,staging.transform,false);
            try{Inject(clone.gameObject);clone.SetParent(parent,false);return clone;}
            catch{Object.Destroy(clone.gameObject);throw;}
        }
        public void PopupTreasure(int id,bool canOperation)=>popupTreasure(id,canOperation);
        public void PopupPiece(int id,bool showButtons)=>popupPiece(id,showButtons);
        public void Dispose(){if(Root){Root.SetActive(false);Object.Destroy(Root);}if(staging)Object.Destroy(staging);}
    }
}
