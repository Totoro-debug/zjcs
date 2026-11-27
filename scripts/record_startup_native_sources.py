"""Record the explicit native Startup bindings after Unity Restore/import."""
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
project = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT / 'unity'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


files = []
for folder in ['Assets/Game/Resources/Startup', 'Assets/Game/Runtime/Official/Startup',
               'Assets/Game/Runtime/Offline/Startup', 'Assets/Game/Editor/Startup', 'Assets/Official/Startup']:
    files.extend(p for p in (project / folder).rglob('*') if p.is_file())
    files.append(project / (folder + '.meta'))
for file in ['CharacterCreationFlowController.cs', 'Zjcs.Presentation.CharacterCreation.asmdef']:
    files.append(project / 'Assets/Game/Runtime/Presentation/CharacterCreation' / file)
source_receipt = json.loads((ROOT / 'assets/reference/startup/source-manifest.json').read_bytes())
receipt = dict(manifest=source_receipt['manifest'], target_unity='2022.3.57f1c2',
               generator_sha256=sha(Path(__file__)),
               source_receipt_sha256=sha(ROOT / 'assets/reference/startup/source-manifest.json'),
               resource_binding='Startup/OfficialStartupAssets',
               operation='Original prefab/component fields and explicit object references; native Sprite geometry, TMP fonts/materials, legacy boot clips and original button Animator graphs; Windows/offline launch host.',
               outputs=[dict(path='unity/' + p.relative_to(project).as_posix(), sha256=sha(p)) for p in sorted(set(files))])
target = ROOT / 'assets/reference/startup/native-source-manifest.json'
if target.exists():
    previous = json.loads(target.read_bytes())
    if previous.get('retired_cache_files'):
        receipt['retired_cache_files'] = previous['retired_cache_files']
target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
print('Recorded', len(receipt['outputs']), 'native Startup files')
