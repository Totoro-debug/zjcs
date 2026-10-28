using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    public static class ProfessionEventFieldValidation
    {
        [Serializable] private sealed class Fixture { public Expected[] items; }
        [Serializable] private sealed class Expected
        {
            public string path, event_name, event_guid;
            public int[] triggers;
            public uint event_id, playing_id;
        }
        private sealed class Host : IProfessionSoundHost
        {
            public int Calls;
            public uint Event;
            public GameObject Emitter;
            public uint PostEvent(uint id, GameObject emitter)
            {
                Calls++; Event = id; Emitter = emitter; return 417;
            }
        }
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Original profession event: " + message);
        }
        public static void Run()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string fixturePath = Path.GetFullPath(Path.Combine(project,
                "../../profession-native-ui/871_156598/typed/event-native-fixture.json"));
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(fixturePath));
            var previous = OfficialWwiseEvent.Host;
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Require(fixture.items.Length == 8, "eight original native events");
                foreach (Expected expected in fixture.items) Validate(expected);
                Debug.Log("Original profession event-field validation passed: 8 native trigger arrays, original SDK event assets, serialized emitter/playing IDs and simple Post dispatch contract. This is a field/code probe, not GUI or audio playback acceptance.");
            }
            finally
            {
                OfficialWwiseEvent.Host = previous;
                AssetDatabase.DeleteAsset("Assets/ProfessionEventFieldProbe");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
        private static void Validate(Expected expected)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(expected.path);
            Require(prefab != null, expected.path + " native prefab import");
            AkEvent component = prefab.GetComponent<AkEvent>();
            Require(component != null, expected.path + " native MonoScript binding");
            Require(component.triggerList.SequenceEqual(expected.triggers), "exact signed original triggers");
            Require(component.triggerList.Count == 1 && component.triggerList[0] == AkEvent.EnableTrigger,
                "native OnEnable trigger identity");
            Require(component.playingId == expected.playing_id && component.soundEmitterObject == prefab,
                "original serialized playing ID and self emitter");
            Require(!component.enableActionOnEvent && !component.useCallbacks && !component.useOtherObject &&
                component.Callbacks.Length == 0, "source event action/callback/emitter scope");
            OfficialWwiseEvent data = component.data;
            WwiseEventReference reference = data.WwiseObjectReference;
            Require(reference != null && reference.id == expected.event_id &&
                reference.objectName == expected.event_name && reference.guid == expected.event_guid,
                "original SDK event reference ID/name/GUID");
            Require(data.IsValid && data.Id == expected.event_id, "native reference validity and ID");
            var host = new Host(); OfficialWwiseEvent.Host = host;
            uint playing = data.Post(prefab);
            Require(host.Calls == 1 && host.Event == expected.event_id && host.Emitter == prefab &&
                playing == 417 && data.PlayingId == 417, "simple native Post order and retained playing ID");
            var empty = new OfficialWwiseEvent();
            Require(!empty.IsValid && empty.Id == 0 && empty.Post(prefab) == 0 && host.Calls == 1,
                "invalid native reference returns zero without posting");
        }
    }
}
