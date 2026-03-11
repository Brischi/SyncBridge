using System.IO;
using System.Text.Json;

namespace SyncBridge.Infrastructure
{
    public static class AppSettingsLoader
    {

        public static AppSettings Load()
        {
            var configPath = Path.Combine(AppContext.BaseDirectory,"config.json");

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException(
                    $"Konfigurationsdatei nicht gefunden: {configPath}\n" +
                    $"Bitte config.json anhand von config.json.example anlegen.");
            }

            try
            {
                var json = File.ReadAllText(configPath);

                var settings = JsonSerializer.Deserialize<AppSettings>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (settings == null)
                {
                    throw new InvalidOperationException(
                        "config.json ist vorhanden, konnte aber nicht gelesen werden (leeres oder ungültiges JSON).");
                }

                return settings;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    "config.json enthält ungültiges JSON. Bitte Syntax prüfen.", ex);
            }
        }
    }
}