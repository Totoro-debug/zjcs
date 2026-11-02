"""Run original populated address-book icons within the full three-page window."""
import hashlib
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_partner_panel_host_validation import main as prepare_host
from validation_workspace import validation_directory
from prepare_current_partner_native import documents


def main():
    work = validation_directory()
    project = work / 'star-god-unity'
    if not (work / 'npc-shared-inputs.json').exists():
        raise ValueError('Prepare the original NPC runtime validation closure first')
    prepare_host()
    for module in ('PartnersUI', 'PartnersNpc'):
        source = ROOT / 'unity/Assets/Game/Runtime/Offline' / module
        # These two runtime modules are owned by this fixture; all other copies
        # retain the full panel's dependency-copy verification.
        for path in source.iterdir():
            if path.is_file(): shutil.copy2(path, project / path.relative_to(ROOT / 'unity'))
    receipt = json.loads((ROOT / 'assets/reference/partner-npc-icons-source-manifest.json').read_bytes())
    bindings = receipt['installed_output_sha256'] | receipt['reused_output_sha256']
    conversation = json.loads((ROOT / 'assets/reference/partner-dialogue-rules-source-manifest.json').read_bytes())
    bindings.update({path: expected for path, expected in conversation['installed_output_sha256'].items()
        if not path.endswith(('.cs', '.cs.meta'))})
    for relative, expected in bindings.items():
        source = ROOT / relative
        if hashlib.sha256(source.read_bytes()).hexdigest() != expected: raise ValueError('Icon source differs: ' + relative)
        target = project / source.relative_to(ROOT / 'unity')
        if target.exists() and target.read_bytes() != source.read_bytes(): raise ValueError('Preserve changed fixture icon: ' + str(target))
        target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(source, target)
    fixture = ROOT / 'scripts/fixtures/partner_panel_host_validation.cs'
    language = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerDialoguePlaybackAssetCatalog.asset'
    language_proof = ROOT / 'assets/reference/partner-dialogue-playback-native-source-manifest.json'
    expected = json.loads(language_proof.read_bytes())['installed_output_sha256'][language.relative_to(ROOT).as_posix()]
    if hashlib.sha256(language.read_bytes()).hexdigest() != expected: raise ValueError('Original dialogue language catalog changed')
    words = documents(language.read_text('utf8'))[11400000]['MonoBehaviour']['texts']
    (project / 'Assets/Editor/npc-icon-dialogue-texts.json').write_text(json.dumps(dict(texts=words), ensure_ascii=False), 'utf8', newline='\n')
    text = fixture.read_text('utf8')
    replacements = {
        'Dialogues = new NewProfileDialogueFixture()': 'Dialogues = PartnerNpcIconValidation.Create(session, character, selected)',
        'word : localizeRoster(key)': 'word : PartnerNpcIconValidation.Localize(key, localizeRoster)',
        'Capture("panel-host-dialogue-empty.png");':
            'Capture("npc-icons-folded.png");\n'
            '        PartnerNpcIconValidation.CheckFolded(view.Content, Click, Capture);\n'
            '        yield return new WaitForSeconds(0.5f);\n'
            '        PartnerNpcIconValidation.CheckUnfolded(view.Content, Click);\n'
            '        Capture("npc-icons-unfolded.png");',
        'SessionState.SetBool(Key, false);': 'PartnerNpcIconValidation.Dispose();\n        SessionState.SetBool(Key, false);',
        'throw new Exception("Original card button has no exposed pointer target");':
            'throw new Exception("Original card button has no exposed pointer target: " + button.name + " active=" + button.activeInHierarchy + " position=" + rect.position + " rect=" + rect.rect);'
    }
    for old, new in replacements.items():
        if text.count(old) != 1: raise ValueError('Review original full-window fixture anchor: ' + old)
        text = text.replace(old, new)
    editor = project / 'Assets/Editor'
    (editor / 'PartnerPanelHostValidation.cs').write_text(text, 'utf8', newline='\n')
    (editor / 'PartnerNpcSharedValidation.cs').unlink(missing_ok=True)
    helper = ROOT / 'scripts/fixtures/partner_npc_icon_validation.cs'
    shutil.copy2(helper, editor / 'PartnerNpcIconValidation.cs')
    inputs = {path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in (fixture, helper, language, language_proof, Path(__file__))}
    (work / 'npc-icon-validation-inputs.json').write_text(json.dumps(inputs, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared full native window with a real saved conversation and all 151 NPC/profession icons', flush=True)


if __name__ == '__main__': main()
