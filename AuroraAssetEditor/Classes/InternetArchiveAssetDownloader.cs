//
//  InternetArchiveDownloader.cs
//  AuroraAssetEditor
//
//  Created by aenrione
//  Copyright (c) 2015 Swizzy. All rights reserved.

namespace AuroraAssetEditor.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Text.RegularExpressions;
    using System.IO;
    using System.Net;
    using System.Text;

    internal class InternetArchiveDownloader
    {
        private const string BaseUrl = "https://archive.org/download/xboxunity-covers-fulldump_202311/xboxunity-covers-fulldump/";
        public static EventHandler<StatusArgs> StatusChanged;

        internal static void SendStatusChanged(string msg)
        {
            var handler = StatusChanged;
            if (handler != null)
                handler.Invoke(null, new StatusArgs(msg));
        }

        public InternetArchiveAsset[] GetTitleInfo(uint titleId)
        {
            string titleFolder = string.Format("{0:X08}", titleId);
            List<InternetArchiveAsset> assets = new List<InternetArchiveAsset>();

            try
            {
                using (WebClient client = new WebClient())
                {
                    string folderUrl = $"{BaseUrl}{titleFolder}/";
                    string htmlContent = client.DownloadString(folderUrl);

                    foreach (string subDir in ParseDirectoriesFromHtmlRegex(htmlContent))
                    {
                        assets.Add(new InternetArchiveAsset
                        {
                            TitleId = titleId,
                            MainFolder = titleFolder,
                            SubFolder = subDir,
                            AssetType = "Cover"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                SendStatusChanged($"Error fetching directory: {ex.Message}");
            }

            return assets.ToArray();
        }

        private IEnumerable<string> ParseDirectoriesFromHtmlRegex(string html)
        {
            const string HREF_REGEX = "<a href=\"(.+)/\">(.+)/</a>";
            
            foreach (Match match in Regex.Matches(html, HREF_REGEX))
            {
                var hrefValue = match.Groups[1].Value;
                var text = match.Groups[2].Value;

                if (hrefValue == text)
                {
                    yield return hrefValue;
                }
            }
        }

        public Image DownloadCover(InternetArchiveAsset asset)
        {
            try
            {
                string coverUrl = $"{BaseUrl}{asset.MainFolder}/{asset.SubFolder}/boxart.png";
                using (WebClient wc = new WebClient())
                {
                    byte[] imageData = wc.DownloadData(coverUrl);

                    if (imageData == null || imageData.Length == 0)
                    {
                        SendStatusChanged("No image data received");
                        return null;
                    }

                    MemoryStream ms = new MemoryStream(imageData);

                    try
                    {
                        using (Image originalImage = Image.FromStream(ms, true, true))
                        {
                            return new Bitmap(originalImage);
                        }
                    }
                    finally
                    {
                        ms.Dispose();
                    }
                }
            }
            catch (WebException webEx)
            {
                SendStatusChanged($"Network error downloading cover: {webEx.Message}");
                return null;
            }
            catch (ArgumentException argEx)
            {
                SendStatusChanged($"Invalid image data received: {argEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                SendStatusChanged($"Error downloading cover: {ex.Message}");
                return null;
            }
        }
    }

    // Modified asset class to handle the nested structure
    public class InternetArchiveAsset
    {
        private Image _cover;
        public uint TitleId { get; set; }
        public string MainFolder { get; set; }  // The title ID folder
        public string SubFolder { get; set; }   // The subfolder containing the actual cover
        public string AssetType { get; set; }
        public bool HaveAsset { get { return _cover != null; } }

        public Image GetCover()
        {
            if (_cover == null)
            {
                var downloader = new InternetArchiveDownloader();
                _cover = downloader.DownloadCover(this);
            }
            return _cover;
        }

        public override string ToString()
        {
            return $"TitleID: {TitleId:X8} - Variant: {SubFolder}";
        }
    }
}
