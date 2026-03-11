using System;
using System.IO;

namespace RemoteSystemWpf.Classes
{
    public static class PathsConfig
    {
        public static readonly string Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "RemoteSystem");

        public static string FFmpegExe => Path.Combine(Root, "ffmpeg", "ffmpeg.exe");
        public static string MediaMTXDir => Path.Combine(Root, "MediaMTX");
        public static string MediaMTXExe => Path.Combine(MediaMTXDir, "mediamtx.exe");
        public static string VlcLibDir => Path.Combine(Root, "libvlc", "win-x64");

        public static string TailscaleExe => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Tailscale", "tailscale-ipn.exe");
    }
}