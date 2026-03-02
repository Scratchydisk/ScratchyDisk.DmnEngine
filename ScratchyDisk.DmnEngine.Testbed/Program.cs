using System.Text.Json;
using System.Text.Json.Serialization;
using ScratchyDisk.DmnEngine.Testbed.Models;
using ScratchyDisk.DmnEngine.Testbed.Services;

// Suppress NLog console output
NLog.LogManager.Configuration = new NLog.Config.LoggingConfiguration();

// Parse --dmn-dir from args
var dmnDir = Directory.GetCurrentDirectory();
for (var i = 0; i < args.Length; i++)
{
    if (args[i].StartsWith("--dmn-dir="))
        dmnDir = args[i]["--dmn-dir=".Length..];
    else if (args[i] == "--dmn-dir" && i + 1 < args.Length)
        dmnDir = args[++i];
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(new DmnFileService(dmnDir));
builder.Services.AddSingleton<DmnExecutionService>();
builder.Services.AddSingleton<CsvBatchTestService>();

builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

// ── Static files (built Nuxt output in wwwroot/) ──
app.UseDefaultFiles();
app.UseStaticFiles();

// ── API endpoints ──
// Routes use /api/dmn/<action>/{**name} so the catch-all captures subdirectory paths

// List DMN files
app.MapGet("/api/dmn", (DmnFileService fileService) =>
{
    return Results.Ok(fileService.ListFiles());
});

// Get DMN XML content
app.MapGet("/api/dmn/xml/{**name}", (string name, DmnFileService fileService) =>
{
    try
    {
        var xml = fileService.ReadXml(name);
        return Results.Text(xml, "application/xml");
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound(new { error = $"File not found: {name}" });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Get parsed definition info
app.MapGet("/api/dmn/info/{**name}", (string name, DmnExecutionService executionService) =>
{
    try
    {
        var info = executionService.GetInfo(name);
        return Results.Ok(info);
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound(new { error = $"File not found: {name}" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Execute a decision
app.MapPost("/api/dmn/execute/{**name}", (string name, ExecuteRequest request, DmnExecutionService executionService) =>
{
    try
    {
        var result = executionService.Execute(name, request);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Ok(new ExecutionResult { Error = ex.Message });
    }
});

// Load test suite
app.MapGet("/api/dmn/tests/{**name}", (string name, DmnFileService fileService) =>
{
    try
    {
        var suite = fileService.LoadTestSuite(name);
        return Results.Ok(suite);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Save test suite
app.MapPut("/api/dmn/tests/{**name}", (string name, TestSuite suite, DmnFileService fileService) =>
{
    try
    {
        fileService.SaveTestSuite(name, suite);
        return Results.Ok(new { saved = true });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Run test cases
app.MapPost("/api/dmn/tests/run/{**name}", (string name, DmnFileService fileService, DmnExecutionService executionService, TestRunRequest request) =>
{
    try
    {
        var suite = fileService.LoadTestSuite(name);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var testCases = suite.TestCases;
        if (request?.TestCaseIds != null && request.TestCaseIds.Count > 0)
        {
            var ids = new HashSet<string>(request.TestCaseIds);
            testCases = testCases.Where(tc => ids.Contains(tc.Id)).ToList();
        }

        var results = testCases.Select(tc => executionService.RunTestCase(name, tc)).ToList();
        sw.Stop();

        // Update last run in the suite and save
        foreach (var result in results)
        {
            var tc = suite.TestCases.FirstOrDefault(t => t.Id == result.TestCaseId);
            if (tc != null)
            {
                tc.LastRun = new TestCaseLastRun
                {
                    Status = result.Status,
                    ActualOutputs = result.ActualOutputs,
                    HitRules = result.HitRules,
                    ExecutionTimeMs = result.ExecutionTimeMs,
                    Error = result.Error,
                    RanAt = DateTimeOffset.UtcNow
                };
            }
        }

        fileService.SaveTestSuite(name, suite);

        return Results.Ok(new TestRunResult
        {
            Results = results,
            TotalTimeMs = sw.ElapsedMilliseconds
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Upload a DMN file
app.MapPost("/api/dmn/upload/{**name}", async (string name, HttpRequest request, DmnFileService fileService) =>
{
    try
    {
        string xmlContent;

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file == null)
                return Results.BadRequest(new { error = "No file uploaded. Use form field 'file'." });

            using var reader = new StreamReader(file.OpenReadStream());
            xmlContent = await reader.ReadToEndAsync();
        }
        else
        {
            using var reader = new StreamReader(request.Body);
            xmlContent = await reader.ReadToEndAsync();
        }

        if (string.IsNullOrWhiteSpace(xmlContent))
            return Results.BadRequest(new { error = "Empty file content" });

        fileService.SaveDmnFile(name, xmlContent);

        return Results.Ok(new UploadResult
        {
            Success = true,
            FileName = name,
            Message = $"File '{name}' uploaded successfully"
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (System.Xml.XmlException)
    {
        return Results.BadRequest(new { error = "Invalid XML content" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// CSV batch test
app.MapPost("/api/dmn/batch-test-csv/{**name}", async (string name, HttpRequest request, CsvBatchTestService batchService) =>
{
    try
    {
        var decisionName = request.Query["decisionName"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(decisionName))
            return Results.BadRequest(new { error = "Query parameter 'decisionName' is required" });

        Stream csvStream;

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file == null)
                return Results.BadRequest(new { error = "No file uploaded. Use form field 'file'." });
            csvStream = file.OpenReadStream();
        }
        else
        {
            csvStream = request.Body;
        }

        var result = batchService.RunBatchTest(name, decisionName, csvStream);
        return Results.Ok(result);
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound(new { error = $"DMN file not found: {name}" });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Import CSV as test cases
app.MapPost("/api/dmn/tests/import-csv/{**name}", async (string name, HttpRequest request, CsvBatchTestService batchService) =>
{
    try
    {
        var decisionName = request.Query["decisionName"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(decisionName))
            return Results.BadRequest(new { error = "Query parameter 'decisionName' is required" });

        Stream csvStream;

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file == null)
                return Results.BadRequest(new { error = "No file uploaded. Use form field 'file'." });
            csvStream = file.OpenReadStream();
        }
        else
        {
            csvStream = request.Body;
        }

        var mode = request.Query["mode"].FirstOrDefault() ?? "append";
        var replace = string.Equals(mode, "replace", StringComparison.OrdinalIgnoreCase);
        var result = batchService.ImportAsTestSuite(name, decisionName, csvStream, replace);
        return Results.Ok(result);
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound(new { error = $"DMN file not found: {name}" });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// ── SPA fallback ──
app.MapFallbackToFile("index.html");

Console.WriteLine($"DMN Testbed serving files from: {dmnDir}");
app.Run();
