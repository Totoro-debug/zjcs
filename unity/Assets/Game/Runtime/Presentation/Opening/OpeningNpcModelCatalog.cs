using System;
using Spine.Unity;
using UnityEngine;

namespace Zjcs.Presentation.Opening
{
    /// <summary>
    /// Official battle Spine models for the NPC characters that appear in the opening
    /// record. Their skeletons are converted into the shared battle Spine payload but
    /// are not part of the monster identity table, so the opening binds them directly.
    /// </summary>
    [CreateAssetMenu(menuName = "Zjcs/Opening NPC Model Catalog")]
    public sealed class OpeningNpcModelCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "Opening/OpeningNpcModelCatalog";

        [Serializable]
        public sealed class Entry
        {
            public int classId;
            public SkeletonDataAsset skeleton;
            public string skin = string.Empty;
            public float scale = 0.01f;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public static OpeningNpcModelCatalog LoadDefault()
        {
            return Resources.Load<OpeningNpcModelCatalog>(DefaultResourcePath);
        }

        public bool TryGet(int classId, out SkeletonDataAsset skeleton, out string skin, out float scale)
        {
            foreach (Entry entry in entries)
            {
                if (entry == null || entry.classId != classId || entry.skeleton == null) continue;
                skeleton = entry.skeleton;
                skin = entry.skin ?? string.Empty;
                scale = entry.scale > 0f ? entry.scale : 0.01f;
                return true;
            }
            skeleton = null;
            skin = string.Empty;
            scale = 0.01f;
            return false;
        }
    }
}
