namespace FitnessClub.Application.Reports;

public sealed record PaymentEntry(decimal Amount, DateTimeOffset PaidAt);

public sealed record SessionLoadEntry(
    Guid TrainerId,
    string TrainerName,
    Guid RoomId,
    string RoomName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int Capacity,
    IReadOnlyList<Guid> BookedClientIds);
