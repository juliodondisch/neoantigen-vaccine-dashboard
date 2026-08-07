using System.Globalization;
using System.Reflection;

namespace NeoantigenPipeline.Api.Common;

/// <summary>
/// Minimal reflection-based TSV reader/writer shared by every step that hands
/// a flat row-shaped payload across the C#/Python subprocess boundary.
/// Column names match property names case-insensitively, ignoring underscores.
/// </summary>
public static class TsvParser
{
    public static List<T> Parse<T>(string text) where T : new()
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r')).ToList();
        if (lines.Count == 0)
            return new List<T>();

        var header = lines[0].Split('\t');
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => Normalize(p.Name), p => p);

        var results = new List<T>();
        for (var i = 1; i < lines.Count; i++)
        {
            var cells = lines[i].Split('\t');
            var item = new T();
            for (var c = 0; c < header.Length && c < cells.Length; c++)
            {
                if (!props.TryGetValue(Normalize(header[c]), out var prop))
                    continue;
                SetValue(item, prop, cells[c]);
            }
            results.Add(item);
        }
        return results;
    }

    public static void Write<T>(string path, IEnumerable<T> rows, string[]? columns = null)
    {
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var selected = columns is null
            ? props
            : columns.Select(c => props.First(p => Normalize(p.Name) == Normalize(c))).ToArray();

        using var writer = new StreamWriter(path);
        writer.WriteLine(string.Join('\t', selected.Select(p => p.Name)));
        foreach (var row in rows)
        {
            var cells = selected.Select(p => Convert.ToString(p.GetValue(row), CultureInfo.InvariantCulture) ?? "");
            writer.WriteLine(string.Join('\t', cells));
        }
    }

    private static void SetValue(object target, PropertyInfo prop, string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return;
        try
        {
            var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            object value = type switch
            {
                _ when type == typeof(string) => raw,
                _ when type == typeof(int) => int.Parse(raw, CultureInfo.InvariantCulture),
                _ when type == typeof(double) => double.Parse(raw, CultureInfo.InvariantCulture),
                _ when type == typeof(bool) => raw is "1" or "true" or "True",
                _ when type.IsEnum => Enum.Parse(type, raw, ignoreCase: true),
                _ => raw,
            };
            prop.SetValue(target, value);
        }
        catch
        {
            // Malformed cell for this column: leave the property at its default rather than aborting the whole row.
        }
    }

    private static string Normalize(string name) => name.Replace("_", "").ToLowerInvariant();
}
