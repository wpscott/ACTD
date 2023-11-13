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

        private static readonly HashSet<string> PossibleWindowTitles = new()
        {
            "معلومات اتصال WebSocket",
            "Informació de connexió del servidor WebSocket",
            "Připojení k WebSocket serveru",
            "WebSocket-forbindelsesinfo",
            "WebSocket-Verbindungsinformationen",
            "Πληροφορίες Σύνδεσης WebSocket",
            "WebSocket Connect Info",
            "Información de conexión de WebSocket",
            "WebSocket'i ühenduse info",
            "WebSocket konexio-informazioa",
            "اطلاعات اتصال سوکت وب",
            "WebSocket-yhteystiedot",
            "Impormasyon ng WebSocket Connect",
            "Informations de connexion WebSocket",
            "מידע חיבור WebSocket",
            "WebSocket कनेक्ट जानकारी दिखाएं",
            "WebSocket kapcsolati információk",
            "WebSocket կապի մանրամասները",
            "Informasi Koneksi WebSocket",
            "Informazioni sulla connessione WebSocket",
            "WebSocket接続情報",
            "WebSocket-კავშირის შესახებ",
            "Zanyariyên girêdanê WebSocket",
            "WebSocket 연결 정보",
            "Maklumat Sambungan WebSocket",
            "WebSocket verbindingsinformatie",
            "Informacje o połączeniu WebSocket",
            "Informação de Conexão WebSocket",
            "Informação de ligação WebSocket",
            "Informațiile conexiunii WebSocket",
            "Сведения о подключении WebSocket",
            "Info WebSocket pripojenia",
            "Podatki o povezavi WebSocket",
            "Anslutningsinfo för WebSocket",
            "WebSocket Bağlanma Bilgileri",
            "Відомості про з'єднання WebSocket",
            "Thông tin kết nối WebSocket",
            "WebSocket 连接信息",
            "WebSocket 連線資訊",
        };

        public static unsafe ObsWebSocketConfig? GetConfigByWindow()
        {
            HWND hWnd = default;
            EnumWindows((hWndParent, _) =>
            {
                Span<char> title = stackalloc char[128];
                fixed (char* pTitle = title)
                {
                    var len = GetWindowText(hWndParent, pTitle, title.Length);
                    if (!PossibleWindowTitles.Contains(title[..len].ToString()))
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