"""精确定位特征串在游戏进程内存中的命中位置并回读上下文，用于排除假阳性。"""
import ctypes
import sys
from ctypes import wintypes

k32 = ctypes.WinDLL("kernel32", use_last_error=True)
PROCESS_QUERY_INFORMATION = 0x0400
PROCESS_VM_READ = 0x0010
MEM_COMMIT = 0x1000
PAGE_NOACCESS = 0x01

k32.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
k32.OpenProcess.restype = wintypes.HANDLE
k32.CloseHandle.argtypes = [wintypes.HANDLE]
k32.ReadProcessMemory.argtypes = [
    wintypes.HANDLE, wintypes.LPCVOID, wintypes.LPVOID, ctypes.c_size_t,
    ctypes.POINTER(ctypes.c_size_t),
]
k32.VirtualQueryEx.argtypes = [wintypes.HANDLE, wintypes.LPCVOID, ctypes.c_void_p, ctypes.c_size_t]


class MBI(ctypes.Structure):
    _fields_ = [("BaseAddress", ctypes.c_void_p), ("AllocationBase", ctypes.c_void_p),
                ("AllocationProtect", wintypes.DWORD), ("PartitionId", wintypes.WORD),
                ("RegionSize", ctypes.c_size_t), ("State", wintypes.DWORD),
                ("Protect", wintypes.DWORD), ("Type", wintypes.DWORD)]


# (标签, 字符串, 编码) —— 编码取自 DLL 内的真实存放形式
NEEDLES = [
    ("UTF16 字面量 /sacr", "/sacr", "utf-16-le"),
    ("UTF16 字面量 读不到 Action 表", "读不到 Action 表", "utf-16-le"),
    ("UTF16 字面量 自动循环：开", "自动循环：开", "utf-16-le"),
    ("UTF16 字面量 用法：/sacr find", "用法：/sacr find", "utf-16-le"),
    ("UTF8 元数据 RotationEngine", "RotationEngine", "utf-8"),
    ("UTF8 元数据 SimpleACR.Core", "SimpleACR.Core", "utf-8"),
    ("UTF8 元数据 ConfigWindow", "ConfigWindow", "utf-8"),
    ("对照 RotationSolver.Basic", "RotationSolver.Basic", "utf-8"),
]


def scan(pid):
    h = k32.OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, False, pid)
    if not h:
        print("OpenProcess 失败")
        return
    try:
        hits = {label: [] for label, _, _ in NEEDLES}
        addr, mbi = 0, MBI()
        buf = ctypes.create_string_buffer(1 << 20)
        got_n = ctypes.c_size_t(0)
        while addr < 0x7FFFFFFFFFFF:
            if k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)) == 0:
                break
            base, size = mbi.BaseAddress or 0, mbi.RegionSize or 0
            if size == 0:
                break
            addr = base + size
            if mbi.State != MEM_COMMIT or (mbi.Protect & PAGE_NOACCESS):
                continue
            off = 0
            while off < size:
                n = min(1 << 20, size - off)
                if not k32.ReadProcessMemory(h, ctypes.c_void_p(base + off), buf, n, ctypes.byref(got_n)):
                    break
                got = got_n.value
                if got == 0:
                    break
                chunk = buf.raw[:got]
                for label, s, enc in NEEDLES:
                    pat = s.encode(enc)
                    i = chunk.find(pat)
                    if i >= 0 and len(hits[label]) < 3:
                        hits[label].append(base + off + i)
                off += got
        for label, _, _ in NEEDLES:
            hs = hits[label]
            if hs:
                a = hs[0]
                ctx = ctypes.create_string_buffer(96)
                k32.ReadProcessMemory(h, ctypes.c_void_p(a), ctx, 96, ctypes.byref(got_n))
                print(f"  {label:<34} 命中 {len(hs)} 处  首个 0x{a:X}")
                print(f"      上下文(utf16le): {ctx.raw[:96].decode('utf-16-le', 'ignore')!r}")
            else:
                print(f"  {label:<34} 未命中 ✘")
    finally:
        k32.CloseHandle(h)


for pid in [int(x) for x in sys.argv[1:]]:
    print(f"===== PID={pid} =====")
    scan(pid)
