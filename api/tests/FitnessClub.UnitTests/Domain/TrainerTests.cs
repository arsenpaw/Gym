using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.UnitTests.Domain;

public class TrainerTests
{
    private static Trainer NewTrainer() =>
        Trainer.Hire(PersonName.Create("Taras", "Bondar", null), PhoneNumber.Create("+380501112233"), null, "  Yoga ");

    [Fact]
    public void Hire_trims_specialization_and_starts_active()
    {
        var trainer = NewTrainer();

        Assert.Equal("Yoga", trainer.Specialization);
        Assert.True(trainer.IsActive);
        Assert.Empty(trainer.WorkingHours);
    }

    [Fact]
    public void Hire_without_specialization_throws()
    {
        Assert.Throws<DomainException>(() =>
            Trainer.Hire(PersonName.Create("Taras", "Bondar", null), PhoneNumber.Create("+380501112233"), null, " "));
    }

    [Fact]
    public void WorkingHours_must_end_after_start()
    {
        Assert.Throws<DomainException>(() => WorkingHours.Create(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(9, 0)));
    }

    [Fact]
    public void SetWorkingHours_with_overlap_on_same_day_throws_and_keeps_old_hours()
    {
        var trainer = NewTrainer();
        trainer.SetWorkingHours([WorkingHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0))]);

        Assert.Throws<DomainException>(() => trainer.SetWorkingHours(
        [
            WorkingHours.Create(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            WorkingHours.Create(DayOfWeek.Tuesday, new TimeOnly(11, 0), new TimeOnly(14, 0)),
        ]));

        Assert.Equal(DayOfWeek.Monday, trainer.WorkingHours.Single().Day);
    }

    [Fact]
    public void SetWorkingHours_allows_split_shifts_on_one_day()
    {
        var trainer = NewTrainer();

        trainer.SetWorkingHours(
        [
            WorkingHours.Create(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0)),
            WorkingHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
        ]);

        Assert.Equal([new TimeOnly(8, 0), new TimeOnly(14, 0)], trainer.WorkingHours.Select(h => h.Start));
    }

    [Fact]
    public void IsWorkingDuring_requires_slot_inside_one_shift()
    {
        var trainer = NewTrainer();
        var day = TestData.Slot(daysFromToday: 1).Start.DayOfWeek;
        trainer.SetWorkingHours([WorkingHours.Create(day, new TimeOnly(9, 0), new TimeOnly(12, 0))]);

        Assert.True(trainer.IsWorkingDuring(TestData.Slot(startHour: 9, durationMinutes: 180)));
        Assert.False(trainer.IsWorkingDuring(TestData.Slot(startHour: 11, durationMinutes: 90)));
        Assert.False(trainer.IsWorkingDuring(TestData.Slot(startHour: 9, daysFromToday: 2)));
    }

    [Fact]
    public void Inactive_trainer_is_not_working()
    {
        var trainer = TestData.Trainer();
        trainer.Deactivate();

        Assert.False(trainer.IsWorkingDuring(TestData.Slot()));
    }

    [Fact]
    public void AssignClient_adds_client_once()
    {
        var trainer = NewTrainer();
        var clientId = Guid.NewGuid();

        trainer.AssignClient(clientId, TestData.Now);

        Assert.Equal(clientId, trainer.Clients.Single().ClientId);
        Assert.Throws<DomainException>(() => trainer.AssignClient(clientId, TestData.Now));
    }

    [Fact]
    public void AssignClient_to_inactive_trainer_throws()
    {
        var trainer = NewTrainer();
        trainer.Deactivate();

        Assert.Throws<DomainException>(() => trainer.AssignClient(Guid.NewGuid(), TestData.Now));
    }

    [Fact]
    public void UnassignClient_removes_client_and_rejects_unknown_client()
    {
        var trainer = NewTrainer();
        var clientId = Guid.NewGuid();
        trainer.AssignClient(clientId, TestData.Now);

        trainer.UnassignClient(clientId);

        Assert.Empty(trainer.Clients);
        Assert.Throws<DomainException>(() => trainer.UnassignClient(clientId));
    }

    [Fact]
    public void LinkIdentity_requires_value()
    {
        var trainer = NewTrainer();

        Assert.Throws<DomainException>(() => trainer.LinkIdentity(" "));

        trainer.LinkIdentity(" auth0|abc ");
        Assert.Equal("auth0|abc", trainer.IdentityUserId);
    }
}
