using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;

namespace RemoteSystemWpf.Helpers
{
    public static class DownloadHelper
    {
        private static readonly HttpClient _client = new HttpClient();

        private static readonly string RootInstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "RemoteSystem");

        public static async Task<bool> DownloadFFmpeg(IProgress<string> progress = null)
        {
            try
            {
                string ffmpegDir = Path.Combine(RootInstallDir, "ffmpeg");
                if (File.Exists(Path.Combine(ffmpegDir, "ffmpeg.exe"))) { progress?.Report("✅ FFmpeg готов"); return true; }

                string url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
                string zipPath = Path.Combine(Path.GetTempPath(), "ffmpeg_temp.zip");

                await DownloadFile(url, zipPath, "FFmpeg", progress);
                progress?.Report("📦 Распаковка FFmpeg...");

                await Task.Run(() =>
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "temp_ff");
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                    ZipFile.ExtractToDirectory(zipPath, tempDir);

                    var exeFile = Directory.GetFiles(tempDir, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (exeFile != null)
                    {
                        Directory.CreateDirectory(ffmpegDir);
                        File.Copy(exeFile, Path.Combine(ffmpegDir, "ffmpeg.exe"), true);
                    }
                    Directory.Delete(tempDir, true);
                });

                if (File.Exists(zipPath)) File.Delete(zipPath);
                return true;
            }
            catch (Exception ex) { progress?.Report($"❌ FFmpeg: {ex.Message}"); throw; }
        }

        public static async Task<bool> DownloadMediaMTX(IProgress<string> progress = null)
        {
            try
            {
                string mtxDir = Path.Combine(RootInstallDir, "MediaMTX");
                if (File.Exists(Path.Combine(mtxDir, "mediamtx.exe"))) { progress?.Report("✅ MediaMTX готов"); return true; }

                string url = "https://github.com/bluenviron/mediamtx/releases/download/v1.16.3/mediamtx_v1.16.3_windows_amd64.zip";
                string zipPath = Path.Combine(Path.GetTempPath(), "mtx_temp.zip");

                await DownloadFile(url, zipPath, "MediaMTX", progress);
                progress?.Report("📦 Распаковка MediaMTX...");

                Directory.CreateDirectory(mtxDir);
                await Task.Run(() =>
                {
                    using (var archive = ZipFile.OpenRead(zipPath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            string fullPath = Path.Combine(mtxDir, entry.FullName);
                            if (string.IsNullOrEmpty(entry.Name)) Directory.CreateDirectory(fullPath);
                            else entry.ExtractToFile(fullPath, true);
                        }
                    }
                });

                if (File.Exists(zipPath)) File.Delete(zipPath);
                return true;
            }
            catch (Exception ex) { progress?.Report($"❌ MediaMTX: {ex.Message}"); throw; }
        }

        public static async Task<bool> DownloadVLC(IProgress<string> progress = null)
        {
            try
            {
                string vlcDir = Path.Combine(RootInstallDir, "libvlc", "win-x64");
                if (File.Exists(Path.Combine(vlcDir, "libvlc.dll"))) { progress?.Report("✅ VLC готов"); return true; }

                string url = "https://get.videolan.org/vlc/3.0.11/win64/vlc-3.0.11-win64.zip";
                string zipPath = Path.Combine(Path.GetTempPath(), "vlc_temp.zip");

                await DownloadFile(url, zipPath, "VLC (100MB)", progress);
                progress?.Report("📦 Распаковка библиотек VLC...");

                await Task.Run(() =>
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "temp_vlc");
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                    ZipFile.ExtractToDirectory(zipPath, tempDir);

                    string innerPath = Directory.GetDirectories(tempDir).FirstOrDefault();
                    if (innerPath != null)
                    {
                        Directory.CreateDirectory(vlcDir);
                        File.Copy(Path.Combine(innerPath, "libvlc.dll"), Path.Combine(vlcDir, "libvlc.dll"), true);
                        File.Copy(Path.Combine(innerPath, "libvlccore.dll"), Path.Combine(vlcDir, "libvlccore.dll"), true);
                        CopyDir(Path.Combine(innerPath, "plugins"), Path.Combine(vlcDir, "plugins"));
                    }
                    Directory.Delete(tempDir, true);
                });

                if (File.Exists(zipPath)) File.Delete(zipPath);
                return true;
            }
            catch (Exception ex) { progress?.Report($"❌ VLC: {ex.Message}"); throw; }
        }

        public static async Task<bool> InstallTailscale(IProgress<string> progress = null)
        {
            try
            {
                string tsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale-ipn.exe");
                if (File.Exists(tsPath)) { progress?.Report("✅ Tailscale уже установлен"); return true; }

                string url = "https://pkgs.tailscale.com/stable/tailscale-setup-latest.exe";
                string exePath = Path.Combine(Path.GetTempPath(), "tailscale_setup.exe");

                await DownloadFile(url, exePath, "Tailscale", progress);
                progress?.Report("⚙️ Установка Tailscale (может потребоваться подтверждение)...");

                await Task.Run(() =>
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "/quiet",
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    var process = Process.Start(startInfo);
                    process?.WaitForExit();
                });

                if (File.Exists(exePath)) File.Delete(exePath);
                return true;
            }
            catch (Exception ex) { progress?.Report($"⚠️ Tailscale: {ex.Message}"); return false; }
        }

        private static async Task DownloadFile(string url, string path, string name, IProgress<string> progress)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var resp = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? -1L;
                using (var s = await resp.Content.ReadAsStreamAsync())
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    byte[] buffer = new byte[8192];
                    long read = 0;
                    int chunk;
                    while ((chunk = await s.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, chunk);
                        read += chunk;
                        if (total > 0) progress?.Report($"Загрузка {name}: {(int)((double)read / total * 100)}%");
                    }
                }
            }
        }

        private static void CopyDir(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dest, Path.GetFileName(d)));
        }
    }
}