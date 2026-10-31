"""Run the pinned APK's AArch64 ELF constructors to recover its native code.

Only local file access and explicitly implemented libc/loader calls are emulated.
Unknown imports stop execution; no original executable instruction is patched.
The default stop is after the original loader restores its four load segments
and decodes its relocation metadata, before Android runtime initialization.
Internal R_AARCH64_RELATIVE entries are applied using the original loader rule;
external imports remain unresolved in the output image. Output is a flat virtual-memory image,
including the original protected header; it is not a rebuilt executable ELF.
Developer dependencies live in .tools/character-art-python.
"""

from __future__ import annotations

import argparse
import collections
import hashlib
import io
from pathlib import Path
import re
import struct
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / ".tools/character-art-python"))

from elftools.elf.elffile import ELFFile
from unicorn import Uc, UcError, UC_ARCH_ARM64, UC_MODE_ARM, UC_HOOK_CODE, UC_HOOK_MEM_INVALID, UC_HOOK_INTR
from unicorn.arm64_const import (
    UC_ARM64_REG_X0, UC_ARM64_REG_X1, UC_ARM64_REG_X2, UC_ARM64_REG_X3,
    UC_ARM64_REG_X4, UC_ARM64_REG_X5, UC_ARM64_REG_X6, UC_ARM64_REG_X7,
    UC_ARM64_REG_LR, UC_ARM64_REG_SP, UC_ARM64_REG_PC, UC_ARM64_REG_TPIDR_EL0,
    UC_ARM64_REG_CPACR_EL1,
)

REGS = [UC_ARM64_REG_X0, UC_ARM64_REG_X1, UC_ARM64_REG_X2, UC_ARM64_REG_X3,
        UC_ARM64_REG_X4, UC_ARM64_REG_X5, UC_ARM64_REG_X6, UC_ARM64_REG_X7]
MASK = (1 << 64) - 1


class NativeRecovery:
    def __init__(self, source: Path, verified_sha256=None):
        self.source = source
        self.raw = source.read_bytes()
        digest = hashlib.sha256(self.raw).hexdigest()
        if digest != (verified_sha256 or "0c3cef25cfa9f079da6344825aae5a52e5eb025e0cc9ece737ec79a94cdfa544"):
            raise ValueError("Native input does not match the pinned APK arm64-v8a/libil2cpp.so")
        self.elf = ELFFile(io.BytesIO(self.raw))
        self.uc = Uc(UC_ARCH_ARM64, UC_MODE_ARM)
        self.loads = [s for s in self.elf.iter_segments() if s["p_type"] == "PT_LOAD"]
        for segment in self.loads:
            start = segment["p_vaddr"] & ~4095
            end = (segment["p_vaddr"] + segment["p_memsz"] + 4095) & ~4095
            self.uc.mem_map(start, end - start)
            self.uc.mem_write(segment["p_vaddr"], segment.data())
        self.stub_base = 0x40000000
        self.heap = 0x100000000
        self.heap_end = 0x140000000
        self.uc.mem_map(self.stub_base, 0x100000)
        self.uc.mem_map(self.heap, self.heap_end - self.heap)
        self.uc.mem_map(0x70000000, 0x200000)
        self.uc.mem_map(0x71000000, 0x1000)
        self.stop = self.stub_base + 0xF0000
        self.uc.reg_write(UC_ARM64_REG_TPIDR_EL0, 0x71000000)
        self.uc.reg_write(UC_ARM64_REG_CPACR_EL1, 3 << 20)
        self.symbols = self.elf.get_section_by_name(".dynsym")
        self.tags = {t.entry.d_tag: t.entry.d_val for t in self.elf.get_section_by_name(".dynamic").iter_tags()}
        self.imports: dict[int, str] = {}
        self.by_name: dict[str, int] = {}
        self.sizes: dict[int, int] = {}
        self.free_blocks: dict[int, list[int]] = collections.defaultdict(list)
        self.files: dict[int, io.BytesIO] = {}
        self.next_fd = 3
        self.calls = collections.Counter()
        self.error: str | None = None
        self.reached_stop = False
        self.internal_relocations = 0
        for address_tag, size_tag in [("DT_RELA", "DT_RELASZ"), ("DT_JMPREL", "DT_PLTRELSZ")]:
            start, size = self.tags[address_tag], self.tags[size_tag]
            for at in range(start, start + size, 24):
                offset, info, addend = struct.unpack("<QQq", self.uc.mem_read(at, 24))
                kind, index = info & 0xFFFFFFFF, info >> 32
                if kind == 1027:
                    value = addend
                elif kind in (1025, 1026):
                    symbol = self.symbols.get_symbol(index)
                    value = symbol["st_value"] or self.stub(symbol.name)
                    value += addend
                else:
                    raise ValueError(f"Unsupported relocation {kind}")
                self.uc.mem_write(offset, struct.pack("<Q", value & MASK))
        self.uc.hook_add(UC_HOOK_CODE, self.on_stub, begin=self.stub_base, end=self.stub_base + 0x100000 - 1)
        self.uc.hook_add(UC_HOOK_MEM_INVALID, self.on_invalid)
        self.uc.hook_add(UC_HOOK_INTR, self.on_interrupt)

    def stub(self, name: str) -> int:
        if name not in self.by_name:
            address = self.stub_base + len(self.by_name) * 16
            self.by_name[name] = address
            self.imports[address] = name
            self.uc.mem_write(address, bytes.fromhex("c0035fd6"))  # ret
        return self.by_name[name]

    def alloc(self, size: int) -> int:
        size = max(size, 1)
        if self.free_blocks[size]:
            return self.free_blocks[size].pop()
        address = self.heap
        self.heap = (address + size + 15) & ~15
        if self.heap >= self.heap_end:
            raise ValueError(f"Emulated heap exhausted: {size:#x}")
        self.sizes[address] = size
        return address

    def string(self, address: int, limit: int = 8192) -> bytes:
        if not address:
            return b""
        result = bytearray()
        while len(result) < limit:
            data = bytes(self.uc.mem_read(address + len(result), min(64, limit - len(result))))
            if 0 in data:
                result += data[:data.index(0)]
                return bytes(result)
            result += data
        raise ValueError(f"Unterminated string at {address:#x}")

    def put_string(self, data: bytes) -> int:
        address = self.alloc(len(data) + 1)
        self.uc.mem_write(address, data + b"\0")
        return address

    def maps(self) -> bytes:
        return b"".join(f"{s['p_vaddr']:x}-{s['p_vaddr'] + s['p_memsz']:x} r-xp {s['p_offset']:08x} 00:00 0 /data/app/lib/arm64/libil2cpp.so\n".encode() for s in self.loads)

    def file_data(self, path: bytes) -> bytes | None:
        if path == b"/proc/self/maps":
            return self.maps()
        if path.endswith(b"/libil2cpp.so"):
            return self.raw
        if path in (b"/proc/self/status", b"/proc/1234/status"):
            return b"Name:\tcom.leiting.zjcs\nTracerPid:\t0\n"
        return None

    def invoke(self, name: str, a: list[int]) -> int:
        u = self.uc
        if name in ("malloc", "calloc"):
            size = a[0] * a[1] if name == "calloc" else a[0]
            result = self.alloc(size)
            if name == "calloc" and size:
                u.mem_write(result, bytes(size))
            return result
        if name == "free":
            if a[0] in self.sizes:
                self.free_blocks[self.sizes[a[0]]].append(a[0])
            return 0
        if name == "realloc":
            dst = self.alloc(a[1])
            if a[0]:
                u.mem_write(dst, bytes(u.mem_read(a[0], min(a[1], self.sizes[a[0]]))))
            return dst
        if name in ("memcpy", "memmove"):
            if a[2]:
                u.mem_write(a[0], bytes(u.mem_read(a[1], a[2])))
            return a[0]
        if name == "memset":
            if a[2]:
                u.mem_write(a[0], bytes([a[1] & 255]) * a[2])
            return a[0]
        if name == "strlen":
            return len(self.string(a[0]))
        if name in ("strcmp", "strncmp", "memcmp"):
            if name == "memcmp":
                left, right = bytes(u.mem_read(a[0], a[2])), bytes(u.mem_read(a[1], a[2]))
            else:
                left, right = self.string(a[0]), self.string(a[1])
                if name == "strncmp":
                    left, right = left[:a[2]], right[:a[2]]
            return (left > right) - (left < right)
        if name in ("strcpy", "strncpy"):
            data = self.string(a[1]) + b"\0"
            if name == "strncpy":
                data = data[:a[2]].ljust(a[2], b"\0")
            u.mem_write(a[0], data)
            return a[0]
        if name == "strstr":
            index = self.string(a[0]).find(self.string(a[1]))
            return a[0] + index if index >= 0 else 0
        if name == "memchr":
            index = bytes(u.mem_read(a[0], a[2])).find(bytes([a[1] & 255]))
            return a[0] + index if index >= 0 else 0
        if name == "fopen":
            path = self.string(a[0])
            print(" fopen", repr(path), flush=True)
            data = self.file_data(path)
            if data is None:
                raise ValueError(f"Unimplemented local file {path!r}")
            handle = self.alloc(256)
            self.files[handle] = io.BytesIO(data)
            return handle
        if name == "fgets":
            data = self.files[a[2]].readline(a[1] - 1)
            if not data:
                return 0
            u.mem_write(a[0], data + b"\0")
            return a[0]
        if name == "fclose":
            self.files.pop(a[0], None)
            return 0
        if name == "sscanf":
            text, fmt = self.string(a[0]), self.string(a[1])
            if self.calls[name] <= 3:
                print(" sscanf", repr(fmt), flush=True)
            pattern = b""
            fields = []
            position = 0
            while position < len(fmt):
                if fmt[position] == 37:
                    spec = re.match(rb"%([*]?)(\d*)(ll|l|hh|h|z)?([xXudscpn])", fmt[position:])
                    if spec is None:
                        raise ValueError(f"Unimplemented sscanf {fmt!r}")
                    skip, width, length, conversion = spec.groups()
                    atom = {b"x": rb"[0-9a-fA-F]+", b"X": rb"[0-9a-fA-F]+", b"p": rb"[0-9a-fA-Fx]+", b"d": rb"[+-]?\d+", b"u": rb"\d+", b"s": rb"\S+", b"c": rb".", b"n": rb""}[conversion]
                    if conversion == b"s" and width:
                        atom = rb"\S{1," + width + b"}"
                    pattern += (b"(?:" if skip else b"(") + atom + b")"
                    if not skip:
                        fields.append((length, conversion))
                    position += len(spec[0])
                elif chr(fmt[position]).isspace():
                    pattern += rb"\s*"
                    position += 1
                else:
                    pattern += re.escape(fmt[position:position + 1])
                    position += 1
            match = re.match(pattern, text)
            if match is None:
                return 0
            for index, ((length, conversion), value) in enumerate(zip(fields, match.groups())):
                pointer = a[index + 2] if index < 6 else struct.unpack("<Q", u.mem_read(u.reg_read(UC_ARM64_REG_SP) + 8 * (index - 6), 8))[0]
                if conversion in (b"s", b"c"):
                    u.mem_write(pointer, value + (b"\0" if conversion == b"s" else b""))
                else:
                    number = match.end(index + 1) if conversion == b"n" else int(value, 16 if conversion in (b"x", b"X", b"p") else 10)
                    size = 8 if length in (b"l", b"ll", b"z") or conversion == b"p" else 4
                    u.mem_write(pointer, (number & ((1 << (size * 8)) - 1)).to_bytes(size, "little"))
            return sum(conversion != b"n" for _, conversion in fields)
        if name == "open":
            path = self.string(a[0])
            print(" open", repr(path), flush=True)
            data = self.file_data(path)
            if data is None:
                raise ValueError(f"Unimplemented local file {path!r}")
            fd = self.next_fd
            self.next_fd += 1
            self.files[fd] = io.BytesIO(data)
            return fd
        if name in ("read", "fread"):
            fd, pointer, count = (a[0], a[1], a[2]) if name == "read" else (a[3], a[0], a[1] * a[2])
            data = self.files[fd].read(count)
            if data:
                u.mem_write(pointer, data)
            return len(data) if name == "read" else len(data) // a[1]
        if name in ("lseek", "lseek64", "fseek"):
            return self.files[a[0]].seek(a[1] if a[1] < (1 << 63) else a[1] - (1 << 64), a[2])
        if name == "close":
            self.files.pop(a[0], None)
            return 0
        if name == "mmap":
            size = (a[1] + 4095) & ~4095
            if a[0] and a[3] & 0x10:
                result = a[0]
            else:
                self.heap = (self.heap + 4095) & ~4095
                result = self.alloc(size)
            print(" mmap", hex(result), hex(size), "fd", hex(a[4]), "offset", hex(a[5]), flush=True)
            if a[4] in self.files:
                file = self.files[a[4]]
                old = file.tell()
                file.seek(a[5])
                data = file.read(a[1])
                file.seek(old)
                u.mem_write(result, data)
            return result
        if name == "munmap":
            return 0
        if name == "dladdr":
            u.mem_write(a[1], struct.pack("<QQQQ", self.put_string(b"/data/app/lib/arm64/libil2cpp.so"), 0, 0, 0))
            return 1
        if name == "dlopen":
            print(" dlopen", repr(self.string(a[0])), flush=True)
            return 0x1234
        if name == "dlsym":
            symbol = self.string(a[1]).decode("ascii")
            if self.calls[name] <= 3:
                print(" dlsym", repr(symbol[:80]), flush=True)
            return self.stub(symbol)
        if name == "dlclose":
            return 0
        if name == "getpid" or name == "gettid":
            return 1234
        if name == "mprotect":
            print(" mprotect", hex(a[0]), hex(a[1]), a[2], flush=True)
            return 0
        if name == "getenv":
            return 0
        if name in ("__cxa_atexit", "__cxa_finalize"):
            return 0
        if name.startswith("ZSTD_trace_decompress_"):
            return 0
        raise ValueError(f"Unimplemented import {name!r}")

    def on_stub(self, uc: Uc, address: int, size: int, user_data):
        if address == self.stop:
            uc.emu_stop()
            return
        name = self.imports.get(address)
        args = [uc.reg_read(r) for r in REGS]
        self.calls[name] += 1
        if self.calls[name] <= 3:
            print(" import", name, [hex(x) for x in args[:4]], "lr", hex(uc.reg_read(UC_ARM64_REG_LR)), flush=True)
        try:
            result = self.invoke(name, args)
            uc.reg_write(UC_ARM64_REG_X0, result & MASK)
        except Exception as exc:
            self.error = str(exc)
            uc.emu_stop()

    def on_invalid(self, uc, access, address, size, value, user_data):
        self.error = f"Unmapped memory {address:#x} size={size} pc={uc.reg_read(UC_ARM64_REG_PC):#x} lr={uc.reg_read(UC_ARM64_REG_LR):#x}"
        return False

    def on_interrupt(self, uc, interrupt, user_data):
        self.error = f"Unhandled interrupt {interrupt} pc={uc.reg_read(UC_ARM64_REG_PC):#x}"
        uc.emu_stop()

    def stop_at(self, address: int):
        def reached(uc, pc, size, user_data):
            self.reached_stop = True
            print("Reached requested original-code boundary", hex(pc), flush=True)
            uc.emu_stop()
        self.uc.hook_add(UC_HOOK_CODE, reached, begin=address, end=address)

    def call(self, address: int, budget: int):
        print("CALL", hex(address), flush=True)
        self.uc.reg_write(UC_ARM64_REG_SP, 0x701FF000)
        self.uc.reg_write(UC_ARM64_REG_LR, self.stop)
        started = time.monotonic()
        try:
            pc = address
            remaining = budget
            last_update = started
            while remaining > 0:
                count = min(remaining, 500_000_000)
                self.uc.emu_start(pc, self.stop, count=count)
                pc = self.uc.reg_read(UC_ARM64_REG_PC)
                remaining -= count
                if self.error or pc == self.stop or self.reached_stop:
                    break
                if time.monotonic() - last_update >= 10:
                    print(" RUN", hex(pc), "instruction budget left", remaining, "heap", hex(self.heap), flush=True)
                    last_update = time.monotonic()
        except UcError as exc:
            self.error = self.error or str(exc)
        pc = self.uc.reg_read(UC_ARM64_REG_PC)
        print("STOP", hex(pc), "seconds", round(time.monotonic() - started, 3), "imports", dict(self.calls), flush=True)
        if self.error:
            raise RuntimeError(self.error)
        if pc != self.stop and not self.reached_stop:
            raise RuntimeError(f"Instruction budget exhausted at {pc:#x}")

    def save(self, output: Path, restore_loader_relocations=False):
        if restore_loader_relocations or self.uc.reg_read(UC_ARM64_REG_PC) == 0x206F73C8:
            if restore_loader_relocations and not self.reached_stop:
                raise ValueError('Loader relocation restoration requires a reached, inspected boundary')
            from unicorn.arm64_const import UC_ARM64_REG_X19
            context = self.uc.reg_read(UC_ARM64_REG_X19)
            raw_context = bytes(self.uc.mem_read(context, 0x198))
            print("Original loader context", [(hex(i), hex(struct.unpack_from('<Q', raw_context, i)[0])) for i in range(0, 0x180, 8)], flush=True)
            for pointer_offset, count_offset in [(0x90, 0x98), (0x58, 0x60)]:
                pointer = struct.unpack_from("<Q", raw_context, pointer_offset)[0]
                count = struct.unpack_from("<I", raw_context, count_offset)[0]
                if not pointer:
                    continue
                if count > 5_000_000:
                    raise ValueError(f"Invalid original relocation count {count}")
                entries = self.uc.mem_read(pointer, count * 24)
                kinds = collections.Counter()
                for offset, info, addend in struct.iter_unpack("<QQq", entries):
                    kind = info & 0xFFFFFFFF
                    kinds[kind] += 1
                    if kind == 1027:
                        self.uc.mem_write(offset, struct.pack("<Q", addend & MASK))
                        self.internal_relocations += 1
                print("Original relocation table", hex(pointer), count, dict(kinds), flush=True)
        header = bytes(self.uc.mem_read(0, 64))
        phoff = struct.unpack_from("<Q", header, 32)[0]
        phnum = struct.unpack_from("<H", header, 56)[0]
        segments = [struct.unpack("<IIQQQQQQ", self.uc.mem_read(phoff + i * 56, 56)) for i in range(phnum)]
        segments = [s for s in segments if s[0] == 1]
        end = max(s[3] + s[5] for s in segments)
        if end > 0x20000000:
            raise ValueError("Original load segments have not been recovered")
        output.parent.mkdir(parents=True, exist_ok=True)
        with output.open("wb") as stream:
            for address in range(0, end, 0x100000):
                stream.write(self.uc.mem_read(address, min(end - address, 0x100000)))
        print("Recovered native virtual image", output, "bytes", end, flush=True)
        print("Internal relative relocations", self.internal_relocations, flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=ROOT / ".tools/source-packages/official-code/libil2cpp.so")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--selected-source", action="store_true", help="Verify the input against the SHA256-pinned selected APK entry, retaining the legacy default pin")
    parser.add_argument("--restore-loader-relocations", action="store_true", help="Restore original X19 loader-context relative relocations at an explicitly inspected stop boundary")
    parser.add_argument("--budget", type=int, default=20_000_000_000)
    parser.add_argument("--call", type=lambda x: int(x, 0), action="append")
    parser.add_argument("--stop-at", type=lambda x: int(x, 0), default=0x206F73C8)
    args = parser.parse_args()
    verified_sha256 = None
    if args.selected_source:
        import zipfile
        from official_source import OfficialSource
        selected = OfficialSource()
        with selected.apk.open('rb') as stream:
            if hashlib.file_digest(stream, 'sha256').hexdigest() != selected.apk_sha256:
                raise ValueError('Selected APK SHA256 mismatch')
        with zipfile.ZipFile(selected.apk) as archive:
            raw = archive.read('lib/arm64-v8a/libil2cpp.so')
        verified_sha256 = hashlib.sha256(raw).hexdigest()
    runtime = NativeRecovery(args.source, verified_sha256)
    if args.stop_at:
        runtime.stop_at(args.stop_at)
    constructors = args.call
    if not constructors:
        constructors = list(struct.unpack("<" + "Q" * (runtime.tags["DT_INIT_ARRAYSZ"] // 8), runtime.uc.mem_read(runtime.tags["DT_INIT_ARRAY"], runtime.tags["DT_INIT_ARRAYSZ"])))
    for address in constructors:
        runtime.call(address, args.budget)
        if runtime.reached_stop:
            break
    if args.output:
        runtime.save(args.output, args.restore_loader_relocations)


if __name__ == "__main__":
    main()
