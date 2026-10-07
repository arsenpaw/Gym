using System.Text.Json.Serialization;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.Application.Trainers;

public sealed record TrainerResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string FullName,
    string Phone,
    string? Email,
    string Specialization,
    bool IsActive,
    string? IdentityUserId,
    IReadOnlyList<WorkingHoursResponse> WorkingHours,
    IReadOnlyList<TrainerClientResponse> Clients)
{
    public static TrainerResponse FromEntity(Trainer trainer, IReadOnlyDictionary<Guid, string> clientNames) =>
        new(
            trainer.Id,
            trainer.Name.FirstName,
            trainer.Name.LastName,
            trainer.Name.MiddleName,
            trainer.Name.FullName,
            trainer.Phone.Value,
            trainer.Email?.Value,
            trainer.Specialization,
            trainer.IsActive,
            trainer.IdentityUserId,
            trainer.WorkingHours.Select(WorkingHoursResponse.FromEntity).ToList(),
            trainer.Clients
                .OrderBy(c => c.AssignedAt)
                .Select(c => new TrainerClientResponse(c.ClientId, clientNames.GetValueOrDefault(c.ClientId), c.AssignedAt))
                .ToList());
}

public sealed record WorkingHoursResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<DayOfWeek>))] DayOfWeek Day,
    TimeOnly Start,
    TimeOnly End)
{
    public static WorkingHoursResponse FromEntity(WorkingHours hours) => new(hours.Day, hours.Start, hours.End);
}

public sealed record TrainerClientResponse(Guid ClientId, string? FullName, DateTimeOffset AssignedAt);
