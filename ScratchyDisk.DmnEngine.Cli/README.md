# dmnrunner - DMN Smoke Test Tool

Command-line tool for quickly testing DMN files. Loads a DMN model, feeds it input parameters, executes a decision, and prints a step-by-step trace of how the result was reached.

## Usage

```
dmnrunner <file.dmn> [options]
```

### Options

| Option | Short | Description |
|--------|-------|-------------|
| `--input Name=Value` | `-i` | Set an input parameter (repeatable) |
| `--input Name:Type=Value` | `-i` | Set an input with an explicit type |
| `--csv <file.csv>` | `-c` | Load input parameters from a CSV file |
| `--template <file.csv>` | `-t` | Generate a template CSV from the DMN inputs, then exit |
| `--decision <name>` | `-d` | Execute a specific decision (default: auto-detect the root) |
| `--version <ver>` | `-v` | Override DMN version (see below) |
| `--help` | `-h` | Show help |

### Running via `dotnet run`

```bash
dotnet run --project ScratchyDisk.DmnEngine.Cli -- model.dmn --input Age=33
```

Everything after `--` is passed to the tool.

## Providing inputs

### Inline

Use `--input` (or `-i`) one or more times:

```bash
dmnrunner model.dmn --input Age=33 --input Name=John --input IsActive=true
```

Values are auto-typed: `true`/`false` become boolean, numeric strings become integer or decimal, everything else stays string.

For explicit typing, use `Name:Type=Value`:

```bash
dmnrunner model.dmn -i Age:integer=33 -i Ratio:double=1.5 -i Start:date=2024-01-15
```

Supported types: `string`, `integer`, `long`, `double`, `number`, `boolean`, `date`.

### Generating a template CSV

Instead of writing the CSV by hand, generate one from the DMN file:

```bash
dmnrunner model.dmn --template params.csv
```

This creates a CSV pre-filled with all input names and types. Open it, fill in the `Value` column, then run:

```bash
dmnrunner model.dmn --csv params.csv
```

### CSV file

```bash
dmnrunner model.dmn --csv params.csv
```

CSV format (first row is a header):

```csv
Name,Type,Value
Age,integer,33
Greeting,string,Hello
IsActive,boolean,true
```

The `Type` column is optional — leave it blank to auto-infer:

```csv
Name,Type,Value
Age,,33
Greeting,,Hello
```

CSV and `--input` can be combined; `--input` values override CSV values for the same name.

## Decision selection

The tool auto-detects **root decisions** — decisions that no other decision depends on. If there is exactly one root, it is executed automatically. Otherwise, use `--decision`:

```bash
dmnrunner model.dmn --input Age=33 --decision MainDT
```

## Output sections

The output has four sections:

1. **Definition** — Lists all inputs (name, type) and decisions (type, hit policy, rule count, dependencies). Root decisions are marked with `<- ROOT`.

2. **Inputs** — The values being fed into the model.

3. **Trace** — Step-by-step execution. For each decision in the dependency chain:
   - Expression decisions show the expression and resulting variable value.
   - Table decisions show which rule(s) hit, the input conditions that matched, and the output expressions.
   - Variable changes between steps are listed.

4. **Result** — Final output variables with values and types.

## Example

```bash
dmnrunner test.dmn --input Age=33 --input Greeting=po --decision MainDT
```

```
=== DMN Smoke Test ===
File: test.dmn (DMN 1.1)

-- Definition --
Inputs:
  Age       any
  Pocet     integer
  Rano      any
  Greeting  any
  Test      any
  Suffix    any

Decisions:
  Age +10                        [expression: Age+10]  requires: Age
  Double (Age+10)                [expression: age10*2]  requires: Age +10
  MainDT                         [table, First, 8 rules]  requires: Double (Age+10), Age, Pocet, Greeting, Rano, Test
  Ensure Eligible                [expression: Eligible==null?false:Eligible]  requires: MainDT
  Secondary DT                   [table, Unique, 4 rules]  requires: Ensure Eligible  <- ROOT
  Category + suffix              [expression: Category+Suffix]  requires: MainDT, Suffix  <- ROOT

-- Inputs --
  Age                  = 33
  Greeting             = "po"

-- Executing: MainDT --

-- Trace --
Step 1: Age +10 [expression: Age+10]
  -> age10 = 43

Step 2: Double (Age+10) [expression: age10*2]
  -> Age2 = 86

Step 3: MainDT [table, First, 8 rules]
  Rule 3 HIT (row-137040852-4)
    input: Pocet (= null) matches "<= 3"
    input: Greeting (= null) matches "not("sss", "www")"
    output -> Category = "c"
    output -> Eligible = true
    output -> Tout =
  -> Category = "c"
  -> Eligible = true

-- Result --
  Category             = "c"                            (string)
  Eligible             = true                           (boolean)
```

The trace shows that `MainDT` depends on two expression decisions (`Age +10` and `Double (Age+10)`) which ran first. The table decision then matched Rule 3 using the FIRST hit policy.

## DMN version support

The tool auto-detects the DMN version from the XML namespace. Supported versions: 1.1, 1.3, 1.3ext, 1.4, 1.5.

Use `--version` to override when the auto-detected version doesn't match the file's conventions. This is common with **Camunda exports**, which use the DMN 1.3 namespace but follow 1.3ext variable naming (output `name` attribute takes priority over `label`):

```bash
dmnrunner camunda-model.dmn --csv params.csv --version 1.3ext
```
