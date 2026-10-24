using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.CharacterCreation
{
    public sealed class CharacterCreationPerformancePlayer : MonoBehaviour
    {
        private const string RootOwner = "/CreatePlayerPanel";
        private const string RoleOwner = "/CreatePlayerPanel/Content/BgGo/RoleContent";
        private const string MaleGlowOwner = RoleOwner + "/MaleGo/ColorImg/RoleImg/ShowSwitch/SelGlow";
        private const string FemaleGlowOwner = RoleOwner + "/FemaleGo/ColorImg/RoleImg/ShowSwitch/SelGlow";
        private readonly Dictionary<string, Animation> players = new Dictionary<string, Animation>(StringComparer.Ordinal);
        private Action resetDiyTabs;
        private Action playRun;
        private Action playIdle;
        private CharacterCreationPerformanceAssets performanceAssets;
        private Animation rootAnimation;

        public bool IsTransitionPlaying => rootAnimation != null && rootAnimation.isPlaying;

        public void Initialize(CharacterCreationPerformanceAssets assets, Action resetTabs,
            Action run, Action idle)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            performanceAssets = assets;
            resetDiyTabs = resetTabs;
            playRun = run;
            playIdle = idle;

            foreach (CharacterCreationTransformOrderBinding order in assets.TransformOrders)
                ConfigureTransformOrder(order);
            foreach (CharacterCreationMeshAssetBinding mesh in assets.Meshes)
                ConfigureMesh(mesh);

            foreach (CharacterCreationAnimationAssetBinding binding in assets.Animations)
            {
                Animation animation = GetAnimation(binding.owner);
                animation.AddClip(binding.clip, binding.alias);
                AnimationState state = animation[binding.alias];
                state.wrapMode = binding.clip.wrapMode;
                if (binding.owner == RootOwner) rootAnimation = animation;
            }
        }

        public bool PlayRoot(string alias)
        {
            return Play(RootOwner, alias);
        }

        public bool PlaySelection(string alias)
        {
            return Play(RoleOwner, alias);
        }

        public void ApplyInitialPageState(string body, string page)
        {
            if (body != "male" && body != "female") throw new ArgumentOutOfRangeException(nameof(body));
            if (page != "appearance" && page != "name") throw new ArgumentOutOfRangeException(nameof(page));

            SampleRoot(body == "female" ? "Shape_Female" : "Shape");
            SampleRoot(body == "female" ? "ShapeToDiy_Female" : "ShapeToDiy_Male");
            if (page == "name") SampleRoot("DiyToSetName");
            playIdle?.Invoke();
        }

        public void PlaySelectedGlow(string body)
        {
            string selectedOwner = body == "female" ? FemaleGlowOwner : MaleGlowOwner;
            string otherOwner = body == "female" ? MaleGlowOwner : FemaleGlowOwner;
            Stop(otherOwner, "Loop");
            Play(selectedOwner, "Loop");
        }

        public bool IsSelectionPlaying(string alias)
        {
            return IsPlaying(RoleOwner, alias);
        }

        public void ResetDIYContentTabGroup()
        {
            resetDiyTabs?.Invoke();
        }

        public void PlayRunAnimation()
        {
            playRun?.Invoke();
        }

        public void PlayIdleAnimaiton()
        {
            playIdle?.Invoke();
        }

        private bool Play(string owner, string alias)
        {
            if (!players.TryGetValue(owner, out Animation animation) || animation[alias] == null) return false;
            animation.Stop();
            return animation.Play(alias);
        }

        private void SampleRoot(string alias)
        {
            AnimationClip clip = performanceAssets.FindAnimation(RootOwner, alias);
            if (clip == null) throw new InvalidOperationException("Official character creation page animation is missing: " + alias);
            clip.SampleAnimation(gameObject, clip.length);
        }

        private void Stop(string owner, string alias)
        {
            if (players.TryGetValue(owner, out Animation animation) && animation[alias] != null)
                animation.Stop(alias);
        }

        private bool IsPlaying(string owner, string alias)
        {
            return players.TryGetValue(owner, out Animation animation) && animation.IsPlaying(alias);
        }

        private Animation GetAnimation(string owner)
        {
            if (players.TryGetValue(owner, out Animation animation)) return animation;
            Transform target = FindOwner(owner);
            if (target == null) throw new InvalidOperationException("Official character creation animation owner is missing: " + owner);
            animation = target.GetComponent<Animation>();
            if (animation == null) animation = target.gameObject.AddComponent<Animation>();
            animation.playAutomatically = false;
            players.Add(owner, animation);
            return animation;
        }

        private void ConfigureTransformOrder(CharacterCreationTransformOrderBinding binding)
        {
            Transform owner = FindOwner(binding.owner);
            if (owner == null) throw new InvalidOperationException("Official character creation transform owner is missing: " + binding.owner);
            CharacterCreationTransformReorderer reorderer = owner.GetComponent<CharacterCreationTransformReorderer>();
            if (reorderer == null) reorderer = owner.gameObject.AddComponent<CharacterCreationTransformReorderer>();
            var orders = new Transform[binding.orders.Length][];
            for (int i = 0; i < binding.orders.Length; i++)
            {
                string[] paths = binding.orders[i].paths;
                if (paths == null) throw new InvalidOperationException("An official character creation transform order has no paths.");
                orders[i] = new Transform[paths.Length];
                for (int j = 0; j < paths.Length; j++)
                {
                    orders[i][j] = FindOwner(paths[j]);
                    if (orders[i][j] == null)
                        throw new InvalidOperationException("An official character creation ordered child is missing: " + paths[j]);
                }
            }
            reorderer.Configure(orders, binding.currentOrderIndex);
        }

        private void ConfigureMesh(CharacterCreationMeshAssetBinding binding)
        {
            Transform owner = FindOwner(binding.owner);
            if (owner == null) throw new InvalidOperationException("Official character creation dissolve mesh owner is missing: " + binding.owner);
            MeshRenderer renderer = owner.GetComponent<MeshRenderer>();
            if (renderer == null) throw new InvalidOperationException("Official character creation dissolve MeshRenderer is missing: " + binding.owner);
            CharacterCreationUIMesh graphic = owner.GetComponent<CharacterCreationUIMesh>();
            if (graphic == null) graphic = owner.gameObject.AddComponent<CharacterCreationUIMesh>();
            graphic.Configure(binding.mesh, renderer, binding.material, binding.tint);
        }

        private Transform FindOwner(string owner)
        {
            if (owner == RootOwner) return transform;
            if (owner == null || !owner.StartsWith(RootOwner + "/", StringComparison.Ordinal)) return null;
            return transform.Find(owner.Substring(RootOwner.Length + 1));
        }
    }
}
