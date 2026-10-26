"""Record native save selector assets and their explicit runtime bindings."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


folders = ['unity/Assets/Official/SaveSelection', 'unity/Assets/Game/Resources/SaveSelection']
paths = [p for folder in folders for p in (ROOT / folder).rglob('*') if p.is_file()]
paths.extend(ROOT / (folder + '.meta') for folder in folders)
paths.extend(ROOT / path for path in [
    'scripts/prepare_official_startup.py',
    'scripts/prepare_official_save_selection.py',
    'scripts/record_save_selection_native_sources.py',
    'unity/Assets/Game/Editor/Startup/OfficialStartupTools.cs',
    'unity/Assets/Game/Editor/Startup/SaveSelectionPlayModeValidation.cs',
    'unity/Assets/Game/Runtime/Official/Startup/PlanesListPanel.cs',
    'unity/Assets/Game/Runtime/Official/Startup/PlanesInfoItemWithRoleInfo.cs',
    'unity/Assets/Game/Runtime/Offline/Startup/OfficialSaveSelectionAssets.cs',
    'unity/Assets/Game/Runtime/Offline/State/ProfileSelectionPreference.cs',
    'unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs',
    'unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs',
    'unity/Assets/Game/Runtime/Presentation/CharacterCreation/OfficialSaveSelectionView.cs',
    'unity/Assets/Game/Runtime/Presentation/CharacterCreation/SaveSelectionProbe.cs',
    'unity/Assets/Game/Runtime/Presentation/Character/CharacterCreationNativeModelFactory.cs',
    'unity/Assets/Game/Runtime/Presentation/Character/CharacterNativePortraitCapture.cs',
])
receipt = dict(
    manifest='854_156383', source_unity='2022.3.57f1', target_unity='2022.3.57f1c2',
    source_receipt_sha256=sha(ROOT / 'assets/reference/save-selection/source-manifest.json'),
    resource_binding='SaveSelection/OfficialSaveSelectionAssets',
    shared_bindings=['Startup/OfficialStartupAssets.themeCanvas', 'World/Mainline/ScreenBlur',
                     'Character/OfficialCharacterAssetCatalog', 'CharacterCreation/CharacterCreationAssets'],
    operation='Restore original PlanesListPanel, embedded role item and launch blur mask as native prefabs; '
              'retain original sprites, TMP font, anchors, UV, legacy dialog curves and tab Animator graphs; '
              'local slots supply row data, head photography and read/create actions.',
    outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)) for p in sorted(set(paths))],
)
target = ROOT / 'assets/reference/save-selection/native-source-manifest.json'
target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
print('Recorded', len(receipt['outputs']), 'native save selection files')
