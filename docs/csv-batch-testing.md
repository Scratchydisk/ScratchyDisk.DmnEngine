# CSV Batch Testing Guide

The DMN Testbed API enables QA and automation teams to validate business rules defined in DMN files by submitting test data as CSV files. This decouples rule testing from .NET development -- rule authors write DMN, QA teams verify via the API using their existing automation tools (Katalon, Postman, curl, CI scripts).

## Prerequisites

- A running DMN Testbed instance (see [testbed.md](testbed.md) for setup)
- DMN file(s) to test (either already in the `--dmn-dir` directory or uploaded via the API)

## CSV Format Specification

### Column Headers

Column headers in the CSV file map to DMN variable names:

- **Input columns**: header = input variable name (case-insensitive matching)
- **Expected output columns**: header = `expected:` prefix + output variable name

Column matching is flexible:
1. Exact variable name (case-insensitive)
2. Table input/output label match
3. Any unmatched columns are reported as warnings (not errors)

### Type Coercion

CSV values are strings. The engine converts them based on the target variable's type:

| CSV Value | Target Type | Result |
|-----------|-------------|--------|
| `"true"` / `"false"` | boolean | `true` / `false` |
| `"42"` | integer/number | `42` |
| `"3.14"` | number/decimal | `3.14` |
| `"2024-01-15"` | date | `2024-01-15` |
| `"hello"` | string | `"hello"` |

If no type is defined for a variable, the engine infers the type from the value (boolean > integer > decimal > string).

### CSV Examples

**Execute-only mode** (no `expected:` columns -- just runs the decision and returns outputs):

```csv
emailTo
customer.services@example.com
customer.billing@example.com
unknown@example.com
```

**Test mode** (with `expected:` columns -- validates outputs against expected values):

```csv
emailTo,expected:emailGroup
customer.services@example.com,CS
customer.billing@example.com,Billing
unknown@example.com,Other
```

**Multiple inputs and outputs:**

```csv
Location,Sole_Trader,CS_Score,expected:Result,expected:Reason
UK,false,35,Decline,Low credit score
UK,true,80,Accept,
US,false,50,Refer,Manual review required
```

Empty expected values are skipped (not validated).

## API Reference

### Upload a DMN File

```
POST /api/dmn/upload/{name}
```

Upload a DMN file so QA teams can push files without server access.

**Parameters:**
- `{name}` -- file path relative to the DMN directory (supports subdirectories, e.g. `subfolder/my-model.dmn`)

**Request:** Multipart form with field `file`, or raw XML body with `Content-Type: application/xml`

```bash
curl -X POST http://localhost:5000/api/dmn/upload/my-model.dmn \
  -F "file=@my-model.dmn"
```

**Response:**
```json
{
  "success": true,
  "fileName": "my-model.dmn",
  "message": "File 'my-model.dmn' uploaded successfully"
}
```

### Batch Test with CSV

```
POST /api/dmn/batch-test-csv/{name}?decisionName={decisionName}
```

Execute each CSV row against a decision, optionally validating against expected outputs.

**Parameters:**
- `{name}` -- DMN file path
- `decisionName` (query, required) -- name of the decision to execute

**Request:** Multipart form with field `file`, or raw CSV body with `Content-Type: text/csv`

```bash
curl -X POST "http://localhost:5000/api/dmn/batch-test-csv/my-model.dmn?decisionName=Auto%20Decision" \
  -F "file=@test-data.csv"
```

**Response:**
```json
{
  "dmnFile": "my-model.dmn",
  "decisionName": "Auto Decision",
  "mode": "test",
  "summary": {
    "totalRows": 3,
    "passed": 2,
    "failed": 1,
    "errors": 0
  },
  "rows": [
    {
      "rowNumber": 1,
      "status": "pass",
      "actualOutputs": {
        "Result": { "value": "Decline", "typeName": "string" }
      },
      "hitRules": [2],
      "executionTimeMs": 5
    },
    {
      "rowNumber": 2,
      "status": "fail",
      "actualOutputs": {
        "Result": { "value": "Refer", "typeName": "string" }
      },
      "failureDetails": {
        "Result": "Expected 'Accept' but got 'Refer'"
      },
      "hitRules": [4],
      "executionTimeMs": 3
    },
    {
      "rowNumber": 3,
      "status": "pass",
      "actualOutputs": {
        "Result": { "value": "Accept", "typeName": "string" }
      },
      "hitRules": [1],
      "executionTimeMs": 2
    }
  ],
  "warnings": [],
  "totalTimeMs": 42
}
```

**Modes:**
- **`"execute"`** -- no `expected:` columns present; all rows have status `"executed"`
- **`"test"`** -- `expected:` columns present; rows have status `"pass"`, `"fail"`, or `"error"`

### Import CSV as Test Cases

```
POST /api/dmn/tests/import-csv/{name}?decisionName={decisionName}
```

Import CSV rows as test cases into the test suite for the specified DMN file.

**Parameters:**
- `{name}` -- DMN file path
- `decisionName` (query, required) -- decision name assigned to imported test cases

**Request:** Multipart form with field `file`

```bash
curl -X POST "http://localhost:5000/api/dmn/tests/import-csv/my-model.dmn?decisionName=Auto%20Decision" \
  -F "file=@test-data.csv"
```

**Response:**
```json
{
  "imported": 5,
  "totalTestCases": 12,
  "warnings": []
}
```

Imported test cases are appended to any existing test cases. Each row becomes a test case named "Row 1", "Row 2", etc.

The import also supports `Name` and `Decision` columns (used by the CSV export for round-trip). If a `Name` column is present, its value is used as the test case name. If a `Decision` column is present, its value overrides the `decisionName` query parameter for that row.

A `#LOOKUPS` section at the end of the file (as produced by CSV export) is automatically stripped on import.

## Katalon Integration

### Basic Workflow

A typical Katalon test project for DMN validation follows this pattern:

1. **Upload the DMN file** (optional -- skip if already on the server)
2. **Run batch CSV test** against the uploaded file
3. **Assert** the summary shows no failures or errors

### Step-by-Step Setup

#### 1. Create a POST Request for Upload (Optional)

In Katalon, create a Web Service Request:
- **Method:** POST
- **URL:** `http://localhost:5000/api/dmn/upload/my-model.dmn`
- **Body:** Form data with file field

#### 2. Create a POST Request for Batch Testing

- **Method:** POST
- **URL:** `http://localhost:5000/api/dmn/batch-test-csv/my-model.dmn?decisionName=My Decision`
- **Body:** Form data with CSV file

#### 3. Add Assertions

Assert on the response JSON:

```groovy
import com.kms.katalon.core.testobject.ResponseObject

// Parse JSON response
def response = WS.sendRequest(batchTestRequest)
WS.verifyResponseStatusCode(response, 200)

def json = new groovy.json.JsonSlurper().parseText(response.getResponseText())

// Assert no failures
assert json.summary.failed == 0 : "Expected 0 failures but got ${json.summary.failed}"
assert json.summary.errors == 0 : "Expected 0 errors but got ${json.summary.errors}"

// Optionally log details
println "Total rows: ${json.summary.totalRows}, Passed: ${json.summary.passed}"
```

### Example Katalon Workflow

```
Test Case: Validate Email Routing Rules
  Step 1: POST /api/dmn/upload/email-routing.dmn        [upload DMN file]
  Step 2: Verify response status = 200
  Step 3: POST /api/dmn/batch-test-csv/email-routing.dmn [run CSV tests]
           ?decisionName=Route Email
  Step 4: Verify response status = 200
  Step 5: Assert response.summary.failed == 0
  Step 6: Assert response.summary.errors == 0
```

### Data-Driven Alternative

Instead of batch CSV upload, Katalon can read the CSV itself and call the execute endpoint per row. This gives more control but requires more setup:

1. Use Katalon's Data File feature to load the CSV
2. For each row, call `POST /api/dmn/execute/{name}` with JSON inputs
3. Assert each response individually

The batch CSV endpoint is simpler for most use cases.

## Testbed UI: CSV Import and Export

The testbed web interface includes **Import CSV** and **Export CSV** buttons in the test suite section.

### Export CSV

1. Select a DMN file and decision
2. Click **Export CSV** in the test suite actions bar
3. A CSV file is downloaded with columns: `Name`, `Decision`, input variables, and `expected:` output variables

The exported CSV works even with zero test cases -- it produces a headers-only template that you can fill in with test data. If any input or output variables have defined allowed values (enumerated lists), a `#LOOKUPS` section is appended at the bottom of the file:

```csv
Name,Decision,Location,Sole_Trader,expected:Result
Happy path,Auto Decision,UK,false,Decline
Edge case,Auto Decision,US,true,Accept

#LOOKUPS
#Column,Allowed Values
#Location,UK,US,EU
#expected:Result,Accept,Decline,Refer
```

The `#LOOKUPS` section is informational and is ignored on re-import.

### Import CSV

1. Select a DMN file and decision
2. Click **Import CSV** in the test suite actions bar
3. Select a `.csv` file
4. The rows are imported as test cases (using the `Name` column if present, otherwise auto-generated names)
5. Run All to execute the imported test cases

Exported CSVs round-trip cleanly: Export CSV, edit in a spreadsheet, Import CSV.

## Error Handling

| Condition | HTTP Status | Error Message |
|-----------|-------------|---------------|
| DMN file not found | 404 | `DMN file not found: {name}` |
| Decision not found | 400 | `Decision 'X' not found in '{name}'. Available: ...` |
| Missing `decisionName` param | 400 | `Query parameter 'decisionName' is required` |
| Empty CSV | 400 | `CSV content is empty` |
| No matching input columns | 400 | `No columns match known inputs for decision 'X'. Expected: ...` |
| Invalid XML (upload) | 400 | `Invalid XML content` |
| Type conversion error (per row) | 200 | Row status: `"error"`, message in `error` field |
| Execution failure (per row) | 200 | Row status: `"error"`, message in `error` field |
| Unrecognised columns | 200 | Listed in `warnings` array (not an error) |

Per-row errors do not fail the entire request. The response always returns with HTTP 200 and includes the error details in the individual row results.

## Tips

- Use the `GET /api/dmn/info/{name}` endpoint to discover input/output variable names and types before building your CSV files. The response includes `allRequiredInputs` for each decision and `tableOutputs` with type information.

- Column matching is case-insensitive, so `CS_Score`, `cs_score`, and `CS_SCORE` all match the same variable.

- When testing decisions with upstream dependencies (DRDs), you only need to provide the leaf-level inputs. The engine executes the full decision tree automatically.

- Expected output columns can reference outputs from upstream decisions, not just the target decision's own outputs.

- Empty cells in expected output columns are skipped -- only non-empty expected values are validated.
