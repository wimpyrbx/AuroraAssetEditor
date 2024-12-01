//
//  XboxAssetDownloader.cs
//  AuroraAssetEditor
//
//  Created by Swizzy on 10/05/2015
//  Copyright (c) 2015 Swizzy. All rights reserved.

namespace AuroraAssetEditor.Classes {
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using System.Xml;

    internal class XboxAssetDownloader {
        public static EventHandler<StatusArgs> StatusChanged;
        private readonly DataContractJsonSerializer _serializer = new DataContractJsonSerializer(typeof(XboxKeywordResponse));

        internal static void SendStatusChanged(string msg) {
            var handler = StatusChanged;
            if(handler != null)
                handler.Invoke(null, new StatusArgs(msg));
        }

        public XboxTitleInfo[] GetTitleInfo(uint titleId, XboxLocale locale) {
            return new[] {
                             XboxTitleInfo.FromTitleId(titleId, locale)
                         };
        }

        public XboxTitleInfo[] GetTitleInfo(string keywords, XboxLocale locale) {
            var url = string.Format("http://marketplace.xbox.com/{0}/SiteSearch/xbox/?query={1}&PageSize=5", locale.Locale, WebUtility.UrlEncode(keywords));
            var wc = new WebClient();
            var ret = new List<XboxTitleInfo>();
            using(var stream = wc.OpenRead(url)) {
                if(stream == null)
                    return ret.ToArray();
                var res = (XboxKeywordResponse)_serializer.ReadObject(stream);
                ret.AddRange(from entry in res.Entries where entry.DetailsUrl != null let tid = entry.DetailsUrl.IndexOf("d802", StringComparison.Ordinal) where tid > 0 && entry.DetailsUrl.Length >= tid + 12 select uint.Parse(entry.DetailsUrl.Substring(tid + 4, 8), NumberStyles.HexNumber) into titleId select XboxTitleInfo.FromTitleId(titleId, locale));
            }
            return ret.ToArray();
        }

        public static XboxLocale[] GetLocales() {
            try {
                var ret = new List<XboxLocale>();
                ret.Add(new XboxLocale("es-AR", "Argentina - Espa\xf1ol"));
                ret.Add(new XboxLocale("pt-BR", "Brasil - Portugu\xeas"));
                ret.Add(new XboxLocale("en-CA", "Canada - English"));
                ret.Add(new XboxLocale("fr-CA", "Canada - Fran\xe7ais"));
                ret.Add(new XboxLocale("es-CL", "Chile - Espa\xf1ol"));
                ret.Add(new XboxLocale("es-CO", "Colombia - Espa\xf1ol"));
                ret.Add(new XboxLocale("es-MX", "M\xe9xico - Espa\xf1ol"));
                ret.Add(new XboxLocale("en-US", "United States - English"));
                ret.Add(new XboxLocale("nl-BE", "Belgi\xeb - Nederlands"));
                ret.Add(new XboxLocale("fr-BE", "Belgique - Fran\xe7ais"));
                ret.Add(new XboxLocale("cs-CZ", "\u010cesk\xe1 Republika - \u010ce\u0161tina"));
                ret.Add(new XboxLocale("da-DK", "Danmark - Dansk"));
                ret.Add(new XboxLocale("de-DE", "Deutschland - Deutsch"));
                ret.Add(new XboxLocale("es-ES", "Espa\xf1a - Espa\xf1ol"));
                ret.Add(new XboxLocale("fr-FR", "France - Fran\xe7ais"));
                ret.Add(new XboxLocale("en-IE", "Ireland - English"));
                ret.Add(new XboxLocale("it-IT", "Italia - Italiano"));
                ret.Add(new XboxLocale("hu-HU", "Magyarorsz\xe1g - Magyar"));
                ret.Add(new XboxLocale("nl-NL", "Nederland - Nederlands"));
                ret.Add(new XboxLocale("nb-NO", "Norge - Norsk bokm\xe5l"));
                ret.Add(new XboxLocale("de-AT", "\xd6sterreich - Deutsch"));
                ret.Add(new XboxLocale("pl-PL", "Polska - Polski"));
                ret.Add(new XboxLocale("pt-PT", "Portugal - Portugu\xeas"));
                ret.Add(new XboxLocale("de-CH", "Schweiz - Deutsch"));
                ret.Add(new XboxLocale("sk-SK", "Slovensko - Sloven\u010dina"));
                ret.Add(new XboxLocale("fr-CH", "Suisse - Fran\xe7ais"));
                ret.Add(new XboxLocale("fi-FI", "Suomi - Suomi"));
                ret.Add(new XboxLocale("sv-SE", "Sverige - Svenska"));
                ret.Add(new XboxLocale("en-GB", "United Kingdom - English"));
                ret.Add(new XboxLocale("el-GR", "\u0395\u03bb\u03bb\u03ac\u03b4\u03b1 - \u0395\u03bb\u03bb\u03b7\u03bd\u03b9\u03ba\u03ac"));
                ret.Add(new XboxLocale("ru-RU", "\u0420\u043e\u0441\u0441\u0438\u044f - \u0420\u0443\u0441\u0441\u043a\u0438\u0439"));
                ret.Add(new XboxLocale("en-AU", "Australia - English"));
                ret.Add(new XboxLocale("en-HK", "Hong Kong SAR / Macao SAR - English"));
                ret.Add(new XboxLocale("en-IN", "India - English"));
                ret.Add(new XboxLocale("id-ID", "Indonesia - Bahasa Indonesia"));
                ret.Add(new XboxLocale("en-MY", "Malaysia - English"));
                ret.Add(new XboxLocale("en-NZ", "New Zealand - English"));
                ret.Add(new XboxLocale("en-PH", "Philippines - English"));
                ret.Add(new XboxLocale("en-SG", "Singapore - English"));
                ret.Add(new XboxLocale("vi-VN", "Vi\u1ec7t Nam - Ti\xea\u0301ng vi\u1ec7t"));
                ret.Add(new XboxLocale("th-TH", "\u0e44\u0e17\u0e22 - \u0e44\u0e17\u0e22"));
                ret.Add(new XboxLocale("ko-KR", "\ub300\ud55c\ubbfc\uad6d - \ud55c\uad6d\uc5b4"));
                ret.Add(new XboxLocale("zh-CN", "\u4e2d\u56fd - \u4e2d\u6587"));
                ret.Add(new XboxLocale("zh-TW", "\u53f0\u7063 - \u7e41\u9ad4\u4e2d\u6587"));
                ret.Add(new XboxLocale("ja-JP", "\u65e5\u672c - \u65e5\u672c\u8a9e"));
                ret.Add(new XboxLocale("zh-HK", "\u9999\u6e2f\u7279\u5225\u884c\u653f\u5340/\u6fb3\u9580\u7279\u5225\u884c\u653f\u5340 - \u7e41\u9ad4\u4e2d\u6587"));
                ret.Add(new XboxLocale("en-ZA", "South Africa - English"));
                ret.Add(new XboxLocale("tr-TR", "T\xfcrkiye - T\xfcrk\xe7e"));
                ret.Add(new XboxLocale("he-IL", "\u05d9\u05e9\u05e8\u05d0\u05dc - \u05e2\u05d1\u05e8\u05d9\u05ea"));
                ret.Add(new XboxLocale("ar-AE", "\u0627\u0644\u0625\u0645\u0627\u0631\u0627\u062a \u0627\u0644\u0639\u0631\u0628\u064a\u0629 \u0627\u0644\u0645\u062a\u062d\u062f\u0629 - \u0627\u0644\u0639\u0631\u0628\u064a\u0629"));
                ret.Add(new XboxLocale("ar-SA", "\u0627\u0644\u0645\u0645\u0644\u0643\u0629 \u0627\u0644\u0639\u0631\u0628\u064a\u0629 \u0627\u0644\u0633\u0639\u0648\u062f\u064a\u0629 - \u0627\u0644\u0639\u0631\u0628\u064a\u0629"));
                ret.Sort((l1, l2) => string.CompareOrdinal(l1.ToString(), l2.ToString()));
                return ret.ToArray();
            }
            catch { return new XboxLocale[0]; }
        }
    }

    public class XboxTitleInfo {
        public enum XboxAssetType {
            Icon,
            Banner,
            Background,
            Screenshot
        }

        public string Title { get; private set; }

        public string TitleId { get; private set; }

        public string Locale { get; private set; }

        public XboxAssetInfo[] AssetsInfo { get; private set; }

        public XboxAsset[] Assets {
            get {
                if(AssetsInfo.Any(info => !info.HaveAsset))
                    XboxAssetDownloader.SendStatusChanged(string.Format("Downloading assets for {0}...", Title));
                var ret = AssetsInfo.Select(info => info.GetAsset()).ToArray();
                return ret;
            }
        }

        public XboxAsset[] IconAssets {
            get {
                if(AssetsInfo.Where(info => info.AssetType == XboxAssetType.Icon).Any(info => !info.HaveAsset))
                    XboxAssetDownloader.SendStatusChanged(string.Format("Downloading icon assets for {0}...", Title));
                var ret = AssetsInfo.Where(info => info.AssetType == XboxAssetType.Icon).Select(info => info.GetAsset()).ToArray();
                return ret;
            }
        }

        public XboxAsset[] BannerAssets {
            get {
                if(AssetsInfo.Where(info => info.AssetType == XboxAssetType.Banner).Any(info => !info.HaveAsset))
                    XboxAssetDownloader.SendStatusChanged(string.Format("Downloading banner assets for {0}...", Title));
                var ret = AssetsInfo.Where(info => info.AssetType == XboxAssetType.Banner).Select(info => info.GetAsset()).ToArray();
                return ret;
            }
        }

        public XboxAsset[] BackgroundAssets {
            get {
                if(AssetsInfo.Where(info => info.AssetType == XboxAssetType.Background).Any(info => !info.HaveAsset))
                    XboxAssetDownloader.SendStatusChanged(string.Format("Downloading background assets for {0}...", Title));
                var ret = AssetsInfo.Where(info => info.AssetType == XboxAssetType.Background).Select(info => info.GetAsset()).ToArray();
                return ret;
            }
        }

        public XboxAsset[] ScreenshotsAssets {
            get {
                if(AssetsInfo.Where(info => info.AssetType == XboxAssetType.Screenshot).Any(info => !info.HaveAsset))
                    XboxAssetDownloader.SendStatusChanged(string.Format("Downloading screenshot assets for {0}...", Title));
                var ret = AssetsInfo.Where(info => info.AssetType == XboxAssetType.Screenshot).Select(info => info.GetAsset()).ToArray();
                return ret;
            }
        }

        private static void ParseXml(Stream xmlData, XboxTitleInfo titleInfo) {
            XboxAssetDownloader.SendStatusChanged("Parsing Title/Asset info...");
            var ret = new List<XboxAssetInfo>();
            using(var xml = XmlReader.Create(xmlData)) {
                while(xml.Read()) {
                    if(!xml.IsStartElement())
                        continue;
                    if (xml.Name.ToLower() == "live:fulltitle") {
                        xml.Read();
                        titleInfo.Title = xml.Value;
                        continue;
                    }
                    // add every 'live:fileurl' within 'live:slideshows' as a screenshot asset
                    if (xml.Name.ToLower() == "live:slideshows") {
                        while (xml.Read()) {
                            if (!xml.IsStartElement() && xml.Name.ToLower() == "live:slideshows") {
                                break;
                            }
                            if (xml.IsStartElement() && xml.Name.ToLower() == "live:fileurl") {
                                xml.Read();
                                var url = new Uri(xml.Value);
                                ret.Add(new XboxAssetInfo(url, XboxAssetType.Screenshot, titleInfo));
                            }
                        }
                    }
                    // add every 'live:image' within 'live:images' that has both 'live:relationshiptype' and 'live:fileurl' properties as an asset
                    // the value of 'live:relationshiptype' is used to determine the asset type
                    if (xml.Name.ToLower() == "live:images") {
                        var imageRelationshipType = "";
                        var imageFileUrl = "";
                        while (xml.Read()) {
                            if (!xml.IsStartElement() && xml.Name.ToLower() == "live:images") {
                                break;
                            }
                            if (!xml.IsStartElement() && xml.Name.ToLower() == "live:image") {
                                if (imageRelationshipType != "" && imageFileUrl != "") {
                                    switch (imageRelationshipType) {
                                        case "15":
                                            ret.Add(new XboxAssetInfo(new Uri(imageFileUrl), XboxAssetType.Icon, titleInfo));
                                            break;
                                        case "23":
                                            ret.Add(new XboxAssetInfo(new Uri(imageFileUrl), XboxAssetType.Icon, titleInfo));
                                            break;
                                        case "25":
                                            ret.Add(new XboxAssetInfo(new Uri(imageFileUrl), XboxAssetType.Background, titleInfo));
                                            break;
                                        case "27":
                                            ret.Add(new XboxAssetInfo(new Uri(imageFileUrl), XboxAssetType.Banner, titleInfo));
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                imageRelationshipType = "";
                                imageFileUrl = "";
                                continue;
                            }
                            if (xml.IsStartElement() && xml.Name.ToLower() == "live:relationshiptype") {
                                xml.Read();
                                imageRelationshipType = xml.Value;
                            }
                            if (xml.IsStartElement() && xml.Name.ToLower() == "live:fileurl") {
                                xml.Read();
                                imageFileUrl = xml.Value;
                            }
                        }
                    }
                }
            }
            titleInfo.AssetsInfo = ret.ToArray();
            XboxAssetDownloader.SendStatusChanged("Finished parsing Title/Asset info...");
        }

        public static XboxTitleInfo FromTitleId(uint titleId, XboxLocale locale) {
            var ret = new XboxTitleInfo {
                                            TitleId = string.Format("{0:X08}", titleId),
                                            Locale = locale.ToString()
                                        };
            var wc = new WebClient();
            var url =
                string.Format(
                              "http://catalog-cdn.xboxlive.com/Catalog/Catalog.asmx/Query?methodName=FindGames&Names=Locale&Values={0}&Names=LegalLocale&Values={0}&Names=Store&Values=1&Names=PageSize&Values=100&Names=PageNum&Values=1&Names=DetailView&Values=5&Names=OfferFilterLevel&Values=1&Names=MediaIds&Values=66acd000-77fe-1000-9115-d802{1:X8}&Names=UserTypes&Values=2&Names=MediaTypes&Values=1&Names=MediaTypes&Values=21&Names=MediaTypes&Values=23&Names=MediaTypes&Values=37&Names=MediaTypes&Values=46",
                              locale.Locale, titleId);
            XboxAssetDownloader.SendStatusChanged("Downloading title/asset information...");
            using(var stream = new MemoryStream(wc.DownloadData(url)))
                ParseXml(stream, ret);
            return ret;
        }

        public class XboxAsset {
            public readonly XboxAssetType AssetType;

            public readonly Image Image;

            public XboxAsset(Image image, XboxAssetType assetType) {
                Image = image;
                AssetType = assetType;
            }
        }

        public class XboxAssetInfo {
            public readonly XboxAssetType AssetType;
            public readonly Uri AssetUrl;
            private readonly XboxTitleInfo _titleInfo;
            private XboxAsset _asset;

            public XboxAssetInfo(Uri assetUrl, XboxAssetType assetType, XboxTitleInfo titleInfo) {
                AssetUrl = assetUrl;
                AssetType = assetType;
                _titleInfo = titleInfo;
            }

            public bool HaveAsset { get { return _asset != null; } }

            public XboxAsset GetAsset() {
                if(_asset != null)
                    return _asset; // We already have it
                var wc = new WebClient();
                var data = wc.DownloadData(AssetUrl);
                var ms = new MemoryStream(data);
                var img = Image.FromStream(ms);
                return _asset = new XboxAsset(img, AssetType);
            }

            public override string ToString() { return string.Format("{0} [ {1} ] {2}", _titleInfo.Title, _titleInfo.TitleId, AssetType); }
        }
    }

    public class XboxLocale {
        public readonly string Locale;

        private readonly string _name;

        public XboxLocale(string locale, string name) {
            Locale = locale;
            _name = name;
        }

        public override string ToString() { return string.Format("{0} [ {1} ]", _name, Locale); }
    }

    [DataContract] public class XboxKeywordResponse {
        [DataMember(Name = "entries")] public Entry[] Entries { get; set; }

        [DataContract] public class Entry {
            [DataMember(Name = "detailsUrl")] public string DetailsUrl { get; set; }
            //There is more data sent both here and ^, but we only need this, so i only added that...
        }
    }
}
