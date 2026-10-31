using System;
using UnityEngine;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.World
{
    // Movement and persistence belong to the map/story host; rendering belongs
    // to the native NPC display module.
    public sealed class OfficialMap13NpcFollowerView : MonoBehaviour
    {
        private OfficialNativeNpcModelView model;
        public Vector3 ModelOffset => new Vector3(0f, -.5f, -.5f);

        public void Initialize(int classId, int sortingLayerId, int sortingOrder)
        {
            if (model != null) throw new InvalidOperationException("An NPC follower can only be initialized once.");
            model = new OfficialNativeNpcModelView(classId, transform, "NpcModel_" + classId, sortingLayerId, sortingOrder);
            float scale = classId == 8402 ? .23f : .3f;
            model.ModelTransform.localScale = new Vector3(scale, scale, 1f);
        }
        private void Update() => model?.Tick(Time.deltaTime);
        private void OnDestroy() { model?.Dispose(); model = null; }
    }
}
