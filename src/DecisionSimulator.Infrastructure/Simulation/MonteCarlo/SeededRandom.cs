namespace DecisionSimulator.Infrastructure.Simulation.MonteCarlo;

/// <summary>
/// A small, fast, deterministic pseudo-random generator (xoshiro256**).
/// <para>
/// <see cref="System.Random"/> is deliberately not used: its algorithm is not
/// guaranteed to be stable across .NET versions, which would break the promise
/// that a stored seed reproduces a stored result. This generator is fully
/// specified here, so a seed always yields the same sequence.
/// </para>
/// </summary>
public sealed class SeededRandom
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public SeededRandom(int seed)
    {
        Seed = seed;

        // SplitMix64 is used to expand the 32-bit seed into the 256-bit state.
        var x = unchecked((ulong)(uint)seed) + 0x9E3779B97F4A7C15UL;
        _s0 = SplitMix64(ref x);
        _s1 = SplitMix64(ref x);
        _s2 = SplitMix64(ref x);
        _s3 = SplitMix64(ref x);
    }

    /// <summary>The seed this generator was created with.</summary>
    public int Seed { get; }

    /// <summary>Returns a uniform double in [0,1).</summary>
    public double NextDouble()
    {
        // 53 significant bits, matching the precision of a double mantissa.
        return (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
    }

    /// <summary>Returns a uniform integer in [0, maxExclusive).</summary>
    public int NextInt(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);

        return (int)(NextUInt64() % (ulong)maxExclusive);
    }

    private ulong NextUInt64()
    {
        var result = RotateLeft(_s1 * 5UL, 7) * 9UL;
        var t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);

        return result;
    }

    private static ulong RotateLeft(ulong value, int shift) => (value << shift) | (value >> (64 - shift));

    private static ulong SplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
