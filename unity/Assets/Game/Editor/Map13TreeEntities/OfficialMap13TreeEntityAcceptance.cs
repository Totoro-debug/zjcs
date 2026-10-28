using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.World.Map13EntityGridViews;
using Zjcs.Official.World.Map13EntityStateViews;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityFadeHandlers;
using Zjcs.Offline.World.NativeMap13TreeEntities;

namespace Zjcs.Editor.Map13TreeEntities
{
    public static class OfficialMap13TreeEntityAcceptance
    {
        public static void Import()
        {
            int objects=0;
            foreach(string name in new[]{"8363_view","8363_snow_view"})
            {
                string path="Assets/Game/Resources/Map13/Native/TreeEntities/"+name+".prefab";
                var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(root,"Original complete tree did not import: "+path);
                Require(root.GetComponentsInChildren<MonoBehaviour>(true).All(value=>value),"Original complete tree has a missing script.");
                var binding=root.GetComponent<NativeMap13TreeEntityBinding>();
                Require(binding&&binding.Driver&&binding.Model,"Original complete tree resource binding is incomplete.");
                Require(binding.OriginalObjects.Length==170&&binding.OriginalObjects.All(value=>value.Value),"Original complete tree lost an original object reference.");
                Require(binding.OriginalObjects.Select(value=>value.Key).Distinct().Count()==170,"Original complete tree repeats source identities.");
                var serialized=new SerializedObject(binding.Driver);var views=serialized.FindProperty("mECViews");
                Require(views.arraySize==9,"Original EC driver view count differs.");
                for(int i=0;i<views.arraySize;i++)Require(views.GetArrayElementAtIndex(i).managedReferenceValue!=null,"Original EC driver lost managed view "+i);
                Require(binding.Driver.GetView<ECWorldStageView>()!=null,"Original world-stage view lost its managed type.");
                Require(binding.Driver.GetView<ECClientVisibilityView>()!=null,"Original client-visibility view lost its managed type.");
                Require(binding.Driver.GetView<ECEntityInteractionShowView>()!=null,"Original interaction view lost its managed type.");
                Require(binding.Driver.GetView<ECAttachAgentView>().Configs.Values.Count>=53,"Original attachment configurations did not deserialize.");
                Require(binding.Driver.GetView<ECAnimatorView>()!=null&&binding.Driver.GetView<ECGridTransformView>()!=null,"Original animator/grid view lost its managed type.");
                var play=binding.Driver.GetView<ECPlayableSupporterView>();
                Require(play!=null&&play.GetPlayableHandler<ECPlayableWorldStageHandler>()!=null,"Original world-stage playable handler did not deserialize.");
                Require(play.GetPlayableHandler<EcMapEntityFadeOutDelayDestoryHandler>()?.FadeOutTime==2.8299999237060547f,"Original fade-out handler did not deserialize.");
                Require(binding.Sprites.Length==1&&binding.Sprites[0].Renderer&&binding.SlicedSprites.Length==1&&binding.SlicedSprites[0].Renderer,"Original outer UI sprite bindings are incomplete.");
                var source=new SerializedObject(binding.Driver);
                var appear=source.FindProperty("mECViews").GetArrayElementAtIndex(8).FindPropertyRelative("mAppearPlayable");
                Require(appear!=null&&appear.objectReferenceValue,"Original appear performance prefab reference is unresolved.");
                objects+=binding.OriginalObjects.Length;
            }
            var output=Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")??throw new InvalidDataException("Validation workspace must be explicit.");
            File.WriteAllText(Path.Combine(output,"native-tree-entity-import.json"),JsonUtility.ToJson(new Proof{original_objects=objects,original_views=18,editor=Application.unityVersion,backend=SystemInfo.graphicsDeviceType.ToString()},true));
            Debug.Log("Map13 complete original tree prefab import passed: "+objects+" source objects, 18 ordered source views.");
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
        [Serializable]private sealed class Proof{public int original_objects,original_views;public string editor,backend;}
    }
}
