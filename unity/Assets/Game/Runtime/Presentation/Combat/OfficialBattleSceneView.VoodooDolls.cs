using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSceneView
    {
        [Serializable] private sealed class VoodooLinkCatalog
        {
            public string manifest;
            public VoodooLinkGraph[] graphs;
        }
        [Serializable] private sealed class VoodooLinkGraph
        {
            public string id, resource;
        }
        private readonly Dictionary<int, MyPlayable> voodooLinks = new Dictionary<int, MyPlayable>();

        private void RefreshVoodooLinks()
        {
            if (nativePlayableScope == null) return;
            foreach (var entry in units)
            {
                var state = entry.Value.State;
                if (state == null || !state.IsVoodooDoll || voodooLinks.ContainsKey(state.Id)) continue;
                Transform source = Body(state.Id);
                if (!source) continue;
                var assets = BattleNativeMonsterAssets.Locate(state.MonsterTemplateId > 0
                    ? state.MonsterTemplateId : state.MonsterId);
                var text = Resources.Load<TextAsset>("Combat/sage-voodoo-doll-current-link-graphs");
                var catalog = text ? JsonUtility.FromJson<VoodooLinkCatalog>(text.text) : null;
                if (assets == null || catalog?.manifest != assets.SourceManifest || catalog.graphs?.Length != 1
                    || catalog.graphs[0]?.id != "link")
                    throw new InvalidOperationException("Original substitute link binding is missing.");
                var prefab = Resources.Load<GameObject>(catalog.graphs[0].resource);
                if (!prefab) throw new InvalidOperationException("Original substitute link playable is missing.");
                // MyFightRoleVoodooDoll.OnStart: source is the substitute; a
                // missing linked entity is passed to the original manager as null.
                Transform target = Body(state.VoodooDoll.TargetId);
                voodooLinks.Add(state.Id, nativePlayableScope.Play(prefab, source.gameObject,
                    target ? target.gameObject : null));
            }
        }

        private void StopVoodooLink(int unitId)
        {
            if (!voodooLinks.TryGetValue(unitId, out var playable)) return;
            // MyFightRoleVoodooDoll.OnEnd stops the graph before its owner ends.
            nativePlayableScope.Stop(playable);
            voodooLinks.Remove(unitId);
        }

        private void ReleaseVoodooLinks()
        {
            foreach (int id in new List<int>(voodooLinks.Keys)) StopVoodooLink(id);
        }
    }
}
