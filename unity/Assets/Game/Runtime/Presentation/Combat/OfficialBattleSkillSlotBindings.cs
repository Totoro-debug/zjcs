using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    [Serializable] public sealed class BattleSkillSlotData
    {
        public string manifest;
        public string petUnlockCondition;
        public BattleSkillListBinding skillUI;
        public BattleSkillSlotBinding[] slots;
        public BattleSkillSlotBinding standalone;
    }
    [Serializable] public sealed class BattleSkillListBinding
    {
        public string owner, rect, pet;
        public float noUnlockPetOffset;
    }
    [Serializable] public sealed class BattleSkillSlotBinding
    {
        public string prefab, owner, icon, quality, cooldown;
        public BattleSkillSlotStateBinding[] states;
    }
    [Serializable] public sealed class BattleSkillSlotStateBinding
    {
        public int state;
        public string[] objects;
    }

    public static class OfficialBattleSkillSlotBindings
    {
        [Serializable] private sealed class Manifest { public string manifest, output_sha256; }

        public static BattleSkillSlotData Load()
        {
            TextAsset text = Resources.Load<TextAsset>("Combat/battle-skill-slots");
            TextAsset source = Resources.Load<TextAsset>("Combat/battle-skill-slots-source-manifest");
            if (text == null || source == null) throw new InvalidOperationException("Official skill-slot bindings are missing.");
            Manifest manifest = JsonUtility.FromJson<Manifest>(source.text);
            string hash;
            using (SHA256 sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.text))).Replace("-", "").ToLowerInvariant();
            BattleSkillSlotData data = JsonUtility.FromJson<BattleSkillSlotData>(text.text);
            if (manifest == null || manifest.manifest != "854_156383" || manifest.output_sha256 != hash
                || data == null || data.manifest != manifest.manifest || data.slots == null || data.slots.Length != 4)
                throw new InvalidOperationException("Official skill-slot bindings or their source hash differ.");
            return data;
        }

        public static WorldGameSkillIcon[] Mount(OfficialBattleHudRenderer renderer)
        {
            BattleSkillSlotData data = Load();
            var result = new WorldGameSkillIcon[data.slots.Length];
            for (int index = 0; index < result.Length; index++)
            {
                BattleSkillSlotBinding binding = data.slots[index];
                var states = new List<WorldGameSkillIcon.StateObjects>();
                foreach (BattleSkillSlotStateBinding state in binding.states)
                {
                    var objects = new List<GameObject>();
                    foreach (string path in state.objects) objects.Add(Require(renderer, path));
                    states.Add(new WorldGameSkillIcon.StateObjects
                        { State = (WorldGameSkillIcon.State)state.state, GameObjects = objects });
                }
                result[index] = Require(renderer, binding.owner).AddComponent<WorldGameSkillIcon>();
                result[index].Initialize(index, states,
                    Require(renderer, binding.icon).GetComponent<Image>(),
                    Require(renderer, binding.quality).GetComponent<Image>(),
                    Require(renderer, binding.cooldown).GetComponent<TMP_Text>());
                result[index].SetUIState(WorldGameSkillIcon.State.None);
            }
            return result;
        }

        public static bool IsPetFunctionUnlocked(int petCount)
        {
            // FunctionType.Pet (62), HasAnyPetCondition.IsMet: player.PetManager.Count > 0.
            BattleSkillSlotData data = Load();
            if (data.petUnlockCondition != "HasAnyPetCondition")
                throw new InvalidOperationException("Unsupported official pet unlock condition.");
            return petCount > 0;
        }

        private static GameObject Require(OfficialBattleHudRenderer renderer, string path) =>
            renderer.Find(path) ?? throw new InvalidOperationException("Official skill-slot node is missing: " + path);
    }
}
