using System.Net;
using System.Text;
using System.Text.Json;
using FitnessClub.Domain.Notifications;
using FitnessClub.Infrastructure.Notifications;
using FitnessClub.UnitTests.Domain;
using Microsoft.Extensions.Options;

namespace FitnessClub.IntegrationTests.Notifications;

public class SendGridNotificationSenderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly StubHandler _handler = new();

    private SendGridNotificationSender Sender() =>
        new(
            new HttpClient(_handler) { BaseAddress = SendGridNotificationSender.BaseAddress },
            Options.Create(new SendGridOptions { ApiKey = "SG.test-key", FromAddress = "club@example.com", FromName = "Fitness Club" }));

    private static Notification Promotion(string? html = "<p>10% OFF</p>") =>
        Notification.Promotion(TestData.Client("olena@example.com"), NotificationContent.Create("10% off", "Dear Olena, 10% off.", html), TestData.Now);

    [Fact]
    public async Task SendAsync_posts_the_email_to_sendgrid_with_text_and_html()
    {
        await Sender().SendAsync(Promotion(), Ct);

        Assert.Equal(HttpMethod.Post, _handler.Request!.Method);
        Assert.Equal("https://api.sendgrid.com/v3/mail/send", _handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", _handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("SG.test-key", _handler.Request.Headers.Authorization.Parameter);
        using var body = JsonDocument.Parse(_handler.Body!);
        var root = body.RootElement;
        Assert.Equal("olena@example.com", root.GetProperty("personalizations")[0].GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("club@example.com", root.GetProperty("from").GetProperty("email").GetString());
        Assert.Equal("Fitness Club", root.GetProperty("from").GetProperty("name").GetString());
        Assert.Equal("10% off", root.GetProperty("subject").GetString());
        var content = root.GetProperty("content");
        Assert.Equal(2, content.GetArrayLength());
        Assert.Equal("text/plain", content[0].GetProperty("type").GetString());
        Assert.Equal("Dear Olena, 10% off.", content[0].GetProperty("value").GetString());
        Assert.Equal("text/html", content[1].GetProperty("type").GetString());
        Assert.Equal("<p>10% OFF</p>", content[1].GetProperty("value").GetString());
    }

    [Fact]
    public async Task SendAsync_without_html_sends_only_text()
    {
        await Sender().SendAsync(Promotion(html: null), Ct);

        using var body = JsonDocument.Parse(_handler.Body!);
        var content = Assert.Single(body.RootElement.GetProperty("content").EnumerateArray());
        Assert.Equal("text/plain", content.GetProperty("type").GetString());
    }

    [Fact]
    public async Task SendAsync_throws_with_sendgrids_reason_when_rejected()
    {
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"errors":[{"message":"The provided authorization grant is invalid, expired, or revoked"}]}""", Encoding.UTF8, "application/json"),
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Sender().SendAsync(Promotion(), Ct));

        Assert.Contains("401", exception.Message);
        Assert.Contains("authorization grant is invalid", exception.Message);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.Accepted);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return Response;
        }
    }
}
