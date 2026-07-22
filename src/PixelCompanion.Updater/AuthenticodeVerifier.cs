using System.Runtime.InteropServices;

namespace PixelCompanion.Updater;

internal static class AuthenticodeVerifier
{
    private static readonly Guid VerifyAction = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    public static bool IsTrusted(string path)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var fileInfo = new WinTrustFileInfo(path);
        var fileInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        try
        {
            Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);
            var trustData = new WinTrustData(fileInfoPointer);
            return WinVerifyTrust(IntPtr.Zero, VerifyAction, ref trustData) == 0;
        }
        finally
        {
            Marshal.FreeHGlobal(fileInfoPointer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo(string path)
    {
        public uint StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>();
        public string FilePath = path;
        public IntPtr FileHandle = IntPtr.Zero;
        public IntPtr KnownSubject = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustData(IntPtr fileInfo)
    {
        public uint StructSize = (uint)Marshal.SizeOf<WinTrustData>();
        public IntPtr PolicyCallbackData = IntPtr.Zero;
        public IntPtr SipClientData = IntPtr.Zero;
        public uint UiChoice = 2;
        public uint RevocationChecks = 0;
        public uint UnionChoice = 1;
        public IntPtr FileInfo = fileInfo;
        public uint StateAction = 0;
        public IntPtr StateData = IntPtr.Zero;
        public IntPtr UrlReference = IntPtr.Zero;
        public uint ProviderFlags = 0;
        public uint UiContext = 0;
        public IntPtr SignatureSettings = IntPtr.Zero;
    }

    [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int WinVerifyTrust(IntPtr window, [MarshalAs(UnmanagedType.LPStruct)] Guid action, ref WinTrustData data);
}
