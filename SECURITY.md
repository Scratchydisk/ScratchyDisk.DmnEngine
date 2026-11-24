# Security policy

## Supported versions

Security fixes are applied to the latest release line.

| Version | Supported |
| --- | --- |
| 2.1.x | Yes |
| 2.0.x | No (upgrade to 2.1) |
| 1.x | No |

## Reporting a vulnerability

Please **do not open a public issue** for a security problem.

Use GitHub's private vulnerability reporting: **Security → Report a vulnerability**
on this repository. That opens a private advisory visible only to the maintainers.

Please include:

- the version of `ScratchyDisk.DmnEngine` and the .NET runtime,
- the smallest DMN model or code that reproduces the issue,
- what you expected and what happened (including any exception or stack trace),
- the impact you believe it has (data disclosure, denial of service, code execution).

You will get an acknowledgement within a few working days. Once the issue is
confirmed we will agree a disclosure date, publish a patched release and credit
you in the advisory unless you ask us not to.

## Scope

The engine is a library: it parses DMN XML and evaluates FEEL expressions. Where
it is given untrusted input, the following are in scope.

**In scope**

- **XML parsing** — external entity (XXE) or DTD handling, entity expansion, and
  any way a model can make the parser read a file or a URL. DMN XML is read with
  DTD processing prohibited and no external resolver.
- **Expression evaluation** — a FEEL expression that crashes the process.
  Evaluation is bounded by regex timeouts and by
  `FeelEvaluationLimits.MaxIterationItems` for range iteration; a bypass that
  exhausts memory or the stack is a vulnerability.
- **File access through imports** — `import/@locationURI` and
  `DmnModelSet.LoadDirectory` resolve model files from disk. Reading outside the
  intended folder, or following a symlink into one, is in scope.
- **CLR interop** — FEEL can call a fixed allow-list of CLR types (`Math`,
  `Convert`, primitives, `DateTime`, `DateTimeOffset`). It can also reflect over
  the public members of any object passed to it as an input value. A way to
  reach beyond that (for example to load a type not in the allow-list) is in
  scope.
- **The published package** — anything that lets a malicious DMN model escalate
  beyond the evaluation limits described above.

**Out of scope**

- **The testbed** is a local development tool. It has no authentication by
  design, binds to localhost and must not be exposed to a network. Running it in
  front of untrusted users is a deployment error, not a vulnerability.
- **The CLI** (`dmnrunner`) is a local tool: it reads the files you point it at
  and is meant to be driven by a human or a trusted pipeline.
- **Trusted input** — a model author can already express arbitrary computation.
  Passing a model you do not trust to `DmnDefinitionFactory` means trusting its
  expressions; the limits above bound resource use, not intent.
- Findings that require input values to expose dangerous members: if you pass a
  rich object as an input parameter, FEEL can call its public instance members.
  Pass plain data (contexts, lists, primitives) when a model is untrusted.

## Hardening checklist for hosts

- Evaluate untrusted models with a strict error handling mode
  (`DmnErrorHandlingMode.Strict`) and treat an evaluation error as a rejection,
  not as "no result".
- Lower `FeelEvaluationLimits.MaxIterationItems` for request-scoped evaluation.
- Pass plain data as input values rather than application objects.
- Run the engine in a process with the least file-system access the models need,
  especially when using `DmnModelSet` or models with imports.
