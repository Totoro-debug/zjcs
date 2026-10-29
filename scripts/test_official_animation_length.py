"""Execute the pinned Unity AnimationClip time-range kernel on source curves.

The fixture replaces only the clip's virtual legacy predicate with `true`.
All range/cache/curve/event instructions are unmodified APK engine code.
"""
import io
from pathlib import Path
import struct
import sys

from official_apk import Apk, ROOT
sys.path.insert(0,str(ROOT/'.tools/character-art-python'))
from elftools.elf.elffile import ELFFile
from unicorn import Uc, UC_ARCH_ARM64, UC_MODE_ARM
from unicorn.arm64_const import UC_ARM64_REG_X0, UC_ARM64_REG_LR, UC_ARM64_REG_SP, UC_ARM64_REG_S0, UC_ARM64_REG_CPACR_EL1
from read_official_native import EngineMethods
from commerce_clip_time import legacy_duration


def native_length(source, clip, events):
    uc=Uc(UC_ARCH_ARM64,UC_MODE_ARM)
    uc.mem_map(0,0x1800000)
    for segment in ELFFile(io.BytesIO(source)).iter_segments():
        if segment['p_type']=='PT_LOAD':uc.mem_write(segment['p_vaddr'],segment.data())
    uc.mem_map(0x70000000,0x40000)
    obj=0x70000000;vtable=obj+0x1000;predicate=obj+0x2000;returned=obj+0x3000
    uc.mem_write(obj,struct.pack('<Q',vtable));uc.mem_write(vtable+0xe0,struct.pack('<Q',predicate))
    uc.mem_write(predicate,bytes.fromhex('20008052c0035fd6'))  # test fixture: return true
    uc.mem_write(obj+0x230,struct.pack('<2f',float('inf'),-float('inf')))
    uc.mem_write(obj+0xc0,struct.pack('<f',clip.m_SampleRate))
    curves=obj+0x4000;keys=obj+0x8000
    uc.mem_write(obj+0x150,struct.pack('<Q',curves))
    uc.mem_write(obj+0x160,struct.pack('<Q',len(clip.m_FloatCurves)))
    for index,curve in enumerate(clip.m_FloatCurves):
        values=curve.curve.m_Curve
        uc.mem_write(curves+index*0xd0+0x60+0x38,struct.pack('<Q',keys))
        uc.mem_write(curves+index*0xd0+0x60+0x48,struct.pack('<Q',len(values)))
        for key in values:
            uc.mem_write(keys,struct.pack('<4fi2f',key.time,key.value,key.inSlope,key.outSlope,key.weightedMode,key.inWeight,key.outWeight));keys+=28
    events_address=obj+0xc000
    uc.mem_write(obj+0x190,struct.pack('<Q',events_address))
    uc.mem_write(obj+0x1a0,struct.pack('<Q',len(events)))
    for index,time in enumerate(sorted(events)):uc.mem_write(events_address+index*0x80,struct.pack('<f',time))
    uc.reg_write(UC_ARM64_REG_SP,obj+0x3f000);uc.reg_write(UC_ARM64_REG_LR,returned)
    uc.reg_write(UC_ARM64_REG_X0,obj);uc.reg_write(UC_ARM64_REG_CPACR_EL1,3<<20)
    uc.emu_start(0xf9c434,returned,count=100000)
    return struct.unpack('<f',struct.pack('<I',uc.reg_read(UC_ARM64_REG_S0)))[0]


def main():
    path=ROOT/'.tools/source-packages/official-code/libunity.so'
    engine=EngineMethods(path)  # verifies the pinned engine hash and icall binding
    assert engine.by_name['UnityEngine.AnimationClip::get_length'][0]==0x5755e0
    apk=Apk()
    obj=next(o for o in apk.objects('defaultpackage_share_b2696e917c258280310268fe4e4132b1.bundle') if o.path_id==6352346077181223357)
    clip=obj.read();assert clip.m_Legacy and clip.m_WrapMode==2
    events=[event.time for event in clip.m_Events]
    assert len(events)==1 and clip.m_Events[0].functionName=='SwitchItem'
    end=max(key.time for curve in clip.m_FloatCurves for key in curve.curve.m_Curve)
    assert end<events[0]
    actual=native_length(engine.image,clip,events)
    assert actual==events[0],(actual,events)
    assert legacy_duration(clip)==actual
    assert native_length(engine.image,clip,[])==end
    assert native_length(engine.image,clip,[end*.5])==end
    after=struct.unpack('<f',struct.pack('<f',events[0]+1))[0]
    assert native_length(engine.image,clip,[after])==after
    print('Original Unity AnimationClip kernel: 9 checks, 0 failures; curves=',end,'event=',events[0],'actual length=',actual)


if __name__=='__main__':main()
