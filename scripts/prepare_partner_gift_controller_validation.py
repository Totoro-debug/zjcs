"""Prepare the actual restored gift subtree and its original runtime closure."""
import json
import argparse
import shutil
from pathlib import Path

from prepare_partner_friendship_dialog_validation import main as prepare_dialog
from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--compile-only', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    if args.compile_only and not (work / 'gift-assets').is_dir():
        shutil.copytree(ROOT / 'unity/Assets/Official/Partners/UI/Gifts', work / 'gift-assets')
    shutil.copytree(ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/FriendshipDialog', work / 'dialog-code', dirs_exist_ok=True)
    prepare_dialog()
    project = work / 'unity'
    shutil.copytree(work / 'gift-controller-code', project / 'Assets/Game/Runtime/Official/PartnersUI/GiftController', dirs_exist_ok=True)
    editor = project / 'Assets/Editor'
    # The gift payload already includes this exact source font/GUID. The base
    # property fixture also copies it into Editor; keep a single original asset.
    for duplicate in (editor / 'FZY4K.ttf', editor / 'FZY4K.ttf.meta'):
        duplicate.unlink(missing_ok=True)
    for metadata in (project / 'Assets/Game/Runtime').rglob('*.meta'):
        if not Path(str(metadata)[:-5]).exists():
            metadata.unlink()
    source = ROOT / 'scripts/fixtures/partner_friendship_dialog_validation.cs'
    text = source.read_text('utf-8-sig').replace('private sealed class Context :', 'public class Context :')
    (editor / source.name).write_text(text, 'utf8')
    probe = (ROOT / 'scripts/fixtures/partner_gift_preferences_validation.cs').read_text('utf-8-sig')
    old = 'var hint = fixture.roots.Single(row => row.name != "ItemIconDefault" && row.name != "ItemIconNpcGift" && row.name != "ReduceDot");'
    new = '''var hint = fixture.roots.Single(row => row.name == "CenterHintContent");
        entries.Add(new OfflineProfessionImageCatalog.Entry { Path = "Upgrade", AssetGuid = "18dffd1b841a0884bb2131048f3c26a1", Asset = Prefab("Upgrade") });'''
    if probe.count(old) != 1:
        raise ValueError('Review changed attachment fixture seam')
    probe = probe.replace(old, new)
    probe = probe.replace('public Color Green => throw Unexpected(); public Color White => throw Unexpected(); public Color LightRed => throw Unexpected();',
                          'public Color Green => Color.green; public Color White => Color.white; public Color LightRed => Color.red; // Controlled fixture palette.')
    probe = probe.replace('public ItemIcon Borrow(int id, Transform slot)', '''public void BindIcon(ItemIcon icon)
    {
        icon.Bind(this);
        foreach (var cell in icon.GetComponentsInChildren<ItemIconAttachment>(true)) cell.Bind(attachments);
    }
    public ItemIcon Borrow(int id, Transform slot)''')
    (editor / 'partner_gift_preferences_validation.cs').write_text(probe, 'utf8')
    for name in ('OfflineProfessionImageCatalog.cs', 'OfflineProfessionImageResourceLoader.cs'):
        shutil.copy2(ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI' / name, editor / name)
    fixture = ROOT / 'scripts/fixtures/partner_gift_controller_validation.cs'
    if fixture.is_file():
        shutil.copy2(fixture, editor / fixture.name)
    assembly = editor / 'Validation.asmdef'
    data = json.loads(assembly.read_bytes())
    data['references'] = sorted(set(data['references']) | {'Zjcs.Official.PartnerGiftControllerUI'})
    assembly.write_text(json.dumps(data, indent=2), 'utf8')
    if args.compile_only:
        return
    native = json.loads((work / 'partner-gift-controller-native-source-manifest.json').read_bytes())
    rules = json.loads((ROOT / 'unity/Assets/Official/Partners/partner-rules.json').read_bytes())
    ids = {row['item_id'] for row in native['dynamic_images']['sprites']}
    npc, = [row for row in rules['tables']['npc'] if row['Id'] == 8401]
    payload = dict(items=[row for row in rules['tables']['item'] if row['ClassId'] in ids],
        images=native['dynamic_images']['sprites'], config=native['icon_config'], attachment=native['attachment_config'],
        countFormat=native['dynamic_images']['texts'][0]['text'],
        roots=[dict(name=name, pointer=reference) for name, reference in native['roots'].items()],
        preferences=[dict(id=int(key), multiplier=value) for key, value in npc['GiftPreferenceDict'].items()])
    (editor / 'gift-preferences-fixture.json').write_text(json.dumps(payload), 'utf8')
    # Actual selected gift data are available to the fixture; player inventory,
    # levels and service responses remain explicitly controlled test endpoints.
    (editor / 'gift-controller-rules.json').write_text(json.dumps(dict(
        gifts=rules['tables']['npc_gift'], npc=npc)), 'utf8')


if __name__ == '__main__':
    main()
