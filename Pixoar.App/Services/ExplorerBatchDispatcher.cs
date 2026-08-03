using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Pixoar.App.Services;

internal static class ExplorerBatchDispatcher
{
    private const string LaunchSwitch = "--explorer-batch";
    private static readonly TimeSpan CollectionWindow = TimeSpan.FromMilliseconds(500);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static bool IsRequest(IReadOnlyList<string> args)
    {
        return args.Count > 0 &&
            string.Equals(args[0], LaunchSwitch, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<int> DispatchAsync(IReadOnlyList<string> args)
    {
        if (!TryParseRequest(args, out var actionArguments, out var filePath, out var error))
        {
            WriteDiagnostic("Explorer batch request rejected.", error);
            return 2;
        }

        var actionKey = CreateActionKey(actionArguments);
        var sessionId = Process.GetCurrentProcess().SessionId;
        var queueDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Pixoar",
            "ExplorerQueue",
            sessionId.ToString(),
            actionKey);
        var workerName = $@"Local\Pixoar.ExplorerBatch.Worker.{sessionId}.{actionKey}";
        var gateName = $@"Local\Pixoar.ExplorerBatch.Gate.{sessionId}.{actionKey}";

        Directory.CreateDirectory(queueDirectory);

        using var workerMutex = new Mutex(initiallyOwned: false, workerName);
        using var gateMutex = new Mutex(initiallyOwned: false, gateName);
        var ownsWorker = false;
        var ownsGate = EnterMutex(gateMutex, Timeout.InfiniteTimeSpan);

        try
        {
            WriteRequest(queueDirectory, filePath);
            ownsWorker = EnterMutex(workerMutex, TimeSpan.Zero);
        }
        finally
        {
            if (ownsGate)
            {
                gateMutex.ReleaseMutex();
            }
        }

        if (!ownsWorker)
        {
            return 0;
        }

        try
        {
            return await RunWorkerAsync(
                queueDirectory,
                actionArguments,
                workerMutex,
                gateMutex,
                () => ownsWorker = false);
        }
        catch (Exception ex)
        {
            WriteDiagnostic("Explorer batch worker failed.", ex.ToString());
            return 1;
        }
        finally
        {
            if (ownsWorker)
            {
                workerMutex.ReleaseMutex();
            }
        }
    }

    private static async Task<int> RunWorkerAsync(
        string queueDirectory,
        IReadOnlyList<string> actionArguments,
        Mutex workerMutex,
        Mutex gateMutex,
        Action workerReleased)
    {
        var exitCode = 0;
        await Task.Delay(CollectionWindow);

        while (true)
        {
            var batch = ReadPendingRequests(queueDirectory);
            if (batch.RequestFiles.Count > 0)
            {
                if (batch.FilePaths.Count > 0)
                {
                    var batchExitCode = await RunCliAsync(actionArguments, batch.FilePaths);
                    exitCode = batchExitCode == 0 ? exitCode : batchExitCode;
                    DeleteRequests(batch.RequestFiles);
                }
                else
                {
                    DeleteRequests(batch.RequestFiles);
                    WriteDiagnostic(
                        "Explorer batch contained no existing input files.",
                        string.Join(Environment.NewLine, batch.RequestFiles));
                    exitCode = 2;
                }

                continue;
            }

            await Task.Delay(CollectionWindow);
            if (ReadPendingRequests(queueDirectory).RequestFiles.Count > 0)
            {
                continue;
            }

            var ownsGate = EnterMutex(gateMutex, Timeout.InfiniteTimeSpan);
            try
            {
                if (ReadPendingRequests(queueDirectory).RequestFiles.Count > 0)
                {
                    continue;
                }

                workerMutex.ReleaseMutex();
                workerReleased();
                return exitCode;
            }
            finally
            {
                if (ownsGate)
                {
                    gateMutex.ReleaseMutex();
                }
            }
        }
    }

    private static void WriteRequest(string queueDirectory, string filePath)
    {
        var requestId = $"{DateTime.UtcNow.Ticks:D19}-{Environment.ProcessId}-{Guid.NewGuid():N}";
        var tempPath = Path.Combine(queueDirectory, $"{requestId}.tmp");
        var requestPath = Path.Combine(queueDirectory, $"{requestId}.request.json");
        try
        {
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, Path.GetFullPath(filePath), SerializerOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, requestPath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static PendingBatch ReadPendingRequests(string queueDirectory)
    {
        var requestFiles = new List<string>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var requestFile in Directory.EnumerateFiles(
            queueDirectory,
            "*.request.json",
            SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var stream = new FileStream(
                    requestFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Delete);
                var filePath = JsonSerializer.Deserialize<string>(stream, SerializerOptions);
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    continue;
                }

                requestFiles.Add(requestFile);
                var fullPath = Path.GetFullPath(filePath);
                if (File.Exists(fullPath))
                {
                    paths.Add(fullPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                WriteDiagnostic($"Invalid Explorer queue request: {requestFile}", ex.ToString());
                TryDeleteRequest(requestFile);
            }
        }

        return new PendingBatch(requestFiles, paths.ToArray());
    }

    private static async Task<int> RunCliAsync(
        IReadOnlyList<string> actionArguments,
        IReadOnlyList<string> filePaths)
    {
        var cliPath = Path.Combine(AppContext.BaseDirectory, "Pixoar.Cli.exe");
        if (!File.Exists(cliPath))
        {
            throw new FileNotFoundException("Pixoar.Cli.exe was not found beside Pixoar.exe.", cliPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = cliPath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in actionArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add("--quiet");
        foreach (var filePath in filePaths)
        {
            startInfo.ArgumentList.Add(filePath);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The Explorer batch worker could not start Pixoar.Cli.exe.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(outputTask, errorTask);

        return process.ExitCode;
    }

    private static bool TryParseRequest(
        IReadOnlyList<string> args,
        out string[] actionArguments,
        out string filePath,
        out string error)
    {
        actionArguments = [];
        filePath = string.Empty;
        error = string.Empty;

        if (args.Count != 5 || !IsRequest(args))
        {
            error = "Expected --explorer-batch followed by an action, option, value, and file path.";
            return false;
        }

        actionArguments = args.Skip(1).Take(3).ToArray();
        filePath = args[4];
        var isConvert =
            actionArguments[0].Equals("convert", StringComparison.OrdinalIgnoreCase) &&
            actionArguments[1].Equals("--format", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(actionArguments[2]);
        var isResize =
            actionArguments[0].Equals("resize", StringComparison.OrdinalIgnoreCase) &&
            actionArguments[1].Equals("--percentage", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(actionArguments[2], out var percentage) &&
            percentage > 0;

        if (!isConvert && !isResize)
        {
            error = "The Explorer batch action was not recognized.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            error = $"The Explorer batch input file does not exist: {filePath}";
            return false;
        }

        return true;
    }

    private static string CreateActionKey(IReadOnlyList<string> actionArguments)
    {
        var value = string.Join(
            '\0',
            actionArguments.Select(argument => argument.Trim().ToLowerInvariant()));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..24];
    }

    private static bool EnterMutex(Mutex mutex, TimeSpan timeout)
    {
        try
        {
            return mutex.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    private static void DeleteRequests(IEnumerable<string> requestFiles)
    {
        foreach (var requestFile in requestFiles)
        {
            TryDeleteRequest(requestFile);
        }
    }

    private static void TryDeleteRequest(string requestFile)
    {
        try
        {
            File.Delete(requestFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            WriteDiagnostic($"Explorer queue cleanup failed: {requestFile}", ex.ToString());
        }
    }

    private static void WriteDiagnostic(string message, string detail)
    {
        WriteUniqueLog(
            "explorer-batch-error",
            $"timestamp={DateTimeOffset.Now:O}{Environment.NewLine}" +
            $"pid={Environment.ProcessId}{Environment.NewLine}" +
            $"message={JsonSerializer.Serialize(message)}{Environment.NewLine}" +
            $"detail={JsonSerializer.Serialize(detail)}{Environment.NewLine}");
    }

    private static void WriteUniqueLog(string prefix, string content)
    {
        try
        {
            var logsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pixoar",
                "Logs");
            Directory.CreateDirectory(logsDirectory);
            var fileName = $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss-fffffff}-{Environment.ProcessId}-{Guid.NewGuid():N}.log";
            File.WriteAllText(Path.Combine(logsDirectory, fileName), content);
        }
        catch
        {
            // Explorer submissions must remain windowless even when diagnostics cannot be written.
        }
    }

    private sealed record PendingBatch(IReadOnlyList<string> RequestFiles, IReadOnlyList<string> FilePaths);
}
