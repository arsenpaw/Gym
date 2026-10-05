namespace FitnessClub.Domain.Trainers;

public sealed record ClientAssignment
{
    public Guid ClientId { get; private init; }
    public DateTimeOffset AssignedAt { get; private init; }

    private ClientAssignment()
    {
    }

    internal static ClientAssignment Create(Guid clientId, DateTimeOffset assignedAt) =>
        new() { ClientId = clientId, AssignedAt = assignedAt };
}
