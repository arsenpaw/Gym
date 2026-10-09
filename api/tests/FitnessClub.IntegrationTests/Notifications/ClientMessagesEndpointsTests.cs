using System.Net;
using System.Net.Http.Json;
using FitnessClub.Application.ClientMessages;
using FitnessClub.Application.Common;
using FitnessClub.Application.Notifications;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.IntegrationTests.Notifications;

public class ClientMessagesEndpointsTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Url(Guid clientId) => $"/api/clients/{clientId}/messages";

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Receptionist)]
    public async Task Preview_returns_the_rendered_email(string role)
    {
        var client = await factory.ClientWithMembershipAsync();

        var preview = await factory.CreateClientWithRoles(role)
            .GetFromJsonAsync<ClientMessagePreviewResponse>($"{Url(client.Id)}/preview?template=Promotion", Ct);

        Assert.Equal("Promotion", preview!.Template);
        Assert.Equal(client.Email!.Value, preview.Recipient);
        Assert.Equal("10% off your next membership", preview.Subject);
        Assert.Contains("Olena", preview.Html);
    }

    [Theory]
    [InlineData("?template=Birthday")]
    [InlineData("")]
    public async Task Preview_with_an_unknown_or_missing_template_returns_400(string query)
    {
        var client = await factory.ClientWithMembershipAsync();

        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync($"{Url(client.Id)}/preview{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Preview_for_an_unknown_client_returns_404()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync($"{Url(Guid.NewGuid())}/preview?template=Promotion", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Send_returns_the_sent_message_and_it_is_listed_for_the_client()
    {
        var client = await factory.ClientWithMembershipAsync();
        var http = factory.CreateClientWithRoles(Roles.Receptionist);

        var response = await http.PostAsJsonAsync(Url(client.Id), new { template = "ExpiryReminder" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = await response.Content.ReadFromJsonAsync<NotificationResponse>(Ct);
        Assert.Equal("Sent", sent!.Status);
        Assert.Equal("ExpiryReminder", sent.Type);
        Assert.Equal("Your membership expires soon", sent.Subject);
        var list = await http.GetFromJsonAsync<List<NotificationResponse>>(Url(client.Id), Ct);
        Assert.Equal(sent.Id, Assert.Single(list!).Id);
    }

    [Fact]
    public async Task Send_with_an_unknown_template_returns_400()
    {
        var client = await factory.ClientWithMembershipAsync();

        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsJsonAsync(Url(client.Id), new { template = "Birthday" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_for_an_unknown_client_returns_404()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync(Url(Guid.NewGuid()), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_for_a_trainer_return_403()
    {
        var trainer = factory.CreateClientWithRoles(Roles.Trainer);
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await trainer.GetAsync($"{Url(id)}/preview?template=Promotion", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await trainer.PostAsJsonAsync(Url(id), new { template = "Promotion" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await trainer.GetAsync(Url(id), Ct)).StatusCode);
    }

    [Fact]
    public async Task Endpoints_without_a_user_return_401()
    {
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url(Guid.NewGuid()), Ct)).StatusCode);
    }
}
