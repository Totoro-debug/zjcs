using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // Official Framework.FirstPass.Runtime/NoDrawingRayCast; namespace adapted for this project.
    [RequireComponent(typeof(CanvasRenderer))]
    public class NoDrawingRayCast : Graphic
    {
        public override void SetMaterialDirty() { }
        public override void SetVerticesDirty() { }
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
    }
}
