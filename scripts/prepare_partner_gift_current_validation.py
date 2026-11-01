"""Run the native gift controller against current atomic local player state."""
import json
import shutil
from pathlib import Path

from official_apk import ROOT
from validation_workspace import validation_directory
from prepare_partner_friendship_current_validation import main as prepare_friendship


def main():
    prepare_friendship()
    work = validation_directory()
    project = work / 'full-unity'
    editor = project / 'Assets/PartnerCurrentValidation/Editor'
    for source in (ROOT / 'unity/Assets/Official/Partners/UI/GiftPresentation').rglob('*'):
        if source.is_file():
            target = project / source.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(source, target)
    for relative in ('Assets/Official/Partners/UI/GiftPresentation.meta',
                     'Assets/Game/Resources/Partners/OfficialPartnerRelationshipImages.asset',
                     'Assets/Game/Resources/Partners/OfficialPartnerRelationshipImages.asset.meta',
                     'Assets/Game/Resources/Partners/OfficialPartnerGiftPageImages.asset',
                     'Assets/Game/Resources/Partners/OfficialPartnerGiftPageImages.asset.meta',
                     'Assets/Game/Resources/Partners/OfficialPartnerGiftPageAssetCatalog.asset',
                     'Assets/Game/Resources/Partners/OfficialPartnerGiftPageAssetCatalog.asset.meta'):
        shutil.copy2(ROOT / 'unity' / relative, project / relative)
    assembly = editor / 'PartnerCurrentValidation.asmdef'
    data = json.loads(assembly.read_bytes())
    data['references'] += ['Zjcs.Official.PartnerGiftControllerUI', 'Zjcs.Offline.State']
    assembly.write_text(json.dumps(data, indent=2), 'utf8', newline='\n')
    base = (ROOT / 'scripts/fixtures/partner_friendship_dialog_validation.cs').read_text('utf8')
    base = base.replace('private sealed class Context :', 'public class Context :')
    # Its unused base test remains available, but only the gift runner is invoked.
    (editor / 'PartnerFriendshipDialogBase.cs').write_text(base, 'utf8', newline='\n')
    probe = (ROOT / 'scripts/fixtures/partner_gift_preferences_validation.cs').read_text('utf8')
    probe = probe.replace('public string CountFormat => fixture.countFormat;',
        'public string CountFormat => fixture.countFormat;\n    public IProfessionImageResourceLoader ImageResources => loader;')
    probe = probe.replace('using Zjcs.Offline.ProfessionUI;', 'using Zjcs.Offline.ProfessionUI;\nusing Zjcs.Offline.PartnersUI;')
    probe = probe.replace('var hint = fixture.roots.Single(row => row.name != "ItemIconDefault" && row.name != "ItemIconNpcGift" && row.name != "ReduceDot");',
        'var hint = fixture.roots.Single(row => row.name == "CenterHintContent");\n        entries.Add(new OfflineProfessionImageCatalog.Entry { Path = "Upgrade", AssetGuid = "18dffd1b841a0884bb2131048f3c26a1", Asset = Prefab("Upgrade") });')
    probe = probe.replace('public Color Green => throw Unexpected(); public Color White => throw Unexpected(); public Color LightRed => throw Unexpected();',
        'public Color Green => Color.green; public Color White => Color.white; public Color LightRed => Color.red; // Fixture host palette.')
    probe = probe.replace('public ItemIcon Borrow(int id, Transform slot)', '''public void BindIcon(ItemIcon icon)
    {
        icon.Bind(this);
        foreach (var cell in icon.GetComponentsInChildren<ItemIconAttachment>(true)) cell.Bind(attachments);
    }
    public ItemIcon Borrow(int id, Transform slot)''')
    probe = probe.replace('loader = new OfflineProfessionImageResourceLoader(catalog);',
        'loader = new OfflineProfessionImageResourceLoader(OfficialPartnerGiftPageAssetCatalog.LoadDefault().Images);')
    probe = probe.replace('() => Load<ItemIconAttachmentConfig>(fixture.attachment)',
        '() => OfficialPartnerGiftPageAssetCatalog.LoadDefault().AttachmentConfig')
    probe = probe.replace('Config = Load<ItemIconConfig>(fixture.config);',
        'Config = OfficialPartnerGiftPageAssetCatalog.LoadDefault().IconConfig;')
    (editor / 'PartnerGiftPreferenceProbe.cs').write_text(probe, 'utf8', newline='\n')
    native = json.loads((ROOT / 'assets/reference/partner-gift-controller-source-manifest.json').read_bytes())
    rules = json.loads((ROOT / 'unity/Assets/Official/Partners/partner-rules.json').read_bytes())['tables']
    ids = {row['item_id'] for row in native['dynamic_images']['sprites']}
    npc, = [row for row in rules['npc'] if row['Id'] == 8401]
    payload = dict(items=[row for row in rules['item'] if row['ClassId'] in ids], images=native['dynamic_images']['sprites'],
        config=native['icon_config'], attachment=native['attachment_config'], countFormat=native['dynamic_images']['texts'][0]['text'],
        roots=[dict(name=name, pointer=pointer) for name, pointer in native['roots'].items()],
        preferences=[dict(id=int(key), multiplier=value) for key, value in npc['GiftPreferenceDict'].items()])
    (project / 'Assets/Editor/gift-preferences-fixture.json').write_text(json.dumps(payload), 'utf8', newline='\n')
    source = (ROOT / 'scripts/fixtures/partner_gift_controller_validation.cs').read_text('utf8')
    source = source.replace('using System;', 'using System;\nusing Zjcs.Offline.PartnersUI;\nusing Zjcs.Offline.Progression;\nusing Zjcs.Offline.State;\nusing Zjcs.Offline.ProfessionUI;', 1)
    source = source.replace('PartnerGiftController', 'PartnerGiftCurrent')
    source = source.replace('IProfessionUpgradePlayer, IProfessionTipDotSystem', 'IProfessionUpgradePlayer, IProfessionTipDotSystem, IOfflinePartnerGiftPresentation')
    source = source.replace('public Input Input; public Confirm Confirm;', 'public List<ProgressionResult> Failures = new List<ProgressionResult>();\n        public void ShowProgressionResult(ProgressionResult result) { Failures.Add(result); }\n        public Input Input; public Confirm Confirm;')
    source = source.replace('EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);',
        'Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");\n        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);')
    start = source.index('    private IEnumerator Check()')
    end = source.index('    private static Dictionary<int, long> Selected', start)
    body = (ROOT / 'scripts/fixtures/partner_gift_current_steps.cs').read_text('utf8')
    setup = source[source.index('        ProfessionGameObjectPoolBridge.Provider', start):source.index('        context.TagSprite =', start)]
    setup = setup.replace('AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/PartnerGift/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic_0.asset")',
        '(TMP_FontAsset)pageAssets.RegionFont')
    setup = setup.replace('AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath("361d52fe7fad65d6413db534072fdf5c"))', 'pageAssets.SourceFont')
    setup = setup.replace('AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PartnerGift/NpcGiftContent.prefab")', 'pageAssets.GiftPrefab')
    source = source[:start] + body.replace('/*NATIVE_SETUP*/', setup) + source[end:]
    source = source.replace('PARTNER_GIFT_CONTROLLER_PASSED: actual gift subtree, pointer selection/reduction/reset/auto/give, rank/max/overflow, title callbacks and lifecycle. Controlled local/popup endpoints; production host/save not covered.',
        'PARTNER_GIFT_CURRENT_PASSED: native pointer selection/reduce/auto/reset/give, current local save/inventory/friendship, locked-file rollback/retry, rank/overflow confirmation and title persistence. Resources-bound native page/images/fonts; formal window routing and popup contents remain pending.')
    (editor / 'PartnerGiftCurrentValidation.cs').write_text(source, 'utf8', newline='\n')
    print('Prepared native gift UI with actual selected local services and isolated saves.')


if __name__ == '__main__':
    main()
