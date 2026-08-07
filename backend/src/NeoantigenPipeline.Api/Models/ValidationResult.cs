namespace NeoantigenPipeline.Api.Models;

public class ValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> MissingTools { get; set; } = new();

    public static ValidationResult Valid() => new() { IsValid = true };

    public static ValidationResult Invalid(params string[] errors) =>
        new() { IsValid = false, Errors = errors.ToList() };

    public void AddError(string error)
    {
        Errors.Add(error);
        IsValid = false;
    }

    public void AddWarning(string warning) => Warnings.Add(warning);

    public void AddMissingTool(string toolName)
    {
        MissingTools.Add(toolName);
        IsValid = false;
    }
}
