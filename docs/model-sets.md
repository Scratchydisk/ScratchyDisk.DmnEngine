# Model sets and metadata-driven loading

A **model set** is a folder of DMN files indexed by their
`definitions/@namespace`. It lets an application load a directory once, pick a
root model by namespace or name, and have its `<import>` graph resolved from the
same folder — without hard-coding file paths.

This is the multi-model counterpart to the single-file API:
`DmnParser.Parse*` + `DmnDefinitionFactory.CreateDmnDefinition` still work for
one model; `DmnModelSet` adds discovery, indexing and import resolution.

## Folder and metadata rules

A model is identified by its namespace:

```xml
<definitions
    xmlns="https://www.omg.org/spec/DMN/20230324/MODEL/"
    id="orders"
    name="Orders"
    namespace="https://example.org/orders">
```

- **`definitions/@namespace`** — required and must be unique in the folder. This
  is the identity used by imports and by `GetByNamespace`/`CreateDefinition`.
- **`definitions/@name`** — optional; usable with `GetByName` when unambiguous.
- **`import/@namespace`** — the model to load.
- **`import/@name`** — the FEEL prefix used in expressions (`cust.Greeting`).
  An empty name exposes imported elements unqualified when there is no local
  conflict.
- **`import/@locationURI`** — optional explicit path, resolved relative to the
  importing file. When absent, the set's namespace index is used.

Example layout:

```text
models/
  orders.dmn          namespace https://example.org/orders
  customers.dmn       namespace https://example.org/customers
  catalog/
    products.dmn      namespace https://example.org/products
```

`LoadDirectory(path, recursive: true)` includes subdirectories.

## Quick start

```csharp
using ScratchyDisk.DmnEngine.Engine.Definition;
using ScratchyDisk.DmnEngine.Engine.Execution.Context;

var models = DmnModelSet.LoadDirectory("models");

// Metadata inspection
Console.WriteLine(string.Join(", ", models.Namespaces));

var definition = models.CreateDefinition("https://example.org/orders");

var context = DmnExecutionContextFactory.CreateExecutionContext(definition)
    .WithInputParameter("https://example.org/customers#name", "Ada");

var result = context.ExecuteDecision("OrderGreeting");
```

`orders.dmn` can import customers by namespace only:

```xml
<import namespace="https://example.org/customers"
        name="cust"
        importType="https://www.omg.org/spec/DMN/20230324/MODEL/"/>
```

and reference `cust.Greeting` without a path or file name.

## API

| Member | Purpose |
| --- | --- |
| `DmnModelSet.LoadDirectory(path, recursive = false, ignoreParseErrors = false)` | Parse and index a folder |
| `Namespaces` / `ModelNames` | Available metadata |
| `Contains(namespace)` | Fast presence check |
| `GetByNamespace(namespace)` | Fresh `DmnModel` for a namespace |
| `TryGetByNamespace(namespace, out model)` | Non-throwing lookup |
| `GetByName(name)` | Lookup by `definitions/@name`; fails if ambiguous |
| `GetModels()` | All models, parsed fresh |
| `CreateDefinition(namespaceOrName)` | Root definition with imports resolved |
| `CreateDefinition(DmnModel root)` | Root definition using this set for imports |
| `Errors` | Non-fatal load problems when `ignoreParseErrors` is true |

Each `CreateDefinition` call parses its models afresh from disk, so one set can
produce multiple independent definitions. Flattening mutates those parsed
copies, never the index.

## Validation and failure modes

- **Duplicate namespace** — `LoadDirectory` throws `DmnModelSetException`,
  naming both files. Namespace is identity, so duplicates are never guessed.
- **Missing namespace** — throws, or is reported in `Errors` when parse errors
  are ignored.
- **Unparseable file** — throws by default; with `ignoreParseErrors: true` the
  file is skipped and recorded in `Errors`.
- **Ambiguous name** — `GetByName` throws; use the namespace.
- **Missing import** — a dependency that cannot be found is currently skipped
  by the resolver, so the failure appears later as an unresolved reference or a
  missing decision. For deployment, validate that every `import/@namespace`
  exists in the set before creating definitions.
- **Cycles** — self-imports and mutual imports are supported and terminate.
- **Local precedence** — a locally declared element shadows an imported element
  with the same name; imported elements remain addressable by prefix.

## How import resolution works

For each `<import>` of a model in the set:

1. If `locationURI` is present, that file is loaded.
2. Otherwise the set's namespace index supplies the file.
3. The imported model's own imports are resolved recursively, with models
   registered by namespace so cycles and diamond imports are visited once.
4. Imported elements are given internal names; the importing model's
   expressions resolve `prefix.element`, `prefix.input` and `prefix.bkm()`
   through per-decision aliases.
5. Absolute `namespace#id` and local `#id` requirement hrefs, and prefixed
   `typeRef`s, are rewritten to the flattened graph.
6. Inputs and results can be addressed as `namespace#name`.

## Scope and limitations

- A model set produces **one executable definition per root**. It is not a
  service registry and does not execute models in isolation.
- There is no version selection or manifest yet; a namespace identifies one
  model in the set. Versioned model sets should use separate folders.
- Imported models are flattened into the root definition; their names are
  internal implementation details.
- Unsupported DMN constructs (decision services, `context`/`invocation`/
  `relation`/`list` decision logic) remain unsupported after import.
- The set indexes metadata at load; parsing happens on demand. Large folders
  are best split by domain or loaded with a narrower directory.

## Relationship to single-model loading

For one self-contained file, nothing changes:

```csharp
var model = DmnParser.ParseAutoDetect("orders.dmn");
var definition = DmnDefinitionFactory.CreateDmnDefinition(model);
```

Use a model set when the root has imports, when several models share a folder,
or when the application selects a model by namespace instead of a file path.
