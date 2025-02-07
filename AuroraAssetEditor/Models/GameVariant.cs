using System;
using Newtonsoft.Json;

namespace AuroraAssetEditor.Models
{
    public class GameVariant
    {
        [JsonProperty("db_id")]
        public int DbId { get; set; }

        [JsonProperty("MediaID")]
        public string MediaId { get; set; }

        [JsonProperty("GameName")]
        public string GameName { get; set; }

        [JsonProperty("Edition")]
        public string Edition { get; set; }

        [JsonProperty("Localization")]
        public string Localization { get; set; }

        [JsonProperty("DiscNum")]
        public string DiscNum { get; set; }

        [JsonProperty("XEX_CRC")]
        public string XexCrc { get; set; }

        [JsonProperty("Serial")]
        public string Serial { get; set; }

        [JsonProperty("Type")]
        public string Type { get; set; }

        [JsonProperty("Region")]
        public string Region { get; set; }

        [JsonProperty("Wave")]
        public string Wave { get; set; }
    }
} 