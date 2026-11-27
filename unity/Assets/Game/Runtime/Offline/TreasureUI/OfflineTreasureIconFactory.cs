using System;
using TMPro;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureUI;
using Object=UnityEngine.Object;

namespace Zjcs.Offline.TreasureUI
{
    // Original UIUtils.Instantiate(All), with scene services bound before activation.
    public sealed class OfflineTreasureIconFactory : IDisposable
    {
        private readonly GameObject staging;
        private readonly IProfessionItemIconContext items;
        private readonly ITreasureIconServices player;
        private readonly IItemIconAttachmentManager attachments;
        private readonly Func<string,string> localize;
        private readonly Func<TMP_FontAsset> font;
        private readonly IProfessionSoundHost sound;
        public OfflineTreasureIconFactory(IProfessionItemIconContext items,ITreasureIconServices player,
            IItemIconAttachmentManager attachments,Func<string,string> localize,Func<TMP_FontAsset> font,IProfessionSoundHost sound)
        {
            this.items=items??throw new ArgumentNullException(nameof(items));this.player=player??throw new ArgumentNullException(nameof(player));
            this.attachments=attachments??throw new ArgumentNullException(nameof(attachments));this.localize=localize??throw new ArgumentNullException(nameof(localize));
            this.font=font??throw new ArgumentNullException(nameof(font));this.sound=sound??throw new ArgumentNullException(nameof(sound));
            staging=new GameObject("Original treasure icon service injection");staging.SetActive(false);
        }
        public GameObject Instantiate(GameObject prefab,Transform parent)
        {
            if(!staging)throw new ObjectDisposedException(nameof(OfflineTreasureIconFactory));
            if(!prefab||!parent)throw new ArgumentNullException();
            var go=Object.Instantiate(prefab,staging.transform,false);
            try {
                OfflineTreasureIconBinding.Bind(go,items,player,attachments,localize,font);
                foreach(var click in go.GetComponentsInChildren<UIClickSound>(true))click.AkEvent.InstanceHost=sound;
                go.transform.SetParent(parent,false);
                go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
                go.SetActive(true);
                return go;
            }
            catch{Object.Destroy(go);throw;}
        }
        public void Dispose(){if(staging)Object.Destroy(staging);}
    }
}
