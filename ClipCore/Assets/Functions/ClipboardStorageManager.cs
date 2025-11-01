using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace ClipCore.Assets.Functions
{
    public class ClipboardStorageManager
    {
        private static readonly string BaseFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ClipCore"
        );

        public static readonly string DataFolderPath = Path.Combine(BaseFolderPath, "Data");
        public static readonly string ImagesFolderPath = Path.Combine(BaseFolderPath, "Images");
        public static readonly string FilesFolderPath = Path.Combine(BaseFolderPath, "Files");
        private static readonly string IndexFilePath = Path.Combine(BaseFolderPath, "index.json");

        private const long MaxFileSize = 1024 * 1024 * 1024; // 1GB limit

        public ClipboardStorageManager()
        {
            EnsureDirectoriesExist();
        }

        public bool IsClipCoreFile(string path)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                var clipCoreRoot = Path.GetFullPath(BaseFolderPath) + Path.DirectorySeparatorChar;

                return fullPath.StartsWith(clipCoreRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(BaseFolderPath);
            Directory.CreateDirectory(DataFolderPath);
            Directory.CreateDirectory(ImagesFolderPath);
            Directory.CreateDirectory(FilesFolderPath);
        }

        public class ClipboardIndex
        {
            public int Id { get; set; }
            public DateTime Timestamp { get; set; }
            public ClipboardType Type { get; set; }
            public string ContentPath { get; set; } = string.Empty;
            public string PreviewText { get; set; } = string.Empty;
            public bool IsFavorite { get; set; }
        }

        // Index dosyasını kaydet
        public async Task SaveIndexAsync(List<ClipboardIndex> items)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(items, options);
                await File.WriteAllTextAsync(IndexFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Index save error: {ex.Message}");
            }
        }

        // Index dosyasını yükle
        public async Task<List<ClipboardIndex>> LoadIndexAsync()
        {
            try
            {
                if (!File.Exists(IndexFilePath))
                    return new List<ClipboardIndex>();

                var json = await File.ReadAllTextAsync(IndexFilePath);

                if (string.IsNullOrWhiteSpace(json))
                    return new List<ClipboardIndex>();

                var items = JsonSerializer.Deserialize<List<ClipboardIndex>>(json);

                if (items == null)
                    return new List<ClipboardIndex>();

                // Corrupt kayıtları filtrele
                return items.Where(x =>
                    x.Id > 0 &&
                    !string.IsNullOrEmpty(x.ContentPath) &&
                    !string.IsNullOrEmpty(x.PreviewText)
                ).ToList();
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON parse error: {ex.Message}");

                // Backup oluştur ve yeni başla
                if (File.Exists(IndexFilePath))
                {
                    var backupPath = IndexFilePath + $".backup_{DateTime.Now:yyyyMMddHHmmss}";
                    File.Copy(IndexFilePath, backupPath, true);
                }

                return new List<ClipboardIndex>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Index load error: {ex.Message}");
                return new List<ClipboardIndex>();
            }
        }
        // Text/Code içeriği kaydet
        public async Task<string> SaveTextContentAsync(int id, string content)
        {
            try
            {
                var filePath = Path.Combine(DataFolderPath, $"{id}.txt");
                await File.WriteAllTextAsync(filePath, content);
                return filePath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Text save error: {ex.Message}");
                return string.Empty;
            }
        }

        // Image içeriği kaydet (Base64'ten dosyaya)
        public async Task<string> SaveImageContentAsync(int id, byte[] imageBytes)
        {
            try
            {
                if (imageBytes.Length > MaxFileSize)
                {
                    System.Diagnostics.Debug.WriteLine($"Image too large: {imageBytes.Length} bytes");
                    return string.Empty;
                }

                if (imageBytes.Length < 8)
                {
                    System.Diagnostics.Debug.WriteLine("Image data too small");
                    return string.Empty;
                }

                var filePath = Path.Combine(ImagesFolderPath, $"{id}.png"); // PNG kullan

                using (var memStream = new MemoryStream(imageBytes))
                {
                    try
                    {
                        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(memStream.AsRandomAccessStream());

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        using (var outputStream = fileStream.AsRandomAccessStream())
                        {
                            // PNG encoder (LOSSLESS)
                            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                                Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, // JPEG yerine PNG
                                outputStream
                            );

                            // Pixel verilerini al
                            var pixelData = await decoder.GetPixelDataAsync(
                                Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, // Orijinal format
                                Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                                new Windows.Graphics.Imaging.BitmapTransform(),
                                Windows.Graphics.Imaging.ExifOrientationMode.RespectExifOrientation,
                                Windows.Graphics.Imaging.ColorManagementMode.ColorManageToSRgb
                            );

                            encoder.SetPixelData(
                                Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                                Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                                decoder.PixelWidth,
                                decoder.PixelHeight,
                                decoder.DpiX,
                                decoder.DpiY,
                                pixelData.DetachPixelData()
                            );

                            // PNG compression level (en yüksek sıkıştırma)
                            var propertySet = new Windows.Graphics.Imaging.BitmapPropertySet();
                            var filterOption = new Windows.Graphics.Imaging.BitmapTypedValue(
                                (byte)Windows.Graphics.Imaging.PngFilterMode.Adaptive, // Adaptive filter
                                Windows.Foundation.PropertyType.UInt8
                            );
                            propertySet.Add("FilterOption", filterOption);

                            await encoder.BitmapProperties.SetPropertiesAsync(propertySet);
                            await encoder.FlushAsync();
                        }

                        // Dosya boyutunu kontrol et
                        var savedFileInfo = new FileInfo(filePath);
                        System.Diagnostics.Debug.WriteLine($"Image saved: {savedFileInfo.Length} bytes (original: {imageBytes.Length} bytes)");

                        return filePath;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"PNG encoding error: {ex.Message}");

                        // Fallback: Raw bytes olarak kaydet (sıkıştırmasız ama güvenli)
                        var fallbackPath = Path.Combine(ImagesFolderPath, $"{id}.dat");
                        await File.WriteAllBytesAsync(fallbackPath, imageBytes);

                        System.Diagnostics.Debug.WriteLine($"Image saved as raw data: {imageBytes.Length} bytes");
                        return fallbackPath;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image save error: {ex.Message}");
                return string.Empty;
            }
        }

        // File referanslarını kaydet
        public async Task<(string contentPath, string previewText)> SaveFileReferencesAsync(int id, List<string> filePaths)
        {
            try
            {
                var savedFiles = new List<string>();
                var fileNames = new List<string>();
                var targetFolder = Path.Combine(FilesFolderPath, id.ToString());
                Directory.CreateDirectory(targetFolder);

                foreach (var sourcePath in filePaths)
                {
                    try
                    {
                        if (File.Exists(sourcePath))
                        {
                            // Dosya boyutu kontrolü
                            var fileInfo = new FileInfo(sourcePath);
                            if (fileInfo.Length > MaxFileSize)
                            {
                                System.Diagnostics.Debug.WriteLine($"File too large: {sourcePath}");
                                continue;
                            }

                            var fileName = Path.GetFileName(sourcePath);
                            var targetPath = Path.Combine(targetFolder, fileName);

                            // Dosyayı kopyala
                            File.Copy(sourcePath, targetPath, overwrite: true);
                            savedFiles.Add(targetPath);
                            fileNames.Add(fileName);
                        }
                        else if (Directory.Exists(sourcePath))
                        {
                            // Klasör ise, klasörü kopyala
                            var dirInfo = new DirectoryInfo(sourcePath);
                            var folderName = dirInfo.Name;
                            var targetPath = Path.Combine(targetFolder, folderName);

                            CopyDirectory(sourcePath, targetPath);
                            savedFiles.Add(targetPath);
                            fileNames.Add(folderName + " (folder)");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"File copy error for {sourcePath}: {ex.Message}");
                    }
                }

                if (savedFiles.Count == 0)
                    return (string.Empty, string.Empty);

                // Liste dosyasını kaydet
                var listPath = Path.Combine(targetFolder, "files.txt");
                await File.WriteAllLinesAsync(listPath, savedFiles);

                var preview = fileNames.Count == 1
                    ? fileNames[0]
                    : $"{fileNames.Count} files: {string.Join(", ", fileNames.Take(3))}{(fileNames.Count > 3 ? "..." : "")}";

                return (listPath, preview);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"File save error: {ex.Message}");
                return (string.Empty, string.Empty);
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var targetFile = Path.Combine(targetDir, fileName);
                File.Copy(file, targetFile, true);
            }

            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(directory);
                var targetSubDir = Path.Combine(targetDir, dirName);
                CopyDirectory(directory, targetSubDir);
            }
        }

        // Dosya içeriğini oku metodunu güncelle:
        public async Task<List<string>> ReadFilePathsAsync(string contentPath)
        {
            try
            {
                if (!File.Exists(contentPath))
                    return new List<string>();

                return (await File.ReadAllLinesAsync(contentPath)).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"File paths read error: {ex.Message}");
                return new List<string>();
            }
        }

        // 24 saatten eski kayıtları sil
        public async Task CleanupOldItemsAsync(List<ClipboardIndex> items)
        {
            try
            {
                var cutoffTime = DateTime.Now.AddHours(-24);
                var itemsToRemove = items.Where(x => x.Timestamp < cutoffTime && !x.IsFavorite).ToList();

                foreach (var item in itemsToRemove)
                {
                    try
                    {
                        var contentPath = item.ContentPath;

                        if (string.IsNullOrEmpty(contentPath))
                        {
                            items.Remove(item);
                            continue;
                        }

                        var fullPath = Path.GetFullPath(contentPath);
                        var filesRoot = Path.GetFullPath(FilesFolderPath) + Path.DirectorySeparatorChar;

                        // File tipi ise ve Files klasörü altındaysa, parent klasörü sil
                        if (item.Type == ClipboardType.File && fullPath.StartsWith(filesRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            var parentDir = Path.GetDirectoryName(fullPath);
                            if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                            {
                                Directory.Delete(parentDir, true);
                            }
                        }
                        // Diğer tipler için sadece dosyayı sil
                        else if (File.Exists(fullPath))
                        {
                            File.Delete(fullPath);
                        }

                        items.Remove(item);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Cleanup error for item {item.Id}: {ex.Message}");
                    }
                }

                // Index'i güncelle
                await SaveIndexAsync(items);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cleanup error: {ex.Message}");
            }
        }

        public bool IsFileSizeValid(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.Length <= MaxFileSize;
                }
                else if (Directory.Exists(filePath))
                {
                    // Klasör ise içindeki tüm dosyaların toplamını kontrol et
                    var dirInfo = new DirectoryInfo(filePath);
                    long totalSize = GetDirectorySize(dirInfo);
                    return totalSize <= MaxFileSize;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private long GetDirectorySize(DirectoryInfo dirInfo)
        {
            long size = 0;

            try
            {
                // Dosyaları topla
                FileInfo[] files = dirInfo.GetFiles();
                foreach (FileInfo file in files)
                {
                    size += file.Length;

                    // 1GB'ı geçerse hemen dur
                    if (size > MaxFileSize)
                        return size;
                }

                // Alt klasörleri topla (recursive)
                DirectoryInfo[] dirs = dirInfo.GetDirectories();
                foreach (DirectoryInfo dir in dirs)
                {
                    size += GetDirectorySize(dir);

                    // 1GB'ı geçerse hemen dur
                    if (size > MaxFileSize)
                        return size;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Erişim engellendi, devam et
            }

            return size;
        }

        public async Task<string> ReadContentAsync(string contentPath, ClipboardType type)
{
    try
    {
        if (!File.Exists(contentPath))
            return string.Empty;

        // Image ise Base64'e çevir
        if (type == ClipboardType.Image)
        {
            var extension = Path.GetExtension(contentPath).ToLower();
            
            // Eğer .dat ise (raw data), direkt base64'e çevir
            if (extension == ".dat")
            {
                var rawBytes = await File.ReadAllBytesAsync(contentPath);
                return Convert.ToBase64String(rawBytes);
            }
            
            // Eğer .png/.jpg ise, WinRT ile oku ve orijinal formatta base64'e çevir
            using (var fileStream = File.OpenRead(contentPath))
            {
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(fileStream.AsRandomAccessStream());
                
                // Pixel verilerini al (LOSSLESS)
                var pixelData = await decoder.GetPixelDataAsync(
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                    Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                    new Windows.Graphics.Imaging.BitmapTransform(),
                    Windows.Graphics.Imaging.ExifOrientationMode.RespectExifOrientation,
                    Windows.Graphics.Imaging.ColorManagementMode.ColorManageToSRgb
                );
                
                var pixels = pixelData.DetachPixelData();
                
                // Bitmap format bilgilerini koruyarak encode et
                using (var memStream = new MemoryStream())
                using (var randomAccessStream = memStream.AsRandomAccessStream())
                {
                    var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                        Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
                        randomAccessStream
                    );
                    
                    encoder.SetPixelData(
                        Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                        Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                        decoder.PixelWidth,
                        decoder.PixelHeight,
                        decoder.DpiX,
                        decoder.DpiY,
                        pixels
                    );
                    
                    await encoder.FlushAsync();
                    
                    var resultBytes = memStream.ToArray();
                    return Convert.ToBase64String(resultBytes);
                }
            }
        }

        // File ise dosya yollarını oku
        if (type == ClipboardType.File)
        {
            var filePaths = await ReadFilePathsAsync(contentPath);
            return string.Join(Environment.NewLine, filePaths);
        }

        // Diğerleri için text oku
        return await File.ReadAllTextAsync(contentPath);
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Content read error: {ex.Message}");
        return string.Empty;
    }
}
    }
}