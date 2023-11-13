using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using static Windows.Win32.PInvoke;

namespace ACTD.Native.Windows
{
    public class NotifyOnlyWindow(string title)
    {
        private const string WindowClassName = "ACTD_Windows_NotifyOnlyWindow";
        private const uint WmUserShellIcon = WM_USER + 1;
        private const nuint OpenButtonId = WM_USER + 1000;
        private const nuint ExitButtonId = WM_USER + 9999;

        private HINSTANCE _hInstance;
        private HWND _hWnd;

        public string Title { get; } = title;

        public void Start()
        {
            RegisterWindowClass();
            InitInstance();
            ShowNotify();

            while (GetMessage(out var msg, default, 0, 0))
            {
                TranslateMessage(msg);
                DispatchMessage(msg);
            }
        }

        private unsafe void ShowNotify()
        {
            PCWSTR szIconName = new((char*)IDI_APPLICATION);

            var ntfd = default(NOTIFYICONDATAW);
            ntfd.hWnd = _hWnd;
            ntfd.cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>();
            ntfd.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP |
                          NOTIFY_ICON_DATA_FLAGS.NIF_GUID | NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE |
                          NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP;
            ntfd.guidItem = Guid.NewGuid();
            ntfd.hIcon = LoadIcon(_hInstance, szIconName);
            ntfd.szTip = Title;
            ntfd.uCallbackMessage = WmUserShellIcon;
            Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, ntfd);
        }

        private void RegisterWindowClass()
        {
            unsafe
            {
                fixed (char* szClassName = WindowClassName)
                {
                    var wcex = default(WNDCLASSEXW);
                    PCWSTR szNull = default;
                    wcex.cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>();
                    wcex.lpfnWndProc = WndProc;
                    wcex.cbClsExtra = 0;
                    wcex.hInstance = GetModuleHandle(szNull);
                    wcex.hCursor = LoadCursor(wcex.hInstance, IDC_ARROW);
                    wcex.hIcon = LoadIcon(wcex.hInstance, IDI_APPLICATION);
                    wcex.hbrBackground = new HBRUSH(new IntPtr(6));
                    wcex.lpszClassName = szClassName;
                    RegisterClassEx(wcex);
                    _hInstance = wcex.hInstance;
                }
            }
        }

        private void InitInstance()
        {
            unsafe
            {
                _hWnd =
                    CreateWindowEx(
                        0,
                        WindowClassName,
                        Title,
                        WINDOW_STYLE.WS_OVERLAPPEDWINDOW,
                        CW_USEDEFAULT,
                        0,
                        1,
                        1,
                        default,
                        default,
                        default,
                        null);
            }

            ShowWindow(_hWnd, SHOW_WINDOW_CMD.SW_HIDE);
            UpdateWindow(_hWnd);
        }

        private LRESULT WndProc(HWND hwnd, uint message, WPARAM wparam, LPARAM lparam)
        {
            switch (message)
            {
                case WM_CREATE:
                    break;

                case WM_COMMAND:
                    var cmdid = wparam.Value & 0xFFFF;
                    switch (cmdid)
                    {
                        case OpenButtonId:
                            ShellExecute(HWND.Null, null, "https://ac.ctrlcv.cc", null, null, SHOW_WINDOW_CMD.SW_SHOW);
                            break;
                        case ExitButtonId:
                            if (MessageBox(hwnd, "确认退出？", Title, MESSAGEBOX_STYLE.MB_OKCANCEL) ==
                                MESSAGEBOX_RESULT.IDOK)
                            {
                                DestroyWindow(hwnd);
                                return new LRESULT(0);
                            }

                            break;
                        default:
                            return DefWindowProc(hwnd, message, wparam, lparam);
                    }

                    break;

                case WM_PAINT:
                    BeginPaint(hwnd, out var ps);

                    // More paint code would go here...
                    EndPaint(hwnd, ps);
                    break;

                case WM_CLOSE:
                    DestroyWindow(hwnd);
                    break;

                case WM_DESTROY:
                    // Save config files and terminate application
                    PostQuitMessage(0);
                    break;

                case WM_QUERYENDSESSION:
                    ShutdownBlockReasonCreate(hwnd, "正在保存文件并关闭");
                    return new LRESULT(0);

                case WM_ENDSESSION:
                    // Save config files and terminate application
                    ShutdownBlockReasonDestroy(hwnd);
                    return new LRESULT(1);

                case WmUserShellIcon:
                    var k = (uint)lparam.Value & 0xFFFF;
                    switch (k)
                    {
                        case WM_LBUTTONUP:
                            break;
                        case WM_RBUTTONUP:
                            var menu = CreatePopupMenu_SafeHandle();
                            if (menu != null)
                            {
                                AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, OpenButtonId, "打开");
                                AppendMenu(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, UIntPtr.Zero, string.Empty);
                                AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, ExitButtonId, "退出");

                                if (GetCursorPos(out var point))
                                {
                                    SetForegroundWindow(hwnd);
                                    TrackPopupMenu(menu, TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON, point.X, point.Y, hwnd,
                                        null);
                                    menu.Dispose();
                                }
                                else
                                {
                                    var error = Marshal.GetLastWin32Error();
                                }
                            }

                            break;
                        default:
                            return DefWindowProc(hwnd, message, wparam, lparam);
                    }

                    break;

                case WM_QUIT:
                    break;
                default:
                    return DefWindowProc(hwnd, message, wparam, lparam);
            }

            return new LRESULT(0);
        }
    }
}