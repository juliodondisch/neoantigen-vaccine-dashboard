using Microsoft.Extensions.Logging.Abstractions;
using NeoantigenPipeline.Api.Common;

namespace NeoantigenPipeline.Tests.Unit;

public class ReferenceSetupServiceTests
{
    private static ReferenceSetupService Build(string referenceRoot)
    {
        var config = new AppConfig
        {
            DataRoot = Path.Combine(referenceRoot, "data"),
            ReferenceRoot = referenceRoot,
            PythonScriptsRoot = Path.Combine(referenceRoot, "scripts"),
        };
        var paths = new PathResolver(config);
        var patientLog = new PatientLogger(paths);
        var files = new FileSystemService(paths, NullLogger<FileSystemService>.Instance);
        var python = new PythonRunner(config, paths, patientLog, NullLogger<PythonRunner>.Instance);
        return new ReferenceSetupService(paths, python, files, NullLogger<ReferenceSetupService>.Instance);
    }

    [Fact]
    public void IsReadyIsFalseWhenFastaMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "ref-test-" + Guid.NewGuid().ToString("N")[..8]);
        var service = Build(root);

        Assert.False(service.IsReady("chr21_test"));

        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void IsReadyIsTrueOnlyWhenBothFastaAndBwaIndexExist()
    {
        var root = Path.Combine(Path.GetTempPath(), "ref-test-" + Guid.NewGuid().ToString("N")[..8]);
        var genomeDir = Path.Combine(root, "chr21_test");
        Directory.CreateDirectory(genomeDir);
        var fasta = Path.Combine(genomeDir, "chr21.fa");
        File.WriteAllText(fasta, ">chr21\nACGT\n");
        var service = Build(root);

        Assert.False(service.IsReady("chr21_test")); // fasta present, index not yet

        File.WriteAllText(fasta + ".bwt.2bit.64", "stub index");

        Assert.True(service.IsReady("chr21_test"));

        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void EstimateRequiredBytesIsMuchLargerForFullGenomeThanTestChromosome()
    {
        var root = Path.Combine(Path.GetTempPath(), "ref-test-" + Guid.NewGuid().ToString("N")[..8]);
        var service = Build(root);

        var chr21Estimate = service.EstimateRequiredBytes("chr21_test");
        var grch38Estimate = service.EstimateRequiredBytes("GRCh38");

        Assert.True(grch38Estimate > chr21Estimate * 10);
    }

    [Fact]
    public void DescribeBlockerIsNullWhenAlreadyReady()
    {
        var root = Path.Combine(Path.GetTempPath(), "ref-test-" + Guid.NewGuid().ToString("N")[..8]);
        var genomeDir = Path.Combine(root, "chr21_test");
        Directory.CreateDirectory(genomeDir);
        File.WriteAllText(Path.Combine(genomeDir, "chr21.fa"), ">chr21\nACGT\n");
        File.WriteAllText(Path.Combine(genomeDir, "chr21.fa.bwt.2bit.64"), "stub index");
        var service = Build(root);

        Assert.Null(service.DescribeBlocker("chr21_test"));

        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void DescribeBlockerExplainsInsufficientSpaceWithGigabyteNumbers()
    {
        var root = Path.Combine(Path.GetTempPath(), "ref-test-" + Guid.NewGuid().ToString("N")[..8]);
        var service = Build(root);

        // GRCh38 needs ~40GB; a dev machine essentially never has that much scratch space
        // free in CI, so this exercises the real HasEnoughDiskSpace() path against the
        // real disk rather than a mock — if it ever flakes true, this machine has >45GB free.
        var blocker = service.DescribeBlocker("GRCh38");

        Assert.NotNull(blocker);
        Assert.Contains("GB", blocker);
        Assert.Contains("GRCh38", blocker);

        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
