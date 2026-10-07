namespace FitnessClub.Application.Reports;

public sealed record LoadReportResponse(DateOnly From, DateOnly To, IReadOnlyList<DailyLoad> Days);

public sealed record DailyLoad(DateOnly Date, IReadOnlyList<TrainerLoad> Trainers, IReadOnlyList<RoomLoad> Rooms);

public sealed record TrainerLoad(Guid TrainerId, string TrainerName, int Sessions, decimal BookedHours, int ClientsBooked);

public sealed record RoomLoad(
    Guid RoomId,
    string RoomName,
    int Sessions,
    decimal OccupiedHours,
    int BookedPlaces,
    int TotalPlaces,
    decimal UtilizationPercent);
