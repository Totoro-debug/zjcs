using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Official.BattleRoundUI;
using Zjcs.Official.BattleRoundUI.DynamicTexture2DAtlas;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using SceneHead = Zjcs.Official.BattleRoundUI.HeadCharacterPortraitETC2Scene;
using Head = Zjcs.Official.BattleRoundUI.HeadCharacterPortrait;

namespace Zjcs.Editor.BattleRound
{
    // Exercises the original request seam using actual native-model head captures.
    internal static class OfficialBattlePortraitRequestAcceptance
    {
        private sealed class Atlas : IFightDynamicTextureETC2, IDisposable
        {
            private readonly IGridDynamicAtlas value = DynamicTexture2DAtlasUtils.CreateGridDynamicAtlas(1024,1024,new Vector2Int(100,120),null,true);
            public AddTexture2DResult Add(Texture2D tex,long key)=>value.Add(tex,key);
            public bool Remove(long key)=>value.Remove(key);
            public GridSpriteInfo Get(long key)=>value.Get(key);
            public bool Contains(long key)=>value.ContainsKey(key);
            public GridSpriteInfo TryGetOrAdd(Texture2D tex,long key)=>tex==null || !tex.isReadable ? null : Contains(key)?Get(key):Add(tex,key).SpriteInfo;
            public void ClearAllSprites()=>value.ClearAllSprites();
            public void Dispose()=>value.Dispose();
        }
        private sealed class Captures : ICharacterPortraitManager
        {
            public readonly Dictionary<long,CharacterPortraitCache> Entries=new Dictionary<long,CharacterPortraitCache>();
            public readonly Texture2D Male,Female;
            public Captures(Texture2D male,Texture2D female){Male=male;Female=female;}
            public CharacterPortraitCache GetCharacterPortrait(ICharacterPortraitRequestor request)
            {
                long key=request.GetRequestId();
                if(!Entries.TryGetValue(key,out var cache))
                {
                    cache=new CharacterPortraitCache();
                    var tex=request.GetSex()==0?Male:Female;
                    cache.UpdateTexture(new CharacterPortraitTexture2D(tex,100,120,true),key,false,out _);
                    Entries.Add(key,cache);
                }
                return cache;
            }
            public void ClearAllCharacterPortraitPersistenceData()=>throw new InvalidOperationException("The request fixture must not touch persistence.");
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static IEnumerator Verify(Texture2D male,Texture2D female)
        {
            var prefab=Resources.Load<GameObject>("Combat/RoundUI/GameRoundCharacterItem_Scene");
            var parent=new GameObject("Portrait request verification");parent.SetActive(false);
            var atlas=new Atlas();
            try
            {
                var card=UnityEngine.Object.Instantiate(prefab,parent.transform,false);
                parent.transform.position=new Vector3(-2048,-2048,-2048);
                var circle=card.GetComponentsInChildren<WorldCircleSpriteRenderer>(true).First();
                var head=circle.gameObject.AddComponent<SceneHead>();
                var request=new CharacterPortraitRequestor();request.SetAnimationName("home/idle_01");
                typeof(Head).GetField("mRequestor",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(head,request);
                typeof(SceneHead).GetField("mCircleSpriteSr",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(head,circle);
                var manager=new Captures(male,female);head.Bind(manager);head.BindAtlas(atlas);
                circle.gameObject.SetActive(true);parent.SetActive(true);
                yield return null;
                var appearance=new object();head.GeneratePortrait(101,Sex.Male,1,appearance);
                long key=ProfessionPortraitRequestKey.GetLongHashCode(101,0);
                Require(request.GetRequestId()==key && request.NeedPersist() && request.GetProfession()==1
                    && request.GetAppearanceData()==appearance && request.GetAnimationName()=="home/idle_01",
                    "Original head request fields or stable player key changed.");
                Require(manager.Entries[key].Requestors.Count==1 && circle.Sprite==atlas.Get(key).Sprite
                    && circle.Sprite.texture.GetPixels32().SequenceEqual(male.GetPixels32()),"Original request did not bind the native male capture through the atlas.");
                Require(request.TryGetCancellationToken(out var token),"Original request cancellation unavailable.");
                head.gameObject.SetActive(false);
                Require(token.IsCancellationRequested && manager.Entries[key].Requestors.Count==0,"Disabling the head did not cancel/unsubscribe.");
                head.gameObject.SetActive(true);
                Require(manager.Entries[key].Requestors.Count==1 && circle.Sprite==atlas.Get(key).Sprite,"Re-enabling did not resume the original cached request.");
                head.GenerateTemporarilyPortrait(Sex.Female,2,appearance);
                long temporary=ProfessionPortraitRequestKey.GetLongHashCode(appearance.GetHashCode(),1);
                Require(request.GetRequestId()==temporary && !request.NeedPersist() && manager.Entries[key].Requestors.Count==0
                    && manager.Entries[temporary].Requestors.Count==1
                    && circle.Sprite.texture.GetPixels32().SequenceEqual(female.GetPixels32()),"Temporary female request key, subscription or captured pixels changed.");
                UnityEngine.Object.Destroy(head);yield return null;
                Require(manager.Entries[temporary].Requestors.Count==0 && request.OnImageUpdate==null,
                    "Destroying the head leaked cache/event subscriptions.");
                Require(male!=null && female!=null,"The request module destroyed caller-owned captures.");
                Debug.Log("Native portrait requests passed: stable and temporary keys, actual male/female capture pixels, atlas notification, cancellation, disable/resume, replacement and destruction subscriptions.");
            }
            finally {UnityEngine.Object.Destroy(parent);atlas.Dispose();}
            yield return null;
        }
    }
}
