using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Zjcs.Official.StoryPlayableManager;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSceneView : IBattleCharacterPartVisibilityHost
    {
        [Serializable] private sealed class VisibilityCatalog { public string manifest; public VisibilityRole[] roles; }
        [Serializable] private sealed class VisibilityRole { public string prefab, root; public string[] parts; }
        private sealed class VisibilityBinding { public Transform entity; public VisibilityRole role; public GameObject[] targets; }
        private static Dictionary<string, VisibilityRole> visibilityRoles;
        private readonly Dictionary<int, VisibilityBinding> visibilityBindings = new Dictionary<int, VisibilityBinding>();

        void IBattleCharacterPartVisibilityHost.SetCharacterPartVisibility(GameObject source, bool visible)
        {
            if (source == null || latestCheckpoint?.Units == null) return;
            foreach (var unit in latestCheckpoint.Units)
            {
                Transform entity = unit == null ? null : Body(unit.Id);
                if (entity == null || entity.gameObject != source) continue;
                var role = FindVisibilityRole(OfficialBattleSceneBindings.EntityId(unit));
                if (role?.parts == null || role.parts.Length == 0) return;
                if (visibilityBindings.TryGetValue(unit.Id, out var cached) && cached.entity == entity && cached.role == role)
                {
                    // Retain the original object references, including Unity's
                    // destroyed-object null semantics during death/teardown.
                    foreach (var target in cached.targets) if (target != null) target.SetActive(visible);
                    return;
                }
                // Resolve the serialized references before changing them. An absent
                // runtime binding is a migration error, not an original null entry.
                var targets = new GameObject[role.parts.Length];
                for (int i = 0; i < targets.Length; i++)
                {
                    string path = role.parts[i];
                    if (path == null) continue;
                    string prefix = role.prefab + "/";
                    if (!path.StartsWith(prefix, StringComparison.Ordinal))
                        throw new InvalidOperationException("Original character part path differs: " + path);
                    string relative = path.Substring(prefix.Length);
                    Transform target;
                    if (relative == role.root + "/ui")
                    {
                        attachedUnitHuds.TryGetValue(unit.Id, out var hud);
                        if (hud == null && units.TryGetValue(unit.Id, out var view)) hud = view.Hud;
                        target = hud?.Root;
                    }
                    else if (relative == role.root + "/body")
                    {
                        // The earlier model extraction omitted the identity view
                        // wrapper. Its original body subtree already exists.
                        target = entity.Find(relative) ?? entity.Find("body");
                    }
                    else target = entity.Find(relative);
                    if (target == null)
                        throw new InvalidOperationException("Original character part is not bound for battle unit "
                            + unit.Id + ": " + path);
                    targets[i] = target.gameObject;
                }
                visibilityBindings[unit.Id] = new VisibilityBinding { entity = entity, role = role, targets = targets };
                // ECFightRoleCharacterView.SetVisibilityPartsActive uses the
                // serialized order and SetActive, including on stop/reuse.
                foreach (GameObject target in targets) if (target != null) target.SetActive(visible);
                return;
            }
        }

        private static VisibilityRole FindVisibilityRole(int entityId)
        {
            if (visibilityRoles == null)
            {
                TextAsset source = Resources.Load<TextAsset>("Combat/character-visibility-bindings");
                if (source == null) throw new InvalidOperationException("Original character visibility bindings are missing.");
                var catalog = JsonUtility.FromJson<VisibilityCatalog>(source.text);
                if (catalog?.manifest != "884_156692" || catalog.roles == null)
                    throw new InvalidOperationException("Original character visibility source differs.");
                var next = new Dictionary<string, VisibilityRole>(StringComparer.Ordinal);
                foreach (var role in catalog.roles)
                {
                    if (role == null || string.IsNullOrEmpty(role.prefab) || string.IsNullOrEmpty(role.root))
                        throw new InvalidOperationException("Original character visibility role is invalid.");
                    next.Add(role.prefab, role);
                }
                visibilityRoles = next;
            }
            return visibilityRoles.TryGetValue(entityId.ToString(CultureInfo.InvariantCulture), out var result) ? result : null;
        }
    }
}
