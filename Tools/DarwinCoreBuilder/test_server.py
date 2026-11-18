"""测试夹具：用真实 FileShare.None 数据租约模拟 Unity 启停，不作为服务器发布。"""
import ctypes
from ctypes import wintypes
import json
from pathlib import Path
import sys
import time
import os
from package import write_json


def run():
    args = sys.argv
    data = Path(args[args.index('-server-data') + 1])
    run_id = args[args.index('-server-run-id') + 1]
    mode = Path(args[1]).read_text()
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
    kernel.CreateFileW.restype = wintypes.HANDLE
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = kernel.CreateFileW(str(data / 'server.lock'), 0xC0000000, 0, None, 4, 0x80, None)
    if handle == ctypes.c_void_p(-1).value:
        raise OSError(ctypes.get_last_error(), 'data lease unavailable')
    try:
        guard = data / 'update.guard'
        if guard.exists() and ('-server-update-id' not in args or args[args.index('-server-update-id') + 1] != guard.read_text()):
            sys.exit(9)
        if mode == 'fail':
            (data / 'candidate-mutation.txt').write_text('must be rolled back')
            sys.exit(7)
        release = json.loads((Path(args[1]).parent / 'release.json').read_text())['release']
        receipt = dict(state='ready', runId=run_id, pid=os.getpid(), release=release)
        write_json(data / 'server-status.json', receipt)
        while True:
            if (data / 'stop.request').exists() and mode != 'save-fail':
                (data / 'world-test.json').write_text('{"saved":true}')
                (data / 'stop.request').unlink()
                write_json(data / 'server-status.json', dict(receipt, state='stopped'))
                return
            time.sleep(.05)
    finally:
        kernel.CloseHandle(handle)


if __name__ == '__main__':
    run()
