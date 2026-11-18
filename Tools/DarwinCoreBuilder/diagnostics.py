"""Unity 启动前检查；缓存权限错误先报告，不让编辑器重复崩溃弹窗。"""
import os
from pathlib import Path
import subprocess


def cache_problem():
    location = os.environ.get('LOCALAPPDATA')
    if not location:
        return None
    cache = Path(location) / 'Unity/Caches/CurlRequestCache.db'
    parent = cache.parent
    while not parent.exists() and parent != parent.parent:
        parent = parent.parent
    try:
        # SQLite 还要创建 WAL/SHM 或重建数据库，因此仅能读已有 db 不代表缓存可用。
        native = Path(__file__).resolve().parent.parent / 'DarwinCoreBuilderCLI.exe'
        if native.is_file():
            probe = subprocess.run([str(native), '--cache-probe', str(parent)], capture_output=True, timeout=10)
            if probe.returncode:
                raise PermissionError(probe.stderr.decode('utf-8', errors='replace'))
        elif os.name == 'nt':
            import ctypes
            kernel = ctypes.WinDLL('kernel32', use_last_error=True)
            kernel.CreateFileW.argtypes = [ctypes.c_wchar_p, ctypes.c_uint, ctypes.c_uint, ctypes.c_void_p, ctypes.c_uint, ctypes.c_uint, ctypes.c_void_p]
            kernel.CreateFileW.restype = ctypes.c_void_p
            kernel.CloseHandle.argtypes = [ctypes.c_void_p]
            handle = kernel.CreateFileW(str(parent), 0x42, 7, None, 3, 0x02000000, None)
            if handle == ctypes.c_void_p(-1).value:
                raise PermissionError(f'Windows directory access error {ctypes.get_last_error()}: {parent}')
            kernel.CloseHandle(handle)
        # 只申请读写句柄并立即关闭，不修改数据库、不清理用户缓存。
        if cache.exists():
            with cache.open('r+b'):
                pass
    except OSError as error:
        return f'Unity Curl cache cannot be opened for update: {cache}. Permission/lock error: {error}. Run the builder from your normal Windows desktop account; do not keep retrying Unity startup in a restricted environment.'
    return None


def check(unity, project):
    expected = (Path(project) / 'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split(': ', 1)[1]
    problem = cache_problem()
    if problem:
        raise PermissionError(problem)
    result = subprocess.run([str(Path(unity).resolve()), '-version'], capture_output=True, timeout=30, check=False)
    actual = result.stdout.decode('utf-8', errors='replace').strip()
    if result.returncode != 0 or actual != expected:
        raise RuntimeError(f'Unity version mismatch/startup error: project={expected}, editor={actual!r}, exit={result.returncode}')
    return f'PASS: Unity {expected}; existing Curl cache can be opened (or has not been created)'
