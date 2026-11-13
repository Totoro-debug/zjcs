using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer), typeof(MeshRenderer))]
public class UIMesh : MaskableGraphic
{
    [SerializeField]
    [HideInInspector]
    private MeshRenderer mMeshRenderer;

    public Mesh Mesh;

    private MaterialPropertyBlock mMPB;

    // Local binding of the original serialized references before activation.
    public void Configure(Mesh mesh, MeshRenderer renderer)
    { Mesh = mesh; mMeshRenderer = renderer; }

    public override Material materialForRendering
    {
        get => Application.isPlaying ? mMeshRenderer.material : mMeshRenderer.sharedMaterial;
    }

    private bool IsValid() => Mesh != null;

    protected override void OnEnable()
    {
        base.OnEnable();
        ApplyMaterialPropertyBlock();
    }

    public override void Rebuild(CanvasUpdate update)
    {
        if (IsValid() && update == CanvasUpdate.PreRender)
        {
            canvasRenderer.SetMesh(Mesh);
            UpdateMaterial();
        }
    }

    protected override void UpdateMaterial()
    {
        if (mMeshRenderer == null || mMeshRenderer.sharedMaterial == null || canvasRenderer == null)
            return;
        canvasRenderer.materialCount = 1;
        canvasRenderer.SetMaterial(materialForRendering, 0);
        canvasRenderer.SetTexture(null);
    }

    private void ApplyMaterialPropertyBlock()
    {
        if (mMPB == null) mMPB = new MaterialPropertyBlock();
        mMPB.Clear();
        mMeshRenderer.GetPropertyBlock(mMPB);
        UIMeshUtils.ApplyMaterialPropertyBlock(materialForRendering, mMPB);
    }

    private void Update() => ApplyMaterialPropertyBlock();
}
