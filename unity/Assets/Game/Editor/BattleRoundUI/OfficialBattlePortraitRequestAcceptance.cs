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
using Selector = Zjcs.Official.BattleRoundUI.GameRoundPlayerPortrait;
using Config = Zjcs.Official.BattleRoundUI.GamePlayManagerConfig;
using SceneHandler = Zjcs.Official.BattleRoundUI.GameRoundPortraitETC2SceneHandler;
using NormalHandler = Zjcs.Official.BattleRoundUI.GameRoundPortraitNormalHandler;
using UiHandler = Zjcs.Official.BattleRoundUI.GameRoundPortraitETC2Handler;
using Atlas = Zjcs.Official.BattleRoundUI.FightDynamicTextureETC2;
using Effect = Zjcs.Official.BattleRoundUI.GameEffectData;
using Head = Zjcs.Official.BattleRoundUI.HeadCharacterPortrait;

namespace Zjcs.Editor.BattleRound
{
    // Exercises the original request seam using actual native-model head captures.
    internal static class OfficialBattlePortraitRequestAcceptance
    {
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
            var config=ScriptableObject.CreateInstance<Config>();
            config.EnablePlayerPortraitETC2=true;config.EnablePlayerPortraitETC2_Scene=true;
            config.DefaultEffect=new Effect {FactionRoundImgETC2Path="default"};
            config.CharacterMeEffect=new Effect {FactionRoundImgETC2Path="me"};
            config.SpactatedCharacterEffect=new Effect {FactionRoundImgETC2Path="me"};
            config.FactionEffectDatas=new List<Effect> {new Effect {FactionRoundImgETC2Path="absent"}};
            var loaded=new List<string>();
            atlas.Initialize(config,path=>{loaded.Add(path);return path=="absent"?null:path=="me"?female:male;});
            Require(loaded.SequenceEqual(new[]{"default","me","me","absent"})
                && atlas.Contains("default".GetHashCode()) && atlas.Contains("me".GetHashCode())
                && !atlas.Contains("absent".GetHashCode()),"Original faction preload ordering, duplicate cache or missing-texture behavior changed.");
            void Field(object owner,string name,object value)=>owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);
            try
            {
                var card=UnityEngine.Object.Instantiate(prefab,parent.transform,false);
                parent.transform.position=new Vector3(-2048,-2048,-2048);
                var head=card.GetComponentInChildren<SceneHead>(true);
                var request=(CharacterPortraitRequestor)typeof(Head).GetField("mRequestor",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(head);
                string animation=request.GetAnimationName();
                var manager=new Captures(male,female);head.Bind(manager);head.BindAtlas(atlas);
                var handler=card.GetComponentInChildren<SceneHandler>(true);handler.BindAtlas(atlas);
                var circle=handler.HeadSr;
                var selector=card.GetComponentInChildren<Selector>(true);selector.BindConfiguration(config);
                circle.gameObject.SetActive(true);parent.SetActive(true);
                yield return null;
                var appearance=new object();selector.SetPortraitData(101,Sex.Male,1,appearance);
                selector.SetPortraitFactionImg("me");
                Require(handler.FactionBgSr.sprite==atlas.Get("me".GetHashCode()).Sprite,"Selector did not delegate the original faction atlas lookup.");
                selector.SetPortraitColor(Color.red);Require(circle.Color==Color.red,"Selector did not delegate portrait color.");
                selector.SetPortraitActive(false);Require(!handler.PlayerIconGo.activeSelf,"Selector did not hide the portrait.");
                selector.SetPortraitActive(true);
                long key=ProfessionPortraitRequestKey.GetLongHashCode(101,0);
                Require(request.GetRequestId()==key && request.NeedPersist() && request.GetProfession()==1
                    && request.GetAppearanceData()==appearance && request.GetAnimationName()==animation,
                    "Original head request fields or stable player key changed.");
                Require(manager.Entries[key].Requestors.Count==1 && circle.Sprite==atlas.Get(key).Sprite
                    && circle.Sprite.texture.GetPixels32().SequenceEqual(male.GetPixels32()),"Original request did not bind the native male capture through the atlas.");
                Require(request.TryGetCancellationToken(out var token),"Original request cancellation unavailable.");
                head.gameObject.SetActive(false);
                Require(token.IsCancellationRequested && manager.Entries[key].Requestors.Count==0,"Disabling the head did not cancel/unsubscribe.");
                head.gameObject.SetActive(true);
                Require(manager.Entries[key].Requestors.Count==1 && circle.Sprite==atlas.Get(key).Sprite,"Re-enabling did not resume the original cached request.");
                selector.SetPortraitData(Sex.Female,2,appearance);
                long temporary=ProfessionPortraitRequestKey.GetLongHashCode(appearance.GetHashCode(),1);
                Require(request.GetRequestId()==temporary && !request.NeedPersist() && manager.Entries[key].Requestors.Count==0
                    && manager.Entries[temporary].Requestors.Count==1
                    && circle.Sprite.texture.GetPixels32().SequenceEqual(female.GetPixels32()),"Temporary female request key, subscription or captured pixels changed.");
                card.SetActive(false);Require(circle.Color==Color.white,"Original handler did not reset tint on disable.");
                UnityEngine.Object.Destroy(card);yield return null;
                Require(manager.Entries[temporary].Requestors.Count==0 && request.OnImageUpdate==null,
                    "Destroying the head leaked cache/event subscriptions.");
                Require(male!=null && female!=null,"The request module destroyed caller-owned captures.");
                foreach(bool enable in new[]{false,true})
                {
                    config.EnablePlayerPortraitETC2=enable;config.EnablePlayerPortraitETC2_Scene=false;
                    var probe=new GameObject("Original handler selection");probe.SetActive(false);probe.transform.SetParent(parent.transform);
                    var dormant=new GameObject("Dormant handler references");dormant.SetActive(false);dormant.transform.SetParent(probe.transform);
                    var normal=dormant.AddComponent<NormalHandler>();var ui=dormant.AddComponent<UiHandler>();
                    var selection=probe.AddComponent<Selector>();selection.BindConfiguration(config);
                    Field(selection,"mNormalHandler",normal);Field(selection,"mETC2Handler",ui);
                    probe.SetActive(true);
                    Require(ReferenceEquals(typeof(Selector).GetField("mCurrentHandler",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(selection),enable?(object)ui:normal),"Original normal/UGUI selector branch changed.");
                    UnityEngine.Object.Destroy(probe);yield return null;
                }
                var owned=atlas.Get("me".GetHashCode()).TexAtlas;atlas.ClearAllSprites();yield return null;
                Require(owned==null && !atlas.Contains("me".GetHashCode()),"Scene atlas clear did not release cached copies.");
                atlas.Dispose();Require(atlas.Get(1)==null && !atlas.Remove(1) && !atlas.Add(male,1).Success,"Disposed atlas retained active operations.");
                config.EnablePlayerPortraitETC2=false;loaded.Clear();
                atlas.Initialize(config,path=>{loaded.Add(path);return male;});
                Require(loaded.Count==0 && atlas.TryGetOrAdd(male,1)==null,"Disabled atlas configuration still loaded or allocated textures.");
                Debug.Log("Native portrait selector passed: scene delegation, normal/UGUI branches, faction preload order, tint reset, activation, clear/dispose and disabled configuration.");
                Debug.Log("Native portrait requests passed: stable and temporary keys, actual male/female capture pixels, atlas notification, cancellation, disable/resume, replacement and destruction subscriptions.");
            }
            finally {UnityEngine.Object.Destroy(parent);atlas.Dispose();UnityEngine.Object.Destroy(config);}
            yield return null;
        }
    }
}
