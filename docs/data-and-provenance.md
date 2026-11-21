# Data and provenance

## Why provenance matters

A number in a decision model is not enough. The person also needs to know where it came from, when
it was true, how reliable it is, and whether someone checked it.

## Sources

`DataSource` represents a source. A source has:

- A name and optional base URL.
- A provider kind: manual, CSV, JSON, REST API, or web research.
- A reliability rating: unknown, low, medium, high, or authoritative.
- An update cadence: irregular, daily, weekly, monthly, quarterly, annual, real-time.
- An active state.

A source is a container for evidence. It is not a claim that every observation from it is correct.

## Indicators

`EconomicIndicator` gives an observation a stable code and unit. For example:

- `IR.CPI.YOY` as percent.
- `IR.FX.USD` as Iranian rial.
- `IR.RENT.INDEX` as an index.
- `IR.UNEMP.RATE` as percent.

An indicator is also tagged with category, country, and geographic scope.

## Observations

`DataObservation` records one reading:

- The value.
- The observation date.
- The source ID.
- The confidence level.
- Whether it has been verified.

The UI never treats a list of observations as a proof of correctness. It shows the source and freshness
beside the value.

## Freshness

Freshness is computed from the observation date and update cadence. The UI distinguishes current,
stale, and aging data. There is no hidden rule that silently replaces an old observation.

## Confidence

Confidence is separate from verification. A verified reading can still have low confidence if the
source or process is weak. An unverified reading can come from an authoritative source and still need
human review.

## Distribution basis

This is the interface's distinction between evidence and assumption:

| Basis | Meaning |
| --- | --- |
| `Empirical` | Derived from observations |
| `Exploratory` | A hypothetical assumption used to learn |
| `UserDefined` | A value supplied or estimated by the user |

The demo data uses only `Exploratory` and `UserDefined` values. It does not present illustrative
numbers as measured observations.

## Built-in sources

The demo seeder creates three sources:

- Central Bank of Iran.
- Statistical Center of Iran.
- Manual personal observations.

The indicators are illustrative and can be replaced or verified from real source data before the
application is used for a consequential decision.
