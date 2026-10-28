"""Port game-owned lightweight UI appearance and complete appearance material manager."""
import json
import re
from pathlib import Path
from prepare_unity_profession_photographer import ROOT, CODE, RUNTIME, save, record
from prepare_unity_profession_character_resources import meta


def build():
    receipt=CODE.parent.parent/'code-manifest.json'
    assembly=CODE.parent/'managed/decoded/Assembly-CSharp.dll'
    checked=json.loads(receipt.read_bytes())
    if checked['manifest']!='871_156598' or record(assembly)['sha256']!=checked['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Unexpected original application assembly')
    ports=[]
    for name in ('SharedLoadedMaterialData','YooAssetTextureLoaderWrap'):
        src=CODE/'AppearanceSystem.Runtime'/(name+'.cs')
        ports.append(save(name,src,src.read_text(encoding='utf-8').replace('namespace AppearanceSystem.Runtime;\n','')))
    src=CODE/'AppearanceAssetManagerConfig.cs'
    ports.append(save('AppearanceAssetManagerConfig',src,src.read_text(encoding='utf-8').replace('using Framework;\n','').replace('[ScriptableObjectConfig]\n','')))
    src=CODE/'IAppearanceAssetManager.cs'
    text=src.read_text(encoding='utf-8').replace('using AppearanceSystem.Runtime;\n','').replace('public interface IAppearanceAssetManager','public interface IAppearanceAssetManager : IProfessionAppearanceUIAssetManager')
    for line in ('\tMaterial GetUIMaterial();\n','\tvoid AddTextureLoaderRef(YooAssetTextureLoader textureLoader, Object go = null);\n','\tvoid RemoveTextureLoaderRef(YooAssetTextureLoader textureLoader, Object go = null);\n'):
        if line not in text:raise ValueError('Original manager interface changed')
        text=text.replace(line,'')
    ports.append(save('IAppearanceAssetManager',src,text))
    src=CODE/'AppearanceAssetManager.cs'
    text=src.read_text(encoding='utf-8')
    text=text[text.index('public class AppearanceAssetManager'):]
    text='using System;\nusing System.Collections.Generic;\nusing UnityEngine;\n'+text
    text=text.replace(': BackgroundServiceBase, IAppearanceAssetManager, IUpdatable', ': IAppearanceAssetManager, IDisposable')
    start=text.index('\tprivate DisposableBag mDisposableBag;');end=text.index('\n\tIReadOnlyDictionary',start)
    text=text[:start]+'''    private IDisposable mLowMemorySubscription;
    private readonly AppearanceAssetManagerConfig Config;
    private readonly Func<Action, IDisposable> subscribeLowMemory;
    public AppearanceAssetManager(AppearanceAssetManagerConfig config, Func<Action, IDisposable> subscribeLowMemory)
    {
        Config = config ? config : throw new ArgumentNullException(nameof(config));
        this.subscribeLowMemory = subscribeLowMemory ?? throw new ArgumentNullException(nameof(subscribeLowMemory));
        Initialize();
    }
'''+text[end:]
    text=text.replace('protected override void Initialize()', 'private void Initialize()').replace('Disposable.AddTo<IDisposable>(ApplicationService.OnLowMemory.Subscribe(OnLowMemory), ref mDisposableBag);','mLowMemorySubscription = subscribeLowMemory(OnLowMemory);')
    text=text.replace('protected override void Dispose()', 'public void Dispose()').replace('mDisposableBag.Dispose();','mLowMemorySubscription.Dispose();').replace('void IUpdatable.Update()', 'public void Update()').replace('Config.Value.','Config.')
    for method in ('GetUIMaterial','AddTextureLoaderRef','RemoveTextureLoaderRef'):
        text=text.replace('IAppearanceAssetManager.'+method, 'IProfessionAppearanceUIAssetManager.'+method)
    # Preserve the original null-loader branch; replace framework logging only.
    text=re.sub(r'\t\t\tGLoggerWarnInterpolatedStringHandler message = .*?Log.Warn\([^\n]*\);', '\t\t\tDebug.LogWarning("textureLoader is null");',text,flags=re.S)
    text=text.replace('((OnDemandTextureLoader)textureLoader).','textureLoader.').replace('Object.Destroy(', 'UnityEngine.Object.Destroy(')
    # ILSpy's older KeyValuePair.Deconstruct(ref...) syntax is an out operation.
    text=re.sub(r'\.Deconstruct\(ref ([^,]+), ref ([^)]+)\)', r'.Deconstruct(out \1, out \2)', text)
    ports.append(save('AppearanceAssetManager',src,text))
    src=CODE/'AppearanceSystem/AppearanceUILite.cs'
    text=src.read_text(encoding='utf-8');text=text[text.index('[DisallowMultipleComponent]'):]
    text='using System;\nusing System.Collections.Generic;\nusing UnityEngine;\n'+text
    text=text.replace('public class AppearanceUILite : MonoBehaviour', 'public class AppearanceUILite : MonoBehaviour, IProfessionAppearanceUnloadable')
    text=text.replace('public SkeletonGraphic SkeletonGraphic;', 'public Component SkeletonGraphic;')
    text=text.replace('private DisposableBag mDisposableBag;', 'private List<IDisposable> mDisposableBag;')
    start=text.index('\t[Inject]');end=text.index('\n\tpublic YooAssetTextureLoader TextureLoader',start)
    text=text[:start]+'''    private IProfessionAppearanceUILiteBindings mBindings;
    private IProfessionAppearanceUILiteBindings Bindings => mBindings ?? ProfessionAppearanceUILiteBindings.Required;
    private IProfessionAppearanceUIAssetManager AppearanceAssetManager => Bindings.AssetManager;
    private IProfessionAppearanceUILiteGraphic GraphicGraph => (IProfessionAppearanceUILiteGraphic)SkeletonGraphic;
    public void Configure(IProfessionAppearanceUILiteBindings bindings)
    { mBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }
'''+text[end:]
    a=text.index('\t\t\t\tstring textureLoaderPath');b=text.index('\n\t\t\t}',a)
    text=text[:a]+'''                string name = GraphicGraph.SkeletonDataName;
                string skeletonPath = Bindings.Config.GetSkeletonPath(name);
                string textureLoaderPath = $"{skeletonPath}/{name}_TextureLoader_{LevelOfResolution}.asset";
                var handle = Bindings.LoadAsset(textureLoaderPath);
                if (mDisposableBag == null) mDisposableBag = new List<IDisposable>();
                mDisposableBag.Add(handle);
                mTextureLoader = (YooAssetTextureLoader)handle.Result;'''+text[b:]
    text=text.replace('!Object.op_Implicit((Object)(object)mTextureLoader)', '!mTextureLoader').replace('!Object.op_Implicit((Object)(object)TextureLoader)', '!TextureLoader')
    text=text.replace('SkeletonGraphic = ((Component)this).GetComponent<SkeletonGraphic>();', 'SkeletonGraphic = GetComponent<UnityEngine.UI.Graphic>();')
    text=text.replace('mDisposableBag.Dispose();', 'if (mDisposableBag != null) foreach (var handle in mDisposableBag) handle.Dispose();')
    text=text.replace('SkeletonGraphic.UpdateMesh()', 'GraphicGraph.UpdateMesh()').replace('SkeletonGraphic.allowMultipleCanvasRenderers', 'GraphicGraph.allowMultipleCanvasRenderers').replace('((Graphic)SkeletonGraphic).mainTexture', 'GraphicGraph.mainTexture').replace('SkeletonGraphic.OverrideTexture','GraphicGraph.OverrideTexture').replace('SkeletonGraphic.CustomTextureOverride','GraphicGraph.CustomTextureOverride').replace('SkeletonGraphic.TexturesMultipleCanvasRenderers.Items','GraphicGraph.TexturesMultipleCanvasRenderers')
    text=text.replace('IAppearanceAssetManager appearanceAssetManager', 'IProfessionAppearanceUIAssetManager appearanceAssetManager')
    text=text.replace('((OnDemandTextureLoader)TextureLoader).', 'TextureLoader.').replace('item.Deconstruct(ref val, ref val2);','item.Deconstruct(out val, out val2);')
    ports.append(save('AppearanceUILite',src,text))
    bindings=RUNTIME/'ProfessionAppearanceUILiteBindings.cs';meta(bindings)
    validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionAppearanceUILiteValidation.cs'
    proof=dict(format=1,manifest=checked['manifest'],code_manifest=record(receipt),application_assembly=record(assembly),ports=ports,
        address_method_source=record(CODE/'AppearanceSystem.Runtime/AppearanceAddressUtils.cs'),generator=record(Path(__file__)),bindings=[record(bindings),record(bindings.with_name(bindings.name+'.meta'))],
        validation=[record(validation),record(validation.with_name(validation.name+'.meta'))],
        operation='Copy the original game-owned AppearanceUILite and complete AppearanceAssetManager methods, manager interface/config and material-reference records. Replace skeleton Graphic operations with an explicit native endpoint, resource/low-memory injection with local contracts, and R3 disposable containers with owned IDisposable handles. Preserve enable/disable order, cached loader after request disposal, placeholder reference handling, duplicate/null behavior, source material pool/counter semantics and low-memory update order. No SDK implementation is read or used.',
        validated_scope='Unity 2022.3.57f1c2 Direct3D Play Mode: real default sword/staff native textures loaded by original application requests; two single-renderer owners, first-owner disable retention, cached loader re-enable after request disposal, multiple renderer null/duplicate placeholders, five exact request releases/atlas placeholder restores, four shared material slots and last return destruction, source renderer pool reset/reuse, low-memory/update and shutdown subscription. Two actual weapon GPU captures viewed. Graphic/loader endpoints, atlas access, resource handles and low-memory event are fixtures; this does not validate production UI integration.',
        limitations=['Native full Graphic, loader/atlas asset conversion, production prefab binding and complete profession page/input/save/Windows release remain incomplete.'])
    out=ROOT/'assets/reference/profession-ui/appearance-ui-lite-source-manifest.json';out.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Ported',len(ports),'original lightweight UI/material manager types.')

if __name__=='__main__': build()
