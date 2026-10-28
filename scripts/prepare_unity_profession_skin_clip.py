"""Port all original skin clipping operations with an explicit native skeleton boundary."""
import hashlib
import json
from pathlib import Path
import re
import shutil

from prepare_unity_profession_photographer import ROOT, CODE, RUNTIME, save, record, clean
from prepare_unity_profession_native_inputs import CACHE


def build():
    manifest = CODE.parent.parent/'code-manifest.json'
    metadata = json.loads(manifest.read_bytes())
    assembly = CODE.parent/'managed/decoded/Assembly-CSharp.dll'
    if metadata['manifest'] != '871_156598' or hashlib.sha256(assembly.read_bytes()).hexdigest() != metadata['assemblies'][assembly.name]['decoded_sha256']:
        raise ValueError('Original skin clip source version changed')
    source = CODE/'AppearanceSystem.Runtime/AppearanceSkinClip.cs'
    text = clean(source.read_text(encoding='utf-8'))
    for using in ('using Common;\n','using Framework.Logging;\n','using Framework.Logging.InterpolatedStringHandler;\n','using Spine;\n','using Spine.Unity;\n'):
        text = text.replace(using,'')
    text = text.replace('namespace AppearanceSystem.Runtime;\n','')
    text = text.replace('private AppearanceCharacter mAppearanceCharacter;', 'private Component mAppearanceCharacter;')
    text = text.replace('private AppearanceSpine mAppearanceSpine;', 'private Component mAppearanceSpine;')
    text = text.replace('private Slot mSlot;', 'private IProfessionSkinClipSlot mSlot;')
    text = text.replace('private BoundingBoxAttachment mBoundingBoxAttachment;', 'private IProfessionSkinClipBoundingBox mBoundingBoxAttachment;')
    text = text.replace('private SkeletonGraphic mSkeletonGraphic;', 'private IProfessionSkinClipGraphic mSkeletonGraphic;\n    private IProfessionSkinClipAppearance mSpineGraph;\n    private IProfessionSkinClipCharacter mCharacterGraph;')
    text = text.replace('private SkeletonRendererDelegate mOnGraphicMeshAndMaterialsUpdated;', 'private Action<IProfessionSkinClipGraphic> mOnGraphicMeshAndMaterialsUpdated;')
    text = text.replace('((Component)this).GetComponent<AppearanceCharacter>()', 'ProfessionAppearanceSkinClipBindings.Required.FindCharacter(this)')
    text = text.replace('((Component)this).GetComponent<AppearanceSpine>()', 'ProfessionAppearanceSkinClipBindings.Required.FindSpine(this)')
    a = text.index('\t\t\tISkeletonComponent skeletonComponent')
    b = text.index('\n\t\t\tif (val != null)', a)
    text = text[:a]+'''            mSpineGraph = ProfessionAppearanceSkinClipBindings.Required.GetAppearance(mAppearanceSpine);
            mCharacterGraph = mAppearanceCharacter ? ProfessionAppearanceSkinClipBindings.Required.GetCharacter(mAppearanceCharacter) : null;
            IProfessionSkinClipGraphic val = mSpineGraph.Graphic;'''+text[b:]
    text = text.replace('AppearanceSpine appearanceSpine = mAppearanceSpine;', 'IProfessionSkinClipAppearance appearanceSpine = mSpineGraph;')
    text = text.replace('(Object)(object)mSkeletonGraphic != (Object)null', 'mSkeletonGraphic != null && mSkeletonGraphic.Instance')
    text = text.replace('(Object)(object)mSkeletonGraphic == (Object)null', 'mSkeletonGraphic == null || !mSkeletonGraphic.Instance')
    text = text.replace('Skeleton skeleton = mAppearanceSpine.SkeletonComponent.Skeleton;', 'IProfessionSkinClipSkeleton skeleton = mSpineGraph.Skeleton;')
    text = text.replace('Slot val = mSlot;', 'IProfessionSkinClipSlot val = mSlot;').replace('Attachment attachment = val.Attachment;', 'object attachment = val.Attachment;')
    text = text.replace('BoundingBoxAttachment val2 = (BoundingBoxAttachment)(object)((attachment is BoundingBoxAttachment) ? attachment : null);', 'IProfessionSkinClipBoundingBox val2 = attachment as IProfessionSkinClipBoundingBox;')
    text = text.replace('((VertexAttachment)val2).WorldVerticesLength', 'val2.WorldVerticesLength')
    a = text.index('\t\t\tGLoggerErrorInterpolatedStringHandler')
    b = text.index('\n\t\t}\n\t\telse', a)
    text = text[:a]+'            Debug.LogError(val2.Name + " 的顶点数数量错误。预期:" + mVertices.Length + "  实际:" + val2.WorldVerticesLength);'+text[b:]
    text = text.replace('OnGraphicMeshAndMaterialsUpdated(SkeletonGraphic skeletonGraphic)', 'OnGraphicMeshAndMaterialsUpdated(IProfessionSkinClipGraphic skeletonGraphic)')
    text = text.replace('mAppearanceCharacter.TryGetSkinName', 'mCharacterGraph.TryGetSkinName').replace('mAppearanceSpine.TryGetMaterial', 'mSpineGraph.TryGetMaterial')
    text = text.replace('((VertexAttachment)mBoundingBoxAttachment).ComputeWorldVertices', 'mBoundingBoxAttachment.ComputeWorldVertices')
    text = re.sub(r'Vector2\.op_Implicit\((new Vector2\(mVertices\[\d\], mVertices\[\d\]\) \* num)\)', r'(Vector3)(\1)', text)
    text = re.sub(r'Vector2\.op_Implicit\((val\d*)\)', r'(Vector2)\1', text)
    for forbidden in ('using Spine.', 'using Spine;', 'GLogger', '.op_'):
        if forbidden in text: raise ValueError('Unresolved original skin-clip dependency: '+forbidden)
    if re.search(r'\b(?:SkeletonGraphic|SkeletonRendererDelegate|BoundingBoxAttachment|VertexAttachment|AppearanceCharacter|AppearanceSpine)\b', text):
        raise ValueError('Unresolved original skin-clip SDK/appearance type')
    port = save('AppearanceSkinClip',source,text)
    bindings = RUNTIME/'ProfessionAppearanceSkinClipBindings.cs'
    validation = ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionAppearanceSkinClipValidation.cs'
    closure = CACHE/'typed/skin-clip-binding-closure.json'
    shutil.copyfile(CACHE/'typed/native-bindings-main-dynamic.json', closure)
    proof = dict(format=1,manifest='871_156598',code_manifest=record(manifest),assembly=record(assembly),ports=[port],
        generator=record(Path(__file__)),native_binding_closure=record(closure),bindings=[record(bindings),record(bindings.with_name(bindings.name+'.meta'))],
        validation=[record(validation),record(validation.with_name(validation.name+'.meta'))],
        operation='Copy every original AppearanceSkinClip method, serialized field and execution order. Keep four-vertex checks, transform/mesh scale, cross-product reorder, reflection orientation, all five shader lines, canvas material matching and skin/mesh event lifecycle. Adapt character/appearance/skeleton/graphic access to explicit interfaces and repair decompiled vector conversions; no skeleton SDK implementation is used.',
        validated_scope='Unity 2022.3.57f1c2 Direct3D Play Mode: original line/diagonal coefficients, mesh scale, reflected/nonuniform transformed endpoints and concave reorder; canvas material-or-main-texture matching, all-renderer reset before skin refresh, skin/mesh subscription lifecycle, absent skin/material/skeleton/slot/attachment branches and original invalid vertex-count error pass. Actual GPU output with the existing original translated UI shader clips the square center while retaining its blue exterior; PNG viewed. Skeleton and weighted vertices are fixtures, not a source character or complete profession-page validation.',
        limitations=['Production original character, native weighted bounding-box computation and skeleton/graphic events remain unbound. This component restoration does not complete character animation, skin rendering or the profession UI.'])
    (ROOT/'assets/reference/profession-ui/skin-clip-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Ported all original appearance skin-clip operations.')


if __name__ == '__main__': build()
