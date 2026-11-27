"""Migrate the original treasure star display while reusing the shared native star/selector components."""
import argparse
import hashlib
import json
from pathlib import Path

from official_source import OfficialSource, ROOT
from prepare_pet_score_chest_preview import original_method

DEST = ROOT/'unity/Assets/Game/Runtime/Official/TreasureUI'
RECEIPT = ROOT/'assets/reference/treasure-ui-controls-source-manifest.json'


def sha(raw): return hashlib.sha256(raw).hexdigest()


def prepare(check=False):
    source = OfficialSource()
    code = source.prepared_path/'official-code'
    proof = source.prepared_path/'code-manifest.json'
    assembly = code/'managed/decoded/Assembly-CSharp.dll'
    if sha(assembly.read_bytes()) != json.loads(proof.read_bytes())['assemblies'][assembly.name]['decoded_sha256']:
        raise ValueError('Original treasure UI assembly differs')
    original = code/'client-source/TreasureStarInfo.cs'
    text = original.read_text('utf-8-sig').replace('using Common;',
        'using Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;')
    text = text.replace('internal class TreasureStarInfo', 'public class TreasureStarInfo')
    text = text.replace('StateSwitcher<bool>', 'WorldStateSwitcher<bool>')
    text = '// Original client star display; see treasure-ui-controls source receipt.\nnamespace Zjcs.Official.TreasureUI\n{\n'+text+'\n}\n'
    outputs = {DEST/'TreasureStarInfo.cs': text.encode(), DEST/'Zjcs.Official.TreasureUI.asmdef':
        (json.dumps(dict(name='Zjcs.Official.TreasureUI', rootNamespace='Zjcs.Official.TreasureUI',
            references=['Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI']), indent=2)+'\n').encode()}
    for path in list(outputs):
        token = hashlib.md5(('zjcs:treasure-ui:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        importer = 'MonoImporter' if path.suffix == '.cs' else 'AssemblyDefinitionImporter'
        outputs[Path(str(path)+'.meta')] = ('fileFormatVersion: 2\nguid: '+token+'\n'+importer+':\n  externalObjects: {}\n').encode()
    folder_token=hashlib.md5(('zjcs:treasure-ui:'+DEST.relative_to(ROOT).as_posix()).encode()).hexdigest()
    outputs[Path(str(DEST)+'.meta')]=('fileFormatVersion: 2\nguid: '+folder_token+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()
    dependencies = [ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI'/name for name in
        ('StarMiniInfo.cs', 'MonoDictionary.cs', 'MonoKeyValuePair.cs', 'Quality.cs')]
    dependencies += [ROOT/'unity/Assets/Game/Runtime/Official/WorldUI/WorldStateSwitcher.cs']
    inputs = [proof, assembly, original, Path(__file__)]
    for source_path, shared_path, signature in (
        (code/'client-source/StarMiniInfo.cs', dependencies[0], 'public class StarMiniInfo'),
        (code/'common-source/Common/Quality.cs', dependencies[3], 'public enum Quality')):
        if original_method(source_path.read_text('utf-8-sig'),signature)!=original_method(shared_path.read_text('utf-8-sig'),signature):
            raise ValueError('Shared original treasure UI dependency changed: '+str(shared_path))
        inputs.append(source_path)
    receipt = dict(manifest=source.version, inputs={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in inputs},
        shared_components={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in dependencies},
        outputs={p.relative_to(ROOT).as_posix():sha(raw) for p,raw in outputs.items()},
        adaptations=['Public namespaced component; original control flow, fields, call order and arguments retained.',
            'Reuse shared original StarMiniInfo, Quality, MonoDictionary and bool state selector; native managed references are rebound explicitly.'])
    prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
    outputs[RECEIPT]=(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n').encode()
    for path,raw in outputs.items():
        if check:
            if not path.is_file() or path.read_bytes()!=raw: raise ValueError('Treasure UI source differs: '+str(path))
        else:
            if path!=RECEIPT and path.is_file() and path.read_bytes()!=raw and sha(path.read_bytes())!=prior.get('outputs',{}).get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve another treasure UI edit: '+str(path))
            path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw)
    print('Verified original treasure star controller and shared UI dependencies',flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    prepare(parser.parse_args().check)
