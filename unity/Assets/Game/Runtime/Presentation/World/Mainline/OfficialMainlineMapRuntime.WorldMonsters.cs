using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spine.Unity;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private const int Map11WorldMonsterEntityClassId = 5110;
        private const float Map11WorldMonsterModelScale = 0.3f;
        private readonly Dictionary<int, SkeletonAnimation> worldMonsterViews =
            new Dictionary<int, SkeletonAnimation>();
        private readonly Dictionary<int, BoneFollower> worldMonsterFollowers =
            new Dictionary<int, BoneFollower>();

        private void InitializeMap11WorldMonsters()
        {
            if (mapClassId != 11) return;

            OfficialBattleModelCatalog catalog = OfficialBattleModelCatalog.LoadDefault();
            foreach (Map10RuntimeEntity entity in mapEntities.Values
                .Where(value => value.EntityClassId == Map11WorldMonsterEntityClassId))
            {
                int stageId = ResolveEntityStageId(entity);
                if (stageId <= 0 || !rosters.TryGetValue(stageId, out Map10RuntimeRoster roster))
                    throw new InvalidDataException("Map11 world monster has no official battle roster: " + stageId);

                Map10RuntimeMonster[] units = (roster.Waves ?? Array.Empty<Map10RuntimeWave>())
                    .Where(value => value != null)
                    .SelectMany(value => value.Units ?? Array.Empty<Map10RuntimeMonster>())
                    .Where(value => value != null && value.MonsterId > 0)
                    .ToArray();
                if (units.Length == 0 || units.Any(value => value.MonsterId != units[0].MonsterId))
                    throw new InvalidDataException("Map11 world monster roster is not a single official main-monster identity: " + stageId);

                int monsterId = units[0].MonsterId;
                string modelKey = catalog.ResolveModelKey(new FightBattleUnitState { MonsterId = monsterId });
                OfficialBattleModelBinding binding = catalog.Find(modelKey);
                OfficialBattleAnimationAlias idle = binding?.animations?.FirstOrDefault(value =>
                    value != null && value.name == "idle_01");
                if (binding == null || binding.skeleton == null || idle == null)
                    throw new InvalidDataException("Map11 world monster has no official idle skeleton binding: " + monsterId);

                GameObject entityObject = entityView.FindEntityObject(entity.PlacementId);
                if (entityObject == null)
                    throw new InvalidDataException("Map11 world monster entity view is missing: " + entity.PlacementId);

                GameObject viewRoot = new GameObject("5011_view");
                viewRoot.transform.SetParent(entityObject.transform, false);
                GameObject modelRoot = new GameObject("Model");
                modelRoot.transform.SetParent(viewRoot.transform, false);
                // 5110.prefab places the ModelSlot and Shadow siblings under this offset.
                modelRoot.transform.localPosition = new Vector3(0f, -0.5f, -0.5f);
                GameObject modelSlot = new GameObject("ModelSlot");
                modelSlot.transform.SetParent(modelRoot.transform, false);
                GameObject shadowSlot = new GameObject("Shadow");
                shadowSlot.transform.SetParent(modelRoot.transform, false);

                var model = new GameObject("MonsterModel_" + monsterId);
                model.transform.SetParent(modelSlot.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = new Vector3(Map11WorldMonsterModelScale,
                    Map11WorldMonsterModelScale, 1f);

                SkeletonAnimation skeleton = model.AddComponent<SkeletonAnimation>();
                skeleton.skeletonDataAsset = binding.skeleton;
                skeleton.initialSkinName = binding.skin;
                skeleton.Initialize(true);
                if (skeleton.Skeleton == null || skeleton.AnimationState == null)
                    throw new InvalidDataException("Map11 world monster skeleton failed to initialize: " + monsterId);
                skeleton.AnimationState.SetAnimation(0, idle.spineName, true);
                if (skeleton.Skeleton.FindBone("shadow") != null)
                    CreateMap11MonsterShadow(entity.PlacementId, skeleton, shadowSlot.transform);
                worldMonsterViews.Add(entity.PlacementId, skeleton);
            }

            if (worldMonsterViews.Count != 6)
                throw new InvalidDataException("Map11 official dynamic world monster bindings are incomplete.");
        }

        private void CreateMap11MonsterShadow(int placementId, SkeletonAnimation skeleton, Transform shadowSlot)
        {
            Sprite source = Resources.Load<Sprite>("Map11/Textures/MonsterShadow");
            if (source == null)
                throw new InvalidDataException("Official Map11 MonsterShadow sprite is missing.");

            var shadow = new GameObject("MonsterShadow");
            shadow.SetActive(false);
            shadow.transform.SetParent(shadowSlot, false);
            var follower = shadow.AddComponent<BoneFollower>();
            follower.initializeOnAwake = false;
            follower.skeletonRenderer = skeleton;
            follower.boneName = "shadow";
            // These settings match the serialized MonsterShadow follower fields.
            follower.followXYPosition = true;
            follower.followZPosition = true;
            follower.followAttachmentZSpacing = false;
            follower.followBoneRotation = true;
            follower.followLocalScale = true;
            follower.followSkeletonFlip = false;

            var spriteObject = new GameObject("Sprite", typeof(SpriteRenderer));
            spriteObject.transform.SetParent(shadow.transform, false);
            spriteObject.transform.localScale = new Vector3(Map11WorldMonsterModelScale,
                Map11WorldMonsterModelScale, 1f);
            SpriteRenderer renderer = spriteObject.GetComponent<SpriteRenderer>();
            renderer.sprite = source;
            renderer.sortingOrder = -10;

            shadow.SetActive(true);
            follower.Initialize();
            worldMonsterFollowers.Add(placementId, follower);
        }
    }
}
