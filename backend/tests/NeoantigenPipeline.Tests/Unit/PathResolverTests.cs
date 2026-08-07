using NeoantigenPipeline.Api.Common;

namespace NeoantigenPipeline.Tests.Unit;

public class PathResolverTests
{
    private static PathResolver Build() => new(new AppConfig
    {
        DataRoot = "/tmp/neoantigen-test-data",
        ReferenceRoot = "/tmp/neoantigen-test-data/references",
        PythonScriptsRoot = "/tmp/neoantigen-test-python",
    });

    [Fact]
    public void RejectsPathTraversalInPatientId()
    {
        var resolver = Build();

        Assert.Throws<ArgumentException>(() => resolver.GetPatientDir("../../etc"));
    }

    [Fact]
    public void RejectsPathTraversalInFileName()
    {
        var resolver = Build();

        Assert.Throws<ArgumentException>(() => resolver.GetStepDir("patient1", "../02_alignment"));
    }

    [Fact]
    public void TimestampFormatIsSortable()
    {
        var t1 = PathResolver.Timestamp();
        System.Threading.Thread.Sleep(1100);
        var t2 = PathResolver.Timestamp();

        Assert.Equal(15, t1.Length); // yyyyMMdd_HHmmss
        Assert.True(string.Compare(t1, t2, StringComparison.Ordinal) <= 0);
    }

    [Fact]
    public void BuildOutputPathNeverCollides()
    {
        var resolver = Build();
        var patientId = "collision-test-" + Guid.NewGuid().ToString("N")[..8];

        var path1 = resolver.BuildOutputPath(patientId, PipelineStepIds.Variants, "somatic", ".vcf.gz");
        System.Threading.Thread.Sleep(1100);
        var path2 = resolver.BuildOutputPath(patientId, PipelineStepIds.Variants, "somatic", ".vcf.gz");

        Assert.NotEqual(path1, path2);

        Directory.Delete(resolver.GetPatientDir(patientId), recursive: true);
    }
}
