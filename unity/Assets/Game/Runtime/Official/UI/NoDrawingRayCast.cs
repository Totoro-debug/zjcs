using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.UI
{
    // Official Framework.FirstPass.Runtime NoDrawingRayCast; namespace adapted.
    [RequireComponent(typeof(CanvasRenderer))]
    public class NoDrawingRayCast : Graphic
    {
        public override void SetMaterialDirty() { }
        public override void SetVerticesDirty() { }
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
    }
}
