namespace Generators;

internal struct FastRandom
{
    private ulong _state;

    private FastRandom(ulong seed) => _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    public static FastRandom CreateSeeded() => new((ulong)Random.Shared.NextInt64());

    private ulong NextUInt64()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 0x2545F4914F6CDD1DUL;
    }

    public int Next(int exclusiveMaximum) => (int)(NextUInt64() % (uint)exclusiveMaximum);
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));
}

public static class Utils
{
    public static void FillShuffled1To9(Span<int> destination)
    {
        for (int i = 0; i < destination.Length; i++) destination[i] = i + 1;
        for (int i = destination.Length - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (destination[i], destination[j]) = (destination[j], destination[i]);
        }
    }

    internal static void FillShuffled1To9(Span<byte> destination, ref FastRandom random)
    {
        for (int i = 0; i < destination.Length; i++) destination[i] = (byte)(i + 1);
        for (int i = destination.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (destination[i], destination[j]) = (destination[j], destination[i]);
        }
    }

    public static int CountDuplicatesInRow(int[,] state, int row)
    {
        int seen = 0, duplicates = 0;
        for (int col = 0; col < state.GetLength(1); col++)
        {
            int bit = 1 << state[row, col];
            if ((seen & bit) != 0) duplicates++; else seen |= bit;
        }
        return duplicates;
    }

    public static int CountDuplicatesInColumn(int[,] state, int col)
    {
        int seen = 0, duplicates = 0;
        for (int row = 0; row < state.GetLength(0); row++)
        {
            int bit = 1 << state[row, col];
            if ((seen & bit) != 0) duplicates++; else seen |= bit;
        }
        return duplicates;
    }

    internal static int CountDuplicatesInRow(ReadOnlySpan<byte> state, int row)
    {
        int seen = 0, duplicates = 0, start = row * 9;
        for (int col = 0; col < 9; col++)
        {
            int bit = 1 << state[start + col];
            if ((seen & bit) != 0) duplicates++; else seen |= bit;
        }
        return duplicates;
    }

    internal static int CountDuplicatesInColumn(ReadOnlySpan<byte> state, int col)
    {
        int seen = 0, duplicates = 0;
        for (int row = 0; row < 9; row++)
        {
            int bit = 1 << state[row * 9 + col];
            if ((seen & bit) != 0) duplicates++; else seen |= bit;
        }
        return duplicates;
    }
}
