namespace FitnessClub.Application.Clients;

public interface IClientService
{
    Task<IReadOnlyList<ClientSummaryResponse>> ListAsync(CancellationToken cancellationToken);

    Task<ClientDetailsResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ClientDetailsResponse> RegisterAsync(ClientRequest request, CancellationToken cancellationToken);

    Task<ClientDetailsResponse> UpdateAsync(Guid id, ClientRequest request, CancellationToken cancellationToken);

    Task<MembershipResponse> PurchaseMembershipAsync(Guid id, PurchaseMembershipRequest request, CancellationToken cancellationToken);

    Task CancelMembershipAsync(Guid id, Guid membershipId, CancellationToken cancellationToken);

    Task<VisitResponse> CheckInAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<VisitResponse>> ListVisitsAsync(Guid id, CancellationToken cancellationToken);
}
