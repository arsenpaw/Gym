using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.Application.Trainers;

internal sealed class TrainerService(
    ITrainerRepository trainers,
    IClientRepository clients,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ITrainerService
{
    public async Task<IReadOnlyList<TrainerSummaryResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var list = await trainers.ListAsync(includeInactive, cancellationToken);
        return list.Select(TrainerSummaryResponse.FromEntity).ToList();
    }

    public async Task<TrainerResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await ToResponseAsync(await FindAsync(id, cancellationToken), cancellationToken);

    public async Task<TrainerResponse> HireAsync(TrainerRequest request, CancellationToken cancellationToken)
    {
        var (name, phone, email) = ProfileValues(request);
        var trainer = Trainer.Hire(name, phone, email, request.Specialization);
        await EnsurePhoneIsUniqueAsync(phone, excludeId: null, cancellationToken);

        trainers.Add(trainer);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(trainer, cancellationToken);
    }

    public async Task<TrainerResponse> UpdateProfileAsync(Guid id, TrainerRequest request, CancellationToken cancellationToken)
    {
        var trainer = await FindAsync(id, cancellationToken);
        var (name, phone, email) = ProfileValues(request);
        await EnsurePhoneIsUniqueAsync(phone, trainer.Id, cancellationToken);
        trainer.UpdateProfile(name, phone, email, request.Specialization);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(trainer, cancellationToken);
    }

    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var trainer = await FindAsync(id, cancellationToken);
        trainer.Activate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var trainer = await FindAsync(id, cancellationToken);
        trainer.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<TrainerResponse> SetWorkingHoursAsync(
        Guid id, IReadOnlyList<WorkingHoursRequest> request, CancellationToken cancellationToken)
    {
        var trainer = await FindAsync(id, cancellationToken);
        trainer.SetWorkingHours(request.Select(h => WorkingHours.Create(h.Day!.Value, h.Start!.Value, h.End!.Value)));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(trainer, cancellationToken);
    }

    public async Task AssignClientAsync(Guid id, Guid clientId, CancellationToken cancellationToken)
    {
        var trainer = await FindAsync(id, cancellationToken);
        if (await clients.GetByIdAsync(clientId, cancellationToken) is null)
            throw new NotFoundException($"Client '{clientId}' was not found.");

        trainer.AssignClient(clientId, timeProvider.GetLocalNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnassignClientAsync(Guid id, Guid clientId, CancellationToken cancellationToken)
    {
        var trainer = await FindAsync(id, cancellationToken);
        trainer.UnassignClient(clientId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task LinkIdentityAsync(Guid id, LinkIdentityRequest request, CancellationToken cancellationToken)
    {
        var trainer = await FindAsync(id, cancellationToken);
        var identityUserId = request.IdentityUserId.Trim();

        var owner = await trainers.GetByIdentityUserIdAsync(identityUserId, cancellationToken);
        if (owner is not null && owner.Id != trainer.Id)
            throw new ConflictException($"Identity user '{identityUserId}' is already linked to another trainer.");

        trainer.LinkIdentity(identityUserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static (PersonName Name, PhoneNumber Phone, EmailAddress? Email) ProfileValues(TrainerRequest request) =>
        (PersonName.Create(request.FirstName, request.LastName, request.MiddleName),
            PhoneNumber.Create(request.Phone),
            string.IsNullOrWhiteSpace(request.Email) ? null : EmailAddress.Create(request.Email));

    private async Task<Trainer> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await trainers.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Trainer '{id}' was not found.");

    private async Task EnsurePhoneIsUniqueAsync(PhoneNumber phone, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await trainers.PhoneExistsAsync(phone, excludeId, cancellationToken))
            throw new ConflictException($"A trainer with phone '{phone}' already exists.");
    }

    private async Task<TrainerResponse> ToResponseAsync(Trainer trainer, CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, string>();
        foreach (var assignment in trainer.Clients)
        {
            var client = await clients.GetByIdAsync(assignment.ClientId, cancellationToken);
            if (client is not null)
                names[client.Id] = client.Name.FullName;
        }

        return TrainerResponse.FromEntity(trainer, names);
    }
}
