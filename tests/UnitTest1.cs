using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class UnitTest1 : IAsyncLifetime, IDisposable
{
    private const ushort HttpPort = 80;

    private readonly CancellationTokenSource _cts = new(TimeSpan.FromMinutes(1));

    private readonly INetwork _network;

    private readonly IContainer _dbContainer;

    private readonly IContainer _appContainer;

    public UnitTest1()
    {
        _network = new NetworkBuilder()
            .Build();

        _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres")
            .WithNetwork(_network)
            .WithNetworkAliases("db")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilContainerIsHealthy())
            .WithVolumeMount("postgres-data", "/var/lib/postgresql/data")
            .Build();

        //var tempContainer = new ContainerBuilder()
        //    .WithCommand("cd ../", "docker build --tag dotnet-docker .")
        //    .Build();

        _appContainer = new ContainerBuilder()
            .WithCommand("cd ../", "docker build --tag dotnet-docker .")
            .WithImage("dotnet-docker")
            .WithNetwork(_network)
            .WithPortBinding(HttpPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPath("/")))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _network.CreateAsync(_cts.Token)
            .ConfigureAwait(false);

        Task dbTask = Task.Run(() => _dbContainer.StartAsync(_cts.Token).ConfigureAwait(false));

        await _appContainer.StartAsync(_cts.Token)
            .ConfigureAwait(false);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts.Dispose();
    }

    [Fact]
    public async Task Test1()
    {
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new UriBuilder("http", _appContainer.Hostname, _appContainer.GetMappedPublicPort(HttpPort)).Uri;

        var httpResponseMessage = await httpClient.GetAsync(string.Empty)
            .ConfigureAwait(false);

        var body = await httpResponseMessage.Content.ReadAsStringAsync()
            .ConfigureAwait(false);

        Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
        Assert.Contains("Welcome", body);
    }
}