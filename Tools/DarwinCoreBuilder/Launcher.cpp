// ✅ 新增：静态链接的 Windows 入口启动随附 Python，不要求 .NET 或系统 Python。
#include <windows.h>
#include <shellapi.h>
#include <string>
#include <cstdio>

static std::wstring Quote(const std::wstring& value)
{
    std::wstring result = L"\"";
    size_t slashes = 0;
    for (wchar_t c : value) {
        if (c == L'\\') { ++slashes; continue; }
        result.append(c == L'"' ? slashes * 2 + 1 : slashes, L'\\');
        result += c;
        slashes = 0;
    }
    result.append(slashes * 2, L'\\');
    return result + L'"';
}

#ifndef CONSOLE
#include "NativeGui.h"
#endif

static int Run(int argc, wchar_t** argv)
{
    wchar_t module[32768];
    if (!GetModuleFileNameW(nullptr, module, 32768)) return 1;
    std::wstring root(module);
    root.resize(root.find_last_of(L"\\/") + 1);
    if (argc == 3 && std::wstring(argv[1]) == L"--cache-probe") {
        // 只申请目录权限，不创建文件、不更改数据库。
        HANDLE directory = CreateFileW(argv[2], FILE_ADD_FILE | FILE_DELETE_CHILD, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, nullptr);
        if (directory == INVALID_HANDLE_VALUE) { fwprintf(stderr, L"Unity cache directory access denied/locked: Windows error %lu\n", GetLastError()); return 2; }
        CloseHandle(directory); return 0;
    }
#ifndef CONSOLE
    if (argc == 1) return NativeGui(root);
    if (argc == 2 && std::wstring(argv[1]) == L"--smoke-gui") return NativeGui(root, true);
#endif
    std::wstring python = root + L"runtime\\python.exe";
    std::wstring command = Quote(python) + L" -X utf8 -u " + Quote(root + L"app\\main.py");
    if (argc == 1) command += L" gui";
    for (int i = 1; i < argc; ++i) command += L" " + Quote(argv[i]);
    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    DWORD flags = 0;
    HANDLE log = INVALID_HANDLE_VALUE;
#ifndef CONSOLE
    flags = CREATE_NO_WINDOW;
    SECURITY_ATTRIBUTES security{ sizeof(security), nullptr, TRUE };
    log = CreateFileW((root + L"launcher.log").c_str(), GENERIC_WRITE, FILE_SHARE_READ, &security, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (log == INVALID_HANDLE_VALUE) {
        MessageBoxW(nullptr, L"无法写入 launcher.log，请将软件解压到可写目录。", L"DarwinCoreBuilder", MB_ICONERROR);
        return 1;
    }
    startup.dwFlags = STARTF_USESTDHANDLES;
    startup.hStdOutput = startup.hStdError = log;
    startup.hStdInput = GetStdHandle(STD_INPUT_HANDLE);
#endif
    BOOL started = CreateProcessW(python.c_str(), &command[0], nullptr, nullptr, TRUE, flags, nullptr, nullptr, &startup, &process);
    DWORD error = GetLastError();
    if (log != INVALID_HANDLE_VALUE) CloseHandle(log);
    if (!started) {
#ifdef CONSOLE
        fwprintf(stderr, L"Cannot start portable Python: Windows error %lu\n", error);
#else
        MessageBoxW(nullptr, L"无法启动随附 runtime/python.exe，请检查软件是否完整解压。", L"DarwinCoreBuilder", MB_ICONERROR);
#endif
        return 1;
    }
    WaitForSingleObject(process.hProcess, INFINITE);
    DWORD exitCode = 1;
    GetExitCodeProcess(process.hProcess, &exitCode);
    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);
#ifndef CONSOLE
    if (exitCode != 0) MessageBoxW(nullptr, L"任务失败，请查看 launcher.log。", L"DarwinCoreBuilder", MB_ICONERROR);
#endif
    return static_cast<int>(exitCode);
}

#ifdef CONSOLE
int wmain(int argc, wchar_t** argv) { return Run(argc, argv); }
#else
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    int argc = 0;
    wchar_t** argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (!argv) return 1;
    int code = Run(argc, argv);
    LocalFree(argv);
    return code;
}
#endif
