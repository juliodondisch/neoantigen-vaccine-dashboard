using Microsoft.AspNetCore.Mvc;
using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;

namespace NeoantigenPipeline.Api.Controllers;

[ApiController]
[Route("api/tools")]
public class ToolsController : ControllerBase
{
    private readonly ToolChecker _tools;
    private readonly FileSystemService _files;
    private readonly ReferenceSetupService _referenceSetup;
    private readonly AppConfig _config;

    public ToolsController(ToolChecker tools, FileSystemService files, ReferenceSetupService referenceSetup, AppConfig config)
    {
        _tools = tools;
        _files = files;
        _referenceSetup = referenceSetup;
        _config = config;
    }

    [HttpGet]
    public ActionResult<List<ToolStatus>> ListAll() => Ok(_tools.CheckAll());

    [HttpGet("{toolName}")]
    public ActionResult<ToolStatus> Get(string toolName) => Ok(_tools.Check(toolName));

    [HttpPost("refresh")]
    public ActionResult<List<ToolStatus>> Refresh()
    {
        _tools.InvalidateCache();
        return Ok(_tools.CheckAll());
    }

    [HttpGet("disk")]
    public ActionResult<DiskStatus> GetDiskStatus() => Ok(new DiskStatus
    {
        AvailableBytes = _files.GetAvailableDiskBytes(),
        DataUsedBytes = 0,
    });

    [HttpGet("references")]
    public ActionResult<ReferenceStatus> GetReferenceStatus([FromQuery] string? genome) =>
        Ok(_referenceSetup.GetStatus(genome ?? _config.DefaultReferenceGenome));
}

public class DiskStatus
{
    public long AvailableBytes { get; set; }
    public long DataUsedBytes { get; set; }
}
