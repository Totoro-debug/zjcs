"""Exercise restored gift prefabs inside the complete original friendship dialog."""
import json
from pathlib import Path
import shutil

from prepare_partner_friendship_dialog_validation import main as prepare_dialog
from prepare_partner_card_native import guid, sha
from official_apk import ROOT
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    shutil.copytree(ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/FriendshipDialog', work / 'dialog-code', dirs_exist_ok=True)
    prepare_dialog()
    project = work / 'unity'
    # Reuse the committed native dialog/dependency bytes, with one GUID per asset.
    staged = project / 'Assets/PartnerGift'
    existing = {guid(path): Path(str(path)[:-5]) for path in staged.rglob('*.meta')}
    receipt = json.loads((ROOT / 'assets/reference/partner-friendship-dialog-source-manifest.json').read_bytes())
    for relative, expected in receipt['installed_output_sha256'].items():
        original = ROOT / relative
        if relative.endswith('.meta') or '/Runtime/' in relative:
            continue
        if sha(original.read_bytes()) != expected:
            raise ValueError('Committed native dialog bytes differ: ' + relative)
        identity = guid(Path(str(original) + '.meta'))
        if identity in existing:
            if existing[identity].read_bytes() != original.read_bytes():
                raise ValueError('Dialog/gift resource identity differs: ' + relative)
            continue
        destination = staged / ('NpcFriendshipDialog.prefab' if original.name == 'NpcFriendshipDialog.prefab'
                                else 'partner-friendship-dialog-texts.json' if original.name == 'partner-friendship-dialog-texts.json'
                                else 'DialogShared/' + original.relative_to(ROOT / 'unity/Assets/Official/Partners').as_posix())
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(original, destination)
        shutil.copy2(Path(str(original) + '.meta'), Path(str(destination) + '.meta'))
        existing[identity] = destination
    source = ROOT / 'scripts/fixtures/partner_friendship_dialog_validation.cs'
    text = source.read_text('utf-8-sig')
    replacements = {
        'public Dictionary<string, string> Texts;': 'public PartnerGiftPreferenceProbe Gifts;\n        public Dictionary<string, string> Texts;',
        'public int CompareRewardItems(int a, int b) => throw new InvalidOperationException("No gift preferences in this scoped native fixture");': 'public int CompareRewardItems(int a, int b) => Gifts.Compare(a, b);',
        'public ItemIcon BorrowGiftIcon(int id, Transform slot) => throw new InvalidOperationException("No gift preferences in this scoped native fixture");': 'public ItemIcon BorrowGiftIcon(int id, Transform slot) => Gifts.Borrow(id, slot);',
        'public void RecycleGiftIcon(ItemIcon icon) => throw new InvalidOperationException("No gift preferences in this scoped native fixture");': 'public void RecycleGiftIcon(ItemIcon icon) => Gifts.Recycle(icon);',
        'ProfessionPropertyBindings.Current = context;': 'context.Gifts = new PartnerGiftPreferenceProbe();\n        context.Texts["ui_ItemIcon_count_key"] = context.Gifts.CountFormat;\n        ProfessionPropertyBindings.Current = context;',
        'GiftPreferenceDict = new Dictionary<int, double>(),': 'GiftPreferenceDict = context.Gifts.Preferences,',
        'Require(!Get<GameObject>(dialog, "mGiftRuleGo").activeSelf, "original empty preference rule hides");': 'Require(Get<GameObject>(dialog, "mGiftRuleGo").activeInHierarchy, "original populated preference rule shows");\n        context.Gifts.AssertPopulated();',
        'Capture("friendship-dialog-open.png");': 'Capture("friendship-dialog-open.png");\n        context.Gifts.ClickFirst(events, cameraUi);',
        'context.Limited = true;': 'context.Gifts.AssertRecycled();\n        npc.GiftPreferenceDict = new Dictionary<int, double>();\n        context.Limited = true;',
        'Require(errors == 0, "native import/render/lifecycle errors");': 'yield return context.Gifts.VerifyAll(canvas.transform, events, cameraUi, () => Capture("native-gift-selected.png"));\n        context.Gifts.Dispose();\n        Require(errors == 0, "native import/render/lifecycle errors");',
        'local production provider and populated gift icons not covered.': '107 original preference sprites, populated native rule, icon popup dispatch and ReduceTip simulated input passed; production provider and popup contents not covered.',
    }
    for old, new in replacements.items():
        if new is None:
            continue
        if text.count(old) != 1:
            raise ValueError('Review changed native dialog fixture seam: ' + old)
        text = text.replace(old, new)
    (project / 'Assets/Editor/partner_friendship_dialog_validation.cs').write_text(text, 'utf8')
    for name in ('OfflineProfessionImageCatalog.cs', 'OfflineProfessionImageResourceLoader.cs'):
        shutil.copy2(ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI' / name, project / 'Assets/Editor' / name)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_gift_preferences_validation.cs', project / 'Assets/Editor')
    native = json.loads((work / 'partner-gift-preferences-native-source-manifest.json').read_bytes())
    rules = json.loads((ROOT / 'unity/Assets/Official/Partners/partner-rules.json').read_bytes())
    ids = {row['item_id'] for row in native['dynamic_images']['sprites']}
    npc, = [row for row in rules['tables']['npc'] if row['Id'] == 8401]
    payload = dict(items=[row for row in rules['tables']['item'] if row['ClassId'] in ids],
        images=native['dynamic_images']['sprites'], config=native['icon_config'], attachment=native['attachment_config'],
        countFormat=native['dynamic_images']['texts'][0]['text'],
        roots=[dict(name=name, pointer=reference) for name, reference in native['roots'].items()],
        preferences=[dict(id=int(key), multiplier=value) for key, value in npc['GiftPreferenceDict'].items()])
    (project / 'Assets/Editor/gift-preferences-fixture.json').write_text(json.dumps(payload), 'utf8')


if __name__ == '__main__':
    main()
