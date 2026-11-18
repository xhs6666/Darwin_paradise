// ✅ 新增：便携发行版使用系统原生控件，避免要求用户安装 Tk 或 .NET。
#include <commdlg.h>
#include <shlobj.h>
#include <vector>

static HWND fields[5], targetBox, logBox;
static HWND actionButtons[6];
static HANDLE taskHandle = nullptr;
static std::wstring softwareRoot;

static std::wstring Text(HWND control)
{
    std::vector<wchar_t> value(GetWindowTextLengthW(control) + 1);
    GetWindowTextW(control, value.data(), static_cast<int>(value.size()));
    return value.data();
}

static void Browse(HWND window, int index)
{
    wchar_t path[32768]{};
    if (index == 0 || index == 4) {
        OPENFILENAMEW dialog{};
        dialog.lStructSize = sizeof(dialog); dialog.hwndOwner = window;
        dialog.lpstrFile = path; dialog.nMaxFile = 32768;
        dialog.lpstrFilter = index == 0 ? L"Unity 编辑器\0Unity.exe\0所有文件\0*.*\0" : L"核心更新包\0*.zip\0";
        dialog.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;
        if (GetOpenFileNameW(&dialog)) SetWindowTextW(fields[index], path);
    } else {
        BROWSEINFOW dialog{}; dialog.hwndOwner = window;
        dialog.lpszTitle = L"选择目录"; dialog.ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE;
        PIDLIST_ABSOLUTE result = SHBrowseForFolderW(&dialog);
        if (result) { if (SHGetPathFromIDListW(result, path)) SetWindowTextW(fields[index], path); CoTaskMemFree(result); }
    }
}

static void StartTask(HWND window, int action)
{
    if (taskHandle) return;
    const wchar_t* commands[] = { L"build", L"verify", L"update", L"start", L"stop", L"doctor" };
    const int selections[][3] = { {0,1,2}, {4,-1,-1}, {3,4,-1}, {3,-1,-1}, {3,-1,-1}, {0,1,-1} };
    const wchar_t* keys[] = { L"--unity", L"--project", L"--output", L"--root", L"--package" };
    std::wstring python = softwareRoot + L"runtime\\python.exe";
    std::wstring command = Quote(python) + L" -X utf8 -u " + Quote(softwareRoot + L"app\\main.py") + L" " + commands[action];
    for (int index : selections[action]) {
        if (index < 0) continue;
        std::wstring value = Text(fields[index]);
        if (value.empty()) { SetWindowTextW(logBox, L"请先填写当前操作所需的路径。可以直接输入新目录。" ); return; }
        if (action != 1) command += L" " + std::wstring(keys[index]);
        command += L" " + Quote(value);
    }
    if (action == 0) command += L" --target " + Quote(Text(targetBox));
    SECURITY_ATTRIBUTES security{sizeof(security), nullptr, TRUE};
    HANDLE log = CreateFileW((softwareRoot + L"task.log").c_str(), GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, &security, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (log == INVALID_HANDLE_VALUE) { SetWindowTextW(logBox, L"无法写入 task.log，请将软件完整解压到可写目录。" ); return; }
    STARTUPINFOW startup{}; startup.cb = sizeof(startup); startup.dwFlags = STARTF_USESTDHANDLES;
    startup.hStdOutput = startup.hStdError = log; startup.hStdInput = GetStdHandle(STD_INPUT_HANDLE);
    PROCESS_INFORMATION process{};
    BOOL ok = CreateProcessW(python.c_str(), &command[0], nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, nullptr, &startup, &process);
    CloseHandle(log);
    if (!ok) { SetWindowTextW(logBox, L"无法启动随附运行时，请检查 runtime/python.exe 是否存在。" ); return; }
    CloseHandle(process.hThread); taskHandle = process.hProcess;
    for (HWND button : actionButtons) EnableWindow(button, FALSE);
    SetWindowTextW(logBox, L"任务已开始，请等待结果。构建和更新过程中不要关闭软件。" );
    SetTimer(window, 1, 250, nullptr);
}

static void RefreshTask(HWND window)
{
    HANDLE file = CreateFileW((softwareRoot + L"task.log").c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file != INVALID_HANDLE_VALUE) {
        LARGE_INTEGER size{}; GetFileSizeEx(file, &size);
        if (size.QuadPart > 0) {
            LARGE_INTEGER start{}; start.QuadPart = size.QuadPart > 60000 ? size.QuadPart - 60000 : 0;
            SetFilePointerEx(file, start, nullptr, FILE_BEGIN);
            char bytes[60001]{}; DWORD count = 0; ReadFile(file, bytes, 60000, &count, nullptr);
            int length = MultiByteToWideChar(CP_UTF8, 0, bytes, count, nullptr, 0);
            std::wstring text(length, L' '); MultiByteToWideChar(CP_UTF8, 0, bytes, count, &text[0], length);
            SetWindowTextW(logBox, text.c_str());
        }
        CloseHandle(file);
    }
    if (taskHandle && WaitForSingleObject(taskHandle, 0) == WAIT_OBJECT_0) {
        DWORD code = 1; GetExitCodeProcess(taskHandle, &code); CloseHandle(taskHandle); taskHandle = nullptr;
        KillTimer(window, 1); for (HWND button : actionButtons) EnableWindow(button, TRUE);
        std::wstring result = Text(logBox) + L"\r\n退出码：" + std::to_wstring(code);
        SetWindowTextW(logBox, result.c_str());
    }
}

static LRESULT CALLBACK BuilderWindow(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
{
    if (message == WM_COMMAND && HIWORD(wParam) == BN_CLICKED) {
        int id = LOWORD(wParam);
        if (id >= 100 && id < 105) Browse(window, id - 100);
        if (id >= 200 && id < 206) StartTask(window, id - 200);
        return 0;
    }
    if (message == WM_TIMER) { RefreshTask(window); return 0; }
    if (message == WM_CLOSE) {
        if (taskHandle) { SetWindowTextW(logBox, (Text(logBox) + L"\r\n任务尚未完成，请等待后关闭。").c_str()); return 0; }
        DestroyWindow(window); return 0;
    }
    if (message == WM_DESTROY) { PostQuitMessage(0); return 0; }
    return DefWindowProcW(window, message, wParam, lParam);
}

static int NativeGui(const std::wstring& root, bool smoke = false)
{
    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    softwareRoot = root;
    HINSTANCE instance = GetModuleHandleW(nullptr);
    WNDCLASSW type{}; type.lpfnWndProc = BuilderWindow; type.hInstance = instance;
    type.lpszClassName = L"DarwinCoreBuilder"; type.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    type.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_BTNFACE + 1); RegisterClassW(&type);
    HWND window = CreateWindowW(type.lpszClassName, L"DarwinCoreBuilder · 服务端构建与更新", WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX, CW_USEDEFAULT, CW_USEDEFAULT, 920, 660, nullptr, nullptr, instance, nullptr);
    if (!window) return 1;
    HFONT font = static_cast<HFONT>(GetStockObject(DEFAULT_GUI_FONT));
    auto control = [&](const wchar_t* kind, const wchar_t* text, DWORD style, int x, int y, int width, int height, int id) {
        HWND item = CreateWindowExW(0, kind, text, WS_CHILD | WS_VISIBLE | style, x, y, width, height, window, reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)), instance, nullptr);
        SendMessageW(item, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE); return item;
    };
    const wchar_t* labels[] = {L"Unity.exe", L"Unity 工程", L"更新包输出目录", L"服务器部署目录", L"ZIP 核心包"};
    for (int i = 0; i < 5; ++i) {
        control(L"STATIC", labels[i], 0, 20, 24 + i*40, 130, 24, 0);
        fields[i] = control(L"EDIT", L"", WS_BORDER | ES_AUTOHSCROLL | WS_TABSTOP, 150, 20 + i*40, 650, 28, 10+i);
        control(L"BUTTON", L"选择", WS_TABSTOP, 810, 20 + i*40, 70, 28, 100+i);
    }
    SetWindowTextW(fields[2], (root + L"packages").c_str());
    targetBox = control(L"COMBOBOX", L"", CBS_DROPDOWNLIST | WS_VSCROLL | WS_TABSTOP, 150, 225, 250, 150, 20);
    for (const wchar_t* target : { L"windows-headless", L"windows-server", L"linux-server" }) SendMessageW(targetBox, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(target));
    SendMessageW(targetBox, CB_SETCURSEL, 0, 0);
    control(L"STATIC", L"更新会保存停服、备份并重新开服；启动失败将恢复旧版与更新前数据。", 0, 20, 265, 860, 25, 0);
    const wchar_t* actions[] = { L"构建核心包", L"校验 ZIP", L"安装 / 更新", L"启动", L"保存停服", L"环境检查" };
    for (int i = 0; i < 6; ++i) actionButtons[i] = control(L"BUTTON", actions[i], WS_TABSTOP, 20+i*145, 300, 135, 34, 200+i);
    logBox = control(L"EDIT", L"先选择当前工程对应的 Unity 编辑器，再填写工程和输出目录。\r\n部署功能目前支持 Windows；无需安装 Python。", WS_BORDER | ES_MULTILINE | ES_READONLY | WS_VSCROLL, 20, 350, 860, 250, 30);
    if (smoke) { DestroyWindow(window); return 0; }
    ShowWindow(window, SW_SHOW); UpdateWindow(window);
    MSG message{};
    while (GetMessageW(&message, nullptr, 0, 0) > 0) {
        if (!IsDialogMessageW(window, &message)) { TranslateMessage(&message); DispatchMessageW(&message); }
    }
    return 0;
}
