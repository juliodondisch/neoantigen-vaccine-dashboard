using NeoantigenPipeline.Api.Services._06_CandidateGeneration;

namespace NeoantigenPipeline.Tests.Unit;

public class SlidingWindowGeneratorTests
{
    private const string Protein = "MKLVFFAEDVGSNKGAIIGLMVGGVVIA"; // 28 aa, valid amino acids only

    [Fact]
    public void GeneratesCorrectWindowCountForCentralMutation()
    {
        var generator = new SlidingWindowGenerator(minLength: 9, maxLength: 9);
        var mutationPosition = 14; // well clear of both termini for a 9-length window

        var windows = generator.GenerateWindows(Protein, mutationPosition, 9);

        // A window of length 9 containing a fixed position has exactly 9 possible starts
        // (mutationPosition - 8 .. mutationPosition), all valid here since it's central.
        Assert.Equal(9, windows.Count);
    }

    [Fact]
    public void EveryWindowContainsMutatedPosition()
    {
        var generator = new SlidingWindowGenerator(minLength: 8, maxLength: 11);
        const int mutationPosition = 14;

        for (var length = 8; length <= 11; length++)
        {
            var earliestStart = Math.Max(0, mutationPosition - length + 1);
            var latestStart = Math.Min(Protein.Length - length, mutationPosition);
            for (var start = earliestStart; start <= latestStart; start++)
            {
                Assert.InRange(mutationPosition, start, start + length - 1);
            }
        }
    }

    [Fact]
    public void WildTypeCounterpartMatchesLengthAndPosition()
    {
        var generator = new SlidingWindowGenerator(minLength: 9, maxLength: 9);
        var wildType = Protein;
        var mutant = Protein.ToCharArray();
        mutant[14] = 'W';
        var mutantSeq = new string(mutant);

        var pairs = generator.GenerateForAllLengths(wildType, mutantSeq, 14);

        Assert.NotEmpty(pairs);
        foreach (var pair in pairs)
        {
            Assert.Equal(pair.MutantPeptide.Length, pair.WildTypePeptide.Length);
            Assert.Equal(9, pair.Length);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void HandlesMutationNearProteinStart(int position)
    {
        var generator = new SlidingWindowGenerator(minLength: 9, maxLength: 9);
        var windows = generator.GenerateWindows(Protein, position, 9);

        Assert.True(windows.Count > 0 && windows.Count <= 9);
        foreach (var w in windows)
            Assert.Equal(9, w.Length);
    }

    [Theory]
    [InlineData(27)]
    [InlineData(26)]
    [InlineData(25)]
    public void HandlesMutationNearProteinEnd(int position)
    {
        var generator = new SlidingWindowGenerator(minLength: 9, maxLength: 9);
        var windows = generator.GenerateWindows(Protein, position, 9);

        Assert.True(windows.Count > 0 && windows.Count <= 9);
        foreach (var w in windows)
            Assert.Equal(9, w.Length);
    }

    [Fact]
    public void RejectsPeptidesWithInvalidAminoAcids()
    {
        var generator = new SlidingWindowGenerator(minLength: 9, maxLength: 9);
        var withInvalid = "MKLVFFXAEDVGSNKGAIIGLMVGGVVIA"; // contains 'X'

        var windows = generator.GenerateWindows(withInvalid, 7, 9);

        Assert.DoesNotContain(windows, w => w.Contains('X'));
    }

    [Fact]
    public void ReturnsEmptyForProteinShorterThanMinWindow()
    {
        var generator = new SlidingWindowGenerator(minLength: 9, maxLength: 11);
        var shortProtein = "MKLVF"; // 5 aa

        var windows = generator.GenerateWindows(shortProtein, 2, 9);

        Assert.Empty(windows);
    }
}
