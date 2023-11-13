using System.Drawing;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using ZXing.Windows.Compatibility;
using static Windows.Win32.PInvoke;

namespace ACTD.Native.Windows
{
    public readonly record struct ObsWebSocketConfig(string Address, string Port, string Password);

    public static class ObsWebSocket
    {
        private const string RootDirectory = "obs-studio";
        private const string FileName = "global.ini";
        private const string Section = "OBSWebSocket";
        private const string PortKey = "ServerPort";
        private const string PasswordKey = "ServerPassword";

        public static unsafe ObsWebSocketConfig? GetConfigByIni()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), RootDirectory,
                FileName);
            if (!File.Exists(path)) return null;

            Span<char> portBuffer = stackalloc char[5];
            Span<char> passwordBuffer = stackalloc char[31];

            fixed (char* pPortBuffer = portBuffer, pPasswordBuffer = passwordBuffer)
            {
                var len = GetPrivateProfileString(
                    Section,
                    PortKey,
                    string.Empty,
                    pPortBuffer,
                    (uint)portBuffer.Length,
                    path
                );

                var port = Marshal.PtrToStringAuto((IntPtr)pPortBuffer, (int)len)!;

                len = GetPrivateProfileString(
                    Section,
                    PasswordKey,
                    string.Empty,
                    pPasswordBuffer,
                    (uint)passwordBuffer.Length,
                    path
                );
                var password = Marshal.PtrToStringAuto((IntPtr)pPasswordBuffer, (int)len)!;

                return new ObsWebSocketConfig(GetPreferredAddress(), port, password);
            }
        }

        private const string ObsWebSocketScheme = "obsws";
        private const string WindowTitle = "WebSocket 连接信息";

        public static unsafe ObsWebSocketConfig? GetConfigByWindow()
        {
            HWND hWnd = default;
            EnumWindows((hWndParent, _) =>
            {
                Span<char> title = stackalloc char[128];
                fixed (char* pTitle = title)
                {
                    var len = GetWindowText(hWndParent, pTitle, title.Length);
                    if (string.Compare(WindowTitle, title[..len].ToString(), StringComparison.Ordinal) != 0)
                        return true;

                    hWnd = hWndParent;
                    return false;
                }
            }, 0);
            if (hWnd.IsNull) return null;

            if (!GetWindowRect(hWnd, out var rect)) return null;
            var bound = new Rectangle(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
            var bitmap = new Bitmap(bound.Width, bound.Height);
            using var g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(new Point(rect.left, rect.top), Point.Empty, bound.Size);

            var reader = new BarcodeReader();
            var result = reader.Decode(bitmap);

            if (result == null) return null;

            return Uri.TryCreate(result.Text, UriKind.Absolute, out var uri) && uri.Scheme == ObsWebSocketScheme
                ? new ObsWebSocketConfig(uri.Host, uri.Port.ToString(), uri.LocalPath[1..])
                : null;
        }

        private static string GetPreferredAddress()
        {
            var preferredAddresses = new SortedSet<(uint, string)>(new ByPriority());
            foreach (var @interface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (@interface.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet
                    or NetworkInterfaceType.Wireless80211
                    or NetworkInterfaceType.GigabitEthernet)) continue;
                var ipProps = @interface.GetIPProperties();

                foreach (var addresses in ipProps.UnicastAddresses)
                {
                    if (addresses.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                    if (addresses.Address.Equals(IPAddress.Loopback)) continue;
                    var address = addresses.Address.ToString();
                    if (address.StartsWith("192.168.0.") || address.StartsWith("192.168.1."))
                    {
                        preferredAddresses.Add((0, address));
                    }

                    else if (address.StartsWith("192.168."))
                    {
                        preferredAddresses.Add((address.StartsWith("192.168.56.") ? 255u : 1u, address));
                    }
                    else if (address.StartsWith("172.16."))
                    {
                        preferredAddresses.Add((1, address));
                    }
                    else if (address.StartsWith("10."))
                    {
                        preferredAddresses.Add((2, address));
                    }
                    else
                    {
                        preferredAddresses.Add((255, address));
                    }
                }
            }

            return preferredAddresses.First().Item2;
        }

        private class ByPriority : IComparer<(uint, string)>
        {
            public int Compare((uint, string) x, (uint, string) y)
            {
                return x.Item1.CompareTo(y.Item1);
            }
        }
    }
}