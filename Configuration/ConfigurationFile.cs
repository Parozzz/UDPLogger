using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace UDPLogger
{
    public class ConfigurationFile
    {
        private static readonly string PATH = Directory.GetCurrentDirectory() + "/configuration.json";
        private static readonly JsonSerializerOptions OPTIONS = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip,
        };

        public string IPAddress { get; set; } = "172.16.4.1";
        public int RemotePort { get; set; } = 8958;
        public int LocalPort { get; set; } = 10000;
        public string DatabasePath { get; set; } = "~\\database.db";

        public string GetFullDatabasePath()
        {
            return DatabasePath.Replace("~\\", Directory.GetCurrentDirectory() + "\\");
        }

        public void Save()
        {
            try
            {
                var jsonString = JsonSerializer.Serialize(this, OPTIONS);
                File.WriteAllText(PATH, jsonString, Encoding.UTF8);
            }
            catch { }
        }

        public static ConfigurationFile Load()
        {
            try
            {
                if (!File.Exists(PATH))
                {
                    return new();
                }

                using var fileStream = File.Open(PATH, FileMode.Open, FileAccess.Read);

                var configuration = JsonSerializer.Deserialize<ConfigurationFile>(fileStream, OPTIONS);
                if (configuration == null)
                {
                    return new();
                }

                return configuration;
            }
            catch
            {
                return new();
            }
        }

    }
}
