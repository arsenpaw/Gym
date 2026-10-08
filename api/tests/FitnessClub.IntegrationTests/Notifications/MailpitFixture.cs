using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace FitnessClub.IntegrationTests.Notifications;

public sealed class MailpitFixture : IAsyncLifetime
{
    private const string Image = "axllent/mailpit:v1.27";
    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;

    private readonly IContainer container = new ContainerBuilder(Image)
        .WithPortBinding(SmtpPort, true)
        .WithPortBinding(HttpPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(HttpPort).ForPath("/readyz")))
        .Build();

    public string Host => container.Hostname;

    public int Smtp => container.GetMappedPublicPort(SmtpPort);

    public HttpClient Api() => new() { BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(HttpPort)}") };

    public async ValueTask InitializeAsync() => await container.StartAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
