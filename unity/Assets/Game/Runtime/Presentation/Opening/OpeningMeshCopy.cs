using UnityEngine;
using Spine.Unity;
using System.Collections.Generic;

namespace Zjcs.Presentation.Opening
{
    /// <summary>CustomRenderExistingMesh.UpdateMaterials, with the native replacement table resolved once.</summary>
    public sealed class OpeningMeshCopy : MonoBehaviour
    {
        private SkeletonAnimation source;
        private MeshFilter filter;
        private MeshRenderer renderer;
        private Dictionary<Material,Material> replacements;
        public void AddRuntimeReplacementMaterial(Material original, Material runtime)
        {
            if(replacements.TryGetValue(original,out Material replacement))replacements[runtime]=replacement;
        }
        public void RemoveRuntimeReplacementMaterial(Material runtime){replacements.Remove(runtime);}
        public void Initialize(SkeletonAnimation skeleton,Dictionary<Material,Material> materials,int order)
        {
            source=skeleton;
            filter=gameObject.AddComponent<MeshFilter>();
            renderer=gameObject.AddComponent<MeshRenderer>();
            replacements=materials;
            renderer.sortingOrder=order;
            skeleton.OnMeshAndMaterialsUpdated+=Refresh;
        }
        private void Refresh(SkeletonRenderer _)
        {
            filter.sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;
            Material[] original=source.GetComponent<MeshRenderer>().sharedMaterials;
            var mapped=new Material[original.Length];
            for(int i=0;i<mapped.Length;i++)replacements.TryGetValue(original[i],out mapped[i]);
            renderer.sharedMaterials=mapped;
        }
        private void OnDestroy(){if(source!=null)source.OnMeshAndMaterialsUpdated-=Refresh;}
    }
}
