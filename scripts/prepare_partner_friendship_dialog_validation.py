"""Prepare the complete native dialog with its actual runtime assembly closure."""
import json
from pathlib import Path
import shutil

from prepare_partner_gift_native_validation import main as prepare_gift
from prepare_partner_roster_ui_validation import ROOT
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    prepare_gift()
    project = work / 'unity'
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    modules = {json.loads(path.read_bytes())['name']: path for path in runtime.rglob('*.asmdef')}
    pending = ['Zjcs.Official.World.Map13MiniMapWindows', 'Zjcs.Official.StoryBubbles']
    selected, external = set(), set()
    while pending:
        name = pending.pop()
        if name in selected:
            continue
        if name not in modules:
            external.add(name)
            continue
        selected.add(name)
        pending.extend(json.loads(modules[name].read_bytes()).get('references', []))
    if external - {'UniTask', 'Unity.TextMeshPro', 'Unity.ugui', 'UnityEngine.UI', 'Unity.RenderPipelines.Universal.Runtime', 'Unity.RenderPipelines.Core.Runtime'}:
        raise ValueError('Review new native dialog dependencies: ' + repr(external))
    for name in selected:
        folder = modules[name].parent
        for path in folder.rglob('*'):
            if path.is_file() and path.suffix in ('.cs', '.meta', '.asmdef'):
                target = project / 'Assets/Game/Runtime' / path.relative_to(runtime)
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(path, target)
    shutil.copytree(work / 'dialog-code', project / 'Assets/Game/Runtime/Official/PartnersUI/FriendshipDialog', dirs_exist_ok=True)
    editor = project / 'Assets/Editor'
    (editor / 'PartnerGiftNativeValidation.cs').unlink()
    shutil.copy2(ROOT / 'scripts/fixtures/partner_friendship_dialog_validation.cs', editor)
    assembly = editor / 'Validation.asmdef'
    data = json.loads(assembly.read_bytes())
    data['references'] = sorted(set(data['references']) | {'Zjcs.Official.PartnerFriendshipDialogUI',
        'Zjcs.Official.World.Map13MiniMapWindows', 'Zjcs.Official.StoryPlayables', 'UniTask'})
    assembly.write_text(json.dumps(data, indent=2), 'utf8')
    manifest = project / 'Packages/manifest.json'
    data = json.loads(manifest.read_bytes())
    data['dependencies']['com.unity.modules.tilemap'] = '1.0.0'
    manifest.write_text(json.dumps(data, indent=2), 'utf8')
    print('Additional original native dialog dependency closure:', len(selected))


if __name__ == '__main__':
    main()
