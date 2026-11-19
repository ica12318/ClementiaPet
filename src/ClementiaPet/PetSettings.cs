using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace ClementiaPet
{
    [DataContract]
    internal sealed class PetSettings
    {
        [DataMember(Name = "schemaVersion", Order = 0)] public int SchemaVersion { get; set; }
        [DataMember(Name = "form", Order = 1)] public string Form { get; set; }
        [DataMember(Name = "x", Order = 2)] public int X { get; set; }
        [DataMember(Name = "y", Order = 3)] public int Y { get; set; }
        [DataMember(Name = "scale", Order = 4)] public double Scale { get; set; }
        [DataMember(Name = "documentId", Order = 5)] public string DocumentId { get; set; }
        [DataMember(Name = "through", Order = 6)] public bool Through { get; set; }
        [DataMember(Name = "hintShown", Order = 7)] public bool HintShown { get; set; }
        [DataMember(Name = "hasPosition", Order = 8)] public bool HasPosition { get; set; }

        [DataMember(Name = "eveningEnabled")] public bool EveningEnabled;
        [DataMember(Name = "idleEnabled")] public bool IdleEnabled;
        [DataMember(Name = "waterEnabled")] public bool WaterEnabled;
        [DataMember(Name = "waterMinutes")] public int WaterMinutes;
        [DataMember(Name = "eveningDate")] public string EveningDate;
        [DataMember(Name = "waterDueUtcTicks")] public long WaterDueUtc;
        [DataMember(Name = "lastSpeechUtcTicks")] public long LastSpeechUtc;

        [OnDeserializing]
        private void SetSpeechDefaults(StreamingContext context)
        {
            EveningEnabled = IdleEnabled = true;
            WaterMinutes = SpeechTiming.DefaultInterval;
        }

        internal PetSettings()
        {
            SetSpeechDefaults(new StreamingContext());
            SchemaVersion = 2;
            Form = "human";
            Scale = 1.0;
            DocumentId = "shore-spelling";
        }
    }

    internal static class SettingsStore
    {
        private static string DirectoryPath
        {
            get
            {
                string overridePath = Environment.GetEnvironmentVariable("CLEMENTIAPET_SETTINGS_DIR");
                if (!string.IsNullOrEmpty(overridePath)) return overridePath;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClementiaPet");
            }
        }

        private static string FilePath { get { return Path.Combine(DirectoryPath, "settings.json"); } }

        internal static PetSettings Load()
        {
            if (!File.Exists(FilePath)) return new PetSettings();
            try
            {
                using (var stream = File.OpenRead(FilePath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(PetSettings));
                    var settings = (PetSettings)serializer.ReadObject(stream);
                    if (settings == null || (settings.SchemaVersion != 1 && settings.SchemaVersion != 2) || settings.Scale <= 0)
                        throw new InvalidDataException("Unsupported settings schema.");
                    if (settings.Form != "ray") settings.Form = "human";
                    settings.SchemaVersion = 2;
                    if (Array.IndexOf(SpeechTiming.Intervals, settings.WaterMinutes) < 0)
                        settings.WaterMinutes = SpeechTiming.DefaultInterval;
                    return settings;
                }
            }
            catch
            {
                PreserveCorruptFile();
                return new PetSettings();
            }
        }

        internal static void Save(PetSettings settings)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                string temp = FilePath + ".tmp";
                using (var stream = File.Create(temp))
                {
                    var serializer = new DataContractJsonSerializer(typeof(PetSettings));
                    serializer.WriteObject(stream, settings);
                    stream.Flush();
                }
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
                else File.Move(temp, FilePath);
            }
            catch
            {
                // Persistence is best-effort and must never terminate the pet.
            }
        }

        private static void PreserveCorruptFile()
        {
            try
            {
                string backup = FilePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json";
                File.Copy(FilePath, backup, true);
            }
            catch
            {
            }
        }
    }
}
