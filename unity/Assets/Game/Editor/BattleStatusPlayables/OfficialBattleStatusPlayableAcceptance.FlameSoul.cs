using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Editor
{
    public static partial class OfficialBattleStatusPlayableAcceptance
    {
        public static void RunFlameSoul()
        {
            SessionState.SetBool(Key + ".flameSoul", true);
            Run();
        }

        private sealed partial class Scope
        {
            public IEnumerator FlameSoulSteps()
            {
                var prefab = Resources.Load<GameObject>(
                    "Combat/FlameSoulGraphs/Prefabs/EcEntity/Fight/FightSkill/24386/24386");
                Require(prefab != null, "The current original 24386 stack status is missing.");
                var original = prefab.GetComponent<MyFightStatus>();
                Require(original != null && original.StackPlayableMap != null
                    && original.StackPlayableMap.Count == 8
                    && original.StackPlayableMap.Select(value => value.StackCount)
                        .SequenceEqual(Enumerable.Range(1, 8)),
                    "The original 24386 status lost its eight native stack thresholds.");
                Require(original.StackPlayableMap.Take(5).All(value => value.PlayablePrefab != null)
                    && original.StackPlayableMap.Skip(5).All(value => value.PlayablePrefab != null)
                    && original.StackPlayableMap.Skip(5).Select(value => value.PlayablePrefab).Distinct().Count() == 1,
                    "The original 24386 status lost its five individual layers or shared maximum-layer effect.");

                var attachment = sourceA.GetComponent<AttachAgent>() ?? sourceA.AddComponent<AttachAgent>();
                var body = new GameObject("body").transform;
                body.SetParent(sourceA.transform, false);
                attachment.myBoneNameList.Add("body");
                attachment.myBoneList.Add(body);
                attachment.Init();

                var instance = new GameObject("Current original Flame Soul status view");
                instance.transform.SetParent(root.transform, false);
                var view = instance.AddComponent<MyFightStatus>();
                view.StackPlayableMap = original.StackPlayableMap;
                var agent = sourceA.GetComponent<MyFightStatusAgent>()
                    ?? sourceA.AddComponent<MyFightStatusAgent>();
                var state = new State(1);
                view.Bind(new Context { State = state, Owner = sourceA, Creator = sourceA,
                    Agent = agent, HasIcon = true }, manager);
                view.OnStart(true);

                for (int stack = 1; stack <= 8; stack++)
                {
                    if (stack > 1) state.Stack(stack);
                    MyPlayable graph = null;
                    for (int frame = 0; frame < 40; frame++)
                    {
                        yield return null;
                        Flush();
                        graph = RootGraph(view);
                        if (graph != null && graph.name.StartsWith(
                            original.StackPlayableMap[stack - 1].PlayablePrefab.name,
                            System.StringComparison.Ordinal)) break;
                    }
                    string expected = original.StackPlayableMap[stack - 1].PlayablePrefab.name;
                    Require(graph != null && graph.gameObject.activeInHierarchy
                        && graph.name.StartsWith(expected, System.StringComparison.Ordinal),
                        "The original 24386 stack did not select its serialized layer graph: " + stack);
                    var particles = graph.GetComponentsInChildren<ParticleSystem>(true);
                    Require(particles.Length > 0, "The original 24386 layer has no particle output: " + stack);
                    foreach (var particle in particles)
                        if (particle.gameObject.activeInHierarchy) particle.Simulate(.2f, false, true, true);
                    foreach (var renderer in graph.GetComponentsInChildren<ParticleSystemRenderer>(true))
                        foreach (var material in renderer.sharedMaterials)
                            if (material != null) Require(material.shader != null && material.shader.isSupported,
                                "The original 24386 Windows shader is unsupported: " + material.name);
                    yield return new WaitForSecondsRealtime(.08f);
                    camera.Render();
                    Capture("flame-soul-stack-" + stack);
                }

                int listeners = state.Listeners;
                view.OnEnd();
                for (int frame = 0; frame < 12; frame++) { yield return null; Flush(); }
                Require(state.Listeners == 0 && agent.MyStatuses.All(status => status != view)
                    && instances.All(value => value == null || !value.gameObject.activeInHierarchy),
                    "The original 24386 visuals or event listeners survived status end.");
                Require(listeners > 0, "The original 24386 status did not bind its live stack counter.");
                manager.RemoveAllPlayable(true); center.RemoveAll();
                UnityEngine.Object.Destroy(instance);
                yield return null;
                Debug.Log("Current original Flame Soul status graph passed: all eight stack thresholds, six serialized particle variants, Windows shader support, rendered output and lifecycle cleanup.");
            }
        }
    }
}
