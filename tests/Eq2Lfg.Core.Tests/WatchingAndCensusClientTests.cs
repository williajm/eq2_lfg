using System.Net;
using Eq2Lfg.Core.Census;
using Eq2Lfg.Core.Watching;

namespace Eq2Lfg.Core.Tests;

public class WatchingAndCensusClientTests : IDisposable
{
    private readonly string tempDir = Directory.CreateTempSubdirectory("eq2lfg-watch").FullName;

    public void Dispose()
    {
        Directory.Delete(tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Tailer_reads_only_lines_appended_after_attach()
    {
        var path = Path.Combine(tempDir, "eq2log_Test.txt");
        File.WriteAllText(path, "old line 1\nold line 2\n");
        var tailer = new LogTailer(path);

        // First read attaches at end of file: history is not replayed.
        Assert.Empty(tailer.ReadNewLines());

        File.AppendAllText(path, "new line 1\nnew line 2\n");
        Assert.Equal(["new line 1", "new line 2"], tailer.ReadNewLines());
        Assert.Empty(tailer.ReadNewLines());
    }

    [Fact]
    public void Tailer_recovers_from_truncation()
    {
        var path = Path.Combine(tempDir, "eq2log_Trunc.txt");
        File.WriteAllText(path, "aaaa\nbbbb\n");
        var tailer = new LogTailer(path);
        tailer.ReadNewLines();

        File.WriteAllText(path, "x\n");
        // Truncation detected: position resets to the new end without throwing.
        Assert.Empty(tailer.ReadNewLines());

        File.AppendAllText(path, "after truncate\n");
        Assert.Equal(["after truncate"], tailer.ReadNewLines());
    }

    [Fact]
    public void Tailer_returns_empty_for_missing_file()
    {
        var tailer = new LogTailer(Path.Combine(tempDir, "missing.txt"));
        Assert.Empty(tailer.ReadNewLines());
    }

    [Fact]
    public void Locator_finds_most_recent_log_and_extracts_identity()
    {
        var serverDir = Path.Combine(tempDir, "logs", "Wuoshi");
        Directory.CreateDirectory(serverDir);
        File.WriteAllText(Path.Combine(serverDir, "eq2log_Older.txt"), "x");
        File.WriteAllText(Path.Combine(serverDir, "eq2log_Newer.txt"), "x");
        File.SetLastWriteTimeUtc(
            Path.Combine(serverDir, "eq2log_Older.txt"), DateTime.UtcNow.AddHours(-2));

        var active = LogFileLocator.FindMostRecent(tempDir);

        Assert.NotNull(active);
        Assert.Equal("Newer", active.CharacterName);
        Assert.Equal("Wuoshi", active.Server);
    }

    [Fact]
    public void Locator_returns_null_without_logs_directory()
    {
        Assert.Null(LogFileLocator.FindMostRecent(Path.Combine(tempDir, "nowhere")));
    }

    private sealed class StubHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
    }

    private static CensusClient Client(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new HttpClient(new StubHandler(status, json)));

    [Fact]
    public async Task Census_client_parses_found_character()
    {
        const string json = """
            {"character_list":[{"type":{"class":"Warden","level":59,"ts_class":"provisioner","ts_level":45},
            "locationdata":{"world":"Wuoshi"},"name":{"first":"Bramwick"}}],"returned":1}
            """;

        var lookup = await Client(json).LookupAsync("Bramwick", "Wuoshi");

        Assert.Equal(CensusLookupStatus.Found, lookup.Status);
        Assert.Equal("Warden", lookup.Info!.Class);
        Assert.Equal(59, lookup.Info.Level);
        Assert.Equal("provisioner", lookup.Info.TradeskillClass);
    }

    [Fact]
    public async Task Census_client_reports_missing_character()
    {
        var lookup = await Client("""{"character_list":[],"returned":0}""").LookupAsync("Ghost", "Wuoshi");
        Assert.Equal(CensusLookupStatus.NotFound, lookup.Status);
    }

    [Fact]
    public async Task Census_client_treats_rate_limit_error_as_error()
    {
        var lookup = await Client("""{"error":"Missing Service ID"}""").LookupAsync("Bramwick", "Wuoshi");
        Assert.Equal(CensusLookupStatus.Error, lookup.Status);
    }

    [Fact]
    public async Task Census_client_treats_http_failure_as_error()
    {
        var lookup = await Client("oops", HttpStatusCode.ServiceUnavailable)
            .LookupAsync("Bramwick", "Wuoshi");
        Assert.Equal(CensusLookupStatus.Error, lookup.Status);
    }
}
