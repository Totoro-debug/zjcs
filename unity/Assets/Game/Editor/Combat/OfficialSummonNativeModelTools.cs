using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialSummonNativeModelTools
    {
        [Serializable] private sealed class Scalar { public string name; public float value; }
        [Serializable] private sealed class Tint { public string name; public Color value; }
        [Serializable] private sealed class TextureBinding { public string name,path; public Vector2 scale,offset; }
        [Serializable] private sealed class MaterialSettings
        {
            public string name; public int renderQueue; public bool enableInstancing,doubleSidedGI;
            public string[] keywords,disabledPasses; public Scalar[] floats,ints;
            public Tint[] colors; public TextureBinding[] textures;
        }
        [Serializable] private sealed class PortraitSettings
        {
            public string name,texture,spritePath;public int width,height;public uint extrude;public float ppu;
            public Vector2 pivot;public Vector4 border;public Vector2[] vertices;public ushort[] triangles;
        }
        [Serializable] private sealed class ModelSettings { public int id; public string shaderPath; public MaterialSettings materialSettings;public PortraitSettings portraitData; }
        [Serializable] private sealed class Output { public string path,sha256,meta_sha256; }
        [Serializable] private sealed class Receipt { public string source_sha256,importer_sha256; public Output[] outputs; }
        public const string SourcePath="Assets/Official/Combat/Summons/";
        public const string DataPath="Assets/Game/Resources/Combat/Summons/";

        public static void ImportAndValidate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(DataPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var outputs=new List<string>();
            const string pipelinePath="Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset";
            const string pipelineBindingPath="Assets/Game/Resources/Combat/OfficialBattleRenderPipelineAssets.asset";
            var pipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(pipelinePath);
            if(!pipeline || AssetDatabase.AssetPathToGUID(pipelinePath)!="3ee5fa15996893c1d929e35fcfd98f7e")
                throw new InvalidDataException("Original APK global render pipeline binding differs.");
            var pipelineBinding=AssetDatabase.LoadAssetAtPath<OfficialBattleRenderPipelineAssets>(pipelineBindingPath);
            if(!pipelineBinding){pipelineBinding=ScriptableObject.CreateInstance<OfficialBattleRenderPipelineAssets>();AssetDatabase.CreateAsset(pipelineBinding,pipelineBindingPath);}
            pipelineBinding.Pipeline=pipeline;EditorUtility.SetDirty(pipelineBinding);outputs.Add(pipelineBindingPath);
            foreach(int model in new[]{3181,3193})
            {
                string text=File.ReadAllText(SourcePath+model+".json");
                var source=JsonUtility.FromJson<OfficialSummonSkeletonSource>(text);
                var settings=JsonUtility.FromJson<ModelSettings>(text);
                if(source.renderer.tintBlack || source.renderer.initialSkinName!="" || source.slots.Any(s=>s.blend!="normal"))
                    throw new InvalidDataException("Restore the original summon skin/tint/blend before importing.");
                foreach(var animation in source.animations)
                    foreach(var track in animation.tracks) track.Curve=ConvertCurve(track.keys);
                var shader=AssetDatabase.LoadAssetAtPath<Shader>(settings.shaderPath);
                if(!shader)throw new InvalidDataException("Original summon shader not imported.");
                string materialPath=DataPath+source.id+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
                material.shader=shader;var m=settings.materialSettings;
                material.name=m.name;material.renderQueue=m.renderQueue;material.shaderKeywords=m.keywords;
                material.enableInstancing=m.enableInstancing;material.doubleSidedGI=m.doubleSidedGI;
                foreach(var p in m.floats)material.SetFloat(p.name,p.value);
                foreach(var p in m.ints)material.SetInteger(p.name,(int)p.value);
                foreach(var p in m.colors)material.SetColor(p.name,p.value);
                foreach(var p in m.textures)
                {
                    var texture=string.IsNullOrEmpty(p.path)?null:AssetDatabase.LoadAssetAtPath<Texture>(p.path);
                    if(!string.IsNullOrEmpty(p.path) && !texture)throw new InvalidDataException("Original summon texture not bound: "+p.path);
                    material.SetTexture(p.name,texture);material.SetTextureScale(p.name,p.scale);material.SetTextureOffset(p.name,p.offset);
                }
                foreach(string pass in m.disabledPasses)material.SetShaderPassEnabled(pass,false);
                Debug.Log("Original summon shader binding: "+source.id+", supported="+shader.isSupported+", queue="+material.renderQueue
                    +", pipelineTag="+material.GetTag("RenderPipeline",false)+", passLightMode="+shader.FindPassTagValue(0,new ShaderTagId("LightMode")).name
                    +", upperLightMode="+shader.FindPassTagValue(0,new ShaderTagId("LIGHTMODE")).name);
                EditorUtility.SetDirty(material);
                string dataPath=DataPath+source.id+"-data.asset";
                var data=AssetDatabase.LoadAssetAtPath<OfficialSummonSkeletonData>(dataPath);
                if(!data){data=ScriptableObject.CreateInstance<OfficialSummonSkeletonData>();AssetDatabase.CreateAsset(data,dataPath);}
                data.name="Official summon "+source.id;data.Source=source;data.Material=material;EditorUtility.SetDirty(data);
                if(source.showOnRoundProgress)
                {
                    var portrait=settings.portraitData;
                    var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(portrait.spritePath);
                    if(!sprite || sprite.texture!=AssetDatabase.LoadAssetAtPath<Texture2D>(portrait.texture)
                        || !sprite.vertices.SequenceEqual(portrait.vertices) || !sprite.triangles.SequenceEqual(portrait.triangles))
                        throw new InvalidDataException("Original summon native Sprite geometry differs: "+portrait.spritePath);
                    data.Portrait=sprite;
                }
                else data.Portrait=null;
                if(source.showOnRoundProgress && !data.Portrait)throw new InvalidDataException("Original summon round portrait is not bound.");
                ValidateShader(shader,m.keywords);
                outputs.Add(materialPath);outputs.Add(dataPath);
            }
            AssetDatabase.SaveAssets();
            string repository=Path.GetFullPath(".").Replace('\\','/').Contains("/.tools/")?Path.GetFullPath("../../.."):Path.GetFullPath("..");
            var receipt=new Receipt {source_sha256=Hash(File.ReadAllBytes(Path.Combine(repository,"assets/reference/battle-rules/summon-models-source-manifest.json"))),
                importer_sha256=Hash(File.ReadAllBytes("Assets/Game/Editor/Combat/OfficialSummonNativeModelTools.cs")),
                outputs=outputs.Select(p=>new Output{path="unity/"+p,sha256=Hash(File.ReadAllBytes(p)),meta_sha256=Hash(File.ReadAllBytes(p+".meta"))}).ToArray()};
            File.WriteAllText(Path.Combine(repository,".tools/battle-summon-model-binding.json"),JsonUtility.ToJson(receipt,true)+"\n",new UTF8Encoding(false));
            Debug.Log("Official summon native data/materials imported: 3181 and 4600, 85 bones, 20 animations; no Spine runtime used by this importer.");
        }

        public static AnimationCurve ConvertCurve(OfficialSummonCurveKey[] keys)
        {
            var frames=keys.Select(k=>new Keyframe(k.time,k.value){weightedMode=WeightedMode.Both,inWeight=1f/3f,outWeight=1f/3f}).ToArray();
            for(int i=0;i<frames.Length-1;i++)
            {
                var a=frames[i];var b=frames[i+1];float duration=b.time-a.time;
                if(duration<=0)throw new InvalidDataException("Original summon curve times are not increasing.");
                var control=keys[i].curve;
                if(keys[i].stepped)a.outTangent=float.PositiveInfinity;
                else if(control.Length==0)a.outTangent=b.inTangent=(b.value-a.value)/duration;
                else
                {
                    if(control.Length!=4)throw new InvalidDataException("Original summon Bezier control count differs.");
                    float first=control[0]-a.time,last=b.time-control[2];
                    if(first<=0 || last<=0 || first>duration || last>duration)throw new InvalidDataException("Restore original summon Bezier time handles.");
                    a.outWeight=first/duration;b.inWeight=last/duration;
                    a.outTangent=(control[1]-a.value)/first;b.inTangent=(b.value-control[3])/last;
                }
                frames[i]=a;frames[i+1]=b;
            }
            var result=new AnimationCurve(frames);
            for(int i=0;i<keys.Length-1;i++)
            {
                var a=keys[i];var b=keys[i+1];
                if(a.stepped || a.curve.Length!=4)continue;
                // Evaluate the original parametric cubic independently of Unity's tangent
                // representation, including non-linear time, to detect altered timing/values.
                const float u=.37f;float v=1-u;
                float time=v*v*v*a.time+3*v*v*u*a.curve[0]+3*v*u*u*a.curve[2]+u*u*u*b.time;
                float expected=v*v*v*a.value+3*v*v*u*a.curve[1]+3*v*u*u*a.curve[3]+u*u*u*b.value;
                if(Mathf.Abs(result.Evaluate(time)-expected)>.002f*Mathf.Max(1,Mathf.Abs(expected)))
                    throw new InvalidDataException("Original summon Bezier differs after native curve import.");
            }
            return result;
        }
        private static void ValidateShader(Shader shader,string[] keywords)
        {
            var data=ShaderUtil.GetShaderData(shader);
            for(int pass=0;pass<data.GetSubshader(0).PassCount;pass++)
                foreach(var stage in new[]{ShaderType.Vertex,ShaderType.Fragment})
                {
                    var result=data.GetSubshader(0).GetPass(pass).CompileVariant(stage,keywords,ShaderCompilerPlatform.D3D,
                        BuildTarget.StandaloneWindows64,UnityEngine.Rendering.GraphicsTier.Tier2,true);
                    if(!result.Success)throw new InvalidDataException("Original summon shader Windows compile failed: "+pass+" "+stage);
                }
        }
        private static string Hash(byte[] value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(value)).Replace("-","").ToLowerInvariant();}
    }
}
