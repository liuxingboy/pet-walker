#include <windows.h>
#include <shellapi.h>
#include <string>
#include <sstream>
#include <vector>
#include <ctime>

// 图标资源 ID
#define IDI_APP_ICON 101

// 全局变量
const WCHAR g_szClassName[] = L"ScheduledPopUpWindowClass";
const WCHAR g_szConfigWindowClassName[] = L"ScheduledPopUpConfigWindowClass";
const WCHAR g_szWindowName[] = L"定时提醒助手";
const WCHAR g_szConfigFileName[] = L"./config.ini";
HWND g_hConfigWnd = NULL; // 设置窗口句柄

// 托盘图标自定义消息
#define WM_TRAYICON (WM_USER + 1)
#define ID_TRAY_EXIT 1001
#define ID_TRAY_SETTINGS 1002

// 默认提醒间隔（分钟）
int g_nReminderIntervalMinutes = 30;
UINT_PTR g_nTimerId = 1;
UINT_PTR g_nStatusTimerId = 2; // 用于更新悬停提示的定时器
time_t g_tLastReminderTime = 0;

// 提醒话语列表
const WCHAR* g_szReminders[] = {
    L"亲爱的，该喝口水休息一下啦~",
    L"眼睛累了吗？望望窗外远方吧。",
    L"站起来活动活动筋骨，身体是革命的本钱哦！",
    L"休息一小会，为了接下来更高效的产出~",
    L"叮咚！您的专属健康官提醒您：该放松一下了。",
    L"深呼吸，闭上眼，享受这一分钟的宁静吧。",
    L"工作是做不完的，但你的健康只有一次，去走走吧。",
    L"来一盘水果或者伸个懒腰，给大脑充充电~"
};

// UI 控件 ID
#define IDC_EDIT_INTERVAL 101
#define IDC_BUTTON_SAVE 102

// 函数声明
LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
LRESULT CALLBACK ConfigWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
void ShowNotification(HWND hwnd);
void LoadConfig();
void SaveConfig();
void UpdateTimer(HWND hwnd);
HWND CreateConfigWindow(HINSTANCE hInstance, HWND hParent);
void AddTrayIcon(HWND hwnd);
void RemoveTrayIcon(HWND hwnd);

void UpdateTrayTip(HWND hwnd)
{
    NOTIFYICONDATAW nid = {0};
    nid.cbSize = sizeof(NOTIFYICONDATAW);
    nid.hWnd = hwnd;
    nid.uID = 1;
    nid.uFlags = NIF_TIP;

    time_t now = time(NULL);
    int secondsPassed = (int)difftime(now, g_tLastReminderTime);
    int minutes = secondsPassed / 60;
    int seconds = secondsPassed % 60;

    swprintf_s(nid.szTip, L"定时提醒助手\n已运行: %d分%d秒", minutes, seconds);
    Shell_NotifyIconW(NIM_MODIFY, &nid);
}

// WinMain函数
int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance,
    LPSTR lpCmdLine, int nCmdShow)
{
    // 初始化随机数种子
    srand((unsigned int)time(NULL));
    g_tLastReminderTime = time(NULL); // 记录启动时间

    WNDCLASSEXW wc = {0};
    WNDCLASSEXW wcConfig = {0};
    HWND hwnd;
    MSG Msg;

    wc.cbSize = sizeof(WNDCLASSEXW);
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.hIcon = LoadIconW(hInstance, MAKEINTRESOURCEW(IDI_APP_ICON));
    wc.hCursor = LoadCursorW(NULL, (LPCWSTR)IDC_ARROW);
    wc.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    wc.lpszClassName = g_szClassName;
    wc.hIconSm = LoadIconW(hInstance, MAKEINTRESOURCEW(IDI_APP_ICON));

    if (!RegisterClassExW(&wc)) return 0;

    wcConfig.cbSize = sizeof(WNDCLASSEXW);
    wcConfig.lpfnWndProc = ConfigWndProc;
    wcConfig.hInstance = hInstance;
    wcConfig.hIcon = LoadIconW(NULL, (LPCWSTR)IDI_APPLICATION);
    wcConfig.hCursor = LoadCursorW(NULL, (LPCWSTR)IDC_ARROW);
    wcConfig.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    wcConfig.lpszClassName = g_szConfigWindowClassName;
    wcConfig.hIconSm = LoadIconW(NULL, (LPCWSTR)IDI_APPLICATION);

    if (!RegisterClassExW(&wcConfig)) return 0;

    // 创建隐藏的主窗口
    hwnd = CreateWindowExW(0, g_szClassName, g_szWindowName, 0, 0, 0, 0, 0, NULL, NULL, hInstance, NULL);
    if (hwnd == NULL) return 0;

    LoadConfig();
    AddTrayIcon(hwnd);
    UpdateTimer(hwnd);

    // 每秒更新一次托盘悬停提示
    SetTimer(hwnd, g_nStatusTimerId, 1000, NULL);

    // 启动通知
    ShowNotification(hwnd);

    while (GetMessageW(&Msg, NULL, 0, 0) > 0)
    {
        TranslateMessage(&Msg);
        DispatchMessageW(&Msg);
    }

    return (int)Msg.wParam;
}

LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    static HINSTANCE hInstance;

    switch (msg)
    {
    case WM_CREATE:
        hInstance = ((LPCREATESTRUCT)lParam)->hInstance;
        break;
    case WM_TIMER:
        if (wParam == g_nTimerId)
        {
            ShowNotification(hwnd);
        }
        else if (wParam == g_nStatusTimerId)
        {
            // 更新悬停提示
            UpdateTrayTip(hwnd);
        }
        break;
    case WM_TRAYICON:
        if (LOWORD(lParam) == WM_RBUTTONUP)
        {
            POINT curPoint;
            GetCursorPos(&curPoint);
            HMENU hMenu = CreatePopupMenu();
            InsertMenuW(hMenu, 0, MF_BYPOSITION | MF_STRING, ID_TRAY_SETTINGS, L"设置间隔");
            InsertMenuW(hMenu, 1, MF_BYPOSITION | MF_STRING, ID_TRAY_EXIT, L"退出程序");

            SetForegroundWindow(hwnd);
            TrackPopupMenu(hMenu, TPM_LEFTALIGN | TPM_RIGHTBUTTON, curPoint.x, curPoint.y, 0, hwnd, NULL);
            DestroyMenu(hMenu);
        }
        else if (LOWORD(lParam) == WM_LBUTTONDBLCLK)
        {
            if (g_hConfigWnd != NULL && IsWindow(g_hConfigWnd))
            {
                SetForegroundWindow(g_hConfigWnd);
            }
            else
            {
                g_hConfigWnd = CreateConfigWindow(hInstance, hwnd);
            }
        }
        break;
    case WM_COMMAND:
        if (LOWORD(wParam) == ID_TRAY_EXIT)
        {
            DestroyWindow(hwnd);
        }
        else if (LOWORD(wParam) == ID_TRAY_SETTINGS)
        {
            if (g_hConfigWnd != NULL && IsWindow(g_hConfigWnd))
            {
                SetForegroundWindow(g_hConfigWnd);
            }
            else
            {
                g_hConfigWnd = CreateConfigWindow(hInstance, hwnd);
            }
        }
        break;
    case WM_DESTROY:
        RemoveTrayIcon(hwnd);
        KillTimer(hwnd, g_nTimerId);
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }
    return 0;
}

LRESULT CALLBACK ConfigWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    static HWND hEditInterval;
    static HINSTANCE hInst;

    switch (msg)
    {
    case WM_CREATE:
        hInst = ((LPCREATESTRUCT)lParam)->hInstance;
        CreateWindowW(L"STATIC", L"提醒间隔（分钟）:", WS_CHILD | WS_VISIBLE, 10, 10, 150, 20, hwnd, NULL, hInst, NULL);
        hEditInterval = CreateWindowW(L"EDIT", NULL, WS_CHILD | WS_VISIBLE | WS_BORDER | ES_NUMBER, 10, 40, 100, 20, hwnd, (HMENU)IDC_EDIT_INTERVAL, hInst, NULL);

        {
            std::wstringstream ss;
            ss << g_nReminderIntervalMinutes;
            SetWindowTextW(hEditInterval, ss.str().c_str());
        }
        CreateWindowW(L"BUTTON", L"保存并应用", WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON, 10, 70, 100, 30, hwnd, (HMENU)IDC_BUTTON_SAVE, hInst, NULL);
        break;
    case WM_COMMAND:
        if (LOWORD(wParam) == IDC_BUTTON_SAVE)
        {
            WCHAR szInterval[16];
            GetWindowTextW(hEditInterval, szInterval, 16);
            try {
                int newInterval = std::stoi(szInterval);
                if (newInterval > 0) {
                    g_nReminderIntervalMinutes = newInterval;
                    SaveConfig();
                    UpdateTimer(GetParent(hwnd));
                    MessageBoxW(hwnd, L"设置已成功保存！", L"提示", MB_ICONINFORMATION | MB_OK);
                    DestroyWindow(hwnd);
                } else {
                    MessageBoxW(hwnd, L"请输入一个有效的正整数！", L"错误", MB_ICONERROR | MB_OK);
                }
            } catch (...) {
                MessageBoxW(hwnd, L"输入无效！", L"错误", MB_ICONERROR | MB_OK);
            }
        }
        break;
    case WM_CLOSE:
        g_hConfigWnd = NULL;
        DestroyWindow(hwnd);
        break;
    default:
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }
    return 0;
}

void AddTrayIcon(HWND hwnd)
{
    NOTIFYICONDATAW nid = {0};
    nid.cbSize = sizeof(NOTIFYICONDATAW);
    nid.hWnd = hwnd;
    nid.uID = 1;
    nid.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
    nid.uCallbackMessage = WM_TRAYICON;
    nid.hIcon = LoadIconW(GetModuleHandle(NULL), MAKEINTRESOURCEW(IDI_APP_ICON));
    wcscpy_s(nid.szTip, L"定时提醒助手");
    Shell_NotifyIconW(NIM_ADD, &nid);
}

void RemoveTrayIcon(HWND hwnd)
{
    NOTIFYICONDATAW nid = {0};
    nid.cbSize = sizeof(NOTIFYICONDATAW);
    nid.hWnd = hwnd;
    nid.uID = 1;
    Shell_NotifyIconW(NIM_DELETE, &nid);
}

void ShowNotification(HWND hwnd)
{
    // 随机选择一条提醒语
    int index = rand() % (sizeof(g_szReminders) / sizeof(g_szReminders[0]));
    const WCHAR* message = g_szReminders[index];

    // 计算已经过了多久
    time_t now = time(NULL);
    std::wstring fullMessage = message;
    if (g_tLastReminderTime != 0) {
        int secondsPassed = (int)difftime(now, g_tLastReminderTime);
        int minutes = secondsPassed / 60;
        int seconds = secondsPassed % 60;

        std::wstringstream ss;
        ss << L"\n(距离上次提醒已过去: " << minutes << L"分" << seconds << L"秒)";
        fullMessage += ss.str();
    }
    g_tLastReminderTime = now;

    NOTIFYICONDATAW nid = {0};
    nid.cbSize = sizeof(NOTIFYICONDATAW);
    nid.hWnd = hwnd;
    nid.uID = 1;
    nid.uFlags = NIF_INFO;
    wcscpy_s(nid.szInfo, fullMessage.c_str());
    wcscpy_s(nid.szInfoTitle, g_szWindowName);
    nid.dwInfoFlags = NIIF_INFO;
    Shell_NotifyIconW(NIM_MODIFY, &nid);
}

HWND CreateConfigWindow(HINSTANCE hInstance, HWND hParent)
{
    HWND hwnd = CreateWindowExW(0, g_szConfigWindowClassName, L"助手设置", WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_VISIBLE, CW_USEDEFAULT, CW_USEDEFAULT, 250, 160, hParent, NULL, hInstance, NULL);
    if (hwnd) SetForegroundWindow(hwnd);
    return hwnd;
}

void LoadConfig()
{
    g_nReminderIntervalMinutes = GetPrivateProfileIntW(L"Settings", L"IntervalMinutes", 30, g_szConfigFileName);
}

void SaveConfig()
{
    WCHAR szInterval[16];
    swprintf_s(szInterval, L"%d", g_nReminderIntervalMinutes);
    WritePrivateProfileStringW(L"Settings", L"IntervalMinutes", szInterval, g_szConfigFileName);
}

void UpdateTimer(HWND hwnd)
{
    KillTimer(hwnd, g_nTimerId);
    SetTimer(hwnd, g_nTimerId, (UINT)g_nReminderIntervalMinutes * 60 * 1000, NULL);
}
