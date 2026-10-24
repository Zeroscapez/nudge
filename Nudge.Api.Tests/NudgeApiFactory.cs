using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Nudge.Api.Tests;

// Starts the real app in memory, with its own throwaway SQLite file so tests never touch nudge.db.
public class NudgeApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"nudge-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Nudge", $"Data Source={_dbPath};Pooling=False");
        builder.UseSetting("Jwt:Key", "test-key-test-key-test-key-test-key-1234");
        // Tests fire lots of requests quickly; keep the rate limiter out of the way.
        builder.UseSetting("RateLimits:AuthPerMinute", "10000");
        builder.UseSetting("RateLimits:NudgesPerMinute", "10000");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        File.Delete(_dbPath);
    }
}
