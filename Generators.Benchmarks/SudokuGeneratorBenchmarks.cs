using BenchmarkDotNet.Attributes;
using Generators.Sudoku;

namespace Generators.Benchmarks;

[SimpleJob(warmupCount: 3, iterationCount: 10)]
[MemoryDiagnoser]
public class SudokuGeneratorBenchmarks
{
    private readonly Board _reusableBoard = new();

    [Benchmark]
    public Board Generate() => SudokuGenerator.Generate(SudokuGenerator.Difficulty.Easy);

    [Benchmark]
    public void GenerateIntoReusedBoard() => SudokuGenerator.Generate(SudokuGenerator.Difficulty.Easy, _reusableBoard);

    [Benchmark]
    public void GenerateExpertIntoReusedBoard() => SudokuGenerator.Generate(SudokuGenerator.Difficulty.Expert, _reusableBoard);
}
