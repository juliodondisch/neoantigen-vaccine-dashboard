using System.Text.Json;

namespace NeoantigenPipeline.Api.Models;

public class StepParameters
{
    public Dictionary<string, object> Values { get; set; } = new();

    public T? Get<T>(string key, T? defaultValue = default)
    {
        if (!Values.TryGetValue(key, out var raw) || raw is null)
            return defaultValue;

        if (raw is T typed)
            return typed;

        if (raw is JsonElement element)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(element.GetRawText());
            }
            catch
            {
                return defaultValue;
            }
        }

        try
        {
            return (T)Convert.ChangeType(raw, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    public double GetDouble(string key, double defaultValue = 0) => Get(key, defaultValue);
    public int GetInt(string key, int defaultValue = 0) => Get(key, defaultValue);
    public bool GetBool(string key, bool defaultValue = false) => Get(key, defaultValue);
    public string? GetString(string key, string? defaultValue = null) => Get(key, defaultValue);
    public bool Has(string key) => Values.ContainsKey(key);
}
