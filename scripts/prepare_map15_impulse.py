"""Pinned world impulse listener and ARM64 Mathf.PerlinNoise kernel inputs."""
import hashlib
import json
import struct
from prepare_map_regions import Regions, ROOT, ART, REF, save
from read_official_native import EngineMethods


def digest(raw):return hashlib.sha256(raw).hexdigest()


def native_samples(engine):
    # Execute the pinned native function itself to create independent fixtures.
    from unicorn import Uc, UC_ARCH_ARM64, UC_MODE_ARM
    from unicorn.arm64_const import UC_ARM64_REG_SP, UC_ARM64_REG_LR, UC_ARM64_REG_S0, UC_ARM64_REG_S1
    cpu=Uc(UC_ARCH_ARM64,UC_MODE_ARM)
    end=max(s['p_vaddr']+s['p_memsz'] for s in engine.segments)
    cpu.mem_map(0,(end+4095)&~4095)
    for segment in engine.segments:
        at=segment['p_offset'];cpu.mem_write(segment['p_vaddr'],engine.image[at:at+segment['p_filesz']])
    stack=0x60000000;cpu.mem_map(stack,0x10000);stop=stack+0x8000
    fbits=lambda value:struct.unpack('<I',struct.pack('<f',value))[0]
    result=[]
    xs=[-1000.125,-435.1221618652344,-256.75,-1.2,-.01,0.,.01,.5,1.,13.7,123.456,255.9,256.,999.9]
    for x in xs:
        for y in [0.,.1,-1.3,256.2]:
            cpu.reg_write(UC_ARM64_REG_SP,stack+0x7000);cpu.reg_write(UC_ARM64_REG_LR,stop)
            cpu.reg_write(UC_ARM64_REG_S0,fbits(x));cpu.reg_write(UC_ARM64_REG_S1,fbits(y))
            cpu.emu_start(0x5038b0,stop,count=10000)
            value=struct.unpack('<f',struct.pack('<I',cpu.reg_read(UC_ARM64_REG_S0)))[0]
            result.append([x,y,value])
    return result


def build():
    e=Regions();listeners=[o for o in e.apk.objects('scenes_map_world') if o.type.name=='MonoBehaviour' and e.mono(o)[0]=='CinemachineImpulseListener']
    if len(listeners)!=1:raise ValueError('World impulse listener binding changed')
    listener=listeners[0];e.input(listener);fields=e.schemas.read(listener)
    go=listener.read(check_read=False).m_GameObject.deref();e.input(go)
    if go.read().m_Name!='freeVCam':raise ValueError('Original world impulse camera changed')
    parent=next(p.component.deref() for p in go.read().m_Component if p.component.deref().type.name=='Transform')
    while parent:
        e.input(parent);value=parent.read();rotation=value.m_LocalRotation
        if any(abs(getattr(rotation,k)-v)>1e-6 for k,v in zip('xyzw',[0,0,0,1])):raise ValueError('New impulse camera orientation requires conversion')
        parent=value.m_Father.deref() if value.m_Father.path_id else None
    if fields['m_ReactionSettings']['m_SecondaryNoise']['m_PathID']:raise ValueError('New world impulse secondary reaction requires conversion')
    index=e.apk.asset('config_ec','/index.g.txt');e.input(index);directories=json.loads(index.read().m_Script);grid={}
    from official_ec_codec import Formats
    from official_map_codec import unpack
    rules=e.apk.asset('config_ec','/WorldGamePlay_part1.g.bytes');e.input(rules);formats=Formats()
    centers={identity:formats.component(typ,raw)['RealCenter'] for parent,identity,components in unpack(rules.read().m_Script.encode('utf8','surrogateescape'))[0]
             if parent==0 and identity in range(6520,6525) for typ,raw in components if typ=='Common.GridTransformComponent'}
    if len(centers)!=5 or any(v!=[0.,0.] for v in centers.values()):raise ValueError('Cannon grid source center changed')
    for identity in range(6520,6525):
        directory=next(k for k,v in directories.items() if identity in v)
        _,go=e.locate('Assets/Prefabs/EcEntity/'+directory+'/'+str(identity)+'.prefab');e.input(go)
        component=next(c.component.deref() for c in go.read().m_Component if c.component.deref().type.name=='MonoBehaviour' and e.mono(c.component.deref())[0]=='MyGridTransform')
        e.input(component);value=e.schemas.read(component)
        if not value['mSetZ'] or value['mOffsetZ']!=0:raise ValueError('Cannon source grid Z configuration changed')
        grid[str(identity)]=dict(set_z=bool(value['mSetZ']),offset=value['mOffsetZ'],real_center=centers[identity])
    engine=EngineMethods(ROOT/'.tools/source-packages/official-code/libunity.so')
    if engine.by_name['UnityEngine.Mathf::PerlinNoise'][0]!=0x5038b0:raise ValueError('Original Mathf noise entry changed')
    permutation=list(struct.unpack_from('<512i',engine.image,engine.offset(0x1cd084)))
    assert permutation[:256]==permutation[256:] and len(set(permutation))==256
    normal=[struct.unpack_from('<f',engine.image,engine.offset(at))[0] for at in [0x1bb7c0,0x1bba08]]
    output=ART/'map15-impulse.json';save(output,dict(listener=fields,permutation=permutation[:256],normalization=normal,grid=grid))
    fixture=ROOT/'scripts/fixtures/map15-perlin-native.json';fixture.parent.mkdir(exist_ok=True)
    save(fixture,dict(engine_sha256=digest(engine.image),entry='0x5038b0',samples=native_samples(engine)))
    code=ROOT/'.tools/source-packages/official-code'
    names=['client-source/MyPlayableCameraShake.cs','client-source/MyGridTransform.cs','client-source/ECGridTransformView.cs',
           'client-source/ZSetUtils.cs','client-source/ECActionPlayableSupporterView.cs']
    package=ROOT/'.tools/unity-cinemachine-source'
    refs=['Runtime/Impulse/CinemachineImpulseDefinition.cs','Runtime/Impulse/CinemachineImpulseManager.cs',
          'Runtime/Impulse/CinemachineImpulseListener.cs','Runtime/Impulse/CinemachineImpulseSource.cs',
          'Runtime/Core/NoiseSettings.cs','Runtime/Core/Predictor.cs']
    sections={}
    for name,address,length in [('icall',0x5038b0,0x28),('noise',0xd7c3a4,0x1f0),('permutation',0x1cd084,2048),('offset',0x1bb7c0,4),('scale',0x1bba08,4)]:
        at=engine.offset(address);sections[name]=dict(address=hex(address),bytes=length,sha256=digest(engine.image[at:at+length]))
    save(REF/'tower-impulse-source-manifest.json',dict(manifest='854_156383',inputs=e.source_records,
        outputs={output.relative_to(ROOT).as_posix():digest(output.read_bytes())},resources={},
        code_sha256={n:digest((code/n).read_bytes()) for n in names},
        engine=dict(file='.tools/source-packages/official-code/libunity.so',sha256=digest(engine.image),sections=sections),
        cinemachine=dict(url='https://packages.unity.com/com.unity.cinemachine/-/com.unity.cinemachine-2.9.7.tgz',
            archive_sha256=digest((package/'com.unity.cinemachine-2.9.7.tgz').read_bytes()),
            files_sha256={'.tools/unity-cinemachine-source/package/'+p:digest((package/'package'/p).read_bytes()) for p in refs}),
        native_fixture=dict(file=fixture.relative_to(ROOT).as_posix(),sha256=digest(fixture.read_bytes()),operation='Independent original ARM64 function execution with Unicorn; 56 input/output vectors'),
        operation='Original freeVCam impulse listener, grid source Z and original native Mathf noise permutation/normalization; no invented camera shake'))
    from prepare_map15_rendering import bind_resources
    bind_resources()
    print('Original world impulse listener and 56 ARM64 Perlin fixtures converted')


if __name__=='__main__':build()
