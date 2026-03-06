using System;
using System.IO;
using System.Net.Http;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Linq;

namespace RemoteSystemWpf.Helpers
{
    public static class DownloadHelper
    {
        private static readonly HttpClient _client = new HttpClient();
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

        public static async Task<bool> DownloadFFmpeg(IProgress<string> progress = null)
        {
            try
            {
                string ffmpegDir = Path.Combine(BaseDir, "ffmpeg");
                string ffmpegExe = Path.Combine(ffmpegDir, "ffmpeg.exe");
                if (File.Exists(ffmpegExe)) { progress?.Report("✅ FFmpeg готов"); return true; }

                string url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
                string zipPath = Path.Combine(BaseDir, "ffmpeg_temp.zip");

                await DownloadFile(url, zipPath, "FFmpeg", progress);
                progress?.Report("📦 Распаковка FFmpeg...");

                await Task.Run(() => {
                    var oldDirs = Directory.GetDirectories(BaseDir, "ffmpeg-*");
                    foreach (var d in oldDirs) try { Directory.Delete(d, true); } catch { }

                    ZipFile.ExtractToDirectory(zipPath, BaseDir);

                    var extractedDir = Directory.GetDirectories(BaseDir, "ffmpeg-*").FirstOrDefault();
                    if (extractedDir != null)
                    {
                        Directory.CreateDirectory(ffmpegDir);
                        string source = Path.Combine(extractedDir, "bin", "ffmpeg.exe");
                        if (File.Exists(source)) File.Copy(source, ffmpegExe, true);
                        try { Directory.Delete(extractedDir, true); } catch { }
                    }
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
                string mtxDir = Path.Combine(BaseDir, "MediaMTX");
                string mtxExe = Path.Combine(mtxDir, "mediamtx.exe");
                if (File.Exists(mtxExe)) { progress?.Report("✅ MediaMTX готов"); return true; }

                string url = "https://github.com/bluenviron/mediamtx/releases/download/v1.16.3/mediamtx_v1.16.3_windows_amd64.zip";
                string zipPath = Path.Combine(BaseDir, "mediamtx_temp.zip");

                await DownloadFile(url, zipPath, "MediaMTX", progress);
                progress?.Report("📦 Распаковка MediaMTX...");

                Directory.CreateDirectory(mtxDir);
                await Task.Run(() => {
                    using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string destPath = Path.Combine(mtxDir, entry.FullName);
                            if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destPath); continue; }
                            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                            entry.ExtractToFile(destPath, true);
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
                string vlcDir = Path.Combine(BaseDir, "libvlc", "win-x64");
                string vlcDll = Path.Combine(vlcDir, "libvlc.dll");
                if (File.Exists(vlcDll)) { progress?.Report("✅ VLC готов"); return true; }

                string url = "https://get.videolan.org/vlc/3.0.11/win64/vlc-3.0.11-win64.zip";
                string zipPath = Path.Combine(BaseDir, "vlc_temp.zip");

                await DownloadFile(url, zipPath, "VLC", progress);
                progress?.Report("📦 Распаковка VLC (100MB)...");

                await Task.Run(() => {
                    string extractPath = Path.Combine(BaseDir, "vlc-3.0.11");
                    if (Directory.Exists(extractPath)) try { Directory.Delete(extractPath, true); } catch { }

                    ZipFile.ExtractToDirectory(zipPath, BaseDir);

                    Directory.CreateDirectory(vlcDir);
                    File.Copy(Path.Combine(extractPath, "libvlc.dll"), Path.Combine(vlcDir, "libvlc.dll"), true);
                    File.Copy(Path.Combine(extractPath, "libvlccore.dll"), Path.Combine(vlcDir, "libvlccore.dll"), true);
                    CopyDirectory(Path.Combine(extractPath, "plugins"), Path.Combine(vlcDir, "plugins"));

                    try { Directory.Delete(extractPath, true); } catch { }
                });

                if (File.Exists(zipPath)) File.Delete(zipPath);
                return true;
            }
            catch (Exception ex) { progress?.Report($"❌ VLC: {ex.Message}"); throw; }
        }

        private static async Task DownloadFile(string url, string path, string name, IProgress<string> progress)
        {
            progress?.Report($"🌐 Скачивание {name}...");
            using (var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? -1L;
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    byte[] buffer = new byte[16384];
                    long readTotal = 0;
                    int read;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, read);
                        readTotal += read;
                        if (total > 0) progress?.Report($"Загрузка {name}: {(int)((double)readTotal / total * 100)}%");
                    }
                }
            }
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var f in Directory.GetFiles(source)) File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(source)) CopyDirectory(d, Path.Combine(dest, Path.GetFileName(d)));
        }
    }
}