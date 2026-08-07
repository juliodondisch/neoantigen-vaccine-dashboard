using NeoantigenPipeline.Api.Common;
using NeoantigenPipeline.Api.Services._01_Upload;
using NeoantigenPipeline.Api.Services._02_Alignment;
using NeoantigenPipeline.Api.Services._03_VariantCalling;
using NeoantigenPipeline.Api.Services._04_ProteinEffects;
using NeoantigenPipeline.Api.Services._05_HlaTyping;
using NeoantigenPipeline.Api.Services._06_CandidateGeneration;
using NeoantigenPipeline.Api.Services._07_Presentation;
using NeoantigenPipeline.Api.Services._08_Immunogenicity;
using NeoantigenPipeline.Api.Services._09_Filtering;
using NeoantigenPipeline.Api.Services._10_Ranking;
using NeoantigenPipeline.Api.Services._11_VaccineDesign;
using NeoantigenPipeline.Api.Testing;

var builder = WebApplication.CreateBuilder(args);

// Config
builder.Services.Configure<AppConfig>(builder.Configuration.GetSection("App"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppConfig>>().Value);

// Infrastructure (singleton — stateless)
builder.Services.AddSingleton<PathResolver>();
builder.Services.AddSingleton<FileSystemService>();
builder.Services.AddSingleton<PythonRunner>();
builder.Services.AddSingleton<ToolChecker>();

// Steps — concrete registration needed because some services take others as constructor
// dependencies (see docs/TECHNICAL_SPEC.md Appendix A). Register both the concrete type
// and IPipelineStep for each.
builder.Services.AddSingleton<UploadService>();
builder.Services.AddSingleton<IPipelineStep>(sp => sp.GetRequiredService<UploadService>());

builder.Services.AddSingleton<AlignmentService>();
builder.Services.AddSingleton<IPipelineStep>(sp => sp.GetRequiredService<AlignmentService>());

builder.Services.AddSingleton<VariantCallingService>();
builder.Services.AddSingleton<IPipelineStep>(sp => sp.GetRequiredService<VariantCallingService>());

builder.Services.AddSingleton<ProteinEffectsService>();
builder.Services.AddSingleton<IPipelineStep>(sp => sp.GetRequiredService<ProteinEffectsService>());

builder.Services.AddSingleton<HlaTypingService>();
builder.Services.AddSingleton<IPipelineStep>(sp => sp.GetRequiredService<HlaTypingService>());

builder.Services.AddSingleton<CandidateGenerationService>();
builder.Services.AddSingleton<IPipelineStep>(sp => sp.GetRequiredService<CandidateGenerationService>());

builder.Services.AddSingleton<PresentationService>();
builder.Services.AddSingleton<IPipelineStep>(sp => sp.GetRequiredService<PresentationService>());

builder.Services.AddSingleton<ImmunogenicityService>();
builder.Services.AddSingleton<IPipelineStep>(sp => sp.GetRequiredService<ImmunogenicityService>());

builder.Services.AddSingleton<FilteringService>();
builder.Services.AddSingleton<IPipelineStep>(sp => sp.GetRequiredService<FilteringService>());

builder.Services.AddSingleton<RankingService>();
builder.Services.AddSingleton<IPipelineStep>(sp => sp.GetRequiredService<RankingService>());

builder.Services.AddSingleton<VaccineDesignService>();
builder.Services.AddSingleton<IPipelineStep>(sp => sp.GetRequiredService<VaccineDesignService>());

builder.Services.AddSingleton<StepRegistry>();
builder.Services.AddSingleton<PatientRepository>();
builder.Services.AddSingleton<JobManager>();
builder.Services.AddSingleton<FixtureSeeder>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.Services.GetRequiredService<AppConfig>().Validate();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
