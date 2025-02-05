using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Linq;

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

            if (File.Exists(md5FilePath))
            {
                foreach (var line in File.ReadAllLines(md5FilePath))
                {
                    var parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        // Store with normalized path separators
                        result[parts[0].Replace('\\', '/')] = parts[1];
                    }
                }
            }
            else if (Directory.GetFiles(assetsPath, "*.asset", SearchOption.AllDirectories).Any())
            {
                // Generate md5.txt if it doesn't exist but there are asset files
                var assetFiles = Directory.GetFiles(assetsPath, "*.asset", SearchOption.AllDirectories);
                var md5Lines = new List<string>();

                foreach (var file in assetFiles)
                {
                    if (VerifyAuroraMagic(file))
                    {
                        var hash = CalculateFileHash(file);
                        var relativePath = GetRelativePath(assetsPath, file);
                        result[relativePath.Replace('\\', '/')] = hash;
                        md5Lines.Add($"{relativePath}:{hash}");
                    }
                }

                if (md5Lines.Any())
                {
                    File.WriteAllLines(md5FilePath, md5Lines);
                }

                // Clear all existing cache since we're regenerating MD5s
                ClearCache();
            }

            _md5Cache = result;
            _currentAssetsPath = assetsPath;
            return result;
        }

        public static Dictionary<string, (string FilePath, string Hash)> CheckFolderCache(string folderPath, string titleId)
        {
            var result = new Dictionary<string, (string FilePath, string Hash)>();
            var assetsRootPath = Path.GetDirectoryName(folderPath);
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

        private static void CheckAssetCache(string folderPath, string titleId, string searchPattern, string assetType, 
            Dictionary<string, (string FilePath, string Hash)> result, Dictionary<string, string> md5Dict, string assetsRootPath)
        {
            try
            {
                var files = Directory.GetFiles(folderPath, searchPattern, SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (!VerifyAuroraMagic(file)) continue;

                    var relativePath = GetRelativePath(assetsRootPath, file).Replace('\\', '/');
                    string hash;
                    
                    // Get hash from md5.txt if available
                    if (!md5Dict.TryGetValue(relativePath, out hash))
                    {
                        continue; // Skip if no MD5 available
                    }

                    var cachePath = GetCachePath(titleId, assetType, hash);
                    var cacheDir = Path.GetDirectoryName(cachePath);

                    // Check if we need to clean up old cache files
                    if (Directory.Exists(cacheDir))
                    {
                        var existingCacheFiles = Directory.GetFiles(cacheDir, $"{assetType}-*.png");
                        foreach (var cacheFile in existingCacheFiles)
                        {
                            if (!cacheFile.EndsWith($"{assetType}-{hash}.png", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    File.Delete(cacheFile);
                                }
                                catch (Exception ex)
                                {
                                    MainWindow.SaveError(ex);
                                }
                            }
                        }
                    }

                    // If cache doesn't exist or is outdated, create it
                    if (!File.Exists(cachePath) || File.GetLastWriteTime(cachePath) < File.GetLastWriteTime(file))
                    {
                        try
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
                                case "screenshot": 
                                    var screenshots = asset.GetScreenshots();
                                    MainWindow.SaveError(new Exception($"Found {screenshots?.Length ?? 0} screenshots in {file}"));
                                    if (screenshots != null && screenshots.Length > 0)
                                    {
                                        // Cache all screenshots with index in filename
                                        for (int i = 0; i < screenshots.Length; i++)
                                        {
                                            if (screenshots[i] != null)
                                            {
                                                // Create thumbnail with appropriate size
                                                using (var thumbnail = CreateThumbnail(screenshots[i], "screenshot"))
                                                {
                                                    var screenshotCachePath = GetCachePath(titleId, $"screenshot{i + 1}", hash);
                                                    MainWindow.SaveError(new Exception($"Caching screenshot {i + 1} to {screenshotCachePath}"));
                                                    
                                                    // Ensure directory exists
                                                    Directory.CreateDirectory(Path.GetDirectoryName(screenshotCachePath));
                                                    
                                                    // Save thumbnail to cache
                                                    thumbnail.Save(screenshotCachePath, ImageFormat.Png);
                                                }
                                            }
                                            else
                                            {
                                                MainWindow.SaveError(new Exception($"Screenshot {i + 1} is null"));
                                            }
                                        }
                                        // Store references for each screenshot
                                        for (int i = 0; i < screenshots.Length; i++)
                                        {
                                            result[$"screenshot{i + 1}"] = (file, hash);
                                        }
                                    }
                                    return; // Skip the rest since we've handled screenshots specially
                            }

                            if (image != null)
                            {
                                CacheImage(image, hash, titleId, assetType);
                                result[assetType] = (file, hash);
                            }
                        }
                        catch (Exception ex)
                        {
                            MainWindow.SaveError(ex);
                        }
                    }
                    else
                    {
                        // Cache exists and is up to date
                        result[assetType] = (file, hash);
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
            if (!File.Exists(assetPath)) return null;

            var assetsRootPath = Path.GetDirectoryName(Path.GetDirectoryName(assetPath)); // Go up two levels to get root
            var relativePath = GetRelativePath(assetsRootPath, assetPath).Replace('\\', '/');
            var md5Dict = GetMd5Cache(assetsRootPath);

            string hash;
            if (!md5Dict.TryGetValue(relativePath, out hash))
            {
                return null;
            }

            // Handle numbered screenshots
            if (assetType.StartsWith("screenshot") && assetType.Length > 10)
            {
                string screenshotNumber = assetType.Substring(10); // Get the number after "screenshot"
                string cachePath = GetCachePath(titleId, $"screenshot{screenshotNumber}", hash);

                // If cache exists and is newer than asset file, use it
                if (File.Exists(cachePath) && 
                    File.GetLastWriteTime(cachePath) >= File.GetLastWriteTime(assetPath))
                {
                    try
                    {
                        return LoadImageFromCache(cachePath);
                    }
                    catch
                    {
                        // If loading cached image fails, continue to regenerate it
                    }
                }

                return null;
            }

            string baseCachePath = GetCachePath(titleId, assetType, hash);

            // If cache exists and is newer than asset file, use it
            if (File.Exists(baseCachePath) && 
                File.GetLastWriteTime(baseCachePath) >= File.GetLastWriteTime(assetPath))
            {
                try
                {
                    return LoadImageFromCache(baseCachePath);
                }
                catch
                {
                    // If loading cached image fails, continue to regenerate it
                }
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
            return Path.Combine(CacheFolder, LocalFolder, titleId, $"{assetType}-{hash}.png");
        }

        public static BitmapImage LoadImageFromCache(string cachePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.None;
            bitmap.UriSource = new Uri(cachePath, UriKind.Relative);
            bitmap.EndInit();
            bitmap.Freeze(); // Makes the image thread-safe
            return bitmap;
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
    }
} 