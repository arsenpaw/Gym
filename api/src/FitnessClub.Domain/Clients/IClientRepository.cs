using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Domain.Clients;

public interface IClientRepository : IRepository<Client>
{
    Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken);

    Task<bool> PhoneExistsAsync(PhoneNumber phone, Guid? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Client>> ListWithMembershipsEndingBetweenAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
