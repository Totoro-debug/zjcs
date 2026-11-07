using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.StoryFollowBone;

namespace Zjcs.Editor
{
    public static partial class OfficialBattleMovementEffectAcceptance
    {
        private static IEnumerator FadeSequenceSteps(Transform parent)
        {
            float previousDelta = Time.captureDeltaTime;
            var root = new GameObject("Original fade sequence verification");
            root.transform.SetParent(parent, false);
            var triggerRoot = new GameObject("Independently disabled trigger");
            triggerRoot.transform.SetParent(root.transform, false);
            var trigger = triggerRoot.AddComponent<MyPlayableFadeEffectSequence>();
            var prefab = Resources.Load<GameObject>(
                "Combat/GroundEffects/Prefabs/EcEntity/Fight/FightSkill/Move/Effect/Eff_Role_Wave");
            Require(prefab != null, "original fade particle input");
            var effects = new MyEffectFadeOut[2];
            for (int i = 0; i < effects.Length; i++)
            {
                var instance = UnityEngine.Object.Instantiate(prefab, root.transform, false);
                instance.transform.localPosition = new Vector3(i * 2, 0, 0);
                var fade = instance.AddComponent<MyEffectFadeOut>();
                fade.myParticles = instance.GetComponentsInChildren<ParticleSystem>(true);
                fade.myRenderers = Array.Empty<SpriteRenderer>();
                fade.mTMPText = Array.Empty<TMPro.TMP_Text>();
                Require(fade.myParticles.Length > 0, "native particles for fade " + i);
                effects[i] = fade;
            }
            void Configure(params MyPlayableFadeEffectSequence.MyPlayableFadeEffectCfg[] entries)
            {
                var serialized = new SerializedObject(trigger);
                var configs = serialized.FindProperty("myTriggerCfgs");
                configs.arraySize = entries.Length;
                for (int i = 0; i < entries.Length; i++)
                {
                    var cfg = configs.GetArrayElementAtIndex(i);
                    cfg.FindPropertyRelative("StartTime").floatValue = entries[i].StartTime;
                    cfg.FindPropertyRelative("Duration").floatValue = entries[i].Duration;
                    cfg.FindPropertyRelative("Effect").objectReferenceValue = entries[i].Effect;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            MyPlayableFadeEffectSequence.MyPlayableFadeEffectCfg Entry(int index, float start)
                => new MyPlayableFadeEffectSequence.MyPlayableFadeEffectCfg
                    { Effect = effects[index], StartTime = start, Duration = .04f };
            void Reset()
            {
                trigger.StopFadeTask();
                foreach (var fade in effects)
                {
                    fade.StopFadeoutTask();
                    foreach (var particle in fade.myParticles)
                    {
                        var emission = particle.emission;
                        emission.enabled = true;
                    }
                }
            }
            bool Emitting(int index)
            {
                foreach (var particle in effects[index].myParticles)
                    if (!particle.emission.enabled) return false;
                return true;
            }
            try
            {
                Time.captureDeltaTime = .02f;
                yield return null;
                foreach (var particle in effects[0].myParticles) particle.Simulate(.15f, false, true, true);
                Capture(root.transform.position, "fade-sequence-native-input");
                Configure(Entry(0, .01f), Entry(1, .01f));
                trigger.Play(null);
                Require(!Emitting(0) && Emitting(1), "current delta is accumulated before the first check; equal due entries remain on separate frames");
                yield return null;
                Require(!Emitting(1), "second due entry runs on the following frame");

                Reset();
                Configure(Entry(0, .08f));
                trigger.Play(null);
                yield return null;
                trigger.Play(null);
                yield return null;
                yield return null;
                Require(Emitting(0), "replay cancels the previous pending deadline and resets elapsed time");
                for (int i = 0; i < 4; i++) yield return null;
                Require(!Emitting(0), "restarted sequence eventually reaches its own deadline");

                Reset();
                Configure(Entry(0, .08f));
                trigger.Play(null);
                trigger.StopFadeTask();
                for (int i = 0; i < 6; i++) yield return null;
                Require(Emitting(0), "explicit cancellation prevents a pending effect");

                Reset();
                Configure(Entry(0, .08f));
                trigger.Play(null);
                triggerRoot.SetActive(false);
                for (int i = 0; i < 6; i++) yield return null;
                Require(!Emitting(0), "original NextFrame sequence continues while its trigger object is inactive");
                triggerRoot.SetActive(true);

                Reset();
                Configure(Entry(0, .08f), Entry(1, 0));
                trigger.Play(null);
                Require(Emitting(0) && Emitting(1), "serialized entries are not sorted by start time");
                for (int i = 0; i < 6; i++) yield return null;
                Require(!Emitting(0) && !Emitting(1), "late entry precedes an earlier-time entry in serialized order");

                Reset();
                var missingEffect = Entry(0, 0); missingEffect.Effect = null;
                Configure(missingEffect, Entry(1, 0));
                trigger.Play(null);
                yield return null;
                Require(Emitting(0) && !Emitting(1), "a missing effect reference advances to the next entry");

                Reset();
                Configure(Entry(0, .08f));
                trigger.Play(null);
                UnityEngine.Object.Destroy(triggerRoot);
                yield return null;
                for (int i = 0; i < 6; i++) yield return null;
                Require(Emitting(0), "destruction cancels pending effects on independent surviving objects");
                Debug.Log("Original fade sequence Play Mode passed: first-frame delta, one entry per frame, serialized order, replay reset, explicit cancellation, inactive trigger continuation, missing effect and destroy cancellation using native particles.");
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
                if (trigger) trigger.StopFadeTask();
                foreach (var effect in effects) if (effect) effect.StopFadeoutTask();
                UnityEngine.Object.Destroy(root);
            }
        }
    }
}
