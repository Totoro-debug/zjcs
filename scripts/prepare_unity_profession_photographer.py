"""Port the original portrait GPU capture and renderer collection methods."""
import hashlib
import json
from pathlib import Path
import re

from prepare_unity_profession_native_inputs import ROOT

CODE = ROOT / '.tools/source-packages/current/871_156598/official-code/client-source'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI'


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def clean(text):
    text = re.sub(r'^\s*//IL_[^\n]*\n', '\n', text, flags=re.M)
    return re.sub(r'\n(?:[ \t]*\n){2,}', '\n\n', text)


def save(name, source, text):
    target = RUNTIME / (name + '.cs')
    target.write_text('namespace Zjcs.Official.ProfessionUI\n{\nusing Object = UnityEngine.Object;\n' + clean(text) + '\n}\n', encoding='utf-8', newline='\n')
    meta = target.with_name(target.name + '.meta')
    meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs-profession-ui-' + name).encode()).hexdigest() + '\n', encoding='utf-8', newline='\n')
    return dict(source=record(source), runtime=record(target), meta=record(meta))


def build():
    ports = []
    for name in ('RendererZSortComparer', 'AppearancePortraitParticleProvider'):
        source = CODE / (name + '.cs')
        ports.append(save(name, source, source.read_text(encoding='utf-8')))
    source = CODE / 'ICharacterPortraitBuilder.cs'
    text = source.read_text(encoding='utf-8').replace('using AppearanceSystem.Runtime;', 'using UnityEngine;').replace('AppearanceCharacter characterAppearance', 'Component characterAppearance')
    ports.append(save('ICharacterPortraitBuilder', source, text))
    source = CODE / 'CharacterPortraitPhotographer.cs'
    text = source.read_text(encoding='utf-8')
    start = text.index('public class CharacterPortraitPhotographer')
    text = 'using System;\nusing System.Collections.Generic;\nusing UnityEngine;\nusing UnityEngine.Rendering;\n\n' + text[start:]
    text = text.replace('public class CharacterPortraitPhotographer : MonoBehaviour', 'public class CharacterPortraitPhotographer : MonoBehaviour, IProfessionPortraitStudio')
    text = text.replace('private AppearanceCharacter mAppearance;', 'private Component mAppearance;').replace('private AppearanceAnimation mAppearanceAnimation;', 'private Component mAppearanceAnimation;')
    text = text.replace('PlayerPortraitSettings.EPortraitType', 'ProfessionPortraitKind')
    a, b = text.index('\t[Inject]'), text.index('\tprivate void OnDestroy()')
    text = text[:a] + '''    private IProfessionPortraitPhotographerBindings mEnvironment;
    public Component Instance => this;
    public void Configure(IProfessionPortraitPhotographerBindings environment)
    { mEnvironment = environment ?? throw new ArgumentNullException(nameof(environment)); }

''' + text[b:]
    text = text.replace('AppearanceData appearanceData = null;', 'object appearanceData = null;').replace('\t\tbool enabled = default;\n', '')
    text = text.replace('Settings.Value', 'mEnvironment.Settings')
    a, b = text.index('\t\tcatch (Exception ex)'), text.index('\t\tRenderTexture temporary')
    text = text[:a] + '''        catch (Exception ex)
        {
            if (requestor == null)
            {
                Debug.LogError("请求对象丢失！\\n 堆栈：\\n " + ex.StackTrace);
                return null;
            }
            string text = appearanceData != null ? mEnvironment.DescribeAppearance(appearanceData) : "皮肤数据无法获取";
            Debug.LogError("皮肤设置错误 皮肤数据 " + text + " \\n堆栈：\\n " + ex.Message + " \\n " + ex.StackTrace);
            return null;
        }
''' + text[b:]
    a, b = text.index('\t\tcatch (Exception ex3)'), text.index('\t\tfinally')
    text = text[:a] + '''        catch (Exception ex3)
        {
            Debug.LogError("CapturePortraitTexture发生异常 msg:" + ex3.Message + "\\n stackTrace:\\n" + ex3.StackTrace);
        }
''' + text[b:]
    text = text.replace('mAppearance.Release(tryUnloadAllAssets: true);', 'mEnvironment.ReleaseAppearance(mAppearance, tryUnloadAllAssets: true);')
    text = text.replace('RenderTargetIdentifier.op_Implicit((Texture)(object)target)', 'new RenderTargetIdentifier(target)')
    text = text.replace('AppearancePortraitUtils.CollectPortraitAndParticleRenderers((SkeletonRenderer)(object)mAppearanceAnimation.SkeletonAnimation,', 'ProfessionPortraitRendererCollector.CollectPortraitAndParticleRenderers(mEnvironment.GetRendererGraph(mAppearanceAnimation),')
    text = text.replace('CharacterPortraitUtils.CreateBuilder(ServiceProvider, portraitType)', 'mEnvironment.CreateBuilder(portraitType)')
    for forbidden in ('Spine.', 'GLogger', 'ILogger', 'Settings.Value', '[Inject]', 'ServiceProvider', 'AppearanceData ', 'AppearanceCharacter ', 'AppearanceAnimation '):
        if forbidden in text: raise ValueError('Unresolved original photographer dependency: ' + forbidden)
    ports.append(save('CharacterPortraitPhotographer', source, text))
    source = CODE / 'AppearancePortraitUtils.cs'
    text = source.read_text(encoding='utf-8')
    a = text.index('\tpublic static void CollectPortraitRenderers')
    text = 'using System.Collections.Generic;\nusing UnityEngine;\n\npublic static class ProfessionPortraitRendererCollector\n{\n    private static readonly int sColorMask = Shader.PropertyToID("_ColorMask");\n\n' + text[a:]
    text = text.replace('SkeletonRenderer skeletonRenderer', 'IProfessionPortraitRendererGraph rendererGraph')
    text = text.replace('\t\tDynamicSkeletonRenderSeparator dynamicSkeletonRenderSeparator = default;\n', '')
    text = text.replace('((Component)skeletonRenderer).TryGetComponent<DynamicSkeletonRenderSeparator>(ref dynamicSkeletonRenderSeparator)', 'rendererGraph.HasSeparator')
    text = text.replace('List<SkeletonPartsRenderer> partsRenderers = ((SkeletonRenderSeparator)dynamicSkeletonRenderSeparator).partsRenderers;', 'IReadOnlyList<MeshRenderer> partsRenderers = rendererGraph.SeparatedRenderers;')
    text = text.replace('SkeletonPartsRenderer val = partsRenderers[i];', 'MeshRenderer val = partsRenderers[i];').replace('val.MeshRenderer', 'val')
    text = text.replace('((Component)skeletonRenderer)', 'rendererGraph.Root').replace('TryGetComponent<MeshRenderer>(ref item)', 'TryGetComponent<MeshRenderer>(out item)')
    text = text.replace('\t\tAppearanceRendererPartHandler appearanceRendererPartHandler = default;\n', '')
    text = text.replace('rendererGraph.Root.TryGetComponent<AppearanceRendererPartHandler>(ref appearanceRendererPartHandler)', 'rendererGraph.LoadedParts != null')
    text = text.replace('\t\t\tSkinType skinType = default;\n', '').replace('\t\t\tAppearanceSkeletonPartEntry appearanceSkeletonPartEntry = default;\n', '')
    text = text.replace('foreach (KeyValuePair<SkinType, AppearanceSkeletonPartEntry> item2 in appearanceRendererPartHandler.LoadedSkeletonDict)', 'foreach (ProfessionPortraitRenderPart appearanceSkeletonPartEntry2 in rendererGraph.LoadedParts)')
    text = text.replace('\t\t\t\titem2.Deconstruct(ref skinType, ref appearanceSkeletonPartEntry);\n', '').replace('\t\t\t\tAppearanceSkeletonPartEntry appearanceSkeletonPartEntry2 = appearanceSkeletonPartEntry;\n', '')
    text = text.replace('appearanceSkeletonPartEntry2.Part.', 'appearanceSkeletonPartEntry2.').replace('TryGetComponent<AppearancePortraitParticleProvider>(ref appearancePortraitParticleProvider)', 'TryGetComponent<AppearancePortraitParticleProvider>(out appearancePortraitParticleProvider)')
    for forbidden in ('SkeletonRenderer', 'AppearanceRendererPartHandler', '.Part.', 'ref ', 'SkinType '):
        if forbidden in text: raise ValueError('Unresolved renderer collection dependency: ' + forbidden)
    ports.append(save('ProfessionPortraitRendererCollector', source, text))
    bindings = RUNTIME / 'ProfessionPortraitPhotographerBindings.cs'
    validation = ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionPhotographerValidation.cs'
    proof = dict(format=1, manifest='871_156598', ports=ports, generator=record(Path(__file__)),
        adapters=[record(bindings), record(bindings.with_name(bindings.name + '.meta'))],
        validation=[record(validation), record(validation.with_name(validation.name + '.meta'))],
        settings=record(ROOT / 'assets/reference/profession-ui/portrait-settings-source-manifest.json'),
        texture_lifecycle=record(ROOT / 'assets/reference/profession-ui/portrait-lifecycle-source-manifest.json'),
        operation='Copy original camera/RenderTexture/CommandBuffer/material-submesh capture, builder caching, texture-pool readback, exception boundaries and release order. Port the complete original particle-provider and descending world-Z comparer, plus the two original renderer collectors. Adapt appearance/studio-builder/separator/part-handler access to explicit interfaces for the independent native backend; no skeleton runtime is used and no placeholder character component is installed.',
        validated_scope='Unity 2022.3.57f1c2 Direct3D Play Mode: real GPU capture at original 260x310 and 100x120 dimensions, transparent background and last non-null submesh material, camera parameters, builder caching, original call/release order and three expected setup failures; renderer/separator/part/particle collection, original provider cache and color mask. Actual output viewed. Geometry, appearance and renderer graph are fixtures; no real character or full profession-page validation.',
        limitations=['The original studio prefab, full appearance preparation/builders and production renderer graph are not bound. A test renderer graph does not prove live character rendering or profession UI completion.'])
    (ROOT / 'assets/reference/profession-ui/photographer-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Ported original GPU portrait photographer, two collectors, comparer, particle provider and builder contract.')


if __name__ == '__main__':
    build()
