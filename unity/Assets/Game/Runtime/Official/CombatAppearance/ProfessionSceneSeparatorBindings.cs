using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.CombatAppearance
{
    // The original application's SDK base operations are supplied by a native backend.
    public interface IProfessionSceneMeshPart
    {
        GameObject GameObject { get; }
        MeshRenderer MeshRenderer { get; }
    }

    public interface IProfessionSceneSeparatorBackend
    {
        List<IProfessionSceneMeshPart> PartsRenderers { get; }
        bool OptimizeRender { get; set; }
        event ProfessionPartSharedMaterial OnGetSharedMaterial;
        IProfessionSceneMeshPart NewPartsRendererGameObject(Transform parent, string name, int layer);
        void OnEnable();
        void ClearSharedMaterials();
    }

    public interface IProfessionSceneSeparatorRenderer
    {
        GameObject GameObject { get; }
        List<IProfessionSkinClipSlot> SeparatorSlots { get; }
        void Initialize(bool overwrite, bool quiet);
        void OnBecameVisible();
        void OnBecameInvisible();
        IProfessionSceneSeparatorBackend CreateSeparatorBackend();
    }
}
