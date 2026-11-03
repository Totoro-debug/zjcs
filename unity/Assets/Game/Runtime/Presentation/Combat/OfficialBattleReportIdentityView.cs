using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    public static class OfficialBattleReportIdentityView
    {
        public sealed class Identity
        {
            public string Name;
            public int Profession;
            public Sprite ProfessionIcon, NpcIcon;
            public Texture PlayerPortrait;
            public bool IsNpc;
        }

        [Serializable] private sealed class Bindings
        {
            public string name, professionGo, profession, npcIcon, portrait, collection;
            public float[] portraitUv;
            public Color portraitColor;
            public bool portraitRaycastTarget;
            public State[] actorStates, photographedStates;
        }
        [Serializable] private sealed class State { public int state; public string[] objects; }
        private static Bindings bindings;

        public static void Bind(Component icon, Identity identity)
        {
            if (icon == null || identity == null) throw new ArgumentNullException(nameof(identity));
            if (identity.IsNpc ? identity.NpcIcon == null : identity.PlayerPortrait == null)
                throw new InvalidOperationException("The report actor's original portrait is unbound.");
            if (identity.Profession != 0 && identity.ProfessionIcon == null)
                throw new InvalidOperationException("The report actor's original profession icon is unbound.");
            if (bindings == null)
            {
                var source = Resources.Load<TextAsset>("Combat/BattleReport/player-identity-bindings");
                if (source == null) throw new InvalidOperationException("Original report identity bindings are absent.");
                bindings = JsonUtility.FromJson<Bindings>(source.text);
            }
            Transform Node(string path)
            {
                Transform node = icon.transform;
                if (!string.IsNullOrEmpty(path)) foreach (string index in path.Split('/')) node = node.GetChild(int.Parse(index));
                return node;
            }
            void SetState(State[] states, int value)
            {
                WorldStateGameObjectConfig.FromBindings(new WorldStateGameObjectData {
                    GoConfig = states.Select(row => new WorldStateGameObjectData.StateGameObjectKV {
                        State = row.state, GameObjects = row.objects.Select(path => Node(path).gameObject).ToList()
                    }).ToList()
                }).SetState(value);
            }
            // The existing Windows profile adapter photographs the original head
            // pose. Both player identities use that native HeadCharacterPortrait
            // branch; NPCs retain the separate original sprite branch.
            SetState(bindings.photographedStates, 0);
            SetState(bindings.actorStates, identity.IsNpc ? 1 : 0);
            var name = Node(bindings.name);
            name.GetComponent<WorldMultiLanguageText>()?.SetKey(string.Empty);
            name.GetComponent<TMP_Text>().text = identity.Name ?? string.Empty;
            Node(bindings.professionGo).gameObject.SetActive(identity.Profession != 0);
            Node(bindings.profession).GetComponent<Image>().sprite = identity.ProfessionIcon;
            Node(bindings.collection).gameObject.SetActive(false);
            if (identity.IsNpc) Node(bindings.npcIcon).GetComponent<Image>().sprite = identity.NpcIcon;
            else
            {
                var raw = Node(bindings.portrait).GetComponent<RawImage>()
                    ?? Node(bindings.portrait).gameObject.AddComponent<RawImage>();
                raw.uvRect = new Rect(bindings.portraitUv[0], bindings.portraitUv[1], bindings.portraitUv[2], bindings.portraitUv[3]);
                raw.color = bindings.portraitColor;
                raw.raycastTarget = bindings.portraitRaycastTarget;
                raw.texture = identity.PlayerPortrait;
                raw.enabled = true;
            }
        }
    }
}
