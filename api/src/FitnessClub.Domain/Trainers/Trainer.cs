using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Domain.Trainers;

public sealed class Trainer : AggregateRoot
{
    public const int SpecializationMaxLength = 100;
    public const int IdentityUserIdMaxLength = 128;

    private readonly List<WorkingHours> _workingHours = [];
    private readonly List<ClientAssignment> _clients = [];

    public PersonName Name { get; private set; } = null!;
    public PhoneNumber Phone { get; private set; } = null!;
    public EmailAddress? Email { get; private set; }
    public string Specialization { get; private set; } = null!;
    public string? IdentityUserId { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<WorkingHours> WorkingHours => _workingHours.AsReadOnly();
    public IReadOnlyCollection<ClientAssignment> Clients => _clients.AsReadOnly();

    private Trainer()
    {
    }

    public static Trainer Hire(PersonName name, PhoneNumber phone, EmailAddress? email, string specialization)
    {
        var trainer = new Trainer { IsActive = true };
        trainer.UpdateProfile(name, phone, email, specialization);
        return trainer;
    }

    public void UpdateProfile(PersonName name, PhoneNumber phone, EmailAddress? email, string specialization)
    {
        if (string.IsNullOrWhiteSpace(specialization))
            throw new DomainException("Specialization is required.");

        var trimmedSpecialization = specialization.Trim();
        if (trimmedSpecialization.Length > SpecializationMaxLength)
            throw new DomainException($"Specialization must be at most {SpecializationMaxLength} characters.");

        Name = name;
        Phone = phone;
        Email = email;
        Specialization = trimmedSpecialization;
    }

    public void LinkIdentity(string identityUserId)
    {
        if (string.IsNullOrWhiteSpace(identityUserId))
            throw new DomainException("Identity user id is required.");

        var trimmed = identityUserId.Trim();
        if (trimmed.Length > IdentityUserIdMaxLength)
            throw new DomainException($"Identity user id must be at most {IdentityUserIdMaxLength} characters.");

        IdentityUserId = trimmed;
    }

    public void SetWorkingHours(IEnumerable<WorkingHours> workingHours)
    {
        var hours = workingHours.ToList();
        for (var i = 0; i < hours.Count; i++)
            for (var j = i + 1; j < hours.Count; j++)
                if (hours[i].Overlaps(hours[j]))
                    throw new DomainException($"Working hours overlap on {hours[i].Day}.");

        _workingHours.Clear();
        _workingHours.AddRange(hours.OrderBy(h => h.Day).ThenBy(h => h.Start));
    }

    public bool IsWorkingDuring(TimeSlot slot) =>
        IsActive
        && slot.Start.ToDateOnly() == slot.End.ToDateOnly()
        && _workingHours.Any(h => h.Covers(slot.Start.DayOfWeek, slot.Start.ToTimeOnly(), slot.End.ToTimeOnly()));

    public void AssignClient(Guid clientId, DateTimeOffset now)
    {
        if (!IsActive)
            throw new DomainException("An inactive trainer cannot take clients.");

        if (_clients.Any(c => c.ClientId == clientId))
            throw new DomainException("The client is already assigned to this trainer.");

        _clients.Add(ClientAssignment.Create(clientId, now));
    }

    public void UnassignClient(Guid clientId)
    {
        var assignment = _clients.FirstOrDefault(c => c.ClientId == clientId)
            ?? throw new DomainException("The client is not assigned to this trainer.");

        _clients.Remove(assignment);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
