using DecisionSimulator.Domain.Exceptions;
using DecisionSimulator.Infrastructure.Simulation.Expressions;
using FluentAssertions;

namespace DecisionSimulator.SimulationTests.Expressions;

public class ExpressionEvaluatorTests
{
    [Theory]
    [InlineData("1 + 2", 3)]
    [InlineData("10 - 4", 6)]
    [InlineData("3 * 4", 12)]
    [InlineData("10 / 4", 2.5)]
    [InlineData("10 % 3", 1)]
    [InlineData("2 ^ 10", 1024)]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("-5 + 3", -2)]
    [InlineData("2 ^ 3 ^ 2", 512)]
    public void Evaluates_arithmetic_with_correct_precedence(string expression, decimal expected)
    {
        ExpressionEvaluator.Parse(expression).Evaluate(new Dictionary<string, decimal>())
            .Should().Be(expected);
    }

    [Fact]
    public void Resolves_named_variables_case_insensitively()
    {
        var evaluator = ExpressionEvaluator.Parse("MONTHLY_INCOME - Monthly_Expenses");

        // Deliberately a case-sensitive dictionary: the evaluator itself must
        // match names without regard to case, not rely on the caller's comparer.
        var result = evaluator.Evaluate(new Dictionary<string, decimal>
        {
            ["monthly_income"] = 1000m,
            ["monthly_expenses"] = 400m
        });

        result.Should().Be(600m);
    }

    [Fact]
    public void Reports_referenced_variables()
    {
        var evaluator = ExpressionEvaluator.Parse("a + b * a + c");

        evaluator.ReferencedVariables.Should().BeEquivalentTo(["a", "b", "c"]);
    }

    [Theory]
    [InlineData("min(3, 7)", 3)]
    [InlineData("max(3, 7)", 7)]
    [InlineData("abs(-9)", 9)]
    [InlineData("round(2.6)", 3)]
    [InlineData("floor(2.9)", 2)]
    [InlineData("ceiling(2.1)", 3)]
    [InlineData("sqrt(81)", 9)]
    [InlineData("clamp(15, 0, 10)", 10)]
    [InlineData("clamp(-5, 0, 10)", 0)]
    public void Evaluates_built_in_functions(string expression, decimal expected)
    {
        ExpressionEvaluator.Parse(expression).Evaluate(new Dictionary<string, decimal>())
            .Should().Be(expected);
    }

    [Fact]
    public void Throws_when_a_variable_is_missing()
    {
        var evaluator = ExpressionEvaluator.Parse("income - expenses");

        var act = () => evaluator.Evaluate(new Dictionary<string, decimal> { ["income"] = 100m });

        act.Should().Throw<FormulaEvaluationException>().WithMessage("*expenses*");
    }

    [Fact]
    public void Throws_on_division_by_zero()
    {
        var evaluator = ExpressionEvaluator.Parse("10 / 0");

        var act = () => evaluator.Evaluate(new Dictionary<string, decimal>());

        act.Should().Throw<FormulaEvaluationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1 +")]
    [InlineData("(1 + 2")]
    [InlineData("1 + 2)")]
    [InlineData("* 5")]
    [InlineData("unknown_function(1)")]
    public void Throws_on_malformed_input(string expression)
    {
        var act = () => ExpressionEvaluator.Parse(expression);

        act.Should().Throw<FormulaEvaluationException>();
    }

    [Fact]
    public void Rejects_attempts_to_reach_the_runtime()
    {
        // The grammar has no member access, no strings, and no type names, so
        // these must all fail at parse time rather than execute anything.
        foreach (var attempt in new[]
                 {
                     "System.IO.File.ReadAllText(\"x\")",
                     "typeof(int)",
                     "new object()",
                     "this.GetType()"
                 })
        {
            var act = () => ExpressionEvaluator.Parse(attempt);
            act.Should().Throw<FormulaEvaluationException>();
        }
    }

    [Fact]
    public void Round_trips_through_its_canonical_form()
    {
        var original = ExpressionEvaluator.Parse("(a + b) * 2");
        var reparsed = ExpressionEvaluator.Parse(original.ToString());

        var variables = new Dictionary<string, decimal> { ["a"] = 3m, ["b"] = 4m };

        reparsed.Evaluate(variables).Should().Be(original.Evaluate(variables));
    }
}
