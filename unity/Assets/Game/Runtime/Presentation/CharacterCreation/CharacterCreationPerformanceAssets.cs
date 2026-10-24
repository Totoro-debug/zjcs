using System;
using UnityEngine;

namespace Zjcs.Presentation.CharacterCreation
{
    [Serializable]
    public sealed class CharacterCreationAnimationAssetBinding
    {
        public string owner;
        public string alias;
        public AnimationClip clip;
    }

    [Serializable]
    public sealed class CharacterCreationMeshAssetBinding
    {
        public string owner;
        public Mesh mesh;
        public Material material;
        public Color tint;
    }

    [Serializable]
    public sealed class CharacterCreationTransformOrder
    {
        public string[] paths;
    }

    [Serializable]
    public sealed class CharacterCreationTransformOrderBinding
    {
        public string owner;
        public int currentOrderIndex;
        public CharacterCreationTransformOrder[] orders;
    }

    public sealed class CharacterCreationPerformanceAssets : ScriptableObject
    {
        public const string ResourcePath = "CharacterCreation/OfficialCharacterCreationPerformanceAssets";

        [SerializeField] private CharacterCreationAnimationAssetBinding[] animations;
        [SerializeField] private CharacterCreationMeshAssetBinding[] meshes;
        [SerializeField] private CharacterCreationTransformOrderBinding[] transformOrders;

        public CharacterCreationAnimationAssetBinding[] Animations => animations ?? Array.Empty<CharacterCreationAnimationAssetBinding>();
        public CharacterCreationMeshAssetBinding[] Meshes => meshes ?? Array.Empty<CharacterCreationMeshAssetBinding>();
        public CharacterCreationTransformOrderBinding[] TransformOrders => transformOrders ?? Array.Empty<CharacterCreationTransformOrderBinding>();

        public static CharacterCreationPerformanceAssets LoadDefault()
        {
            CharacterCreationPerformanceAssets assets = Resources.Load<CharacterCreationPerformanceAssets>(ResourcePath);
            if (assets == null) throw new InvalidOperationException("Official character creation performance assets are not installed.");
            assets.ValidateBindings();
            return assets;
        }

        public AnimationClip FindAnimation(string owner, string alias)
        {
            foreach (CharacterCreationAnimationAssetBinding binding in Animations)
                if (binding != null && binding.owner == owner && binding.alias == alias) return binding.clip;
            return null;
        }

        public bool TryGetMesh(string owner, out CharacterCreationMeshAssetBinding binding)
        {
            foreach (CharacterCreationMeshAssetBinding value in Meshes)
                if (value != null && value.owner == owner)
                {
                    binding = value;
                    return true;
                }
            binding = null;
            return false;
        }

        public bool TryGetTransformOrder(string owner, out CharacterCreationTransformOrderBinding binding)
        {
            foreach (CharacterCreationTransformOrderBinding value in TransformOrders)
                if (value != null && value.owner == owner)
                {
                    binding = value;
                    return true;
                }
            binding = null;
            return false;
        }

        public void SetGeneratedData(CharacterCreationAnimationAssetBinding[] generatedAnimations,
            CharacterCreationMeshAssetBinding[] generatedMeshes,
            CharacterCreationTransformOrderBinding[] generatedTransformOrders)
        {
            animations = generatedAnimations;
            meshes = generatedMeshes;
            transformOrders = generatedTransformOrders;
        }

        public void ValidateBindings()
        {
            if (Animations.Length < 13 || Meshes.Length != 2 || TransformOrders.Length != 1)
                throw new InvalidOperationException("Official character creation performance assets are incomplete.");

            foreach (CharacterCreationAnimationAssetBinding binding in Animations)
                if (binding == null || string.IsNullOrEmpty(binding.owner) || string.IsNullOrEmpty(binding.alias)
                    || binding.clip == null || !binding.clip.legacy)
                    throw new InvalidOperationException("An official character creation animation binding is invalid.");

            foreach (CharacterCreationMeshAssetBinding binding in Meshes)
                if (binding == null || string.IsNullOrEmpty(binding.owner) || binding.mesh == null || binding.material == null)
                    throw new InvalidOperationException("An official character creation dissolve mesh binding is invalid.");

            foreach (CharacterCreationTransformOrderBinding binding in TransformOrders)
                if (binding == null || string.IsNullOrEmpty(binding.owner) || binding.orders == null
                    || binding.orders.Length != 2 || binding.currentOrderIndex < 0
                    || binding.currentOrderIndex >= binding.orders.Length)
                    throw new InvalidOperationException("The official character creation transform orders are invalid.");
        }
    }
}
