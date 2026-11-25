# ScratchyDisk.DmnEngine 2.2.0

This is the first published release since 2.0.1 and includes all of 2.1.0.

## DMN 1.6

- DMN 1.6 models are supported through the `20240513/MODEL/` namespace and the `Parse16`/`ParseString16` entry points.
- FEEL 1.6 additions: descendant expressions (`...`), temporal `.value`, single-parameter rounding, `date and time(date, time, timezone)`, exponential and leading-dot numeric literals, and decimal-to-integer conversion.
- DMN 1.6 diagnostics are lenient by default, with an opt-in strict mode.
- FEEL 1.5 gaps are closed: `list replace`, `range(string)` and range expressions, `overlaps before/after`, temporal and range properties, IANA timezone strings, and corrected `round half down` / `decimal` rounding.

## Decisions and expressions

- Decision services and business knowledge models are first-class FEEL values: they can be invoked positionally or with named arguments, returned, passed as arguments and invoked again.
- Declared FEEL types are enforced on decisions, decision-table outputs, knowledge-model parameters/results and decision-service parameters/results, including item definitions and collections. A non-conforming value raises a DMN evaluation error instead of being silently coerced.
- Decision and knowledge-model bodies may be a context, relation, list, invocation or decision table, including nested tables and function definitions inside context entries.
- Decision-table input entries fall back to the full FEEL grammar, so Camunda-style `and`/`or` expressions work.
- FEEL names containing `-` resolve inside table expressions, and context keys may be quoted or contain spaces, operators and emoji.

## Imports and model sets

- `DmnModelSet` indexes a folder of DMN files by namespace and model name, so `import` elements resolve across files.
- Imported models are flattened with deterministic `mN.` prefixes and per-element aliases; import-qualified references in expressions, type references and knowledge requirements resolve to the flattened model.
- Input parameters named with an import namespace bind to the flattened model element.

## Reliability

- Iteration is bounded. Fractional range endpoints are an evaluation error, and `for`/`some`/`every`/boxed iteration stop with a clear error at `FeelEvaluationLimits.MaxIterationItems` (default 1,000,000) instead of exhausting memory.
- A nested decision table with the `ANY` hit policy rejects rules whose outputs disagree.
- Allowed output values are compared with invariant formatting, so `PRIORITY` and `OUTPUT ORDER` results do not depend on machine culture.
- DMN XML is read with DTD processing prohibited and no external resolver.
- `COLLECT LIST` results are visible to dependent decisions under the decision name, and `string(null)` propagates null.

## Tooling

- A DMN TCK conformance runner executes the OMG suite at a pinned commit and compares the results against a committed ratchet baseline.
- The test bed client uses Nuxt 4 and Vue 3.5.
- Runtime dependencies: Antlr4.Runtime.Standard 4.13.1 and NLog 6.2.2.
