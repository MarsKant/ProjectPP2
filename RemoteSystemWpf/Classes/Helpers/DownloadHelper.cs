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

                progress?.Report($"📁 Папка программы: {BaseDir}");
                progress?.Report($"📁 Целевая папка: {ffmpegDir}");

                // Создаем папку если нет
                Directory.CreateDirectory(ffmpegDir);

                // Проверяем существующий файл
                if (File.Exists(ffmpegExe))
                {
                    FileInfo fi = new FileInfo(ffmpegExe);
                    progress?.Report($"📄 Найден существующий ffmpeg.exe, размер: {fi.Length} байт");

                    if (fi.Length > 1000000) // Больше 1MB
                    {
                        progress?.Report("✅ FFmpeg уже существует и имеет нормальный размер");
                        return true;
                    }
                    else
                    {
                        progress?.Report("⚠️ Файл слишком маленький, удаляем...");
                        File.Delete(ffmpegExe);
                    }
                }

                // Пробуем разные ссылки
                string[] urls = new[]
                {
                    "https://github.com/ShareX/FFmpeg/releases/download/ffmpeg-8.0/ffmpeg-8.0-win-x64.zip",
                    "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip",
                    "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
                };

                string zipPath = Path.Combine(BaseDir, "ffmpeg_temp.zip");
                bool downloaded = false;

                foreach (string url in urls)
                {
                    try
                    {
                        progress?.Report($"🌐 Пробуем скачать: {url}");

                        using (var response = await _client.GetAsync(url))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                long totalBytes = response.Content.Headers.ContentLength ?? 0;
                                progress?.Report($"✅ Найдено! Размер: {totalBytes / 1024 / 1024} MB");

                                using (var fs = new FileStream(zipPath, FileMode.Create))
                                {
                                    await response.Content.CopyToAsync(fs);
                                }

                                downloaded = true;
                                break;
                            }
                            else
                            {
                                progress?.Report($"❌ Ошибка {response.StatusCode}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"❌ Ошибка: {ex.Message}");
                    }
                }

                if (!downloaded)
                {
                    progress?.Report("❌ Не удалось скачать FFmpeg ни по одной ссылке");
                    return false;
                }

                progress?.Report("📦 Распаковка...");

                // Проверяем размер zip
                FileInfo zipInfo = new FileInfo(zipPath);
                progress?.Report($"ZIP файл размер: {zipInfo.Length} байт");

                ZipFile.ExtractToDirectory(zipPath, ffmpegDir);
                // Ищем ffmpeg.exe
                var files = Directory.GetFiles(ffmpegDir, "ffmpeg.exe", SearchOption.AllDirectories);
                progress?.Report($"Найдено файлов ffmpeg.exe: {files.Length}");

                if (files.Length > 0)
                {
                    foreach (string file in files)
                    {
                        progress?.Report($"  📄 {file}");
                    }

                    // Копируем первый найденный в корень
                    if (files[0] != ffmpegExe)
                    {
                        File.Copy(files[0], ffmpegExe, true);
                        progress?.Report($"✅ FFmpeg скопирован в {ffmpegExe}");

                        FileInfo newFi = new FileInfo(ffmpegExe);
                        progress?.Report($"Размер: {newFi.Length} байт");
                    }
                }
                else
                {
                    progress?.Report("❌ ffmpeg.exe не найден в архиве");

                    // Показываем что есть в архиве
                    var allFiles = Directory.GetFiles(ffmpegDir, "*.*", SearchOption.AllDirectories);
                    progress?.Report($"Всего файлов в архиве: {allFiles.Length}");
                    foreach (string file in allFiles.Take(10))
                    {
                        progress?.Report($"  {file}");
                    }
                }

                // Очистка
                File.Delete(zipPath);

                // Финальная проверка
                if (File.Exists(ffmpegExe))
                {
                    FileInfo finalFi = new FileInfo(ffmpegExe);
                    progress?.Report($"✅ Готово! FFmpeg в {ffmpegExe}, размер {finalFi.Length} байт");
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
                progress?.Report($"❌ Критическая ошибка: {ex.Message}");
                return false;
            }
        }

    public static async Task<bool> DownloadMediaMTX(IProgress<string> progress = null)
        {
            try
            {
                string mtxDir = Path.Combine(BaseDir, "MediaMTX");
                string mtxExe = Path.Combine(mtxDir, "mediamtx.exe");

                if (File.Exists(mtxExe))
                    return true;

                Directory.CreateDirectory(mtxDir);

                string url = "https://github.com/bluenviron/mediamtx/releases/download/v1.16.3/mediamtx_v1.16.3_windows_amd64.zip";
                string zipPath = Path.Combine(BaseDir, "mediamtx_temp.zip");

                progress?.Report("Загрузка MediaMTX...");

                using (var response = await _client.GetAsync(url))
                {
                    response.EnsureSuccessStatusCode();
                    using (var fs = new FileStream(zipPath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                progress?.Report("Распаковка MediaMTX...");
                ZipFile.ExtractToDirectory(zipPath, mtxDir);

                // Создаем конфиг
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
    }
}