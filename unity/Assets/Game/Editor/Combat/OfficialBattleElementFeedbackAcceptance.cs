using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleElementFeedbackAcceptance
    {
        public static void Run()
        {
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                int count = 0;
                foreach (int effectType in new[] { 0,1,2,5,6,7 })
                    foreach (float? value in new float?[] { null,0f,0.2f,-0.2f,-0.56f,1f })
                    {
                        Verify(effectType,value,false,true);
                        count++;
                    }
                Verify(0,0.2f,true,true);
                Debug.Log("Original element feedback checks passed: "+count+" cases and break precedence; original text/materials/curves, immunity hides numbers, true-damage priority and sequence-frame binding. Live OnEnable is checked separately in Play Mode.");
            }
            finally { ShaderUtil.allowAsyncCompilation = asyncCompilation; }
        }

        private static void Verify(int effectType, float? value, bool broken, bool trueDamage)
        {
            var root = new GameObject("ElementFeedbackFixture");
            var hud = new GameObject("ElementFeedbackHud",typeof(RectTransform));
            OfficialBattleSceneView scene = null;
            try
            {
                scene = new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(),root.transform,null,
                    new OfficialBattleGridMapper(root.transform,new FightBattleGrid {Width=4,Height=4}),null,
                    (transform,unit)=>{},OfficialBattleHudCatalog.LoadDefault(),hud.transform);
                var checkpoint = new FightBattleCheckpoint();
                checkpoint.Units.Add(new FightBattleUnitState {Id=2,X=1,Y=1,Hp=100,MaxHp=100});
                var step = new FightBattleStepEvent();
                step.DamageEvents.Add(new FightBattleDamageEvent
                {
                    TargetId=2,HasDamageValue=true,DamageValue=value==1f ? 0 : -100,Damage=value==1f ? 0 : -100,
                    Critical=effectType==1 || effectType==6,Block=effectType==2 || effectType==7,
                    ElementType=effectType>=5 ? 200 : 101,IsElementAffect=value.HasValue,
                    ElementConfrontation=value ?? 0f,BreakResilience=broken,TrueDamage=trueDamage
                });
                scene.ShowDamageFloats(step,checkpoint,OfficialBattleDamageColors.Load());
                var view = root.GetComponentsInChildren<OfficialBattleDamageFloat>(true).Single();
                Require(view.EffectType==effectType,"Element feedback selected another damage effect.");
                var element = view.GetComponentInChildren<ElementConfrontationUI>(true);
                var binding = OfficialBattleElementFeedbackCatalog.Find(effectType);
                Require(element!=null,"Original element component was not bound.");
                if(!broken && value.HasValue && value!=0f)
                {
                    // Edit Mode does not dispatch this runtime component's OnEnable.
                    // Sample its bound clip here; simulated-input Play Mode checks the actual activation.
                    string clip=binding.triggers.Single(item=>item.trigger==element.TriggerName).clip;
                    Require(view.GetComponent<OfficialBattleHudAnimationPlayer>().Play(binding.elementPath,clip),
                        "Original element clip was not compiled.");
                    Require(element.DescribeText.color.a==0f,"Original element clip did not apply its initial alpha.");
                }
                scene.Tick(0.03f);
                scene.Tick(0.06f);
                string expected = broken || !value.HasValue || value==0f ? "" : value==1f ? "免疫" : value>0f ? "抵抗" : "克制";
                Require(element.DescribeText.text==expected,"Original element description differs: "+element.DescribeText.text);
                if (!string.IsNullOrEmpty(expected))
                {
                    Require(element.DescribeText.gameObject.activeInHierarchy,"Element description remained inactive.");
                    Require(element.TriggerName==(value>0f ? "DiKangTrigger" : "KeZhiTrigger"),"Original element Animator branch differs.");
                    Require(element.DescribeText.color.a>0f,"Original element animation was not activated with its parent.");
                    Require(Mathf.Abs(element.DescribeText.rectTransform.rect.xMax)<0.0001f,
                        "Original right text pivot was lost; the description overlaps the damage number.");
                    Require(element.DescribeText.font.name=="FZY4K SDF - dynamic"
                        && element.DescribeText.fontSharedMaterial.shader.name=="Zjcs/Combat/WorldTextMeshPro",
                        "Original dynamic font/material binding differs: "+element.DescribeText.font.name
                        +" / "+element.DescribeText.fontSharedMaterial.shader.name);
                    var material = element.DescribeText.fontSharedMaterial;
                    Require(material.IsKeywordEnabled("OUTLINE_ON")
                        && Mathf.Approximately(material.GetFloat("_OutlineWidth"),Mathf.Min(1f,40f/element.DescribeText.fontSize))
                        && material.GetColor("_OutlineColor")==new Color(0.13333334028720856f,0.13333334028720856f,0.13333334028720856f,1f),
                        "Native TMPEffect outline normalization or color differs.");
                    VerifyBackground(element);
                    VerifyPixels(root,element.DescribeText);
                    if(value<0f)
                    {
                        var sequence=element.transform.Find("Mesh").GetComponent<SpriteRenderer>();
                        float before=sequence.sharedMaterial.GetFloat("_SquenceRate");
                        scene.Tick(0.02f);
                        Require(sequence.gameObject.activeInHierarchy
                            && sequence.sharedMaterial.GetFloat("_SquenceRate")>before,
                            "Original weakness sequence-frame curve did not animate the SpriteRenderer material: "
                            +before+" -> "+sequence.sharedMaterial.GetFloat("_SquenceRate")+", active="
                            +sequence.gameObject.activeInHierarchy+", effect="+effectType);
                    }
                }
                else if (!broken)
                    Require(!element.DescribeText.gameObject.activeSelf,"Neutral damage retained stale element text.");
                if (value==1f && !broken)
                    Require(!view.GetComponentsInChildren<TextMesh>(true).Any(label=>label.gameObject.activeInHierarchy),
                        "Element immunity exposed a zero number after the parent animation.");
                string rootPrefix = "/"+binding.rootName+"/";
                Transform trueNode=view.transform.Find(OfficialBattleHudCatalog.LoadDefault().FindWorldDamage(effectType).truePath.Substring(rootPrefix.Length));
                Require(trueNode!=null && trueNode.gameObject.activeSelf==(trueDamage && !broken && !value.HasValue),
                    "Break/element/true-damage precedence differs from CharacterDamageUI.Init.");
            }
            finally
            {
                scene?.Dispose();
                UnityEngine.Object.DestroyImmediate(hud);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void VerifyBackground(ElementConfrontationUI element)
        {
            var background = element.transform.Find("Bg").GetComponent<SpriteRenderer>();
            var autoSize = background.GetComponent<DamageAutoSizeTextBack>();
            Require(background.drawMode==SpriteDrawMode.Sliced && background.sprite.border==new Vector4(12,12,12,12),
                "Original nine-slice background binding differs.");
            float width = background.size.x;
            float right = background.transform.localPosition.x+width*0.5f*background.transform.localScale.x;
            string previous = element.DescribeText.text;
            element.DescribeText.text="克制免疫";
            autoSize.OnRefresh();
            Require(background.size.x>width && Mathf.Abs(right-
                (background.transform.localPosition.x+background.size.x*0.5f*background.transform.localScale.x))<0.000001f,
                "Element background did not grow while preserving its original right anchor: "
                +width+" -> "+background.size.x+", anchor="+right+" -> "
                +(background.transform.localPosition.x+background.size.x*0.5f*background.transform.localScale.x));
            element.DescribeText.text=previous;
            autoSize.OnRefresh();
        }

        private static void VerifyPixels(GameObject root, TMP_Text label)
        {
            var cameraObject = new GameObject("ElementFeedbackRasterCamera");
            var target = new RenderTexture(256,256,24);
            var image = new Texture2D(256,256,TextureFormat.RGBA32,false);
            var previous = RenderTexture.active;
            var textRenderer = label.GetComponent<Renderer>();
            bool wasEnabled = textRenderer.enabled;
            try
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer=31;
                label.ForceMeshUpdate();
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled=false;
                camera.orthographic=true;
                camera.orthographicSize=2;
                camera.transform.position=label.transform.position+new Vector3(0,0,-10);
                camera.cullingMask=1<<31;
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=Color.black;
                camera.targetTexture=target;
                camera.Render();
                Require(!ShaderUtil.ShaderHasError(label.fontSharedMaterial.shader),
                    "Original element shader failed in the active outline variant.");
                RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,256,256),0,0);
                image.Apply();
                Color32[] withText=image.GetPixels32();
                textRenderer.enabled=false;
                camera.Render();
                image.ReadPixels(new Rect(0,0,256,256),0,0);
                image.Apply();
                Color32[] withoutText=image.GetPixels32();
                int changed=0;
                for(int i=0;i<withText.Length;i++)
                    if(Math.Abs(withText[i].r-withoutText[i].r)+Math.Abs(withText[i].g-withoutText[i].g)+Math.Abs(withText[i].b-withoutText[i].b)>3)changed++;
                Require(changed>5,"Original element text produced no raster pixels: "+label.text);
            }
            finally
            {
                textRenderer.enabled=wasEnabled;
                RenderTexture.active=previous;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static void Require(bool value,string message)
        {
            if(!value)throw new InvalidOperationException(message);
        }
    }
}
