using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        public static void RunWeaponHostMovement()
        {
            OfficialBattleWeaponHostMotionNumericalValidation.Check();
            RunNativeWeaponPrefabs();
            SessionState.SetBool(Key + ".weapon-host-motion", true);
        }

        private static Vector3 WeaponHostPosition(ProfessionNativeSceneWeaponRenderer renderer)
        {
            var movement = typeof(ProfessionNativeSceneWeaponRenderer).GetField("movement", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer);
            return (Vector3)typeof(ProfessionNativeScenePhysicsMovement).GetField("position", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(movement);
        }

        private static IEnumerator CheckWeaponHostMovement(ProfessionNativeSceneRenderer body,
            ProfessionNativeSceneWeaponRenderer renderer, string name)
        {
            if (!renderer.State.SkinConstraints.Any(index => renderer.State.Source.Constraints[index].Kind == "physics")) yield break;
            var originalPosition = body.transform.localPosition;
            var originalRotation = body.transform.localRotation;
            try
            {
                var bones = WeaponMatrices(renderer.State);
                float clock = NativeWeaponClock(renderer);
                var sampled = WeaponHostPosition(renderer);
                Capture(NativeWeaponBounds(body.gameObject), "weapon-host-before-" + name);
                body.transform.localPosition += new Vector3(.35f, .1f, 0);
                body.transform.localRotation *= Quaternion.Euler(0, 0, 25);
                renderer.UpdateAnimationAndMesh(); renderer.UpdateAnimationAndMesh();
                Require(NativeWeaponClock(renderer) == clock && WeaponHostPosition(renderer) == sampled
                    && WeaponMatrixDelta(bones, WeaponMatrices(renderer.State)) < .0001f,
                    "host movement is not consumed by material/mesh redraw: " + name);
                for (int frame = 0; frame < 4; frame++)
                {
                    var expected = renderer.transform.position;
                    yield return null;
                    Require(Vector3.Distance(WeaponHostPosition(renderer), expected) < .0001f,
                        "real Update consumes the current weapon host position once: " + name);
                }
                Require(NativeWeaponClock(renderer) > clock && WeaponMatrixDelta(bones, WeaponMatrices(renderer.State)) > .0001f,
                    "moved native weapon has an advanced physical pose: " + name);
                CheckNativeWeaponPose(renderer, name);
                Capture(NativeWeaponBounds(body.gameObject), "weapon-host-after-" + name);
                clock = NativeWeaponClock(renderer); sampled = WeaponHostPosition(renderer);
                renderer.enabled = false;
                body.transform.localPosition += Vector3.right * .2f;
                yield return null;
                Require(NativeWeaponClock(renderer) == clock && WeaponHostPosition(renderer) == sampled,
                    "disabled renderer retains its host sample and physics history: " + name);
                renderer.enabled = true;
                yield return null; yield return null;
                Require(NativeWeaponClock(renderer) > clock && Vector3.Distance(WeaponHostPosition(renderer), renderer.transform.position) < .0001f,
                    "enabled renderer resumes host sampling and physics: " + name);
                Debug.Log("Native weapon host movement passed: " + name);
            }
            finally
            {
                renderer.enabled = true;
                body.transform.localPosition = originalPosition;
                body.transform.localRotation = originalRotation;
            }
        }
    }
}
