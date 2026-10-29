"""Execute the fixed APK's unmodified TMP italic block and glyph metric getters."""
import struct
import sys

from official_apk import ROOT, Apk
sys.path.insert(0,str(ROOT/'.tools/character-art-python'))
from read_official_native import NativeMethods
from unicorn import Uc,UC_ARCH_ARM64,UC_MODE_ARM
from unicorn import arm64_const as registers


def native_edges(image,angle,bearing,height,padding,style_padding,scale):
    uc=Uc(UC_ARCH_ARM64,UC_MODE_ARM)
    for address in [0x11217000,0x11218000,0x11443000]:
        uc.mem_map(address,4096);uc.mem_write(address,image[address:address+4096])
    base=0x70000000;stack=base+0x10000
    uc.mem_map(base,0x20000)
    uc.mem_write(base+0x5f0,struct.pack('<i',angle))
    uc.mem_write(stack+0x60,struct.pack('<f',.01))
    uc.mem_write(stack+0x174,struct.pack('<f',padding))
    uc.mem_write(stack+0xd0,struct.pack('<f',style_padding))
    uc.mem_write(stack+0x1050+4,struct.pack('<f',height))
    uc.mem_write(stack+0x1050+12,struct.pack('<f',bearing))
    uc.reg_write(registers.UC_ARM64_REG_X19,base)
    uc.reg_write(registers.UC_ARM64_REG_SP,stack)
    uc.reg_write(registers.UC_ARM64_REG_CPACR_EL1,3<<20)
    for name,value in [('S3',20),('S15',0),('S8',scale),('S14',0),('S10',0)]:
        uc.reg_write(getattr(registers,'UC_ARM64_REG_'+name),struct.unpack('<I',struct.pack('<f',value))[0])
    uc.emu_start(0x11218a90,0x11217efc,count=200)
    return [struct.unpack('<f',struct.pack('<I',uc.reg_read(getattr(registers,'UC_ARM64_REG_'+name))))[0] for name in ['S12','S11','S15','S3']]


def main():
    native=NativeMethods(ROOT/'.tools/source-packages/commerce-native/libil2cpp-restored.bin',ROOT/'.tools/source-packages/official-code/global-metadata.dat')
    font=Apk().asset('textmesh_pro_assets_fonts_&_materials','/FZY4K SDF - dynamic.asset')
    assert struct.unpack_from('<i',font.get_raw_data(),1004)[0]==15
    for args in [(15,17,20,4,0,1),(0,30,36,4,1,.75),(-15,12,15,4,0,1.5)]:
        angle,bearing,height,padding,style_padding,scale=args
        top=angle*.01*(bearing+padding+style_padding)*scale
        bottom=angle*.01*(bearing-height-padding-style_padding)*scale
        shift=(top-bottom)*.5
        expected=[top-shift,20+top-shift,bottom-shift,20+bottom-shift]
        actual=native_edges(native.image,*args)
        for a,b in zip(actual,expected):assert abs(a-b)<.00001,(args,actual,expected)
    cap=struct.unpack_from('<f',font.get_raw_data(),164)[0]
    for base_size,new_size,orthographic in [(30,42,True),(40,20,True),(30,42,False)]:
        uc=Uc(UC_ARCH_ARM64,UC_MODE_ARM)
        for address in [0x1125f000,0x3983000]:
            uc.mem_map(address,4096);uc.mem_write(address,native.image[address:address+4096])
        obj=0x70000000;stack=obj+0x10000;uc.mem_map(obj,0x20000)
        uc.reg_write(registers.UC_ARM64_REG_SP,stack)
        uc.reg_write(registers.UC_ARM64_REG_CPACR_EL1,3<<20)
        uc.mem_write(stack+0x20,struct.pack('<Q',obj));uc.mem_write(obj+0x305,bytes([orthographic]))
        uc.reg_write(registers.UC_ARM64_REG_W0,40)
        for name,value in [('S8',cap),('S9',base_size),('S10',new_size)]:
            uc.reg_write(getattr(registers,'UC_ARM64_REG_'+name),struct.unpack('<I',struct.pack('<f',value))[0])
        uc.emu_start(0x1125f4e4,0x1125f520,count=100)
        actual=struct.unpack('<f',uc.mem_read(obj+0x61c,4))[0]
        expected=(base_size-new_size)*.5*cap/40*(1 if orthographic else .1)
        assert abs(actual-expected)<.00001,(actual,expected)
    print('Original TMP italic and midsize native blocks: 16 checks, 0 failures')


if __name__=='__main__':main()
