using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Visits;

namespace FitnessClub.Application.Clients;

internal sealed class ClientService(
    IClientRepository clients,
    IMembershipPlanRepository plans,
    IPaymentRepository payments,
    IVisitRepository visits,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IClientService
{
    public async Task<IReadOnlyList<ClientSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var today = Today();
        var list = await clients.ListAsync(cancellationToken);
        return list.Select(client => ClientSummaryResponse.FromEntity(client, today)).ToList();
    }

    public async Task<ClientDetailsResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ClientDetailsResponse.FromEntity(await FindAsync(id, cancellationToken), Today());

    public async Task<ClientDetailsResponse> RegisterAsync(ClientRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetLocalNow();
        var profile = Profile.From(request);
        await EnsurePhoneIsUniqueAsync(profile.Phone, excludeId: null, cancellationToken);

        var client = Client.Register(profile.Name, profile.DateOfBirth, profile.Phone, profile.Email, now);
        clients.Add(client);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ClientDetailsResponse.FromEntity(client, ToDate(now));
    }

    public async Task<ClientDetailsResponse> UpdateAsync(Guid id, ClientRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetLocalNow();
        var client = await FindAsync(id, cancellationToken);
        var profile = Profile.From(request);
        await EnsurePhoneIsUniqueAsync(profile.Phone, client.Id, cancellationToken);

        client.UpdateProfile(profile.Name, profile.DateOfBirth, profile.Phone, profile.Email, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ClientDetailsResponse.FromEntity(client, ToDate(now));
    }

    public async Task<MembershipResponse> PurchaseMembershipAsync(
        Guid id, PurchaseMembershipRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetLocalNow();
        var today = ToDate(now);
        var client = await FindAsync(id, cancellationToken);
        var planId = request.PlanId.GetValueOrDefault();
        var plan = await plans.GetByIdAsync(planId, cancellationToken)
            ?? throw new NotFoundException($"Membership plan '{planId}' was not found.");

        var payment = client.PurchaseMembership(plan, request.StartsOn ?? today, request.PaymentMethod.GetValueOrDefault(), now);
        payments.Add(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var membership = client.Memberships.Single(m => m.Id == payment.MembershipId);
        return MembershipResponse.FromEntity(membership, today);
    }

    public async Task CancelMembershipAsync(Guid id, Guid membershipId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetLocalNow();
        var client = await FindAsync(id, cancellationToken);
        if (client.Memberships.All(m => m.Id != membershipId))
            throw new NotFoundException($"Membership '{membershipId}' was not found for client '{id}'.");

        client.CancelMembership(membershipId, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<VisitResponse> CheckInAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetLocalNow();
        var client = await FindAsync(id, cancellationToken);

        var visit = client.CheckIn(now);
        visits.Add(visit);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return VisitResponse.FromEntity(visit);
    }

    public async Task<IReadOnlyList<VisitResponse>> ListVisitsAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await FindAsync(id, cancellationToken);
        var list = await visits.ListForClientAsync(client.Id, cancellationToken);
        return list.Select(VisitResponse.FromEntity).ToList();
    }

    private DateOnly Today() => ToDate(timeProvider.GetLocalNow());

    private static DateOnly ToDate(DateTimeOffset now) => DateOnly.FromDateTime(now.DateTime);

    private async Task<Client> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await clients.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Client '{id}' was not found.");

    private async Task EnsurePhoneIsUniqueAsync(PhoneNumber phone, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await clients.PhoneExistsAsync(phone, excludeId, cancellationToken))
            throw new ConflictException($"A client with phone '{phone}' already exists.");
    }

    private sealed record Profile(PersonName Name, DateOnly DateOfBirth, PhoneNumber Phone, EmailAddress? Email)
    {
        public static Profile From(ClientRequest request) =>
            new(
                PersonName.Create(request.FirstName, request.LastName, request.MiddleName),
                request.DateOfBirth.GetValueOrDefault(),
                PhoneNumber.Create(request.Phone),
                string.IsNullOrWhiteSpace(request.Email) ? null : EmailAddress.Create(request.Email));
    }
}
