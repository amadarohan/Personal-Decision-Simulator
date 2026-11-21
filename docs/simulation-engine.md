# Simulation engine

## Purpose

The engine projects a scenario across a month-by-month horizon. It does not forecast. It answers,
"Given these assumptions, what does the range of outcomes look like?"

## Core components

| Component | Responsibility |
| --- | --- |
| `MonteCarloRunner` | Samples distributions and aggregates paths |
| `SeededRandom` | Generates the xoshiro256** pseudo-random sequence |
| `DistributionFactory` | Converts variable definitions into distributions |
| `CorrelatedSampler` | Preserves declared correlations between assumptions |
| `CashFlowProjectionModel` | Projects cash flows by month |
| `FinancialModels` | Implements financial projections |
| `FormulaModel` | Evaluates expression-based models |
| `SensitivityAnalyzer` | Measures the effect of changing one assumption |
| `StressTestRunner` | Applies a stress scenario |
| `SimulationOrchestrator` | Coordinates model selection and execution |

## Determinism

A run stores an integer seed. `SeededRandom` expands that seed with SplitMix64 and advances state
with a fully specified xoshiro256** implementation. The code deliberately does not use
`System.Random`, whose algorithm is not stable across .NET versions.

The same model, variables, correlations, and seed therefore produce the same sequence and result.

## Distributions

The engine supports fixed, uniform, normal, log-normal, triangular, and discrete distributions.
Every distribution implements `Sample(double u)`, where `u` is a uniform value in `[0,1)`. This
keeps sampling independent of the random-number source.

## Iterations and percentiles

`MonteCarloOptions` defaults to 1,000 iterations and reports percentiles 10, 25, 50, 75, and 90.
It allows at most 200,000 iterations.

The result includes:

- Per-metric mean and standard deviation.
- Per-metric percentile map.
- Per-month percentile bands for cash.
- Share of paths with negative cash.
- Warning counts.
- Whether the result is effectively deterministic.
- Whether declared correlation was honoured.

## Time series

Each month receives the sampled value for every path. `Statistics.Percentile` sorts the path values
and reports the requested percentile. The UI can use these bands to draw a fan chart and show how
uncertainty grows over the horizon.

## Correlation

A `ScenarioDependency` records the key that another variable depends on and a correlation
coefficient. `CorrelatedSampler` generates correlated draws and reports whether the coefficient was
honoured. Correlation prevents two related assumptions from being sampled independently.

## Assumptions and provenance

A scenario variable has a `DistributionBasis`:

- `Empirical`: derived from observations.
- `Exploratory`: a hypothetical assumption for exploration.
- `UserDefined`: a value the user supplied or estimated.

A run must not label an exploratory or user-defined value as measured. The UI keeps the distinction
visible.

## Reproducibility

The application stores the seed and model version with `SimulationRun`. Together with the stored
scenario variables and model definition, they identify the inputs needed to reproduce a result.

## Validation

- Horizon must be positive.
- Iteration count must be between 1 and 200,000.
- Percentiles must be in `[0,100]`.
- Unknown variable keys fail rather than silently becoming zero.
