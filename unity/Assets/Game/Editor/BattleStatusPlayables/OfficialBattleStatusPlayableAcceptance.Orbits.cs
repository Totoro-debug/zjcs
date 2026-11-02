using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Editor
{
    public static partial class OfficialBattleStatusPlayableAcceptance
    {
        private sealed partial class Scope
        {
            [Serializable] private sealed class OrbitCatalog { public string manifest; public OrbitEntry[] graphs; }
            [Serializable] private sealed class OrbitEntry { public int id; public string resource; }

            private static List<Transform> Orbiters(MyPlayableOrbitMovement orbit) =>
                (List<Transform>)typeof(MyPlayableOrbitMovement).GetField("mOrbiters", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(orbit);

            private static MyPlayable RootGraph(MyFightStatus view) =>
                (MyPlayable)typeof(MyFightStatus).GetField("mStackPlayable", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);

            private static void CheckOrbit(MyPlayableOrbitMovement orbit, Transform body, int count)
            {
                var children = Orbiters(orbit);
                Require(children.Count == count, "Native orbit count differs from the current status: " + count);
                for (int i = 0; i < children.Count; i++)
                {
                    Vector3 expected = body.position + Quaternion.AngleAxis(
                        (360f / count * i + orbit.Time * orbit.OrbitDegreesPerSec) % 360f, orbit.Axis)
                        * Vector3.left * orbit.OrbitDistance;
                    Require(Vector3.Distance(children[i].position, expected) < .001f,
                        "Native orbit lost its configured axis/radius/time/spacing or attachment target.");
                    if (orbit.IsLookAt)
                        Require(Vector3.Dot(children[i].forward, (body.position - children[i].position).normalized) > .999f,
                            "Native orbiter stopped facing its target.");
                }
            }

            private IEnumerator OrbitSteps()
            {
                var text = Resources.Load<TextAsset>("Combat/status-orbits");
                Require(text != null, "Native status orbit catalog is missing.");
                var catalog = JsonUtility.FromJson<OrbitCatalog>(text.text);
                Require(catalog.manifest == "884_156692" && catalog.graphs.Length == 3,
                    "Native status orbit catalog has the wrong source/version.");
                var attachment = sourceA.GetComponent<AttachAgent>() ?? sourceA.AddComponent<AttachAgent>();
                var body = new GameObject("body").transform; body.SetParent(sourceA.transform, false);
                attachment.myBoneNameList.Add("body"); attachment.myBoneList.Add(body); attachment.Init();
                var agent = sourceA.GetComponent<MyFightStatusAgent>();
                foreach (var entry in catalog.graphs)
                {
                    var prefab = Resources.Load<GameObject>(entry.resource);
                    Require(prefab != null, "Original orbit prefab failed to load: " + entry.id);
                    Require(prefab.GetComponentsInChildren<MyPlayableOrbitMovement>(true).Length == 1,
                        "Original orbit graph has the wrong serialized node closure.");
                    var state = new State(1);
                    var go = new GameObject("Original orbit graph " + entry.id); go.transform.SetParent(root.transform, false);
                    var view = go.AddComponent<MyFightStatus>(); view.StackPlayable = prefab;
                    view.Bind(new Context { State = state, Owner = sourceA, Creator = sourceB, Agent = agent }, manager);
                    view.OnStart(true);
                    MyPlayable rootGraph = RootGraph(view);
                    var orbit = rootGraph.GetComponentInChildren<MyPlayableOrbitMovement>(true);
                    for (int frame = 0; frame < 40 && Orbiters(orbit).Count == 0; frame++) { yield return null; Flush(); }
                    yield return null; Flush();
                    CheckOrbit(orbit, body, 1);
                    Transform first = Orbiters(orbit)[0];
                    int max = orbit.OrbiterInfos.Max(value => value.LevelUpperLimit);
                    state.Stack(max); yield return null; Flush();
                    CheckOrbit(orbit, body, max);
                    Require(Orbiters(orbit)[0] == first, "Same-prefab stack growth discarded an existing orbiter.");
                    var createdOrbiters = Orbiters(orbit).ToArray();
                    body.localPosition = new Vector3(.15f, .1f, 0); yield return null; Flush();
                    CheckOrbit(orbit, body, max);
                    foreach (var particle in rootGraph.GetComponentsInChildren<ParticleSystem>(true))
                        if (particle.gameObject.activeInHierarchy) particle.Simulate(.2f, false, true, true);
                    Capture("orbit-" + entry.id + "-" + max);
                    if (entry.id == 24477) Require(orbit.mySpeed < 0 && orbit.Time < 0, "Original reverse-speed orbit lost signed time.");
                    state.Stack(1); yield return null; Flush();
                    CheckOrbit(orbit, body, 1);
                    Require(Orbiters(orbit)[0] == first, "Same-prefab stack reduction discarded the surviving orbiter.");
                    view.OnEnd(); yield return null; Flush();
                    Require(Orbiters(orbit).Count == 0 && state.Listeners == 0, "Orbit end failed to return graphics and unsubscribe.");
                    Require(createdOrbiters.All(value => !value.gameObject.activeSelf), "Orbit reset hid active graphics without returning them to their original pool.");
                    // A fully resumed status can reuse the same original graph at the same stack.
                    view.OnStart(false); yield return null; Flush();
                    orbit = RootGraph(view).GetComponentInChildren<MyPlayableOrbitMovement>(true);
                    for (int frame = 0; frame < 40 && Orbiters(orbit).Count == 0; frame++) { yield return null; Flush(); }
                    yield return null; Flush();
                    CheckOrbit(orbit, body, 1);
                    view.OnEnd(); yield return null; Flush();
                    manager.RemoveAllPlayable(true); center.RemoveAll();
                    body.localPosition = Vector3.zero;
                }
                Require(agent.MyStatuses.Count == 0, "Native orbit verification retained a status view.");
                Debug.Log("Original orbit graphs passed: complete native hierarchies, live status stack counts, target following, spacing/axis/radius/signed time, LookAt, retained orbiters, pool cleanup and same-stack reuse.");
            }
        }
    }
}
