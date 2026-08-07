namespace NeoantigenPipeline.Api.Common;

public class StepRegistry
{
    private readonly Dictionary<string, IPipelineStep> _steps;
    private readonly List<Models.StepDefinition> _orderedDefinitions;
    private readonly List<IPipelineStep> _orderedSteps;

    public StepRegistry(IEnumerable<IPipelineStep> steps)
    {
        _orderedSteps = steps.OrderBy(s => s.Definition.Order).ToList();
        _steps = _orderedSteps.ToDictionary(s => s.Definition.Id);
        _orderedDefinitions = _orderedSteps.Select(s => s.Definition).ToList();
    }

    public IReadOnlyList<Models.StepDefinition> GetAllDefinitions() => _orderedDefinitions;

    public IPipelineStep GetStep(string stepId)
    {
        if (!_steps.TryGetValue(stepId, out var step))
            throw new KeyNotFoundException($"No step registered with id '{stepId}'.");
        return step;
    }

    public bool TryGetStep(string stepId, out IPipelineStep? step) => _steps.TryGetValue(stepId, out step);

    public IPipelineStep? GetPreviousStep(string stepId)
    {
        var idx = _orderedSteps.FindIndex(s => s.Definition.Id == stepId);
        return idx > 0 ? _orderedSteps[idx - 1] : null;
    }

    public IPipelineStep? GetNextStep(string stepId)
    {
        var idx = _orderedSteps.FindIndex(s => s.Definition.Id == stepId);
        return idx >= 0 && idx < _orderedSteps.Count - 1 ? _orderedSteps[idx + 1] : null;
    }

    public IReadOnlyList<IPipelineStep> GetAllSteps() => _orderedSteps;

    public int StepCount => _orderedSteps.Count;
}
