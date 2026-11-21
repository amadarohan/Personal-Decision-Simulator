using DecisionSimulator.Infrastructure.Simulation.Distributions;
using DecisionSimulator.Infrastructure.Simulation.MonteCarlo;

namespace DecisionSimulator.Infrastructure.Simulation.Correlation;

/// <summary>
/// A declared correlation between two variables.
/// <para>
/// Correlations are never inferred or invented. Each one must carry a stated
/// basis and, where applicable, a source reference, because a correlation
/// assumption can dominate a simulation's output. The UI must show the basis
/// next to every correlation the user has not personally confirmed.
/// </para>
/// </summary>
public sealed class CorrelationAssumption
{
    public required string VariableA { get; init; }

    public required string VariableB { get; init; }

    /// <summary>Correlation coefficient in [-1, 1].</summary>
    public required decimal Coefficient { get; init; }

    /// <summary>Why this correlation is believed to hold.</summary>
    public required string Basis { get; init; }

    /// <summary>Where the basis came from. Null when the user supplied it directly.</summary>
    public string? SourceReference { get; init; }

    /// <summary>True when the user entered this value themselves.</summary>
    public bool IsUserSupplied { get; init; }

    public void Validate()
    {
        if (Coefficient is < -1m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Coefficient),
                $"The correlation between '{VariableA}' and '{VariableB}' must be between -1 and 1.");
        }

        if (string.Equals(VariableA, VariableB, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A variable cannot be correlated with itself.");
        }

        if (string.IsNullOrWhiteSpace(Basis))
        {
            throw new ArgumentException(
                $"The correlation between '{VariableA}' and '{VariableB}' needs a stated basis. " +
                "Correlations must never be presented as established without one.");
        }
    }
}

/// <summary>
/// Generates correlated uniform draws using the Iman-Conover rank correlation
/// method.
/// <para>
/// Iman-Conover is used rather than sampling directly from a Gaussian copula
/// because it preserves each variable's marginal distribution exactly: the
/// marginals are sampled independently and then reordered to match a target
/// rank correlation. A triangular income distribution and a log-normal price
/// distribution can therefore be correlated without either being distorted.
/// </para>
/// <para>
/// The method needs the whole sample matrix at once, because reordering is a
/// global operation over all iterations. <see cref="Generate"/> therefore
/// returns a complete matrix rather than one draw at a time.
/// </para>
/// </summary>
public sealed class CorrelatedSampler
{
    private readonly string[] _keys;
    private readonly double[,]? _cholesky;

    /// <param name="keys">Variable keys in a fixed order.</param>
    /// <param name="assumptions">Declared correlations between those keys.</param>
    public CorrelatedSampler(IReadOnlyList<string> keys, IReadOnlyList<CorrelationAssumption> assumptions)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(assumptions);

        _keys = keys.ToArray();

        var correlation = new double[_keys.Length, _keys.Length];
        for (var i = 0; i < _keys.Length; i++)
        {
            correlation[i, i] = 1d;
        }

        foreach (var assumption in assumptions)
        {
            assumption.Validate();

            var a = Array.FindIndex(_keys, k => string.Equals(k, assumption.VariableA, StringComparison.OrdinalIgnoreCase));
            var b = Array.FindIndex(_keys, k => string.Equals(k, assumption.VariableB, StringComparison.OrdinalIgnoreCase));

            // A correlation naming a variable that is not being sampled is
            // ignored rather than silently applied to the wrong thing.
            if (a < 0 || b < 0)
            {
                continue;
            }

            correlation[a, b] = (double)assumption.Coefficient;
            correlation[b, a] = (double)assumption.Coefficient;
        }

        _cholesky = CholeskyOrNull(correlation, _keys.Length);
        IsCorrelationHonoured = _cholesky is not null;
    }

    public IReadOnlyList<string> Keys => _keys;

    /// <summary>
    /// False when the declared correlations are mutually inconsistent (the
    /// matrix is not positive definite). In that case sampling falls back to
    /// independence and the caller must tell the user, rather than quietly
    /// producing output that does not match the stated assumptions.
    /// </summary>
    public bool IsCorrelationHonoured { get; }

    /// <summary>
    /// Produces a matrix of uniforms in [0,1), indexed <c>[iteration, keyIndex]</c>.
    /// </summary>
    public double[,] Generate(int iterations, SeededRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);

        var size = _keys.Length;
        var uniforms = new double[iterations, size];

        // Step 1: independent uniform draws for every variable.
        for (var t = 0; t < iterations; t++)
        {
            for (var k = 0; k < size; k++)
            {
                uniforms[t, k] = random.NextDouble();
            }
        }

        if (size < 2 || _cholesky is null)
        {
            return uniforms;
        }

        // Step 2: correlated normal scores via the Cholesky factor.
        var scores = new double[iterations, size];
        var normals = new double[size];

        for (var t = 0; t < iterations; t++)
        {
            for (var k = 0; k < size; k++)
            {
                normals[k] = NormalDistribution.StandardNormalQuantile(random.NextDouble());
            }

            for (var i = 0; i < size; i++)
            {
                var sum = 0d;
                for (var j = 0; j <= i; j++)
                {
                    sum += _cholesky[i, j] * normals[j];
                }

                scores[t, i] = sum;
            }
        }

        // Step 3: reorder each variable's independent draws so that their ranks
        // match the ranks of the correlated scores. Marginals are preserved
        // exactly; only the pairing between variables changes.
        var result = new double[iterations, size];

        for (var k = 0; k < size; k++)
        {
            var sortedValues = new double[iterations];
            var scoreColumn = new double[iterations];

            for (var t = 0; t < iterations; t++)
            {
                sortedValues[t] = uniforms[t, k];
                scoreColumn[t] = scores[t, k];
            }

            Array.Sort(sortedValues);

            var order = Enumerable.Range(0, iterations).ToArray();
            Array.Sort(order, (x, y) => scoreColumn[x].CompareTo(scoreColumn[y]));

            for (var rank = 0; rank < iterations; rank++)
            {
                result[order[rank], k] = sortedValues[rank];
            }
        }

        return result;
    }

    /// <summary>
    /// Lower-triangular Cholesky factor, or null when the matrix is not positive
    /// definite.
    /// </summary>
    private static double[,]? CholeskyOrNull(double[,] matrix, int size)
    {
        var lower = new double[size, size];

        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j <= i; j++)
            {
                var sum = matrix[i, j];

                for (var k = 0; k < j; k++)
                {
                    sum -= lower[i, k] * lower[j, k];
                }

                if (i == j)
                {
                    if (sum <= 1e-12)
                    {
                        return null;
                    }

                    lower[i, j] = Math.Sqrt(sum);
                }
                else
                {
                    lower[i, j] = sum / lower[j, j];
                }
            }
        }

        return lower;
    }
}
