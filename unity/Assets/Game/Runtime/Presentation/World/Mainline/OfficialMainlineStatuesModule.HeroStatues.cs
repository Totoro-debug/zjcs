using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.World;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private sealed partial class OfficialMainlineStatuesModule
        {
            private string HeroReceipt(Map10RuntimeEntity entity) => "hero-activation:" + host.mapClassId + ":" + entity.PlacementId;
            public bool HeroActivated(Map10RuntimeEntity entity) => host.progressionSave.Save.Player.AppliedInventoryEventIds.Contains(HeroReceipt(entity));

            public void RecoverHeroStates()
            {
                foreach (Map10RuntimeEntity entity in host.mapEntities.Values.Where(value => value.TypeId == 223))
                {
                    entity.State = host.regions.IsPublicRegion(entity.Region) ? 2 : HeroActivated(entity) ? 1 : 0;
                    if (HeroActivated(entity) && !host.localSave.completedEntityPlacements.Contains(entity.PlacementId))
                        host.localSave.completedEntityPlacements = host.localSave.completedEntityPlacements.Concat(new[] { entity.PlacementId }).ToArray();
                }
            }

            private Map11RuntimeWitchResult[] HeroEffects(Map10RuntimeEntity entity, Map11RuntimeStageResultRef[] references)
            {
                OfficialMainlineHero hero = host.Interactions.heroes.Single(value => value.EntityClassId == entity.EntityClassId);
                int level = host.Interactions.regions.Single(value => value.MapClassId == host.mapClassId && value.Region == entity.Region).Level;
                return references.Select(reference =>
                {
                    Map11RuntimeWitchResult source = hero.Effects.Single(value => value.ComponentType == reference.ComponentType
                        && value.ComponentIndex == reference.ComponentIndex);
                    return new Map11RuntimeWitchResult
                    {
                        ComponentType = source.ComponentType, ComponentIndex = source.ComponentIndex,
                        HpPercentChange = source.HpPercentChange, StatusEffects = source.StatusEffects,
                        AwardItems = Array.Empty<Map10RuntimeAwardItem>(),
                        ExtraProps = (source.ExtraProps ?? Array.Empty<OfficialMainlineExtraProp>()).Select(prop =>
                            new OfficialMainlineExtraProp
                            {
                                PropId = prop.PropId, PropType = prop.PropType, Level = prop.Level + level,
                                Value = host.progression.Catalog.GetLevelProps(prop.PropId, prop.Level + level)[prop.PropType]
                            }).ToArray()
                    };
                }).ToArray();
            }

            private Map11RuntimeWitchResult[] HeroActivationEffects(Map10RuntimeEntity entity)
            {
                OfficialWorldStageRule stage = JsonUtility.FromJson<OfficialWorldStageRule>(host.entityTemplates[entity.EntityClassId]
                    .Components.Single(value => value.Type == "WorldStageComponent").Json);
                var touch = stage.StateInfos[0].SubStateList[0];
                if (!stage.AutoNextState || stage.DestroyOnEnd || touch.Behaviour.CompType != 2
                    || touch.Conditions.Count != 0 || touch.Results.Any(value => value.CompType != 238 && value.CompType != 212))
                    throw new InvalidDataException("The official hero activation stage differs from its binding.");
                return HeroEffects(entity, touch.Results.Select(value => new Map11RuntimeStageResultRef
                    { ComponentType = value.CompType, ComponentIndex = value.CompIndex }).ToArray());
            }

            public void InteractHero(Map10RuntimeEntity entity)
            {
                host.progressionSave.Reload();
                if (host.regions.IsPublicRegion(entity.Region)) { ShowHeroVisit(entity); return; }
                if (HeroActivated(entity)) return; // Native private state 1 is ScriptWait.
                OfficialMap11WitchWindow window = OpenWitchWindow("WorldTouchDialogWithPreview", entity.EntityClassId);
                InitHeroTitle(window, entity);
                var fields = window.Source("StageUIWithPreview").fields;
                window.Find(fields.mDialogContent).gameObject.SetActive(true);
                window.SetText(fields.myBtnText, window.Text("ui_witch_visit_btn_text"));
                if (!string.IsNullOrEmpty(fields.myFuncDescText))
                    window.SetText(fields.myFuncDescText, window.TextOrKey("entity_" + entity.EntityClassId + "_funcdesc"));
                window.BindButton(fields.myTouchBtn, () => ShowHeroActivationPreview(entity));
                witchCloseRequested = CloseWitchWindow;
                window.Show();
            }

            private void InitHeroTitle(OfficialMap11WitchWindow window, Map10RuntimeEntity entity)
            {
                var stage = window.Source("StageDialog").fields;
                window.SetText(stage.myStageName, window.Text("entity_" + entity.EntityClassId + "_name"));
                window.SetText(stage.myStageDesc, window.Text("entity_" + entity.EntityClassId + "_desc"));
                if (!string.IsNullOrEmpty(stage.myStageIcon)) window.Find(stage.myStageIcon).gameObject.SetActive(false);
                if (!string.IsNullOrEmpty(stage.myBgIcon)) window.Find(stage.myBgIcon).GetComponent<UnityEngine.UI.Image>().sprite =
                    Resources.Load<OfficialMap11UiAssets>(host.NativeUiResource("DynamicItems")).mapBackground;
                window.EntityIcon();
            }

            private void ShowHeroActivationPreview(Map10RuntimeEntity entity)
            {
                Map11RuntimeWitchResult[] effects = HeroActivationEffects(entity);
                OfficialMap11WitchWindow preview = OpenWitchWindow("WorldStageInteractionPreviewDialogHeroStatue", entity.EntityClassId);
                void Finish()
                {
                    RequireWitchResult(host.progressionSave.ApplyHeroStatueActivation(host.mapClassId, entity.PlacementId, HeroReceipt(entity),
                        effects.SelectMany(value => value.ExtraProps).Select(prop => new OfflineExtraProperty
                            { PropId = prop.PropId, PropType = prop.PropType, Level = prop.Level, Count = 1 }).ToArray(),
                        effects.Sum(value => value.HpPercentChange)));
                    CloseWitchWindow();
                    RecoverHeroStates();
                    host.regions.RefreshRegionProgress();
                    host.RefreshEntityActivity();
                    host.SaveLocalProgress();
                }
                preview.Preview("entity_" + entity.EntityClassId + "_entitytouchtitle",
                    "entity_" + entity.EntityClassId + "_entitytouchdesc", effects, Finish);
                witchCloseRequested = Finish;
                preview.Show();
            }

            private void ShowHeroVisit(Map10RuntimeEntity entity)
            {
                OfficialMap11WitchWindow window = OpenWitchWindow("WorldTouchDialogPeriodicityInteraction", entity.EntityClassId);
                InitHeroTitle(window, entity);
                var source = window.Source("PeriodicityInteractionStageUI");
                bool available = !host.progressionSave.HasVisitedStatueOn(DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                window.State(source, available ? 1 : 0);
                window.SetText(source.fields.mPeriodicityInteractionTips, window.Text("map_periodicity_interaction_tips_1"));
                window.BindButton(source.fields.mInteractionBtn, () => VisitHero(entity));
                witchCloseRequested = CloseWitchWindow;
                window.Show();
            }

            private void VisitHero(Map10RuntimeEntity entity)
            {
                string date = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                host.progressionSave.Reload();
                if (!host.regions.IsPublicRegion(entity.Region) || host.progressionSave.HasVisitedStatueOn(date)) { ShowHeroVisit(entity); return; }
                var awards = host.Interactions.heroes.Single(value => value.EntityClassId == entity.EntityClassId).PeriodicAwards;
                int weight = UnityEngine.Random.Range(0, awards.Sum(value => value.Weight));
                var chosen = awards.First(value => (weight -= value.Weight) < 0);
                var effects = HeroEffects(entity, chosen.StageResults);
                RequireWitchResult(host.progressionSave.ApplyStatueVisit(date, "hero", host.mapClassId, entity.PlacementId,
                    "statue-visit:" + date, effects.Sum(value => value.HpPercentChange), WitchStatuses(effects), WitchAwards(effects),
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
                OpenWitchWindow("WorldStageInteractionPreparePlayableDialog", entity.EntityClassId).Show();
                witchCloseRequested = null;
                host.StartCoroutine(ShowHeroVisitResult(entity, chosen, effects));
            }

            private IEnumerator ShowHeroVisitResult(Map10RuntimeEntity entity, Map11RuntimePeriodicityAward chosen, Map11RuntimeWitchResult[] effects)
            {
                if (chosen.ActionTime > 0) yield return new WaitForSeconds(chosen.ActionTime);
                if (host.closed || witchDialog == null) yield break;
                var preview = OpenWitchWindow("WorldStageInteractionPreviewDialogHeroStatue", entity.EntityClassId);
                preview.Preview("map_periodicity_random_award_interaction_title_" + chosen.PeriodicityId,
                    "map_periodicity_random_award_interaction_desc_" + chosen.PeriodicityId, effects, CloseWitchWindow);
                witchCloseRequested = CloseWitchWindow;
                preview.Show();
            }
        }
    }
}
