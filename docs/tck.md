# DMN TCK conformance baseline

The [OMG DMN Technology Compatibility Kit](https://github.com/dmn-tck/tck) is a
black-box suite of DMN models and expected results. It is the closest thing the
DMN ecosystem has to an external conformance measurement: every vendor runs the
same files and publishes the same result CSV.

## Canonical specifications

Use the specification page for the version under test:

- **DMN 1.6** — https://www.omg.org/spec/DMN/1.6
- **DMN 1.5** — https://www.omg.org/spec/DMN/1.5
- **DMN 1.4** — https://www.omg.org/spec/DMN/1.4

The current TCK snapshot declares the DMN 1.5 namespace, so DMN 1.5 is the
primary reference for interpreting its models. Use DMN 1.6 for 1.6-only
behaviour and DMN 1.4 for 1.4-specific parsing or model behaviour.

This repository carries a local runner (`ScratchyDisk.DmnEngine.Tck`) and a
committed baseline (`docs/tck-baseline.json`). The baseline is a ratchet, not a
certification: it records the expected result case by case, so a regression is
always reported as a new failure rather than hidden in a total.

## Suite snapshot

The suite is pinned to a commit, not a branch. The current snapshot is
`a428da2f9c5ff2a4b8566ab511144e9022d743fe` (2026-11-20). Its models declare the
DMN 1.5 namespace; the engine auto-detects the version per model, so the runner
itself is version-agnostic.

Covered:

- `compliance-level-2` — 29 suites, 28 model files, 116 cases.
- `compliance-level-3` — 124 suites, 180 model files, 3,321 cases (some suites
  carry several models so imports can be exercised).

`non-compliant` is deliberately excluded: those cases are known to have no
passing implementation.

One TCK test *case* is one result. A case with several `resultNode`s passes only
when all of them pass. This matches the published result format.

## Fetch and run

```bash
./scripts/fetch-tck.sh
dotnet run --project ScratchyDisk.DmnEngine.Tck -- --tck artifacts/tck/TestCases
```

The download lands in `artifacts/tck`, which is already gitignored. The script
writes the pinned commit to `artifacts/tck/.commit`; the runner records it in the
baseline. The TCK is not part of CI — it downloads an external repository — so run
it locally before and after a change to conformance behaviour.

Useful options:

| Option | Purpose |
| --- | --- |
| `--level 2`, `--level 3`, `--level all` | Restrict by compliance level |
| `--suite 0105,0106` | Restrict by suite name substring |
| `--case 016` | Restrict by case id (debugging one failure) |
| `--out`, `--details`, `--markdown` | Output paths |
| `--write-baseline <json>` | Replace the baseline |
| `--baseline <json>` | Compare and fail on new failures |
| `--commit <sha>` | Override the TCK commit recorded in the baseline |

Outputs:

- `artifacts/tck/tck_results.csv` — the official shape
  (`"suite/model","file","case","SUCCESS|ERROR|IGNORED","message"`), one row per
  case.
- `artifacts/tck/tck_details.csv` — one row per result node, with expected and
  actual values for triage.
- `artifacts/tck/tck_summary.md` — per-level totals, top failing suites and
  unsupported constructs.

Exit codes: `0` no behavioural failures (or baseline comparison clean), `1`
failures or new failures, `2` setup/usage error.

## Result vocabulary

| Status | Meaning |
| --- | --- |
| `PASS` | Expected value matched, or the expected error occurred |
| `FAIL` | Evaluation ran and the answer was wrong, or an error was raised where a value was expected |
| `PARSE-FAIL` | The model or test XML could not be read |
| `NOT-FOUND` | The result node names a decision the engine did not produce |
| `UNSUPPORTED` | The engine cannot evaluate the construct at all (an external Java function) |
| `FIXTURE-ERROR` | The test value cannot be represented (for example a negative year) |

`PARSE-FAIL`, `NOT-FOUND`, `UNSUPPORTED` and `FIXTURE-ERROR` are **blocked**:
they never reached a decision. The summary reports `executed pass rate` over
cases that did, and lists blocked reasons separately, so a missing feature does
not masquerade as a wrong answer.

The official CSV maps this vocabulary to the TCK's three statuses:
`PASS → SUCCESS`, `UNSUPPORTED`/`FIXTURE-ERROR → IGNORED`, everything else →
`ERROR`.

## Comparison semantics

The TCK does not define comparison rules; each vendor runner decides. This
runner uses:

- Numbers: absolute tolerance `1e-8` (the same value the Camunda runner uses).
- Contexts: compared by key, order-insensitive.
- Lists: compared in order.
- Ranges: compared as the four FEEL properties `start`, `start included`, `end`,
  `end included`.
- Date-time: when the expected value carries an offset, instants are compared;
  when it does not, wall-clock values are compared.
- Multi-hit hit policies (`rule order`, `output order`, `collect` with `list`)
  always return a list, even for a single hit.
- Single-output decisions return the scalar; compound outputs return a context.

Error results need one extra rule. The TCK accepts "any error" for
`errorResult="true"`, which lets a vendor paper over missing features. This
runner separates:

- a genuine decision error (division by zero, invalid function argument) —
  `PASS` for an error-result test;
- an infrastructure failure before evaluation (`outputType is null`, input not
  bound, model not parsed) — `UNSUPPORTED`, never a pass.

That distinction is why the raw pass rate is much lower than a vendor runner's
"green" board would suggest.

## Baseline workflow

```bash
# regenerate after a deliberate change
dotnet run --project ScratchyDisk.DmnEngine.Tck -- --write-baseline docs/tck-baseline.json

# check for regressions (what CI should run)
dotnet run --project ScratchyDisk.DmnEngine.Tck -- --baseline docs/tck-baseline.json
```

The baseline stores every non-passing case by
`level/suite/test-file/case-id`, with its status and reason. A case that is
absent from the baseline is assumed to have passed, so a newly failing case is
always a new failure. New passes are reported as fixed and do not fail the run.
Review the baseline diff when updating it: a large unexplained change usually
means the suite or the harness changed, not the engine.

Regenerate the baseline only after the corresponding engine change is
committed, so the committed baseline always describes a reachable tree state.

## Current baseline

Captured 2026-11-24 against TCK `a428da2`, engine `2.1.0`.

| Level | Cases | PASS | FAIL | PARSE-FAIL | NOT-FOUND | BLOCKED | Executed pass rate |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| compliance-level-2 | 116 | 116 | 0 | 0 | 0 | 0 | 100.0% |
| compliance-level-3 | 3,321 | 3,303 | 0 | 0 | 0 | 18 | 100.0% |
| **Total** | **3,437** | **3,419** | **0** | **0** | **0** | **18** | **100.0%** |

`BLOCKED` is `PARSE-FAIL` + `NOT-FOUND` + `UNSUPPORTED` + `FIXTURE-ERROR`.
3,419 cases reached a decision and every one of them produced the expected
value or the expected error. Level 2 is fully cleared.

The only remaining gap is 18 cases in `0076-feel-external-java`, which invoke
Java classes from a Java library shipped with the TCK. A .NET engine cannot load
them, so they are reported as `UNSUPPORTED` rather than as failures. Supporting
them would need a bridge to a JVM, which is out of scope for this engine.

## Harness notes and limitations

- Each test file is executed against its own model; parsed definitions are not
  shared between suites. Imports declared by the model under test are resolved
  from its directory, and the definition factory flattens the imported models
  into a single executable model with deterministic `mN.` name prefixes and
  per-element aliases (see [model-sets.md](model-sets.md)).
- Business knowledge models may have any boxed body (literal expression, context,
  relation, list, invocation, nested function definition or decision table) and
  can be invoked directly as well as from FEEL. Decision services are modelled as
  callable functions over their output decisions.
- Boxed `conditional`, `for`, `some`, `every` and `filter` expressions are
  executed; so are nested decision tables inside contexts and BKM bodies.
- FEEL expanded years and 9-digit fractional seconds are represented by
  `FeelExpandedDate`/`FeelExpandedDateTime`.
- External Java functions (`0076-feel-external-java`) cannot be evaluated on
  .NET and are reported as `UNSUPPORTED`.
- The runner is a local measurement, not a submission. Publishing results on the
  TCK site is a separate PR to `TestResults/<Vendor>/<version>`.

## Updating the suite

```bash
TCK_COMMIT=<sha> ./scripts/fetch-tck.sh
```

Then regenerate the baseline and review the result diff. Pinning is deliberate:
a suite update should be a visible commit, not a moving target in CI.
