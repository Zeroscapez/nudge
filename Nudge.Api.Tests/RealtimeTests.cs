using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Nudge.Api.Dtos;

namespace Nudge.Api.Tests;

public class RealtimeTests(NudgeApiFactory factory) : IClassFixture<NudgeApiFactory>
{
    [Fact]
    public async Task A_nudge_is_pushed_to_the_receiver_over_SignalR()
    {
        var zack = await TestClient.RegisterAsync(factory);
        var ana = await TestClient.RegisterAsync(factory);
        await zack.BefriendAsync(ana);

        // Connect as Ana through the in-memory test server.
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/nudge"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(ana.Token);
            })
            .Build();
        var received = new TaskCompletionSource<NudgeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<NudgeResponse>("NudgeReceived", n => received.TrySetResult(n));
        await hub.StartAsync();

        try
        {
            await zack.NudgeAsync(toUserId: ana.User.Id, note: "ping", emojis: "👋");

            var nudge = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("ping", nudge.Note);
            Assert.Equal(zack.User.Id, nudge.From.Id);
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }

    [Fact]
    public async Task The_hub_rejects_connections_without_a_token()
    {
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/nudge"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await Assert.ThrowsAnyAsync<Exception>(() => hub.StartAsync());
    }
}
