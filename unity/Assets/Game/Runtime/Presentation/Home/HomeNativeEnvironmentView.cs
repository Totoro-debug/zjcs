using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Zjcs.Official.HomeSceneUiControls;

namespace Zjcs.Presentation.Home
{
    // Original TimelinePlayable's clip/mixer graph and HomeTimeDriver's explicit
    // stage time. Assets retain the original hierarchy, component fields and curves.
    internal sealed class HomeNativeEnvironmentView : IDisposable
    {
        private readonly HomeNativeEnvironmentAssets assets;
        private readonly GameObject day;
        private readonly GameObject night;
        private readonly SpriteRenderer moon;
        private readonly SpriteRenderer outline;
        private readonly SpriteRenderer glow;
        private PlayableGraph graph;
        private AnimationClipPlayable dayPlayable;
        private AnimationClipPlayable nightPlayable;
        private int moonIndex = -1;

        public HomeNativeEnvironmentView(HomeSceneView scene)
        {
            assets = Resources.Load<HomeNativeEnvironmentAssets>("Home/HomeNativeEnvironmentAssets");
            if (assets == null || assets.Day == null || assets.Night == null
                || assets.DayClip == null || assets.NightClip == null || assets.Duration <= 0)
                throw new InvalidOperationException("The original home environment assets are not bound.");
            try
            {
                day = UnityEngine.Object.Instantiate(assets.Day, scene.Root.transform, false);
                night = UnityEngine.Object.Instantiate(assets.Night, scene.Root.transform, false);
                foreach (GameObject root in new[] { day, night })
                    foreach (HomeCameraFollower follower in root.GetComponentsInChildren<HomeCameraFollower>(true))
                        follower.Target = scene.Camera.transform;
                moon = night.transform.Find("Moon/Scale/Moon").GetComponent<SpriteRenderer>();
                outline = night.transform.Find("Moon/Scale/Moon_Outline").GetComponent<SpriteRenderer>();
                glow = night.transform.Find("Moon/Scale/Moon_Glow").GetComponent<SpriteRenderer>();
                graph = PlayableGraph.Create("HomeTimeline");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                dayPlayable = CreateClipPlayable(assets.DayClip, day.GetComponent<Animator>(), "Day");
                nightPlayable = CreateClipPlayable(assets.NightClip, night.GetComponent<Animator>(), "Night");
                graph.Play();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private AnimationClipPlayable CreateClipPlayable(AnimationClip clip, Animator animator, string name)
        {
            var output = AnimationPlayableOutput.Create(graph, name, animator);
            var mixer = AnimationMixerPlayable.Create(graph, 2);
            output.SetSourcePlayable(mixer);
            var playable = AnimationClipPlayable.Create(graph, clip);
            graph.Connect(playable, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);
            return playable;
        }

        public void Apply(float dayProgress, int phase)
        {
            if (phase != moonIndex)
            {
                var sprites = Array.Find(assets.Moons, row => row.Index == phase);
                if (sprites == null) throw new InvalidOperationException("Unbound original home moon phase: " + phase);
                moon.sprite = sprites.Moon;
                outline.sprite = sprites.Outline;
                glow.sprite = sprites.Glow;
                moonIndex = phase;
            }
            double time = assets.Duration * (double)dayProgress;
            dayPlayable.SetTime(time);
            nightPlayable.SetTime(time);
            graph.Evaluate();
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (day != null) UnityEngine.Object.Destroy(day);
            if (night != null) UnityEngine.Object.Destroy(night);
        }
    }
}
