# DMN conformance test policy

This repository builds to the OMG DMN and FEEL specifications. Every behavioural change is test-first, and every conformance test must cite its canonical source.

## Canonical sources

- **DMN 1.6** — OMG document `formal/25-12-02`, https://www.omg.org/spec/DMN/1.6
- **DMN 1.6 model XSD** — https://www.omg.org/spec/DMN/20240513/DMN16.xsd
- **B-FEEL language URI** — https://www.omg.org/spec/DMN/20240513/B-FEEL/
- **DMN 1.5** — https://www.omg.org/spec/DMN/1.5
- **DMN 1.4** — https://www.omg.org/spec/DMN/1.4
- DMN 1.1–1.3 specifications as referenced by the existing regression suite.

Use the specification page for the version under test. Use DMN 1.6 for clause 10/11 behavior, DMN 1.5 for the 1.5 core clauses and gap fixes, and DMN 1.4 for 1.4-specific model or parsing behavior.

## Test-first rule

1. Write the failing test before production code.
2. Confirm the failure is for the expected reason, not a compile or fixture error.
3. Implement only enough to pass the test.
4. Refactor with the full suite green.
5. Do not weaken or delete an assertion to make a build pass.

## Citation rule

Every conformance test must record a canonical reference in its name or metadata:

- DMN 1.6 section, table, grammar rule or official example.
- DMN 1.5 section, table, grammar rule or official example.
- XSD element or namespace where the requirement is structural.

Preferred test-name shape:

```text
Dmn16_10_3_1_2_Rule68_DescendantExpression
Dmn15_10_3_4_4_ListReplace_Positional
```

A conformance test without a citation is a defect in the suite. `ConformanceManifest` maps every conformance test class to its source, and `ConformanceTraceabilityTests` fails when a class is unmapped or a test name lacks a standard reference.

## Required evidence

- The failing-test commit or run before implementation.
- The passing conformance test after implementation.
- The full DMN 1.1–1.6 regression suite green.
- A traceability entry linking behaviour, source and test.
- For behaviour the OMG DMN TCK exercises, the TCK result against the committed
  baseline ([docs/tck.md](tck.md)), with no new failures.

## README requirement

The README must include a **Testing and conformance** section explaining the test-first flow, the citation and naming rule, the required regression suite, and links to the canonical sources above. It must also link this policy.

## Scope

This policy applies to parser, definition, evaluator, built-in, error-handling, B-FEEL, ONNX and conformance-fixture work. Pure refactors may omit a new specification test only when no observable behaviour changes, and must keep the existing suite green.
