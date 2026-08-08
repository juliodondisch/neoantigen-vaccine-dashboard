using Microsoft.AspNetCore.Mvc;
using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Models;
using NeoantigenPipeline.Api.Models.Dto;

namespace NeoantigenPipeline.Api.Controllers;

[ApiController]
[Route("api/patients/{patientId}/steps/{stepId}/files")]
public class FilesController : ControllerBase
{
    private readonly FileSystemService _files;
    private readonly PathResolver _paths;
    private readonly PatientLogger _patientLog;
    private readonly ILogger<FilesController> _logger;

    public FilesController(FileSystemService files, PathResolver paths, PatientLogger patientLog, ILogger<FilesController> logger)
    {
        _files = files;
        _paths = paths;
        _patientLog = patientLog;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<List<ManagedFile>> List(string patientId, string stepId) =>
        Ok(_files.ListStepFiles(patientId, stepId));

    [HttpPost("upload")]
    public async Task<ActionResult<UploadResponse>> Upload(string patientId, string stepId, [FromForm] List<IFormFile> files, [FromForm] string? fileKind)
    {
        if (files.Count == 0)
            return BadRequest(new UploadResponse { Success = false, Error = "No files provided" });

        var uploaded = new List<ManagedFile>();
        foreach (var file in files)
        {
            var saved = await _files.SaveUploadAsync(patientId, stepId, file, fileKind);
            uploaded.Add(saved);
            _patientLog.Info(patientId, stepId, $"Uploaded '{saved.Name}' ({saved.SizeBytes} bytes, kind={fileKind ?? "unspecified"})");
        }
        return Ok(new UploadResponse { Success = true, UploadedFiles = uploaded });
    }

    [HttpPost("register")]
    public async Task<ActionResult<UploadResponse>> RegisterPath(string patientId, string stepId, [FromBody] RegisterFileRequest request)
    {
        try
        {
            var registered = await _files.RegisterExternalFileAsync(patientId, stepId, request.SourcePath, request.FileKind, request.Copy);
            _patientLog.Info(patientId, stepId, $"Registered external path '{request.SourcePath}' -> '{registered.Name}' (copy={request.Copy})");
            return Ok(new UploadResponse { Success = true, UploadedFiles = new List<ManagedFile> { registered } });
        }
        catch (FileNotFoundException ex)
        {
            _patientLog.Warning(patientId, stepId, $"Register path failed for '{request.SourcePath}': {ex.Message}");
            return BadRequest(new UploadResponse { Success = false, Error = ex.Message });
        }
    }

    [HttpGet("{fileName}/download")]
    public ActionResult Download(string patientId, string stepId, string fileName)
    {
        try
        {
            var stream = _files.OpenRead(patientId, stepId, fileName);
            _patientLog.Info(patientId, stepId, $"Downloaded '{fileName}'");
            return File(stream, "application/octet-stream", fileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{fileName}/preview")]
    public ActionResult<string> Preview(string patientId, string stepId, string fileName, [FromQuery] int maxLines = 100)
    {
        var text = _files.ReadTextFile(patientId, stepId, fileName);
        if (text is null)
            return NotFound();
        var lines = text.Split('\n').Take(maxLines);
        return Ok(string.Join('\n', lines));
    }

    [HttpDelete("{fileName}")]
    public ActionResult Delete(string patientId, string stepId, string fileName)
    {
        var deleted = _files.DeleteFile(patientId, stepId, fileName);
        if (deleted)
            _patientLog.Warning(patientId, stepId, $"Deleted '{fileName}'");
        return deleted ? NoContent() : NotFound();
    }
}

public class RegisterFileRequest
{
    public string SourcePath { get; set; } = "";
    public string? FileKind { get; set; }
    public bool Copy { get; set; } = false;
}
