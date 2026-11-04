"""Prepare the exact native playback prefab and saved-state option input check."""
import hashlib
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_partner_panel_validation import main as prepare_panel
from prepare_partner_star_god_validation import verified_installed_bytes
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    project = work / 'star-god-unity'
    if not project.exists(): prepare_panel()
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    from prepare_partner_star_god_validation import copy_modules
    modules = {json.loads(path.read_bytes())['name']: path for path in runtime.rglob('*.asmdef')}
    pending, selected = ['Zjcs.Offline.PartnersUI'], set()
    while pending:
        name = pending.pop()
        if name in selected or name not in modules: continue
        selected.add(name); pending.extend(json.loads(modules[name].read_bytes()).get('references', []))
    copy_modules(project, runtime, modules, selected)
    editor = project / 'Assets/Editor'
    for name in ('PartnerPanelNativeGraphicValidation.cs', 'PartnerStarGodLocalValidation.cs'):
        (editor / name).unlink(missing_ok=True)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_dialogue_playback_validation.cs', editor / 'PartnerDialoguePlaybackValidation.cs')
    definition = editor / 'Validation.asmdef'
    value = json.loads(definition.read_bytes())
    value['references'] = list(dict.fromkeys(value['references'] + ['Zjcs.Official.PartnerDialoguePlaybackUI']))
    definition.write_text(json.dumps(value), 'utf8', newline='\n')
    receipt = json.loads((ROOT / 'assets/reference/partner-dialogue-playback-native-source-manifest.json').read_bytes())
    actors = json.loads((ROOT / 'assets/reference/partner-panel-actors-source-manifest.json').read_bytes())
    adapted = {row['path']: row for row in actors['adaptations']}
    actor_outputs = {row['path']: row['sha256'] for row in actors['outputs']}
    for relative, expected in receipt['installed_output_sha256'].items():
        if '/Runtime/' in relative: continue
        source = ROOT / relative
        if relative in adapted:
            if actors['manifest'] != receipt['manifest'] or adapted[relative]['baseline_sha256'] != expected:
                raise ValueError('Original playback actor baseline differs: ' + relative)
            expected = actor_outputs[relative]
        data = verified_installed_bytes(source, expected, relative)
        target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists() and target.read_bytes().splitlines() != data.splitlines(): raise ValueError('Preserve differing validation asset: ' + str(target))
        target.write_bytes(data)
    # The condition player uses the actual shared world lookup even though this
    # input check selects a conversation whose first branches need no map facts.
    world = json.loads((ROOT / 'assets/reference/partner-conversation-world-source-manifest.json').read_bytes())
    for relative, expected in world['installed_output_sha256'].items():
        if '/Runtime/' in relative: continue
        source = ROOT / relative
        if hashlib.sha256(source.read_bytes()).hexdigest() != expected: raise ValueError('Changed conversation world input')
        target = project / source.relative_to(ROOT / 'unity'); target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    configuration = json.loads((ROOT / 'assets/reference/partner-dialogue-rules-source-manifest.json').read_bytes())
    for relative, expected in configuration['installed_output_sha256'].items():
        if '/Runtime/' in relative: continue
        source = ROOT / relative
        if hashlib.sha256(source.read_bytes()).hexdigest() != expected: raise ValueError('Changed original conversation table binding')
        target = project / source.relative_to(ROOT / 'unity'); target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    print('Prepared exact conversation window and saved-state option input check', flush=True)


if __name__ == '__main__': main()
