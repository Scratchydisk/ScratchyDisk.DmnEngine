# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ScratchyDisk.DmnEngine is a .NET rule engine that executes decisions defined in DMN (Decision Model and Notation) models. It evaluates decision tables and expression decisions from OMG-standard DMN XML files (versions 1.1, 1.3, 1.3ext, 1.4, 1.5) or programmatically built definitions. Expressions are evaluated using a full FEEL (Friendly Enough Expression Language) evaluator built with ANTLR4.

**NuGet package:** `ScratchyDisk.DmnEngine`
**Target framework:** .NET 10.0
**Current version:** 2.0.0

## Build and Test Commands

```bash
# Build the solution
dotnet build ScratchyDisk.DmnEngine.sln

# Run all tests (consolidated test project)
dotnet test ScratchyDisk.DmnEngine.Tests/ScratchyDisk.DmnEngine.Tests.csproj

# Run a single test by fully qualified name
dotnet test ScratchyDisk.DmnEngine.Tests/ScratchyDisk.DmnEngine.Tests.csproj --filter "FullyQualifiedName~TestMethodName"

# Run a single test class
dotnet test ScratchyDisk.DmnEngine.Tests/ScratchyDisk.DmnEngine.Tests.csproj --filter "FullyQualifiedName~SfeelExpressionsTests"
```

## Solution Structure

- **ScratchyDisk.DmnEngine/** - Core library (.NET 10.0)
- **ScratchyDisk.DmnEngine.Tests/** - Consolidated test project (.NET 10.0)
- **ScratchyDisk.DmnEngine.Test.Shared/** - Shared test code (linked into test project)
- **ScratchyDisk.DmnEngine.Testbed/** - Web-based test lab (ASP.NET Core + Nuxt SPA). See [docs/testbed.md](docs/testbed.md).
- **ScratchyDisk.DmnEngine.Cli/** - Command-line DMN runner
- **build/** and **build.tasks/** - Custom MSBuild process (safe to ignore for development)

## Architecture

### Core Pipeline

```
DMN XML ──DmnParser──▶ DmnModel ──DmnDefinitionFactory──▶ DmnDefinition ──DmnExecutionContextFactory──▶ DmnExecutionContext
                                                                                                            │
Code ──DmnDefinitionBuilder──▶ DmnDefinition ──────────────────────────────────────────────────────────────────┘
```

1. **Parsing** (`parser/`): `DmnParser` deserializes DMN XML (v1.1, 1.3, 1.3ext, 1.4, 1.5) into `DmnModel` DTOs. Supports auto-detection of DMN version from XML namespace.
2. **Definition** (`engine/definition/`): `DmnDefinitionFactory` transforms `DmnModel` into `DmnDefinition` — validation, variable type resolution, and dependency tree construction. Definitions are "virtually immutable" (exposed via read-only interfaces).
3. **Builder** (`engine/definition/builder/`): `DmnDefinitionBuilder` provides a fluent API to create `DmnDefinition` programmatically without XML.
4. **Decisions** (`engine/decisions/`): Two types — `DmnExpressionDecision` (single expression → output) and `DmnDecisionTable` (rules with inputs, outputs, hit policies).
5. **Execution** (`engine/execution/`): `DmnExecutionContext` manages variables, resolves decision dependencies recursively, evaluates expressions via the FEEL evaluator, and returns `DmnDecisionResult`. Decision table input entries use `ParseSimpleUnaryTests` first, with an automatic fallback to `ParseExpression` (full FEEL grammar) for compatibility with Camunda-style expressions that use `or`/`and` operators.

### FEEL Evaluator Pipeline

```
FEEL expression string
  ──FeelLexer.g4──▶ Token stream
  ──FeelNameResolver──▶ Merged tokens (multi-word names resolved)
  ──FeelParser.g4──▶ Parse tree
  ──FeelAstBuilder──▶ FeelAstNode (AST)
  ──FeelEvaluator──▶ Result value
```

- **Grammar** (`feel/grammar/`): `FeelLexer.g4` and `FeelParser.g4` — ANTLR4 grammars compiled at build time via `Antlr4BuildTasks`
- **Parsing** (`feel/parsing/`):
  - `FeelScope` — registry of known variable and function names for scope-aware parsing
  - `FeelNameResolver` — post-lexer token stream rewriter that merges adjacent name tokens into multi-word identifiers (FEEL allows spaces in names)
  - `FeelAstBuilder` — ANTLR visitor that converts parse tree to AST nodes
- **AST** (`feel/ast/`): `FeelAstNode` hierarchy — literals, operators, control flow, collections, functions, unary tests
- **Evaluation** (`feel/eval/`):
  - `FeelEvaluationContext` — variable scope chain with nested context support
  - `FeelEvaluator` — tree-walking interpreter with FEEL three-valued logic and null propagation
- **Functions** (`feel/functions/`): `FeelBuiltInFunctions` — ~80 built-in FEEL functions (string, numeric, list, date/time, context, range, boolean, conversion)
- **Types** (`feel/types/`): `FeelTime`, `FeelYmDuration`, `FeelRange`, `FeelContext`, `FeelFunction`, `FeelTypeCoercion`, `FeelValueComparer`
- **Facade** (`feel/FeelEngine.cs`): Public API — `EvaluateExpression()`, `EvaluateSimpleUnaryTests()`, `ParseExpression()`, `ParseSimpleUnaryTests()`

### FEEL Type Mappings

| FEEL Type | .NET Type |
|-----------|-----------|
| `number` | `decimal` |
| `string` | `string` |
| `boolean` | `bool` |
| `date` | `DateOnly` |
| `time` | `FeelTime` |
| `date and time` | `DateTimeOffset` |
| `years and months duration` | `FeelYmDuration` |
| `days and time duration` | `TimeSpan` |
| `list` | `List<object>` |
| `context` | `FeelContext` |
| `range` | `FeelRange` |
| `function` | `FeelFunction` |

### Key Design Patterns

- **Virtual immutability**: Definitions are effectively immutable after creation (hidden behind read-only interfaces like `IDmnVariable`, `IDmnDefinition`). Safe for concurrent access.
- **Factory pattern**: `DmnDefinitionFactory` and `DmnExecutionContextFactory` are the primary creation points. `DmnDefinitionFactory` has virtual protected methods for subclassing.
- **Expression caching**: Parsed FEEL AST nodes (`FeelAstNode`) are cached with configurable scope (None, Execution, Context, Definition, Global) via `ParsedExpressionCacheScopeEnum`. AST nodes are immutable and thread-safe.
- **CLR interop**: The FEEL evaluator supports CLR instance method calls (e.g., `.ToString()`) and static method calls (e.g., `double.Parse()`, `Math.Abs()`) for backward compatibility with v1.x expressions.

### Decision Table Hit Policies

Unique, First, Priority, Any, Collect (with aggregations: List, Sum, Min, Max, Count), RuleOrder, OutputOrder.

### Dependencies

- **Antlr4.Runtime.Standard** 4.13.1 — ANTLR4 runtime for FEEL parser
- **Antlr4BuildTasks** 12.8 — Build-time grammar compilation (private asset)
- **NLog** 5.3.4 — Logging

## Test Architecture

Tests use **MSTest** with **FluentAssertions**. Test code lives in the shared project and uses an inheritance pattern:

- `DmnTestBase` — abstract base providing `Source` property that determines whether tests run against DMN 1.1, 1.3, 1.3ext, 1.4, 1.5 XML, or the builder API
- Primary test classes inherit `DmnTestBase` and contain actual test logic (default: DMN 1.1)
- Derived classes override `Source` to reuse the same tests against other DMN versions and builder-based definitions
- `DmnBuilderSamples` — auto-generated class that recreates DMN XML test models using the builder API

Test folders: `builder/` (builder tests), `complex/` (integration/scenario tests), `unit/` (unit tests), `dmn/` (sample DMN XML files organized by version), `issue/` (regression tests).

## Versioning

Version is managed centrally in `Version.props` and propagated through `Directory.Build.props`. The custom build system (enabled via `RadUseCustomBuild` env var) handles NuGet packaging and doc generation but is not required for development builds.
