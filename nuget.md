# ScratchyDisk.DmnEngine

A .NET rule engine that executes decisions defined in DMN (Decision Model and Notation) models. It evaluates decision tables, expression decisions, boxed expressions, business knowledge models and decision services from OMG-standard DMN XML files (versions 1.1, 1.3, 1.3ext, 1.4, 1.5 and 1.6), or from definitions built programmatically using a fluent API.

Expressions are evaluated using a full FEEL (Friendly Enough Expression Language) interpreter built on ANTLR4, supporting the complete expression language including `if`/`then`/`else`, `for`/`in`/`return`, quantifiers, list/context operations, ranges, and ~80 built-in functions.

## Quick Start

```csharp
var def = DmnParser.Parse(fileName);
var ctx = DmnExecutionContextFactory.CreateExecutionContext(def);
ctx.WithInputParameter("input name", inputValue);
var result = ctx.ExecuteDecision("decision name");
```

You can also create definitions programmatically using the `DmnDefinitionBuilder` fluent API instead of parsing DMN XML.

## Key Features

- Full FEEL evaluator with ANTLR4 grammar, including first-class functions and declared-type conformance
- DMN 1.1 through 1.6 with auto-detection
- All standard decision table hit policies
- Boxed decision logic: contexts, relations, lists, invocations, conditionals, iterations, filters and nested decision tables
- Business knowledge models and decision services, invokable from expressions or directly
- DMN imports resolved across a folder of models (`DmnModelSet`)
- Decision requirement graphs (DRDs) with automatic dependency resolution
- Fluent builder API for programmatic definitions
- CLR interop from FEEL expressions
- Expression caching with configurable scope
- Thread-safe immutable definitions
- OMG DMN TCK runner with a committed conformance baseline

## Documentation

Full documentation, architecture details, and tools (web-based testbed, CLI runner, TCK runner) are available in the [readme](https://github.com/Scratchydisk/ScratchyDisk.DmnEngine/blob/master/readme.md) at the [GitHub repository](https://github.com/Scratchydisk/ScratchyDisk.DmnEngine). Conformance status and the baseline workflow are documented in [docs/tck.md](https://github.com/Scratchydisk/ScratchyDisk.DmnEngine/blob/master/docs/tck.md), and multi-model loading in [docs/model-sets.md](https://github.com/Scratchydisk/ScratchyDisk.DmnEngine/blob/master/docs/model-sets.md).
