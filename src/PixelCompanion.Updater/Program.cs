using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using PixelCompanion.Core.Services;

namespace PixelCompanion.Updater;

internal static partial class Program
{
    private const string MainExecutableName = "PixelCompanion.exe";

    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        var options = ParseArguments(args);
        if (!options.TryGetValue("install-dir", out var installDirectory) ||
            !options.TryGetValue("current-version", out var currentVersionText) ||
            !Version.TryParse(currentVersionText, out var currentVersion))
        {
            ShowError("The updater was started with invalid arguments.\n업데이터 실행 정보가 올바르지 않습니다.");
            return 2;
        }

        var mainExecutable = Path.Combine(Path.GetFullPath(installDirectory), MainExecutableName);
        var dataRoot = options.GetValueOrDefault("data-dir") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelCompanion");
        var downloadDirectory = Path.Combine(dataRoot, "updates", "downloads");
        var logPath = Path.Combine(dataRoot, "logs", "updater.log");

        try
        {
            Directory.CreateDirectory(downloadDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await LogAsync(logPath, "Checking GitHub Releases.");

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var updateService = new GitHubReleaseUpdateService(client);
            var check = await updateService.CheckAsync(currentVersion);
            if (!check.IsUpdateAvailable || check.Release is null)
                throw new InvalidOperationException(check.Error ?? "No newer release is available.");

            var installerPath = Path.Combine(downloadDirectory, GitHubReleaseUpdateService.InstallerAssetName);
            await DownloadAsync(client, check.Release.InstallerDownload, installerPath);
            var expectedHash = check.Release.AssetSha256 ??
                await DownloadChecksumAsync(client, check.Release.ChecksumDownload);
            if (expectedHash is null)
                throw new InvalidDataException("The release checksum is missing or invalid.");

            var actualHash = await ComputeSha256Async(installerPath);
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(expectedHash), Convert.FromHexString(actualHash)))
                throw new InvalidDataException("The installer SHA-256 checksum does not match.");

            if (!AuthenticodeVerifier.IsTrusted(installerPath))
                throw new InvalidDataException("The installer does not have a trusted Windows code signature.");

            if (options.TryGetValue("current-pid", out var pidText) && int.TryParse(pidText, out var pid))
                await WaitForProcessExitAsync(pid);

            var installer = Process.Start(new ProcessStartInfo(installerPath)
            {
                UseShellExecute = true,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS"
            }) ?? throw new InvalidOperationException("The installer could not be started.");
            await installer.WaitForExitAsync();
            if (installer.ExitCode != 0)
                throw new InvalidOperationException($"The installer exited with code {installer.ExitCode}.");

            if (File.Exists(mainExecutable))
                Process.Start(new ProcessStartInfo(mainExecutable) { UseShellExecute = true });
            await LogAsync(logPath, $"Updated successfully to {check.Release.TagName}.");
            return 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or UnauthorizedAccessException or CryptographicException)
        {
            await LogAsync(logPath, "Update failed: " + ex);
            ShowError("The update could not be completed. The existing installation was not replaced.\n" +
                      "업데이트를 완료하지 못했습니다. 기존 설치는 교체되지 않았습니다.\n\n" + ex.Message);
            if (File.Exists(mainExecutable))
                Process.Start(new ProcessStartInfo(mainExecutable) { UseShellExecute = true });
            return 1;
        }
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index + 1 < args.Length; index += 2)
        {
            if (args[index].StartsWith("--", StringComparison.Ordinal))
                result[args[index][2..]] = args[index + 1];
        }
        return result;
    }

    private static async Task DownloadAsync(HttpClient client, Uri source, string destination)
    {
        var temporary = destination + ".tmp";
        using var response = await client.GetAsync(source, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = File.Open(temporary, FileMode.Create, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output);
        await output.FlushAsync();
        File.Move(temporary, destination, true);
    }

    private static async Task<string?> DownloadChecksumAsync(HttpClient client, Uri? source)
    {
        if (source is null) return null;
        var text = await client.GetStringAsync(source);
        var match = Sha256Regex().Match(text);
        return match.Success ? match.Value.ToLowerInvariant() : null;
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }

    private static async Task WaitForProcessExitAsync(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (ArgumentException) { }
        catch (OperationCanceledException) { }
    }

    private static Task LogAsync(string path, string message) =>
        File.AppendAllTextAsync(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");

    private static void ShowError(string message) =>
        MessageBox(IntPtr.Zero, message, "Pixel Companion Updater", 0x10);

    [GeneratedRegex("[0-9a-fA-F]{64}", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr window, string text, string caption, uint type);
}
