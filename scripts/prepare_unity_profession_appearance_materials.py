"""Port the original appearance material operations without a skeleton SDK."""
import hashlib
import json
from pathlib import Path

from prepare_unity_profession_photographer import ROOT, CODE, save, record


def build():
    manifest = CODE.parent.parent / 'code-manifest.json'
    source_manifest = json.loads(manifest.read_bytes())
    assembly = CODE.parent / 'managed/decoded/Assembly-CSharp.dll'
    if source_manifest['manifest'] != '871_156598' or hashlib.sha256(assembly.read_bytes()).hexdigest() != source_manifest['assemblies'][assembly.name]['decoded_sha256']:
        raise ValueError('Original material code version changed')
    source = CODE / 'AppearanceSystem.Runtime/AppearanceMaterialUtils.cs'
    text = source.read_text(encoding='utf-8').replace('using Spine.Unity;\n', '').replace('namespace AppearanceSystem.Runtime;\n', '')
    text = text.replace('Vector4.op_Implicit(vector3)', '(Vector4)vector3')
    text = text.replace('IsMaskable(SkeletonGraphic skeletonGraphic)', 'IsMaskable(Component skeletonGraphic)')
    text = text.replace('TryGetComponent<Mask>(ref val)', 'TryGetComponent<Mask>(out val)')
    if 'Spine.' in text or 'SkeletonGraphic ' in text or 'ref val' in text or '.op_' in text:
        raise ValueError('Unresolved original material dependency')
    port = save('AppearanceMaterialUtils', source, text)
    validation = ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionAppearanceMaterialValidation.cs'
    proof = dict(format=1,manifest='871_156598',code_manifest=record(manifest),assembly=record(assembly),
        ports=[port],generator=record(Path(__file__)),validation=[record(validation),record(validation.with_name(validation.name+'.meta'))],
        operation='Copy all original material property IDs, dye-channel cache and maximum, resets, four effect keywords, stencil/alpha-clip and Unity mask traversal. Repair decompiled Vector3 conversion and out syntax. The mask parameter becomes Component because the original accesses only its transform; no skeleton SDK is used.',
        validated_scope='Unity 2022.3.57f1c2 Direct3D Play Mode: original dye-channel cache/maximum/Clear semantics; main/dye texture, light/hair and four effect keyword resets; all stencil fields and cast-to-int alpha-clip cases; enabled parent mask traversal and sort-override stencil depth pass. An actual GPU draw with the existing original translated UI shader has an opaque blue center and transparent corners; output PNG viewed. The translated source programs do not declare UNITY_UI_ALPHACLIP: Unity retains that operation in shaderKeywords while IsKeywordEnabled returns false. No new shader variants are introduced.',
        limitations=['The original appearance components and production character renderer are not bound. Material fixtures do not validate original actor geometry, animation, skin clipping or the complete profession UI.'])
    (ROOT/'assets/reference/profession-ui/appearance-materials-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Ported all original appearance material operations.')


if __name__ == '__main__': build()
