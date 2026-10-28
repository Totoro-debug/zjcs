using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11UiTools
    {
        private static void BindSteleAuxiliary(string key, OfficialMap11UiData data, Func<string, Transform> require)
        {
            WorldMultiLanguageText Text(string path)
            {
                var node = require(path).gameObject;
                var text = node.GetComponent<WorldMultiLanguageText>() ?? node.AddComponent<WorldMultiLanguageText>();
                text.myKey = data.components.Single(c => c.panel == key && c.path == path && c.type == "MultiLanguageText").fields.myKey;
                return text;
            }
            foreach (var source in data.components.Where(c => c.panel == key))
            {
                var node = require(source.path).gameObject;
                var fields = source.fields;
                if (source.type == "MaintainGlobalScaleUI")
                    node.AddComponent<WorldMaintainGlobalScaleUI>().SetFixedScale(fields.mFixedGlobalScale);
                if (source.type == "CountRedDotImage") node.AddComponent<WorldCountRedDotImage>().mCountLanguage = Text(fields.mCountLanguage);
                if (source.type == "PreviewTool") node.AddComponent<WorldPreviewTool>();
                if (source.type == "UIBloomGroup")
                {
                    var bloom = node.AddComponent<WorldUIBloomGroup>();
                    bloom.Masks = fields.Masks.Select(path => require(path).GetComponent<Image>()).ToArray();
                    bloom.IsOverrideMask = fields.IsOverrideMask != 0;
                    bloom.OccluderCount = fields.OccluderCount;
                }
            }
        }

        private static void BindBattleStelePrefab(GameObject node, OfficialMap11UiComponent source,
            Func<string, Transform> require, Func<string, WorldMultiLanguageText> text)
        {
            var fields = source.fields;
            var view = node.AddComponent<WorldBattleSteleStageUI>();
            view.mRewardSlot = require(fields.mRewardSlot);
            view.mTeamBtn = require(fields.mTeamBtn).GetComponent<Button>();
            view.mBattleBtn = require(fields.mBattleBtn).GetComponent<Button>();
            view.mTitleText = text(fields.mTitleText);
            view.mRecommendCombatRatingText = text(fields.mRecommendCombatRatingText);
            view.mCurrentCombatRatingText = text(fields.mCurrentCombatRatingText);
            view.mRankSuppressionText = text(fields.mRankSuppressionText);
            view.mTeamInviteText = text(fields.mTeamInviteText);
            // JsonUtility retains signed Int64 managed IDs. Do not pass this
            // graph through a double-based JSON serializer: these IDs exceed 2^53.
            WorldStateSwitcher<bool> Switcher(OfficialMap11StateSwitcher binding)
            {
                var configs = new List<IWorldStateConfig>();
                foreach (var id in binding.mStateConfigs)
                {
                    var reference = fields.references.RefIds.Single(r => r.rid == id.rid);
                    switch (reference.type.@class)
                    {
                        case "StateGameObjectConfig":
                            configs.Add(WorldStateGameObjectConfig.FromBindings(new WorldStateGameObjectData
                            {
                                GoConfig = reference.data.mStateGameObjectData.GoConfig.Select(row =>
                                    new WorldStateGameObjectData.StateGameObjectKV
                                    {
                                        State = row.State.mValue,
                                        GameObjects = row.GameObjects.Select(path => require(path).gameObject).ToList()
                                    }).ToList()
                            }));
                            break;
                        case "StateMultiLanguagesConfig":
                            configs.Add(WorldStateMultiLanguagesConfig.FromBindings(
                                reference.data.mStateMultiLanguageTextDatas.Select(row => new WorldStateMultiLanguageData
                                {
                                    Text = text(row.Text),
                                    Keys = WorldStateKVMatch<string>.FromBindings(row.Keys.mSerializeBuffer.Select(entry =>
                                        new WorldMonoKeyValuePair<WorldStateConfigValue, string>
                                        { Key = entry.Key.mValue, Value = entry.Value }))
                                }).ToArray()));
                            break;
                        default:
                            throw new InvalidDataException("Unconverted stele state config: " + reference.type.@class);
                    }
                }
                return WorldStateSwitcher<bool>.FromBindings(node, configs);
            }
            view.mTeamStateSwitcher = Switcher(fields.mTeamStateSwitcher);
            view.mRankSuppressionSwitcher = Switcher(fields.mRankSuppressionSwitcher);
        }
    }
}
