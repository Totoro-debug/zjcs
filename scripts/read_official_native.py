"""Read original IL2CPP method pointers and disassemble named official methods.

Consumes recover_official_native.py's flat image and the pinned v31 metadata.
Printed assembly is source inspection, not a reconstructed managed method body.
"""
from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
import struct
import sys
import io

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
from capstone import Cs, CS_ARCH_ARM64, CS_MODE_LITTLE_ENDIAN
from elftools.elf.elffile import ELFFile


class EngineMethods:
    """Pinned Unity native icall registrations (ADRP/ADD pairs, no symbols needed)."""
    def __init__(self, source: Path):
        self.image = source.read_bytes()
        if hashlib.sha256(self.image).hexdigest() != '5142b00b7ef6fa53828de13afd2cfd07a6a7763100d145cbb50a22e0114a0340':
            raise ValueError('Unity engine does not match the pinned APK')
        self.segments = [dict(s.header) for s in ELFFile(io.BytesIO(self.image)).iter_segments() if s['p_type'] == 'PT_LOAD']
        self.by_name = {}; self.by_address = {}
        code = next(s for s in self.segments if s['p_flags'] & 1)
        def adrp(word, pc):
            imm = ((word >> 5) & 0x7ffff) * 4 + ((word >> 29) & 3)
            if imm & (1 << 20):imm -= 1 << 21
            return (pc & ~4095) + (imm << 12)
        for at in range(code['p_offset'], code['p_offset'] + code['p_filesz'] - 20, 4):
            a,b,c,d,branch = struct.unpack_from('<5I', self.image, at)
            if a & 0x9f00001f != 0x90000000 or b & 0x9f00001f != 0x90000001:continue
            if c & 0xffc003ff != 0x91000000 or d & 0xffc003ff != 0x91000021:continue
            pc = at - code['p_offset'] + code['p_vaddr']
            if branch >> 26 != 5:continue
            relative = branch & 0x3ffffff
            if relative & (1 << 25):relative -= 1 << 26
            if pc + 16 + relative * 4 != 0x854d98:continue
            name_address = adrp(a,pc) + ((c >> 10) & 4095)
            target = adrp(b,pc+4) + ((d >> 10) & 4095)
            offset = self.offset(name_address)
            name = self.image[offset:self.image.index(0,offset)].decode()
            if not name.startswith('UnityEngine.'):continue
            self.by_name[name] = (target,pc)
            self.by_address.setdefault(target,[]).append(name)

    def offset(self, address):
        for s in self.segments:
            if s['p_vaddr'] <= address < s['p_vaddr'] + s['p_filesz']:
                return address - s['p_vaddr'] + s['p_offset']
        raise ValueError(f'Unmapped original Unity address {address:#x}')

    def disassemble(self, start, length):
        at = self.offset(start)
        for insn in Cs(CS_ARCH_ARM64,CS_MODE_LITTLE_ENDIAN).disasm(self.image[at:at+length],start):
            print(f'{insn.address:#x} {insn.mnemonic} {insn.op_str}')


class NativeMethods:
    def __init__(self, image: Path, metadata: Path):
        self.image = image.read_bytes()
        self.metadata = metadata.read_bytes()
        if hashlib.sha256(self.image).hexdigest() != "8e49906e085ba3819c4b47fd3bd12dcdd594ff0fcc75d418a87ac35a4566305b":
            raise ValueError("Restored native image does not match the pinned APK")
        if hashlib.sha256(self.metadata).hexdigest() != "58884b94b56ef35665f44d3f186fce8b8c854cf3bab0a74e4733a18b6a9175b4":
            raise ValueError("Metadata does not match the pinned APK")
        self.header = struct.unpack_from("<64I", self.metadata)
        assert self.header[:2] == (0xFAB11BAF, 31)
        self.types = [struct.unpack_from("<16i8H2I", self.metadata, at) for at in range(self.header[40], self.header[40] + self.header[41], 88)]
        self.methods = [struct.unpack_from("<IiiiiiIHHHH", self.metadata, at) for at in range(self.header[12], self.header[12] + self.header[13], 36)]
        self.by_name = {}
        self.by_address = {}
        self.modules = {}
        self.rw_start = 0x11528210
        for at in range(self.header[42], self.header[42] + self.header[43], 40):
            image_info = struct.unpack_from("<10I", self.metadata, at)
            name = self.string(image_info[0])
            name_address = self.image.find(name.encode() + b"\0")
            if name_address < 0:
                continue  # Hot-update assemblies need not have native modules.
            reference = self.image.find(struct.pack("<Q", name_address), self.rw_start)
            if reference < 0:
                continue
            fields = struct.unpack_from("<18Q", self.image, reference)
            if reference % 8 or fields[1] > 200_000:
                raise ValueError(f"Invalid native module reference for {name}: {reference:#x}")
            if fields[2] + fields[1] * 8 > len(self.image):
                continue  # A BSS table initialized by an Android constructor.
            pointers = struct.unpack_from(f"<{fields[1]}Q", self.image, fields[2])
            self.modules[name] = (reference, fields, pointers)
            for ti in range(image_info[2], image_info[2] + image_info[3]):
                info = self.types[ti]
                type_name = self.string(info[0])
                namespace = self.string(info[1])
                qualified = namespace + "." + type_name if namespace else type_name
                for mi in range(info[9], info[9] + info[16]):
                    method = self.methods[mi]
                    token = method[6] & 0xFFFFFF
                    address = pointers[token - 1]
                    full_name = qualified + "." + self.string(method[0])
                    self.by_name[full_name] = (address, mi)
                    if address:
                        self.by_address.setdefault(address, []).append(full_name)

    def string(self, index: int) -> str:
        start = self.header[6] + index
        return self.metadata[start:self.metadata.index(0, start)].decode("utf-8")

    def disassemble(self, start: int, length: int):
        disassembler = Cs(CS_ARCH_ARM64, CS_MODE_LITTLE_ENDIAN)
        for insn in disassembler.disasm(self.image[start:start + length], start):
            annotation = ""
            if insn.mnemonic in ("bl", "b") and insn.op_str.startswith("#0x"):
                target = int(insn.op_str[1:], 16)
                if target in self.by_address:
                    annotation = " ; " + " / ".join(self.by_address[target][:3])
            print(f"{insn.address:#x} {insn.mnemonic} {insn.op_str}{annotation}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("methods", nargs="*")
    parser.add_argument("--engine", action="store_true", help="Read the pinned libunity.so icalls instead of IL2CPP")
    parser.add_argument("--image", type=Path, default=ROOT / ".tools/source-packages/commerce-native/libil2cpp-restored.bin")
    parser.add_argument("--metadata", type=Path, default=ROOT / ".tools/source-packages/official-code/global-metadata.dat")
    parser.add_argument("--address", type=lambda v: int(v, 0))
    parser.add_argument("--length", type=lambda v: int(v, 0))
    args = parser.parse_args()
    native = EngineMethods(ROOT/'.tools/source-packages/official-code/libunity.so') if args.engine else NativeMethods(args.image, args.metadata)
    if args.address is not None:
        native.disassemble(args.address, args.length or 256)
    for requested in args.methods:
        for name, (address, index) in native.by_name.items():
            if requested not in name:
                continue
            ends = [p for p in native.by_address if p > address]
            length = args.length if args.length is not None else min(ends) - address if ends else 256
            origin=f'registration={index:#x}' if args.engine else f'metadata_index={index}'
            print(f"\n{name} {origin} address={address:#x} length={length:#x}")
            if length and address:
                native.disassemble(address, length)


if __name__ == "__main__":
    main()
