using ClipCore.Assets.Pages;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using Windows.UI.ApplicationSettings;

namespace ClipCore.Assets.Functions
{
    internal class TrayIconManager
    {
        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 1;

        // Mouse messages
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_LBUTTONDBLCLK = 0x0203;

        private IntPtr _hWnd;
        private NotifyIconData _notifyIconData;
        private IntPtr _customIcon = IntPtr.Zero;
        private Window? _window;
        private SUBCLASSPROC? _wndProcDelegate; 
        private bool _isWindowVisible = false;
        private LocalizationManager? _localizationManager;

        // Win32 API Imports
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(NotifyIconMessage dwMessage, ref NotifyIconData lpData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadImage(IntPtr hInstance, string lpName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("comctl32.dll")]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll")]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, IntPtr uIdSubclass);

        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string? lpNewItem);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        // Constants
        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_LEFTALIGN = 0x0000;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint MF_STRING = 0x0000;
        private const uint MF_SEPARATOR = 0x0800; 
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE = 0xF020;

        // Menu IDs
        private const int MENU_SHOW = 1001;
        private const int MENU_MINIMIZE = 1002;
        private const int MENU_EXIT = 1003;

        private const int HOME_PAGE = 2001;
        private const int FAVORITES_PAGE = 2003;
        private const int SETTINGS_PAGE = 2002;

        private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NotifyIconData
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public NotifyIconFlags uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [Flags]
        private enum NotifyIconFlags
        {
            Message = 0x01,
            Icon = 0x02,
            Tip = 0x04
        }

        private enum NotifyIconMessage
        {
            Add = 0x00,
            Modify = 0x01,
            Delete = 0x02
        }

        public void InitializeTrayIcon(Window window, string? iconPath = null, string tooltipText = "ClipCore")
        {
            _window = window;
            _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

            _localizationManager = LocalizationManager.Instance;
            _localizationManager.LanguageChanged += OnLanguageChanged;

            // Load icon
            if (!string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(iconPath))
            {
                _customIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE);
            }

            if (_customIcon == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("Using default application icon.");
                _customIcon = LoadIcon(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION
            }

            // Setup notify icon
            _notifyIconData = new NotifyIconData
            {
                cbSize = Marshal.SizeOf(typeof(NotifyIconData)),
                hWnd = _hWnd,
                uID = 1,
                uFlags = NotifyIconFlags.Message | NotifyIconFlags.Icon | NotifyIconFlags.Tip,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _customIcon,
                szTip = tooltipText
            };

            bool result = Shell_NotifyIcon(NotifyIconMessage.Add, ref _notifyIconData);
            if (!result)
            {
                System.Diagnostics.Debug.WriteLine("Failed to add tray icon.");
                return;
            }

            // Subclass window to handle tray icon messages
            _wndProcDelegate = new SUBCLASSPROC(WndProc);
            SetWindowSubclass(_hWnd, _wndProcDelegate, IntPtr.Zero, IntPtr.Zero);
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            if (_localizationManager == null)
                return;
            UpdateTooltip(_localizationManager.Get("AppName"));
        }

        private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == WM_SYSCOMMAND)
            {
                int command = (int)wParam & 0xFFF0;
                if (command == SC_MINIMIZE)
                {
                    _window?.DispatcherQueue.TryEnqueue(() =>
                    {
                        _window.AppWindow.Hide();
                        _isWindowVisible = false;
                    });
                    return IntPtr.Zero; // Minimize işlemini engelle ve gizle
                }
            }

            if (uMsg == WM_TRAYICON)
            {
                int message = (int)lParam & 0xFFFF;

                switch (message)
                {
                    case WM_LBUTTONUP:
                        ToggleWindow();
                        break;

                    case WM_RBUTTONUP:
                        ShowContextMenu();
                        break;
                }
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private void ToggleWindow()
        {
            if (_isWindowVisible)
            {
                MinimizeWindow();
            }
            else
            {
                ShowWindow();
            }
        }

        private void ShowContextMenu()
        {
            IntPtr hMenu = CreatePopupMenu(); 
            var loc = _localizationManager;

            // Sadece uygun olanı göster
            if (_isWindowVisible)
            {
                AppendMenu(hMenu, MF_STRING, (IntPtr)MENU_MINIMIZE, loc?.Get("Hide"));
            }
            else
            {
                AppendMenu(hMenu, MF_STRING, (IntPtr)MENU_SHOW, loc?.Get("Show"));
            }

            AppendMenu(hMenu, MF_SEPARATOR, IntPtr.Zero, null);
            AppendMenu(hMenu, MF_STRING, (IntPtr)HOME_PAGE, loc?.Get("Homepage"));
            AppendMenu(hMenu, MF_STRING, (IntPtr)FAVORITES_PAGE, loc?.Get("Favorites"));
            AppendMenu(hMenu, MF_STRING, (IntPtr)SETTINGS_PAGE, loc?.Get("Settings"));

            AppendMenu(hMenu, MF_SEPARATOR, IntPtr.Zero, null);
            AppendMenu(hMenu, MF_STRING, (IntPtr)MENU_EXIT, loc?.Get("Exit"));

            GetCursorPos(out POINT pt);
            SetForegroundWindow(_hWnd);
            uint cmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_LEFTALIGN | TPM_RIGHTBUTTON, pt.X, pt.Y, _hWnd, IntPtr.Zero);
            DestroyMenu(hMenu);

            switch (cmd)
            {
                case MENU_SHOW:
                    ShowWindow();
                    break;
                case HOME_PAGE:
                    if (!_isWindowVisible)
                        ShowWindow();
                    if (_window is ClipCoreWindow clipCoreWindow)
                    {
                        ClipCoreWindow.HomepageSwitch(clipCoreWindow);
                    }
                    break;
                case FAVORITES_PAGE:
                    if (!_isWindowVisible)
                        ShowWindow();
                    if (_window is ClipCoreWindow clipCoreWindow2)
                    {
                        ClipCoreWindow.FavoritesPageSwitch(clipCoreWindow2);
                    }
                    break;
                case SETTINGS_PAGE:
                    if (!_isWindowVisible)
                        ShowWindow();
                    if (_window is ClipCoreWindow clipCoreWindow3)
                    {
                        ClipCoreWindow.SettingsPageSwitch(clipCoreWindow3);
                    }
                    break;
                case MENU_MINIMIZE:
                    MinimizeWindow();
                    break;
                case MENU_EXIT:
                    ExitApplication();
                    break;
            }
        }

        private void ShowWindow()
        {
            _window?.DispatcherQueue.TryEnqueue(() =>
            {
                var appWindow = _window.AppWindow;
                if (!appWindow.IsVisible)
                {
                    appWindow.Show();
                }
                SetForegroundWindow(_hWnd); 
                _isWindowVisible = true;
            });
        }

        private void MinimizeWindow()
        {
            _window?.DispatcherQueue.TryEnqueue(() =>
            {
                _window.AppWindow.Hide(); 
                _isWindowVisible = false;
            });
        }

        private void ExitApplication()
        {
            _window?.DispatcherQueue.TryEnqueue(() =>
            {
                RemoveTrayIcon();
                Application.Current.Exit();
            });
        }

        public void UpdateIcon(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath) || !System.IO.File.Exists(iconPath))
                return;

            IntPtr newIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE);
            if (newIcon == IntPtr.Zero)
                return;

            // Destroy old icon if it was custom loaded
            if (_customIcon != IntPtr.Zero)
            {
                DestroyIcon(_customIcon);
            }

            _customIcon = newIcon;
            _notifyIconData.hIcon = _customIcon;
            Shell_NotifyIcon(NotifyIconMessage.Modify, ref _notifyIconData);
        }

        public void UpdateTooltip(string tooltip)
        {
            _notifyIconData.szTip = tooltip;
            Shell_NotifyIcon(NotifyIconMessage.Modify, ref _notifyIconData);
        }

        public void RemoveTrayIcon()
        {
            if (_hWnd != IntPtr.Zero)
            {
                if (_localizationManager != null)
                {
                    _localizationManager.LanguageChanged -= OnLanguageChanged;
                }

                Shell_NotifyIcon(NotifyIconMessage.Delete, ref _notifyIconData);

                if (_wndProcDelegate != null)
                {
                    RemoveWindowSubclass(_hWnd, _wndProcDelegate, IntPtr.Zero);
                }

                if (_customIcon != IntPtr.Zero)
                {
                    DestroyIcon(_customIcon);
                    _customIcon = IntPtr.Zero;
                }
            }
        }
    }
}