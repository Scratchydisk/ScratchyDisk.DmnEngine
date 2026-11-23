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
certification: it makes regressions visible while conformance work is in
progress.

## Suite snapshot

The suite is pinned to a commit, not a branch. The current snapshot is
`a428da2f9c5ff2a4b8566ab511144e9022d743fe` (2026-11-20). Its models declare the
DMN 1.5 namespace; the engine auto-detects the version per model, so the runner
itself is version-agnostic.

Covered:

- `compliance-level-2` — 28 models, 116 cases.
- `compliance-level-3` — 180 models, 3,321 cases.

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
baseline.

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
| `UNSUPPORTED` | The engine cannot evaluate the construct at all (decision service, import, unparsed logic kind) |
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

The current baseline was captured against the working tree, which contains
uncommitted engine changes. Regenerate it after those changes are committed
before wiring the baseline into CI.

## Current baseline

Captured 2026-11-23 against TCK `a428da2`, engine `2.0.1`.

| Level | Cases | PASS | FAIL | PARSE-FAIL | NOT-FOUND | BLOCKED | Executed pass rate |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| compliance-level-2 | 116 | 116 | 0 | 0 | 0 | 0 | 100.0% |
| compliance-level-3 | 3,321 | 2,984 | 180 | 30 | 19 | 163 | 94.3% |
| **Total** | **3,437** | **3,100** | **180** | **30** | **19** | **163** | **94.5%** |

`BLOCKED` is `PARSE-FAIL` + `NOT-FOUND` + `UNSUPPORTED` + `FIXTURE-ERROR`.
3,274 cases reached a decision. Level 2 is fully cleared.

The remaining blockers are now small: 30 import-related parse failures, 82
cases using a decision logic kind the parser does not read (`context`,
`invocation`, `relation`, `functionDefinition`, `list`), and a handful of
decision-service input bindings.

Of the cases that did execute, the 180 failures concentrate in imports
(`1159`), range functions (`1156`), custom types in `instance of` (`0070`),
output coercion (`0082`), years-and-months duration functions (`1121`),
equality and date/time helper functions.

## Harness limitations

- One model per test file. Multi-model import suites are reported as
  `PARSE-FAIL`/`UNSUPPORTED`, not resolved.
- Business knowledge models are supported when their body is a literal
  expression. Decision services are not modelled, so those cases are
  `UNSUPPORTED` rather than failing.
- FEEL expanded years and 9-digit fractional seconds are represented by
  `FeelExpandedDate`/`FeelExpandedDateTime`.
- The runner is a local measurement, not a submission. Publishing results on the
  TCK site is a separate PR to `TestResults/<Vendor>/<version>`.

## Updating the suite

```bash
TCK_COMMIT=<sha> ./scripts/fetch-tck.sh
```

Then regenerate the baseline and review the result diff. Pinning is deliberate:
a suite update should be a visible commit, not a moving target in CI.
