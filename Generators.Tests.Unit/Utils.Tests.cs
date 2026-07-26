using Shouldly;

namespace Generators.Tests.Unit;

public class UtilsTests
{
    [Fact]
    public void FillShuffled1To9_ShouldNot_HaveDuplicates()
    {
        int[] sut = new int[9];

        Utils.FillShuffled1To9(sut);

        sut.Distinct().Count().ShouldBe(sut.Length);
        Assert.All(sut, n => Assert.InRange(n, 1, 9));
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3, 4, 5 }, 0)]
    [InlineData(new[] { 1, 1, 2, 3, 4 }, 1)]
    [InlineData(new[] { 1, 1, 1, 2, 3 }, 2)]
    [InlineData(new[] { 1, 1, 2, 2, 3 }, 2)]
    [InlineData(new[] { 1, 1, 1, 2, 2, 2 }, 4)]
    public void CountDuplicatesInRow_Should_CountProperly(int[] input, int expected)
    {
        int[,] state = new int[1, input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            state[0, i] = input[i];
        }

        int result = Utils.CountDuplicatesInRow(state, 0);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3, 4, 5 }, 0)]
    [InlineData(new[] { 1, 1, 2, 3, 4 }, 1)]
    [InlineData(new[] { 1, 1, 1, 2, 3 }, 2)]
    [InlineData(new[] { 1, 1, 2, 2, 3 }, 2)]
    [InlineData(new[] { 1, 1, 1, 2, 2, 2 }, 4)]
    public void CountDuplicatesInColumn_Should_CountProperly(int[] input, int expected)
    {
        int[,] state = new int[input.Length, 1];
        for (int i = 0; i < input.Length; i++)
        {
            state[i, 0] = input[i];
        }

        int result = Utils.CountDuplicatesInColumn(state, 0);

        result.ShouldBe(expected);
    }
}
