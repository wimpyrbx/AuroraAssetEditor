using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace AuroraAssetEditor.Classes
{
    public static class AssetCache
    {
        private const string CacheFolder = "thumbs";
        private const string LocalFolder = "local";

        // Thumbnail size limits
        private const int MaxBoxartWidth = 300;
        private const int MaxBoxartHeight = 400;
        private const int MaxBackgroundWidth = 480;
        private const int MaxBackgroundHeight = 270;
        private const int MaxBannerWidth = 210;
        private const int MaxBannerHeight = 48;
        private const int MaxIconWidth = 32;
        private const int MaxIconHeight = 32;
        private const int MaxScreenshotWidth = 320;
        private const int MaxScreenshotHeight = 180;

        private static Dictionary<string, string> _md5Cache;
        private static string _currentAssetsPath;

        public static void Initialize()
        {
            // Create cache directory structure if it doesn't exist
            var localCachePath = Path.Combine(CacheFolder, LocalFolder);
            Directory.CreateDirectory(localCachePath);
            _md5Cache = null;
            _currentAssetsPath = null;
        }

        private static string GetRelativePath(string basePath, string fullPath)
        {
            // Convert paths to absolute paths
            basePath = Path.GetFullPath(basePath);
            fullPath = Path.GetFullPath(fullPath);

            // Get the URI for both paths
            var baseUri = new Uri(basePath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? basePath : basePath + Path.DirectorySeparatorChar);
            var fullUri = new Uri(fullPath);

            // Get the relative path
            var relativeUri = baseUri.MakeRelativeUri(fullUri);
            
            // Convert to a path and replace forward slashes with backslashes
            return Uri.UnescapeDataString(relativeUri.ToString());
        }

        private static Dictionary<string, string> GetMd5Cache(string assetsPath)
        {
            // If we already have the cache for this path, return it
            if (_md5Cache != null && _currentAssetsPath == assetsPath)
                return _md5Cache;

            var md5FilePath = Path.Combine(assetsPath, "md5.txt");
            var result = new Dictionary<string, string>();

            // Read existing MD5s if file exists
            if (File.Exists(md5FilePath))
            {
                foreach (var line in File.ReadAllLines(md5FilePath))
                {
                    var parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        result[parts[0]] = parts[1];
                    }
                }
            }

            _md5Cache = result;
            _currentAssetsPath = assetsPath;
            return result;
        }

        public static Dictionary<string, (string FilePath, string Hash, bool HasCache)> CheckFolderCache(string folderPath, string titleId)
        {
            var result = new Dictionary<string, (string FilePath, string Hash, bool HasCache)>();
            // Get the root path (the directory containing the game folders)
            var assetsRootPath = Path.GetDirectoryName(Path.GetDirectoryName(folderPath));
            var md5Dict = GetMd5Cache(assetsRootPath);
            
            // Check each asset type using the MD5s from file
            CheckAssetCache(folderPath, titleId, "GC*.asset", "boxart", result, md5Dict, assetsRootPath);
            CheckAssetCache(folderPath, titleId, "BK*.asset", "background", result, md5Dict, assetsRootPath);
            CheckAssetCache(folderPath, titleId, "GL*.asset", "banner", result, md5Dict, assetsRootPath);
            CheckAssetCache(folderPath, titleId, "GL*.asset", "icon", result, md5Dict, assetsRootPath);
            CheckAssetCache(folderPath, titleId, "SS*.asset", "screenshot", result, md5Dict, assetsRootPath);

            // Also check alternative naming patterns
            if (!result.ContainsKey("boxart")) CheckAssetCache(folderPath, titleId, "boxart*.asset", "boxart", result, md5Dict, assetsRootPath);
            if (!result.ContainsKey("background")) CheckAssetCache(folderPath, titleId, "background*.asset", "background", result, md5Dict, assetsRootPath);
            if (!result.ContainsKey("banner")) CheckAssetCache(folderPath, titleId, "banner*.asset", "banner", result, md5Dict, assetsRootPath);
            if (!result.ContainsKey("icon")) CheckAssetCache(folderPath, titleId, "icon*.asset", "icon", result, md5Dict, assetsRootPath);
            if (!result.ContainsKey("screenshot")) CheckAssetCache(folderPath, titleId, "screenshot*.asset", "screenshot", result, md5Dict, assetsRootPath);

            return result;
        }

        private static void UpdateMd5File(string assetsPath, string relativePath, string newHash)
        {
            var md5FilePath = Path.Combine(assetsPath, "md5.txt");
            var lines = new List<string>();
            
            // Read existing lines if file exists
            if (File.Exists(md5FilePath))
            {
                lines = File.ReadAllLines(md5FilePath)
                    .Where(line => !line.StartsWith(relativePath + ":")) // Remove old entry for this file
                    .ToList();
            }
            
            // Add new entry
            lines.Add($"{relativePath}:{newHash}");
            
            // Sort lines for consistency
            lines.Sort();
            
            // Write back to file
            File.WriteAllLines(md5FilePath, lines);
            
            // Update in-memory cache
            if (_md5Cache != null && _currentAssetsPath == assetsPath)
            {
                _md5Cache[relativePath] = newHash;
            }
        }

        private static void CheckAssetCache(string folderPath, string titleId, string searchPattern, string assetType, 
            Dictionary<string, (string FilePath, string Hash, bool HasCache)> result, Dictionary<string, string> md5Dict, string assetsRootPath)
        {
            try
            {
                var files = Directory.GetFiles(folderPath, searchPattern, SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (!VerifyAuroraMagic(file)) continue;

                    var relativePath = GetRelativePath(assetsRootPath, file).Replace('\\', '/');
                    string hash;
                    
                    // First try to get hash from md5.txt
                    if (!md5Dict.TryGetValue(relativePath, out hash))
                    {
                        // If not found, calculate it and update md5.txt
                        hash = CalculateFileHash(file);
                        UpdateMd5File(assetsRootPath, relativePath, hash);
                    }

                    if (assetType == "screenshot")
                    {
                        var assetBytes = File.ReadAllBytes(file);
                        var asset = new AuroraAsset.AssetFile(assetBytes);
                        var screenshots = asset.GetScreenshots();
                        
                        if (screenshots != null && screenshots.Length > 0)
                        {
                            for (int i = 0; i < screenshots.Length; i++)
                            {
                                var screenshotCachePath = GetCachePath(titleId, $"screenshot{i + 1}", hash);
                                bool hasCache = File.Exists(screenshotCachePath);
                                
                                if (!hasCache && screenshots[i] != null)
                                {
                                    using (var thumbnail = CreateThumbnail(screenshots[i], "screenshot"))
                                    {
                                        Directory.CreateDirectory(Path.GetDirectoryName(screenshotCachePath));
                                        thumbnail.Save(screenshotCachePath, ImageFormat.Png);
                                        hasCache = true;
                                    }
                                }
                                result[$"screenshot{i + 1}"] = (file, hash, hasCache);
                            }
                        }
                    }
                    else
                    {
                        var cachePath = GetCachePath(titleId, assetType, hash);
                        bool hasCache = File.Exists(cachePath);
                        
                        if (!hasCache)
                        {
                            var assetBytes = File.ReadAllBytes(file);
                            var asset = new AuroraAsset.AssetFile(assetBytes);
                            Image image = null;

                            switch (assetType)
                            {
                                case "boxart": image = asset.HasBoxArt ? asset.GetBoxart() : null; break;
                                case "background": image = asset.HasBackground ? asset.GetBackground() : null; break;
                                case "banner": image = asset.HasIconBanner ? asset.GetBanner() : null; break;
                                case "icon": image = asset.HasIconBanner ? asset.GetIcon() : null; break;
                            }

                            if (image != null)
                            {
                                CacheImage(image, hash, titleId, assetType);
                                hasCache = true;
                            }
                        }
                        result[assetType] = (file, hash, hasCache);
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
            }
        }

        private static bool VerifyAuroraMagic(string fileName)
        {
            try
            {
                using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var br = new BinaryReader(stream))
                    return br.ReadUInt32() == 0x41455852; /* RXEA in LittleEndian format */
            }
            catch
            {
                return false;
            }
        }

        public static BitmapImage GetCachedImage(string assetPath, string titleId, string assetType)
        {
            if (!File.Exists(assetPath))
            {
                Debug.WriteLine($"Asset file does not exist: {assetPath}");
                return null;
            }

            // Get the root path (the directory containing the game folders)
            var assetsRootPath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assetPath)));
            Debug.WriteLine($"Assets root path: {assetsRootPath}");

            var relativePath = GetRelativePath(assetsRootPath, assetPath).Replace('\\', '/');
            Debug.WriteLine($"Relative path: {relativePath}");

            var md5Dict = GetMd5Cache(assetsRootPath);
            Debug.WriteLine($"MD5 cache entries: {md5Dict.Count}");

            string hash;
            if (!md5Dict.TryGetValue(relativePath, out hash))
            {
                Debug.WriteLine($"No MD5 hash found for: {relativePath}");
                // Calculate hash directly if not in cache
                hash = CalculateFileHash(assetPath);
                UpdateMd5File(assetsRootPath, relativePath, hash);
            }
            Debug.WriteLine($"Hash: {hash}");

            // Handle numbered screenshots
            if (assetType.StartsWith("screenshot") && assetType.Length > 10)
            {
                string screenshotNumber = assetType.Substring(10); // Get the number after "screenshot"
                string cachePath = GetCachePath(titleId, $"screenshot{screenshotNumber}", hash);
                Debug.WriteLine($"Screenshot cache path: {cachePath}");

                if (File.Exists(cachePath))
                {
                    Debug.WriteLine($"Found screenshot cache file: {cachePath}");
                    return LoadImageFromCache(cachePath);
                }
                Debug.WriteLine($"Cache file not found: {cachePath}");
            }
            else
            {
                string cachePath = GetCachePath(titleId, assetType, hash);
                Debug.WriteLine($"Cache path: {cachePath}");

                if (File.Exists(cachePath))
                {
                    Debug.WriteLine($"Found cache file: {cachePath}");
                    return LoadImageFromCache(cachePath);
                }
                Debug.WriteLine($"Cache file not found: {cachePath}");
            }

            return null;
        }

        public static void CacheImage(Image image, string hash, string titleId, string assetType)
        {
            if (image == null) return;

            try
            {
                string cachePath = GetCachePath(titleId, assetType, hash);

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));

                // Create thumbnail with appropriate size
                using (var thumbnail = CreateThumbnail(image, assetType))
                {
                    // Save thumbnail to cache
                    thumbnail.Save(cachePath, ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
            }
        }

        private static Image CreateThumbnail(Image original, string assetType)
        {
            int maxWidth, maxHeight;

            // Set maximum dimensions based on asset type
            switch (assetType.ToLower())
            {
                case "boxart":
                    maxWidth = MaxBoxartWidth;
                    maxHeight = MaxBoxartHeight;
                    break;
                case "background":
                    maxWidth = MaxBackgroundWidth;
                    maxHeight = MaxBackgroundHeight;
                    break;
                case "banner":
                    maxWidth = MaxBannerWidth;
                    maxHeight = MaxBannerHeight;
                    break;
                case "icon":
                    maxWidth = MaxIconWidth;
                    maxHeight = MaxIconHeight;
                    break;
                default: // screenshots and others
                    maxWidth = MaxScreenshotWidth;
                    maxHeight = MaxScreenshotHeight;
                    break;
            }

            // Calculate new dimensions maintaining aspect ratio
            double scale = Math.Min((double)maxWidth / original.Width, (double)maxHeight / original.Height);
            int newWidth = (int)(original.Width * scale);
            int newHeight = (int)(original.Height * scale);

            // Create thumbnail
            var thumbnail = new Bitmap(newWidth, newHeight);
            using (var graphics = Graphics.FromImage(thumbnail))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(original, 0, 0, newWidth, newHeight);
            }

            return thumbnail;
        }

        private static string CalculateFileHash(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string GetCachePath(string titleId, string assetType, string hash)
        {
            // Ensure titleId is exactly 8 characters with leading zeros if needed
            if (!string.IsNullOrEmpty(titleId) && titleId.Length < 8)
            {
                titleId = titleId.PadLeft(8, '0');
            }
            return Path.Combine(CacheFolder, LocalFolder, titleId, $"{assetType}-{hash}.png");
        }

        public static BitmapImage LoadImageFromCache(string cachePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(cachePath, UriKind.RelativeOrAbsolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
                return null;
            }
        }

        public static void ClearCache()
        {
            try
            {
                var localCachePath = Path.Combine(CacheFolder, LocalFolder);
                if (Directory.Exists(localCachePath))
                {
                    Directory.Delete(localCachePath, true);
                    Directory.CreateDirectory(localCachePath);
                }
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
            }
        }

        // Add a separate method for cleanup that can be called manually if needed
        public static void CleanupOrphanedCacheFiles(string assetsPath)
        {
            var md5FilePath = Path.Combine(assetsPath, "md5.txt");
            var validMd5s = new HashSet<string>();

            // Read valid MD5s from file
            if (File.Exists(md5FilePath))
            {
                foreach (var line in File.ReadAllLines(md5FilePath))
                {
                    var parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        validMd5s.Add(parts[1].ToLowerInvariant());
                    }
                }
            }

            // Clean up orphaned cache files
            var localCachePath = Path.Combine(CacheFolder, LocalFolder);
            if (Directory.Exists(localCachePath))
            {
                foreach (var titleIdDir in Directory.GetDirectories(localCachePath))
                {
                    foreach (var cacheFile in Directory.GetFiles(titleIdDir, "*-*.png"))
                    {
                        // Extract MD5 from filename (format is assettype-md5.png)
                        var fileName = Path.GetFileNameWithoutExtension(cacheFile);
                        var md5 = fileName.Split('-').LastOrDefault();
                        
                        if (md5 != null && !validMd5s.Contains(md5.ToLowerInvariant()))
                        {
                            try
                            {
                                File.Delete(cacheFile);
                                Debug.WriteLine($"Deleted orphaned cache file: {cacheFile}");
                            }
                            catch (Exception ex)
                            {
                                MainWindow.SaveError(ex);
                            }
                        }
                    }

                    // Clean up empty directories
                    if (!Directory.EnumerateFileSystemEntries(titleIdDir).Any())
                    {
                        try
                        {
                            Directory.Delete(titleIdDir);
                            Debug.WriteLine($"Deleted empty cache directory: {titleIdDir}");
                        }
                        catch (Exception ex)
                        {
                            MainWindow.SaveError(ex);
                        }
                    }
                }
            }
        }

        public static bool HasAllCacheFiles(string assetPath, string titleId)
        {
            if (!File.Exists(assetPath)) return false;

            // Get the root path (the directory containing the game folders)
            var assetsRootPath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assetPath)));
            var relativePath = GetRelativePath(assetsRootPath, assetPath).Replace('\\', '/');
            var md5Dict = GetMd5Cache(assetsRootPath);

            string hash;
            if (!md5Dict.TryGetValue(relativePath, out hash))
            {
                hash = CalculateFileHash(assetPath);
            }

            // Check if all possible cache files exist based on the asset filename
            var filename = Path.GetFileName(assetPath);
            if (filename.StartsWith("GC"))
            {
                return File.Exists(GetCachePath(titleId, "boxart", hash));
            }
            else if (filename.StartsWith("BK"))
            {
                return File.Exists(GetCachePath(titleId, "background", hash));
            }
            else if (filename.StartsWith("GL"))
            {
                return File.Exists(GetCachePath(titleId, "banner", hash)) &&
                       File.Exists(GetCachePath(titleId, "icon", hash));
            }
            else if (filename.StartsWith("SS"))
            {
                // For screenshots, check if at least the first screenshot exists
                // If it does, we assume the asset has been processed
                return File.Exists(GetCachePath(titleId, "screenshot1", hash));
            }

            return false;
        }
    }
} 