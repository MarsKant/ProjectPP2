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

                progress?.Report($"📁 Папка FFmpeg: {ffmpegDir}");

                // Создаем папку если нет
                Directory.CreateDirectory(ffmpegDir);

                // Проверяем существующий файл
                if (File.Exists(ffmpegExe))
                {
                    FileInfo fi = new FileInfo(ffmpegExe);
                    progress?.Report($"📄 Найден существующий ffmpeg.exe, размер: {fi.Length} байт");

                    if (fi.Length > 1000000) // Больше 1MB - значит файл нормальный
                    {
                        progress?.Report("✅ FFmpeg уже существует");
                        return true;
                    }
                    else
                    {
                        progress?.Report("⚠️ Файл слишком маленький, удаляем...");
                        File.Delete(ffmpegExe);
                    }
                }

                // Используем WebClient для скачивания
                string url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
                string zipPath = Path.Combine(BaseDir, "ffmpeg_temp.zip");

                progress?.Report($"🌐 Скачивание FFmpeg с {url}...");

                using (var client = new System.Net.WebClient())
                {
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        if (e.TotalBytesToReceive > 0)
                        {
                            int percent = (int)((double)e.BytesReceived / e.TotalBytesToReceive * 100);
                            progress?.Report($"Загрузка: {percent}% ({e.BytesReceived / 1024 / 1024} МБ из {e.TotalBytesToReceive / 1024 / 1024} МБ)");
                        }
                    };

                    await client.DownloadFileTaskAsync(url, zipPath);
                }

                progress?.Report("📦 Распаковка FFmpeg...");

                // Распаковываем
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, BaseDir);

                // Ищем папку с ffmpeg
                var directories = Directory.GetDirectories(BaseDir, "ffmpeg-*");
                if (directories.Length > 0)
                {
                    string extractedDir = directories[0];
                    progress?.Report($"📂 Найдена папка: {extractedDir}");

                    // Ищем ffmpeg.exe в bin папке
                    string binDir = Path.Combine(extractedDir, "bin");
                    if (Directory.Exists(binDir))
                    {
                        string sourceExe = Path.Combine(binDir, "ffmpeg.exe");
                        if (File.Exists(sourceExe))
                        {
                            File.Copy(sourceExe, ffmpegExe, true);
                            progress?.Report($"✅ FFmpeg скопирован в {ffmpegExe}");

                            FileInfo finalFi = new FileInfo(ffmpegExe);
                            progress?.Report($"Размер: {finalFi.Length} байт");
                        }
                    }

                    // Удаляем временную папку
                    try { Directory.Delete(extractedDir, true); } catch { }
                }
                else
                {
                    // Если не нашли папку, ищем по всей директории
                    var files = Directory.GetFiles(BaseDir, "ffmpeg.exe", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        File.Copy(files[0], ffmpegExe, true);
                        progress?.Report($"✅ FFmpeg найден и скопирован");
                    }
                }

                // Очистка
                File.Delete(zipPath);

                // Финальная проверка
                if (File.Exists(ffmpegExe))
                {
                    FileInfo finalFi = new FileInfo(ffmpegExe);
                    progress?.Report($"✅ FFmpeg готов! Размер: {finalFi.Length} байт");
                    return true;
                }
                else
                {
                    progress?.Report("❌ ffmpeg.exe не создан");
                    return false;
                }
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
        public static async Task<bool> DownloadVLC(IProgress<string> progress = null)
        {
            try
            {
                string vlcDir = Path.Combine(BaseDir, "libvlc", "win-x64");
                string vlcDll = Path.Combine(vlcDir, "libvlc.dll");

                if (File.Exists(vlcDll))
                {
                    progress?.Report("VLC уже существует");
                    return true;
                }

                Directory.CreateDirectory(vlcDir);

                // Скачиваем VLC 3.0.11 (стабильная версия)
                string url = "https://get.videolan.org/vlc/3.0.11/win64/vlc-3.0.11-win64.zip";
                string zipPath = Path.Combine(BaseDir, "vlc_temp.zip");

                progress?.Report("Загрузка VLC (60 МБ, может занять время)...");

                using (var client = new System.Net.WebClient())
                {
                    await client.DownloadFileTaskAsync(url, zipPath);
                }

                progress?.Report("Распаковка VLC...");

                // ИСПРАВЛЕНО: убрали третий параметр
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, BaseDir);

                // Ищем распакованную папку
                string extractedDir = Path.Combine(BaseDir, "vlc-3.0.11");
                if (Directory.Exists(extractedDir))
                {
                    // Копируем нужные файлы
                    string pluginsSrc = Path.Combine(extractedDir, "plugins");
                    string pluginsDst = Path.Combine(vlcDir, "plugins");

                    // Копируем папку plugins
                    CopyDirectory(pluginsSrc, pluginsDst);

                    // Копируем основные DLL
                    File.Copy(Path.Combine(extractedDir, "libvlc.dll"), Path.Combine(vlcDir, "libvlc.dll"), true);
                    File.Copy(Path.Combine(extractedDir, "libvlccore.dll"), Path.Combine(vlcDir, "libvlccore.dll"), true);

                    // Удаляем временную папку
                    try { Directory.Delete(extractedDir, true); } catch { }
                }

                File.Delete(zipPath);
                progress?.Report("✅ VLC готов");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"❌ Ошибка: {ex.Message}");
                return false;
            }
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectory(dir, dest);
            }
        }
    }
}