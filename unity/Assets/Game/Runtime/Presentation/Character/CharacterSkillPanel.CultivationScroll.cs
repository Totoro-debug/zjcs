using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Skills;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        private CultivationScrollSource cultivationScrollSource;

        private sealed class CultivationScrollSource : LoopScrollPrefabSource, LoopScrollDataSource
        {
            internal readonly Transform Pool;
            internal readonly Dictionary<Transform, Dictionary<string, RectTransform>> Nodes = new Dictionary<Transform, Dictionary<string, RectTransform>>();
            internal readonly Dictionary<Transform, int> Ids = new Dictionary<Transform, int>();
            internal readonly Dictionary<Transform, int> SkillIds = new Dictionary<Transform, int>();
            private readonly Stack<GameObject> available = new Stack<GameObject>();
            internal Func<GameObject> Create;
            internal Action<Transform, int> Bind;
            internal CultivationScrollSource(Transform pool) { Pool = pool; }
            public Transform GetPoolRoot() => Pool;
            public GameObject GetObject(int index) => available.Count > 0 ? available.Pop() : Create();
            public void ReturnObject(Transform value, int index)
            {
                value.gameObject.SetActive(false);
                value.SetParent(Pool, false);
                available.Push(value.gameObject);
            }
            public void ProvideData(Transform value, int index) => Bind(value, index);
        }

        private void BindOriginalCultivationSelection(int skillId, Dictionary<string, RectTransform> windowNodes)
        {
            bool switching = promoteSkillId != skillId;
            promoteSkillId = skillId;
            var root = AuxObject(AuxObject(skillUiTemplates["skill_promote"])["root"]);
            var binding = FindOriginalComponent(root, "SkillPromotePanel");
            SetAuxiliaryText(windowNodes[Convert.ToString(binding["SkillNameText"])], SkillName(skillId));
            var icon = windowNodes[Convert.ToString(binding["ItemIconSlot"])].GetChild(0) as RectTransform;
            string path = windowNodes.First(pair => pair.Value == icon).Key;
            var iconNodes = windowNodes.Where(pair => pair.Key == path || pair.Key.StartsWith(path + "/", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            BindOriginalSkillIcon(skillId, icon, iconNodes, iconLayout: FindLayoutNode(root, path));
            FindDeep(icon, "SkillStarInfo")?.gameObject.SetActive(false);
            int rank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId);
            BindOriginalPromoteProperties(skillId, rank, OriginalMinimumSkillLevel(), windowNodes);
            var content = FindOriginalComponent(root, "SkillPromotePanelRankUpContent");
            var cost = FindOriginalComponent(FindLayoutNode(root, Convert.ToString(content["UseItemContent"])), "PlayerUseItemContent");
            promoteButtons[Convert.ToString(cost["UseItemBtn"])].onClick.RemoveAllListeners();
            BindOriginalCultivationRank(root, skillId, rank, windowNodes, promoteButtons);
            foreach (var row in cultivationScrollSource.Ids)
            {
                var selected = FindDeep(row.Key, "SelAnimGo");
                if (selected != null) selected.gameObject.SetActive(cultivationScrollSource.SkillIds[row.Key] == skillId);
            }
            if (switching) PlayOfficialLayoutAnimations("skill_promote", AuxObject(skillUiTemplates["skill_promote"]), windowNodes, "SkillPromotePanel_Switch");
        }
    }
}
