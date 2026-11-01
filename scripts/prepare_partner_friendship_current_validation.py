"""Exercise the selected local provider inside the complete original dialog."""
import json
import shutil
from pathlib import Path

from official_apk import ROOT
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    project = work / 'full-unity'
    if not (project / 'ProjectSettings/ProjectVersion.txt').is_file():
        raise ValueError('Reuse the owned full Unity validation cache')
    for relative in ('Assets/Game/Runtime/Offline/PartnersUI',
                     'Assets/Game/Runtime/Official/PartnersUI/FriendshipDialog'):
        for source in (ROOT / 'unity' / relative).rglob('*'):
            if source.is_file() and source.suffix in ('.cs', '.meta', '.asmdef'):
                target = project / source.relative_to(ROOT / 'unity')
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, target)
    for name in ('partner-property-ui.json', 'partner-property-texts.json'):
        for suffix in ('', '.meta'):
            relative = Path('Assets/Official/Partners') / (name + suffix)
            shutil.copy2(ROOT / 'unity' / relative, project / relative)
    for suffix in ('', '.meta'):
        relative = Path('Assets/Game/Resources/Partners/OfficialPartnerFriendshipAssetCatalog.asset' + suffix)
        shutil.copy2(ROOT / 'unity' / relative, project / relative)
    editor = project / 'Assets/PartnerCurrentValidation/Editor'
    editor.mkdir(parents=True, exist_ok=True)
    refs = ['Zjcs.Offline.PartnersUI', 'Zjcs.Offline.Progression', 'Zjcs.Offline.ProfessionUI',
        'Zjcs.Official.PartnerFriendshipDialogUI', 'Zjcs.Official.PartnerGiftUI', 'Zjcs.Official.ProfessionUI',
        'Zjcs.Official.WorldUI', 'Zjcs.Official.World.NativeLabelUI', 'Zjcs.Official.World.Map13MiniMapWindows',
        'Zjcs.Official.StoryPlayables', 'UniTask', 'Unity.TextMeshPro', 'Unity.ugui',
        'Unity.RenderPipelines.Universal.Runtime', 'Unity.RenderPipelines.Core.Runtime']
    (editor / 'PartnerCurrentValidation.asmdef').write_text(json.dumps(dict(name='PartnerCurrentValidation',
        references=refs, includePlatforms=['Editor']), indent=2), 'utf8')
    # Reuse the already verified native window/input/font fixture. Its business
    # test doubles are replaced by the real selected local provider below.
    text = (ROOT / 'scripts/fixtures/partner_friendship_dialog_validation.cs').read_text('utf8')

    def replace(old, new):
        nonlocal text
        if text.count(old) != 1:
            raise ValueError('Review changed original-window fixture seam: ' + old[:100])
        text = text.replace(old, new)

    text = text.replace('PartnerFriendshipDialogValidation', 'PartnerFriendshipCurrentValidation')
    text = text.replace('PartnerFriendshipDialogRunner', 'PartnerFriendshipCurrentRunner')
    text = text.replace('"PartnerFriendshipDialog', '"PartnerFriendshipCurrent')
    replace('using Zjcs.Official.PartnersUI;', 'using Zjcs.Official.PartnersUI;\nusing Zjcs.Offline.PartnersUI;\nusing Zjcs.Offline.Progression;\nusing Zjcs.Offline.ProfessionUI;')
    replace('EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);',
        'Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");\n        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);')
    replace('public Dictionary<string, string> Texts;', 'public PartnerGiftPreferenceProbe Gifts;\n        public Dictionary<string, string> Texts;')
    replace('public int SubRank => 2;', 'public int Rank = 5;\n        public int SubRank => Rank;')
    replace('AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/PartnerGift/partner-friendship-dialog-texts.json")',
        'OfficialPartnerFriendshipAssetCatalog.LoadDefault().DialogTexts')
    replace('ProfessionPropertyBindings.Current = context; LanguageManager.Initialize(context); OfficialWwiseEvent.Host = context;', '''
        var localAssets = OfficialPartnerFriendshipAssetCatalog.LoadDefault();
        var propertyTexts = JsonUtility.FromJson<TextCatalog>(localAssets.PropertyTexts.text);
        foreach (var row in propertyTexts.texts) context.Texts[row.key] = row.text;
        context.Gifts = new PartnerGiftPreferenceProbe();
        context.Texts["ui_ItemIcon_count_key"] = context.Gifts.CountFormat;
        var bindings = new OfflineProfessionPropertyBindings(localAssets.PropertyConfiguration,
            context.Gifts.ImageResources, context, (owner, path, name, desc, control) => { context.Popups++; },
            (owner, rank, desc, effect, first, second) => { throw new InvalidOperationException("No blessing in current partner rows"); });
        var selected = OfficialPartnerAssetCatalog.LoadDefault();
        var properties = new OfflinePartnerFriendshipProperties(selected, bindings, () => context.Limited);
        var current = new OfflinePartnerFriendshipDialogServices(selected, OfficialPartnerGiftAssetCatalog.LoadDefault(), properties,
            () => context.Rank, context.Gifts.Borrow, context.Gifts.Recycle, key => new CultureTextArg(key));
        ProfessionPropertyBindings.Current = bindings; LanguageManager.Initialize(context); OfficialWwiseEvent.Host = context;
        Require(properties.TravelNpcPropTypeList.Select(value => (int)value).SequenceEqual(new[] { 6010, 6011, 6012 }), "selected travel property settings");
        Require(current.CompareRewardItems(50, 1) < 0 && current.CompareRewardItems(1, 1130) < 0, "original exp/currency reward priorities");''')
    replace('"Assets/PartnerGift/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic_0.asset"',
        '"Assets/Official/Partners/UI/Card/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic_0.asset"')
    replace('"Assets/Editor/FZY4K.ttf"', '"Assets/Official/Partners/UI/Card/Fonts/FZY4K.TTF"')
    replace('dialog.Configure(context);', 'dialog.Configure(current);')
    replace('''var npc = new PartnerFriendshipDialogNpc { Properties = new PartnerFriendshipNpcInfo { Id = 8401, LevelPropId = 77 },
            GiftPreferenceDict = new Dictionary<int, double>(), Name = "原生窗口验证伙伴", AssistFriendshipLevel = 3 };''',
        'var npc = current.GetNpc(8401);')
    replace('var visibleLevels = Enumerable.Range(2, 19).Where(level => level != 4).ToArray();',
        'var visibleLevels = current.GetFriendshipLevels(8401).Where(row => row.Key != 1 && row.Value.SubRankLimit <= current.SubRank + 1).Select(row => row.Key).ToArray();')
    replace('rows.Count == visibleLevels.Length && context.RequestedLevels.SequenceEqual(visibleLevels)',
        'rows.Count == visibleLevels.Length && rows.Select(row => Get<int>(row, "mLevel")).SequenceEqual(visibleLevels)')
    replace('rows.All(row => Get<WorldMultiLanguageText>(row, "mTravelPropText").gameObject.activeInHierarchy)',
        'rows.All(row => Get<WorldMultiLanguageText>(row, "mTravelPropText").gameObject.activeInHierarchy == '
        'current.GetFriendshipLevelPropDiff(npc.Properties, Get<int>(row, "mLevel") - 1, Get<int>(row, "mLevel"))'
        '.Any(prop => prop.Value > 0 && current.TravelNpcPropTypeList.Contains(prop.Key)))')
    replace('Require(!Get<GameObject>(dialog, "mGiftRuleGo").activeSelf, "original empty preference rule hides");',
        'Require(Get<GameObject>(dialog, "mGiftRuleGo").activeInHierarchy, "selected populated preference rule");\n        context.Gifts.AssertPopulated();')
    replace('Require(Get<GameObject>(dialog, "mAssistTipGo").activeInHierarchy, "original friendship condition is visible");',
        'Require(!Get<GameObject>(dialog, "mAssistTipGo").activeSelf && npc.AssistFriendshipLevel == null, "current CanMeet condition hides friendship assist tip");')
    replace('Require(Get<WorldMultiLanguageText>(dialog, "mAssistConditionText").Text.Contains("3"), "original assist level formats");',
        'Require(!npc.IsStarGodNpc && npc.Properties.LevelPropId == 5015, "selected normal partner projection");')
    replace('Capture("friendship-dialog-open.png");',
        'Capture("friendship-current-open.png");\n        context.Gifts.ClickFirst(events, cameraUi);')
    replace('context.Limited = true; context.RequestedLevels.Clear(); npc.AssistFriendshipLevel = null; npc.IsStarGodNpc = true;',
        'context.Gifts.AssertRecycled(); context.Limited = true; context.Rank = 2;\n        visibleLevels = current.GetFriendshipLevels(8401).Where(row => row.Key != 1 && row.Value.SubRankLimit <= current.SubRank + 1).Select(row => row.Key).ToArray();')
    replace('rows.Select(row => row.GetInstanceID()).SequenceEqual(originalRowIds)',
        'rows.Select(row => row.GetInstanceID()).SequenceEqual(originalRowIds.Take(visibleLevels.Length))')
    replace('context.RequestedLevels.SequenceEqual(visibleLevels.SelectMany(level => new[] { level, level }))',
        'rows.Select(row => Get<int>(row, "mLevel")).SequenceEqual(visibleLevels)')
    replace('Capture("friendship-dialog-star-god-limited.png");', 'Capture("friendship-current-limited.png");')
    replace('Object.Destroy(host); yield return null;',
        'context.Gifts.AssertRecycled(); context.Gifts.Dispose(); Object.Destroy(host); yield return null;')
    replace('AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PartnerGift/" + name + ".prefab")',
        'OfficialPartnerFriendshipAssetCatalog.LoadDefault().DialogPrefab')
    replace('PARTNER_FRIENDSHIP_DIALOG_PASSED: complete native dialog animation, assist condition, rank gates, travel, pooled lifecycle and simulated scroll; local production provider and populated gift icons not covered.',
        'PARTNER_FRIENDSHIP_CURRENT_PASSED: selected local provider, original property definitions/texts, reward sorting, real native dialog/preferences, rank/travel gates, pooled lifecycle and simulated scroll/icon/mask input; production window routing and popup contents remain outside this fixture.')
    (editor / 'PartnerFriendshipCurrentValidation.cs').write_text(text, 'utf8', newline='\n')
    probe = (ROOT / 'scripts/fixtures/partner_gift_preferences_validation.cs').read_text('utf8')
    probe = probe.replace('public string CountFormat => fixture.countFormat;',
        'public string CountFormat => fixture.countFormat;\n    public IProfessionImageResourceLoader ImageResources => loader;')
    (editor / 'PartnerGiftPreferenceProbe.cs').write_text(probe, 'utf8', newline='\n')
    native = json.loads((ROOT / 'assets/reference/partner-gift-preferences-source-manifest.json').read_bytes())
    rules = json.loads((ROOT / 'unity/Assets/Official/Partners/partner-rules.json').read_bytes())['tables']
    ids = {row['item_id'] for row in native['dynamic_images']['sprites']}
    npc, = [row for row in rules['npc'] if row['Id'] == 8401]
    payload = dict(items=[row for row in rules['item'] if row['ClassId'] in ids], images=native['dynamic_images']['sprites'],
        config=native['icon_config'], attachment=native['attachment_config'], countFormat=native['dynamic_images']['texts'][0]['text'],
        roots=[dict(name=name, pointer=reference) for name, reference in native['roots'].items()],
        preferences=[dict(id=int(key), multiplier=value) for key, value in npc['GiftPreferenceDict'].items()])
    fixture = project / 'Assets/Editor/gift-preferences-fixture.json'
    fixture.write_text(json.dumps(payload, ensure_ascii=False), 'utf8')
    print('Prepared current local provider/native dialog validation in the owned full-project cache.')


if __name__ == '__main__':
    main()
