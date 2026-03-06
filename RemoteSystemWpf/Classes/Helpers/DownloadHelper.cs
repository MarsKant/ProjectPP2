using System;
using System.IO;
using System.Net.Http;
using System.IO.Compression;
using System.Threading.Tasks;

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

                progress?.Report($"📁 Папка программы: {BaseDir}");

                Directory.CreateDirectory(ffmpegDir);

                if (File.Exists(ffmpegExe))
                {
                    FileInfo fi = new FileInfo(ffmpegExe);
                    progress?.Report($"✅ FFmpeg уже существует, размер: {fi.Length} байт");
                    return true;
                }

                string url = "https://github.com/ShareX/FFmpeg/releases/download/ffmpeg-8.0/ffmpeg-8.0-win-x64.zip";
                string zipPath = Path.Combine(BaseDir, "ffmpeg_temp.zip");

                progress?.Report("🌐 Скачивание FFmpeg...");

                using (var response = await _client.GetAsync(url))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        progress?.Report($"❌ Ошибка загрузки: {response.StatusCode}");
                        return false;
                    }

                    using (var fs = new FileStream(zipPath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                progress?.Report("📦 Распаковка...");
                ZipFile.ExtractToDirectory(zipPath, ffmpegDir);

                // Ищем ffmpeg.exe
                var files = Directory.GetFiles(ffmpegDir, "ffmpeg.exe", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    if (files[0] != ffmpegExe)
                    {
                        if (File.Exists(ffmpegExe))
                            File.Delete(ffmpegExe);
                        File.Move(files[0], ffmpegExe);
                    }

                    // Очищаем временные папки
                    foreach (var dir in Directory.GetDirectories(ffmpegDir))
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }

                    progress?.Report($"✅ FFmpeg готов");
                }
                else
                {
                    progress?.Report("❌ ffmpeg.exe не найден в архиве");
                }

                File.Delete(zipPath);
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"❌ Ошибка: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> DownloadMediaMTX(IProgress<string> progress = null)
        {
            try
            {
                string mtxDir = Path.Combine(BaseDir, "MediaMTX");
                string mtxExe = Path.Combine(mtxDir, "mediamtx.exe");

                progress?.Report($"📁 Папка MediaMTX: {mtxDir}");

                Directory.CreateDirectory(mtxDir);

                if (File.Exists(mtxExe))
                {
                    progress?.Report("✅ MediaMTX уже существует");
                    return true;
                }

                string url = "https://github.com/bluenviron/mediamtx/releases/download/v1.16.3/mediamtx_v1.16.3_windows_amd64.zip";
                string zipPath = Path.Combine(BaseDir, "mediamtx_temp.zip");

                progress?.Report("🌐 Скачивание MediaMTX...");

                using (var response = await _client.GetAsync(url))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        progress?.Report($"❌ Ошибка загрузки: {response.StatusCode}");
                        return false;
                    }

                    using (var fs = new FileStream(zipPath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                progress?.Report("📦 Распаковка...");
                ZipFile.ExtractToDirectory(zipPath, mtxDir);

                // Создаем конфиг если нет
                string configPath = Path.Combine(mtxDir, "mediamtx.yml");
                if (!File.Exists(configPath))
                {
                    string defaultConfig = @"rtspPort: 8554
rtmpPort: 1935
hlsPort: 8888
webrtcPort: 8889
";
                    File.WriteAllText(configPath, defaultConfig);
                }

                File.Delete(zipPath);
                progress?.Report("✅ MediaMTX готов");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"❌ Ошибка: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> DownloadAll(IProgress<string> progress = null)
        {
            bool ffmpegOk = await DownloadFFmpeg(progress);
            bool mtxOk = await DownloadMediaMTX(progress);

            return ffmpegOk && mtxOk;
        }
    }
}