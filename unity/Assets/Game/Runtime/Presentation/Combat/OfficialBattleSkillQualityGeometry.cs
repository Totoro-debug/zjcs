using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Combat
{
    /// <summary>
    /// Restores Image.GenerateSimpleSprite's original padding and fractional atlas UVs after
    /// binding the packed Sprite to its complete native atlas. The original Image and mask remain.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public sealed class OfficialBattleSkillQualityGeometry : BaseMeshEffect
    {
        private BattleSkillQualityBinding binding;

        public void Bind(BattleSkillQualityBinding value)
        {
            var image = (Image)graphic;
            if (value == null || image.sprite != value.sprite || image.type != Image.Type.Simple
                || image.preserveAspect || value.sourceSize.x <= 0 || value.sourceSize.y <= 0)
                throw new InvalidOperationException("Unsupported original skill quality Image geometry.");
            binding = value;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || binding == null || vertices.currentVertCount == 0) return;
            if (((Image)graphic).sprite != binding.sprite)
                throw new InvalidOperationException("The original skill quality Sprite changed without its geometry.");
            Rect rect = graphic.GetPixelAdjustedRect();
            Vector2 min = rect.min + Vector2.Scale(rect.size, new Vector2(
                binding.trimOffset.x / binding.sourceSize.x, binding.trimOffset.y / binding.sourceSize.y));
            Vector2 max = min + Vector2.Scale(rect.size, new Vector2(
                binding.trimSize.x / binding.sourceSize.x, binding.trimSize.y / binding.sourceSize.y));
            Vector2 uvMin = binding.uvMin;
            Vector2 uvMax = binding.uvMax;
            UIVertex vertex = default;
            vertices.PopulateUIVertex(ref vertex, 0);
            vertices.Clear();
            vertex.position = new Vector3(min.x, min.y); vertex.uv0 = new Vector2(uvMin.x, uvMin.y); vertices.AddVert(vertex);
            vertex.position = new Vector3(min.x, max.y); vertex.uv0 = new Vector2(uvMin.x, uvMax.y); vertices.AddVert(vertex);
            vertex.position = new Vector3(max.x, max.y); vertex.uv0 = new Vector2(uvMax.x, uvMax.y); vertices.AddVert(vertex);
            vertex.position = new Vector3(max.x, min.y); vertex.uv0 = new Vector2(uvMax.x, uvMin.y); vertices.AddVert(vertex);
            vertices.AddTriangle(0, 1, 2);
            vertices.AddTriangle(2, 3, 0);
        }
    }
}
