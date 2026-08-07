using System.Text.Json;

namespace NeoantigenPipeline.Api.Models;

public class PythonResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public List<string> OutputFiles { get; set; } = new();
    public Dictionary<string, object> Summary { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static PythonResponse Parse(string stdout)
    {
        if (!TryParse(stdout, out var response) || response is null)
            throw new InvalidOperationException("Could not parse a ###JSON_START###/###JSON_END### block from Python stdout.");
        return response;
    }

    public static bool TryParse(string stdout, out PythonResponse? response)
    {
        response = null;
        const string startMarker = "###JSON_START###";
        const string endMarker = "###JSON_END###";

        var startIdx = stdout.IndexOf(startMarker, StringComparison.Ordinal);
        var endIdx = stdout.IndexOf(endMarker, StringComparison.Ordinal);
        if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx)
            return false;

        var json = stdout[(startIdx + startMarker.Length)..endIdx].Trim();
        try
        {
            response = JsonSerializer.Deserialize<PythonResponse>(json, JsonOptions);
            return response is not null;
        }
        catch
        {
            return false;
        }
    }
}
