"""Port original home direction guides, screenshot visibility and fade lifecycle."""
import hashlib
import json
from pathlib import Path
import re

from prepare_home_auto_increase_runtime import ROOT,CODE,sha,metadata

DEST=ROOT/'unity/Assets/Game/Runtime/Official/HomeSceneUiControls'
NAMES=('AUIDirectionGuider','AHomeDirectionGuider','HomeHangUpDirectionGuider','AwardRetrieveNpcDirectionGuider',
    'HomeScreenShootActive','Home.HomeFadeProxy','Home.HomeFadeList','HomeViewMode','SetHomeSceneUIActiveData','HangUpTipLevelChangeData')
PORTS='''using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.World.NativeCamera;
namespace Zjcs.Official.HomeSceneUiControls
{
    public interface IHomeDirectionFocusController { void SetFocusPositionXForce(float x); }
    public interface IHomeDirectionCameraServices
    {
        UnityEvent<Vector3> ViewPosUpdated { get; }
        UnityEngine.Object ControlBrainIdentity { get; }
        Vector3 CameraFollowPosition { get; }
        Vector3 GetViewPos();
        IHomeDirectionFocusController GetHomeController();
    }
    public interface IHomeDirectionFurniture
    { UnityEngine.Object UnityIdentity { get; } Transform transform { get; } int TipLevel { get; } }
    public interface IHomeDirectionNpcData
    { int Id { get; } NpcType NpcType { get; } int HomePositionIndex { get; } }
    public sealed class HomeDirectionNpcInfo { public string FigureModelUI; }
    public interface IHomeSceneUiServices
    {
        IHomeDirectionCameraServices Camera { get; }
        IDragControllerManager DragControllerManager { get; }
        float DirectionGuideFocusDuration { get; }
        IHomeDirectionFurniture GetFurniture(EFurniture type);
        IDisposable SubscribeHangUpTips(Action<HangUpTipLevelChangeData> callback);
        UnityEvent OnHomeNpcRefresh { get; }
        List<IHomeDirectionNpcData> HomeNpcDatas { get; }
        int AwardRetrieveHomeNpcId { get; }
        Transform GetSpawnPoint(int index);
        HomeDirectionNpcInfo GetNpcInfo(int id);
        IHomeMenuPrefabHandle LoadPrefab(string path);
        GameObject InstantiatePrefab(GameObject prefab,Transform slot);
        void FreezeSkeletonGraphic(GameObject instance);
        HomeViewMode HomeViewMode { get; }
        IDisposable SubscribeUiActive(Action<SetHomeSceneUIActiveData> callback);
    }
}
'''
CANCELLATION='''using System;
using System.Threading;
namespace Zjcs.Official.HomeSceneUiControls
{
    // MessagePipe.CancellationTokenDisposable, verified against selected AOT:
    // .ctor creates CTS; Token returns CTS.Token; Dispose calls Cancel then
    // Dispose, in this order. It neither clears the field nor guards repeats.
    public sealed class HomeFadeCancellationTokenDisposable:IDisposable
    {
        readonly CancellationTokenSource cancellationTokenSource=new CancellationTokenSource();
        public CancellationToken Token=>cancellationTokenSource.Token;
        public void Dispose(){cancellationTokenSource.Cancel();cancellationTokenSource.Dispose();}
    }
}
'''


def build():
    inputs,outputs={},{}
    def read(path,verify=False):
        if verify:
            proof=path.with_suffix('.decompilation.json');data=json.loads(proof.read_bytes());assert sha(path)==data['sha256'],path
            for relative,digest in data['inputs'].items():assert sha(ROOT/relative)==digest,relative
            inputs[proof.relative_to(ROOT).as_posix()]=sha(proof)
        inputs[path.relative_to(ROOT).as_posix()]=sha(path);return path.read_text('utf-8-sig')
    def write(name,text):
        path=DEST/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_text(text,'utf8',newline='\n')
        for item in (path,metadata(path)):outputs[item.relative_to(ROOT).as_posix()]=sha(item)
    prefix='using Zjcs.Official.HomeUI;\nusing Zjcs.Official.HomePlayerPanelUI;\nusing Zjcs.Official.World.NativeCamera;\nusing SafeArea=Zjcs.Official.HomeScreenUI.SafeArea;\n'
    for name in NAMES:
        text=read(CODE/'client-source-resolved'/Path(*name.split('.')).with_suffix('.cs'),True)
        for using in ('Common','Framework','Framework.DependencyInjection','Framework.Logging','Framework.Logging.InterpolatedStringHandler','MessagePipe','AppearanceSystem','R3','Home'):
            text=text.replace('using '+using+';\n','')
        text=text.replace('namespace Home;\n','')
        text=re.sub(r'\t\[Inject\]\n\tprivate [^\n]+ \{ get; set; \}\n','',text)
        short=name.rsplit('.',1)[-1]
        if short=='AUIDirectionGuider':
            at=text.index('\n{',text.index('public abstract class'))+2
            text=text[:at]+'\n\tpublic IHomeSceneUiServices Services { protected get; set; }\n'+text[at:]
            text=text.replace('CameraControlManager.ViewPosUpdated','Services.Camera.ViewPosUpdated')
            text=text.replace('DragControllerManager.','Services.DragControllerManager.')
            text=text.replace('HomeCameraController controller = CameraControlManager.ControlBrain.GetController<HomeCameraController>()',
                'IHomeDirectionFocusController controller = Services.Camera.GetHomeController()')
            text=text.replace('CameraControlManager.ControlBrain.GetViewPos()','Services.Camera.GetViewPos()')
            text=text.replace('CameraControlManager.ControlBrain == null','Services.Camera.ControlBrainIdentity == null')
            text=text.replace('CameraControlManager.ControlBrain.MonoProxy.CameraFollow.transform.position','Services.Camera.CameraFollowPosition')
            # Exactly one log-only block; retain the actual coroutine start.
            start=text.index('\t\tGLoggerDebugInterpolatedStringHandler message =')
            end=text.index('\t\tmCoroutine = StartCoroutine(MoveToTarget());',start)
            logging=text[start:end]
            assert 'message.AppendLiteral("[AUIDirectionGuider] HandleGuideBtnOnClick")' in logging
            assert logging.rstrip().endswith('209);')
            text=text[:start]+text[end:]
        elif short=='AHomeDirectionGuider':
            text=text.replace('HomeSettings.Value.DirectionGuideFocusDuration','Services.DirectionGuideFocusDuration')
        elif short=='HomeHangUpDirectionGuider':
            text=text.replace('R3.DisposableBag','HomePanelDisposableBag')
            text=text.replace('Subscriber.Subscribe(OnTipLevelChange).AddTo(ref mDisposableBag)',
                'mDisposableBag.Add(Services.SubscribeHangUpTips(OnTipLevelChange))')
            text=text.replace('BaseHangUpFurniture furnitureComponent = EntityManager.GetFurnitureComponent<BaseHangUpFurniture>(mFurniture)',
                'IHomeDirectionFurniture furnitureComponent = Services.GetFurniture(mFurniture)')
            text=text.replace('!(furnitureComponent != null)',
                '(furnitureComponent == null || furnitureComponent.UnityIdentity == null)')
        elif short=='AwardRetrieveNpcDirectionGuider':
            text=text.replace('HomeNpcDataBase','IHomeDirectionNpcData')
            text=text.replace('HomeNpcManager.','Services.')
            text=text.replace('HomeNpcDataManager.Instance.HomeNpcDatas','Services.HomeNpcDatas')
            text=text.replace('Parsers.GameSettings.AwardRetrieveSetting.HomeNpcId','Services.AwardRetrieveHomeNpcId')
            text=text.replace('NpcInfo npcInfo = Parsers.Instance.NpcInfoParser.Get','HomeDirectionNpcInfo npcInfo = Services.GetNpcInfo')
            text=text.replace('ResourceLoader.LoadPrefab','Services.LoadPrefab').replace('ServiceProvider.Instantiate','Services.InstantiatePrefab')
            text=text.replace('mNpc.GetComponent<SkeletonGraphicProxy>().SkeletonGraphic.freeze = true','Services.FreezeSkeletonGraphic(mNpc)')
        elif short=='HomeScreenShootActive':
            at=text.index('\n{',text.index('public class'))+2;text=text[:at]+'\n\tpublic IHomeSceneUiServices Services { private get; set; }\n'+text[at:]
            text=text.replace('R3.DisposableBag','HomePanelDisposableBag')
            text=text.replace('Subscriber.Subscribe(mOnActive).AddTo(ref mDisposable)','mDisposable.Add(Services.SubscribeUiActive(mOnActive))')
            text=text.replace('HomeData.HomeViewMode','Services.HomeViewMode')
        elif short=='HomeFadeList':
            text=text.replace('CancellationTokenDisposable','HomeFadeCancellationTokenDisposable')
        assert not re.search(r'\b(?:Inject|Parsers|CameraControlManager|HomeNpcManager|ResourceLoader|Subscriber|R3|HomeSettings)\b',text),short
        assert 'Unknown result type' not in text,short
        write(short+'.cs','// Original home scene UI; explicit local service bindings.\n'+prefix+'namespace Zjcs.Official.HomeSceneUiControls\n{\n'+text+'}\n')
    # Reuse only the consumed original helper; map center calculations belong
    # to the existing map module and are not called by this UI.
    text=read(CODE/'client-source-resolved/DirectionGuiderUtils.cs',True)
    start=text.index('\tpublic static Vector2 GetGuiderPosition(');end=text.index('\n\tpublic static Pos2d ',start)
    write('DirectionGuiderUtils.cs','using UnityEngine;\nnamespace Zjcs.Official.HomeSceneUiControls\n{\npublic static class DirectionGuiderUtils\n{\n'+text[start:end]+'\n}\n}\n')
    text=read(CODE/'common-source/Common/NpcType.cs')
    text=text.replace('namespace Common;','namespace Zjcs.Official.HomeSceneUiControls\n{')
    write('NpcType.cs',text+'}\n')
    read(CODE/'common-source/HomeSettings.cs')
    # Full cancellation lifecycle is absent from dummy IL; verify its actual
    # three AOT methods and the exact BCL call targets before emitting C#.
    from capstone import Cs,CS_ARCH_ARM64,CS_MODE_ARM
    folder=ROOT/'.tools/source-packages/official-code/aot-metadata';proof=folder/'source-manifest.json';data=json.loads(read(proof))
    image=folder/'native-image.so';raw=image.read_bytes();expected={Path(row['path']).name:row['sha256'] for row in data['outputs']}
    assert hashlib.sha256(raw).hexdigest()==expected[image.name];inputs[image.relative_to(ROOT).as_posix()]=sha(image)
    methods=[]
    for name,address,size,calls in [('.ctor',0x1051d308,108,[0xf931fb4,0x105c20f8,0xf931fcc,0x105e0a08,0xf931fd8]),
        ('get_Token',0x1051f58c,28,[0x105e04a0,0xf931f9c]),('Dispose',0x1051f5a8,48,[0x105e0c7c,0x105e1150,0xf931f9c])]:
        code=raw[address:address+size];ops=list(Cs(CS_ARCH_ARM64,CS_MODE_ARM).disasm(code,address))
        actual=[int(i.op_str[1:],16) for i in ops if i.mnemonic in ('b','bl')]
        assert actual==calls,(name,actual)
        methods.append(dict(method='MessagePipe.CancellationTokenDisposable.'+name,address=hex(address),byte_count=size,sha256=hashlib.sha256(code).hexdigest(),call_targets=[hex(x) for x in calls]))
    for name in ('MessagePipe','mscorlib'):
        p=CODE.parent/'native-conversion/inputs/Managed'/(name+'.dll');inputs[p.relative_to(ROOT).as_posix()]=sha(p)
    write('HomeFadeCancellationTokenDisposable.cs',CANCELLATION)
    write('HomeSceneUiServices.cs',PORTS)
    write('Zjcs.Official.HomeSceneUiControls.asmdef',json.dumps(dict(name='Zjcs.Official.HomeSceneUiControls',
        references=['Unity.ugui','UniTask','Zjcs.Official.HomeUI','Zjcs.Official.HomePlayerPanelUI','Zjcs.Official.HomeFunctionMenuUI',
            'Zjcs.Official.HomeScreenUI','Zjcs.Official.World.NativeCamera','spine-unity','spine-csharp']),indent=2)+'\n')
    item=metadata(DEST,True);outputs[item.relative_to(ROOT).as_posix()]=sha(item)
    receipt=dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,
        cancellation_aot=methods,cancellation_bcl_targets={'0x105e0a08':'CancellationTokenSource..ctor','0x105e04a0':'CancellationTokenSource.get_Token','0x105e0c7c':'CancellationTokenSource.Cancel','0x105e1150':'CancellationTokenSource.Dispose'},
        scope='Original home guide/screenshot/fade components and cancellation lifecycle. Native hierarchy bindings, local production hosts and actual runtime validation remain required.')
    (ROOT/'assets/reference/home-scene-ui-runtime-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Original home scene UI controls:',len(outputs),'outputs',flush=True)


if __name__=='__main__':build()
