namespace FitnessClub.Application.Trainers;

public interface ITrainerService
{
    Task<IReadOnlyList<TrainerSummaryResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<TrainerResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<TrainerResponse> HireAsync(TrainerRequest request, CancellationToken cancellationToken);

    Task<TrainerResponse> UpdateProfileAsync(Guid id, TrainerRequest request, CancellationToken cancellationToken);

    Task ActivateAsync(Guid id, CancellationToken cancellationToken);

    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);

    Task<TrainerResponse> SetWorkingHoursAsync(Guid id, IReadOnlyList<WorkingHoursRequest> request, CancellationToken cancellationToken);

    Task AssignClientAsync(Guid id, Guid clientId, CancellationToken cancellationToken);

    Task UnassignClientAsync(Guid id, Guid clientId, CancellationToken cancellationToken);

    Task LinkIdentityAsync(Guid id, LinkIdentityRequest request, CancellationToken cancellationToken);
}
