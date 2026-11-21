using System.Globalization;
using System.Text;
using DecisionSimulator.Domain.Exceptions;

namespace DecisionSimulator.Infrastructure.Simulation.Expressions;

/// <summary>
/// A safe, deterministic arithmetic expression evaluator.
/// <para>
/// This exists because model formulas must never be executed as arbitrary code.
/// The evaluator supports only numbers, named variables, the operators
/// <c>+ - * / % ^</c>, parentheses, and a fixed set of pure functions. There is
/// no reflection, no type system, no I/O, and no way to reach the CLR.
/// </para>
/// <para>
/// Expressions are parsed once into an immutable syntax tree and can then be
/// evaluated many times with different variable values, which is what the Monte
/// Carlo engine needs.
/// </para>
/// </summary>
public sealed class ExpressionEvaluator
{
    private readonly Node _root;

    private ExpressionEvaluator(Node root, IReadOnlyCollection<string> referencedVariables)
    {
        _root = root;
        ReferencedVariables = referencedVariables;
    }

    /// <summary>Variable names referenced by the expression, in first-seen order.</summary>
    public IReadOnlyCollection<string> ReferencedVariables { get; }

    /// <summary>
    /// Parses an expression. Throws <see cref="FormulaEvaluationException"/> with
    /// a human-readable message when the input is malformed.
    /// </summary>
    public static ExpressionEvaluator Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new FormulaEvaluationException("The formula is empty.");
        }

        var parser = new Parser(expression);
        var root = parser.ParseExpression();
        parser.ExpectEnd();
        return new ExpressionEvaluator(root, parser.ReferencedVariables);
    }

    /// <summary>
    /// Evaluates the expression using the supplied variable values. Missing
    /// variables raise a <see cref="FormulaEvaluationException"/> rather than
    /// silently defaulting to zero.
    /// </summary>
    public decimal Evaluate(IReadOnlyDictionary<string, decimal> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return _root.Evaluate(variables);
    }

    public decimal Evaluate(params (string Name, decimal Value)[] variables)
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in variables)
        {
            map[name] = value;
        }

        return Evaluate(map);
    }

    private abstract class Node
    {
        public abstract decimal Evaluate(IReadOnlyDictionary<string, decimal> variables);
    }

    private sealed class ConstantNode(decimal value) : Node
    {
        public decimal Value { get; } = value;

        public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables) => Value;
    }

    private sealed class VariableNode(string name) : Node
    {
        public string Name { get; } = name;

        public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables)
        {
            if (variables.TryGetValue(Name, out var value))
            {
                return value;
            }

            // Fall back to a case-insensitive scan so that a caller using a
            // plain Dictionary still gets the intuitive behaviour.
            foreach (var (key, candidate) in variables)
            {
                if (string.Equals(key, Name, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            throw new FormulaEvaluationException($"The formula references an unknown variable '{Name}'.");
        }
    }

    private sealed class UnaryNode(char op, Node operand) : Node
    {
        public char Operator { get; } = op;

        public Node Operand { get; } = operand;

        public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables)
        {
            var value = Operand.Evaluate(variables);
            return Operator == '-' ? -value : value;
        }
    }

    private sealed class BinaryNode(char op, Node left, Node right) : Node
    {
        public char Operator { get; } = op;

        public Node Left { get; } = left;

        public Node Right { get; } = right;

        public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables)
        {
            var l = Left.Evaluate(variables);
            var r = Right.Evaluate(variables);

            return Operator switch
            {
                '+' => l + r,
                '-' => l - r,
                '*' => l * r,
                '/' => r == 0m
                    ? throw new FormulaEvaluationException("The formula divides by zero.")
                    : l / r,
                '%' => r == 0m
                    ? throw new FormulaEvaluationException("The formula takes a remainder by zero.")
                    : l % r,
                '^' => Pow(l, r),
                _ => throw new FormulaEvaluationException($"Unsupported operator '{Operator}'.")
            };
        }

        private static decimal Pow(decimal baseValue, decimal exponent)
        {
            // Decimal has no Pow. Convert through double but guard the domain so
            // that NaN/Infinity never leak into financial results.
            var result = Math.Pow((double)baseValue, (double)exponent);

            if (double.IsNaN(result) || double.IsInfinity(result))
            {
                throw new FormulaEvaluationException("The formula produced a non-finite value.");
            }

            if (Math.Abs(result) > (double)decimal.MaxValue)
            {
                throw new FormulaEvaluationException("The formula produced a value outside the supported range.");
            }

            return (decimal)result;
        }
    }

    private sealed class FunctionNode(string name, IReadOnlyList<Node> arguments) : Node
    {
        public string Name { get; } = name;

        public IReadOnlyList<Node> Arguments { get; } = arguments;

        public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables)
        {
            var args = new decimal[Arguments.Count];
            for (var i = 0; i < Arguments.Count; i++)
            {
                args[i] = Arguments[i].Evaluate(variables);
            }

            return Apply(Name, args);
        }

        private static decimal Apply(string name, decimal[] args)
        {
            return name.ToLowerInvariant() switch
            {
                "min" => Require(args, 2, name, () => Math.Min(args[0], args[1])),
                "max" => Require(args, 2, name, () => Math.Max(args[0], args[1])),
                "abs" => Require(args, 1, name, () => Math.Abs(args[0])),
                "round" => Require(args, 1, name, () => Math.Round(args[0], MidpointRounding.AwayFromZero)),
                "floor" => Require(args, 1, name, () => Math.Floor(args[0])),
                "ceiling" => Require(args, 1, name, () => Math.Ceiling(args[0])),
                "sqrt" => Require(args, 1, name, () => args[0] < 0m
                    ? throw new FormulaEvaluationException("sqrt() requires a non-negative argument.")
                    : (decimal)Math.Sqrt((double)args[0])),
                "clamp" => Require(args, 3, name, () => Math.Min(Math.Max(args[0], args[1]), args[2])),
                _ => throw new FormulaEvaluationException($"Unknown function '{name}'.")
            };
        }

        private static decimal Require(decimal[] args, int expected, string name, Func<decimal> compute)
        {
            if (args.Length != expected)
            {
                throw new FormulaEvaluationException(
                    $"Function '{name}' expects {expected} argument(s) but received {args.Length}.");
            }

            return compute();
        }
    }

    /// <summary>
    /// Recursive-descent parser. Grammar (lowest to highest precedence):
    /// <code>
    /// expression := term (('+' | '-') term)*
    /// term       := power (('*' | '/' | '%') power)*
    /// power      := unary ('^' power)?          // right-associative
    /// unary      := ('+' | '-') unary | primary
    /// primary    := number | identifier | identifier '(' arguments ')' | '(' expression ')'
    /// </code>
    /// </summary>
    private sealed class Parser(string text)
    {
        private readonly List<string> _referenced = [];
        private int _position;

        public IReadOnlyCollection<string> ReferencedVariables => _referenced;

        public Node ParseExpression()
        {
            var left = ParseTerm();

            while (true)
            {
                SkipWhitespace();
                if (Peek() is not ('+' or '-'))
                {
                    return left;
                }

                var op = Next();
                var right = ParseTerm();
                left = new BinaryNode(op, left, right);
            }
        }

        public void ExpectEnd()
        {
            SkipWhitespace();
            if (_position < text.Length)
            {
                throw new FormulaEvaluationException(
                    $"Unexpected character '{text[_position]}' at position {_position + 1}.");
            }
        }

        private Node ParseTerm()
        {
            var left = ParsePower();

            while (true)
            {
                SkipWhitespace();
                if (Peek() is not ('*' or '/' or '%'))
                {
                    return left;
                }

                var op = Next();
                var right = ParsePower();
                left = new BinaryNode(op, left, right);
            }
        }

        private Node ParsePower()
        {
            var left = ParseUnary();
            SkipWhitespace();

            if (Peek() != '^')
            {
                return left;
            }

            Next();
            var right = ParsePower();
            return new BinaryNode('^', left, right);
        }

        private Node ParseUnary()
        {
            SkipWhitespace();
            var c = Peek();

            if (c is '+' or '-')
            {
                Next();
                return new UnaryNode(c, ParseUnary());
            }

            return ParsePrimary();
        }

        private Node ParsePrimary()
        {
            SkipWhitespace();

            if (_position >= text.Length)
            {
                throw new FormulaEvaluationException("The formula ends unexpectedly.");
            }

            var c = text[_position];

            if (c == '(')
            {
                Next();
                var inner = ParseExpression();
                SkipWhitespace();

                if (Peek() != ')')
                {
                    throw new FormulaEvaluationException($"Missing closing parenthesis at position {_position + 1}.");
                }

                Next();
                return inner;
            }

            if (char.IsDigit(c) || c == '.')
            {
                return ParseNumber();
            }

            if (char.IsLetter(c) || c == '_')
            {
                return ParseIdentifierOrCall();
            }

            throw new FormulaEvaluationException($"Unexpected character '{c}' at position {_position + 1}.");
        }

        private ConstantNode ParseNumber()
        {
            var start = _position;

            while (_position < text.Length && (char.IsDigit(text[_position]) || text[_position] == '.'))
            {
                _position++;
            }

            var literal = text[start.._position];

            if (!decimal.TryParse(literal, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormulaEvaluationException($"'{literal}' is not a valid number.");
            }

            return new ConstantNode(value);
        }

        private Node ParseIdentifierOrCall()
        {
            var start = _position;

            while (_position < text.Length && (char.IsLetterOrDigit(text[_position]) || text[_position] == '_'))
            {
                _position++;
            }

            var name = text[start.._position];
            SkipWhitespace();

            if (Peek() != '(')
            {
                if (!_referenced.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    _referenced.Add(name);
                }

                return new VariableNode(name);
            }

            Next();
            var arguments = new List<Node>();
            SkipWhitespace();

            if (Peek() == ')')
            {
                Next();
                ValidateFunction(name, 0);
                return new FunctionNode(name, arguments);
            }

            while (true)
            {
                arguments.Add(ParseExpression());
                SkipWhitespace();

                if (Peek() == ',')
                {
                    Next();
                    continue;
                }

                if (Peek() == ')')
                {
                    Next();
                    break;
                }

                throw new FormulaEvaluationException(
                    $"Expected ',' or ')' in the argument list of '{name}' at position {_position + 1}.");
            }

            ValidateFunction(name, arguments.Count);
            return new FunctionNode(name, arguments);
        }

        /// <summary>
        /// Rejects unknown functions and wrong arity at parse time, so a typo
        /// surfaces immediately instead of failing on the first evaluation.
        /// </summary>
        private static void ValidateFunction(string name, int argumentCount)
        {
            var expected = name.ToLowerInvariant() switch
            {
                "min" or "max" => 2,
                "abs" or "round" or "floor" or "ceiling" or "sqrt" => 1,
                "clamp" => 3,
                _ => throw new FormulaEvaluationException($"Unknown function '{name}'.")
            };

            if (argumentCount != expected)
            {
                throw new FormulaEvaluationException(
                    $"Function '{name}' expects {expected} argument(s) but received {argumentCount}.");
            }
        }

        private char Peek() => _position < text.Length ? text[_position] : '\0';

        private char Next() => text[_position++];

        private void SkipWhitespace()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position]))
            {
                _position++;
            }
        }
    }

    /// <summary>
    /// Renders the parsed expression back to a canonical, fully parenthesised
    /// string. Useful for logging and for showing the user exactly what will be
    /// evaluated, independent of the original whitespace.
    /// </summary>
    public override string ToString()
    {
        var builder = new StringBuilder();
        Render(_root, builder);
        return builder.ToString();
    }

    private static void Render(Node node, StringBuilder builder)
    {
        switch (node)
        {
            case ConstantNode constant:
                builder.Append(constant.Value.ToString(CultureInfo.InvariantCulture));
                break;

            case VariableNode variable:
                builder.Append(variable.Name);
                break;

            case UnaryNode unary:
                builder.Append(unary.Operator);
                Render(unary.Operand, builder);
                break;

            case BinaryNode binary:
                builder.Append('(');
                Render(binary.Left, builder);
                builder.Append(binary.Operator);
                Render(binary.Right, builder);
                builder.Append(')');
                break;

            case FunctionNode function:
                builder.Append(function.Name).Append('(');
                for (var i = 0; i < function.Arguments.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    Render(function.Arguments[i], builder);
                }

                builder.Append(')');
                break;
        }
    }
}
