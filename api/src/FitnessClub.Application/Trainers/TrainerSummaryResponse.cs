using FitnessClub.Domain.Trainers;

namespace FitnessClub.Application.Trainers;

public sealed record TrainerSummaryResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string FullName,
    string Phone,
    string? Email,
    string Specialization,
    bool IsActive)
{
    public static TrainerSummaryResponse FromEntity(Trainer trainer) =>
        new(
            trainer.Id,
            trainer.Name.FirstName,
            trainer.Name.LastName,
            trainer.Name.MiddleName,
            trainer.Name.FullName,
            trainer.Phone.Value,
            trainer.Email?.Value,
            trainer.Specialization,
            trainer.IsActive);
}
