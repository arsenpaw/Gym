using FitnessClub.Domain.Clients;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;
using FitnessClub.IntegrationTests.Infrastructure;
using FitnessClub.UnitTests.Domain;

namespace FitnessClub.IntegrationTests.Persistence;

public class TrainerRepositoryTests(FitnessClubApiFactory factory) : PersistenceTestBase(factory)
{
    private static Trainer NewTrainer() =>
        Trainer.Hire(PersonName.Create("Taras", "Bondar", null), PhoneNumber.Create(UniquePhone()), null, "Yoga");

    [Fact]
    public async Task Trainer_round_trips_with_working_hours_and_clients()
    {
        var client = Client.Register(PersonName.Create("Olena", "Shevchenko", null), new DateOnly(1995, 3, 14), EmailAddress.Create(TestData.UniqueEmail()), PhoneNumber.Create(UniquePhone()), Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));
        var trainer = NewTrainer();
        trainer.SetWorkingHours(
        [
            WorkingHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            WorkingHours.Create(DayOfWeek.Wednesday, new TimeOnly(14, 0), new TimeOnly(20, 0)),
        ]);
        trainer.AssignClient(client.Id, Now);
        trainer.LinkIdentity($"auth0|{Guid.NewGuid():N}");
        await SaveAsync<ITrainerRepository>(trainers => trainers.Add(trainer));

        var loaded = await ReadAsync<ITrainerRepository, Trainer?>(trainers => trainers.GetByIdentityUserIdAsync(trainer.IdentityUserId!, Ct));

        Assert.NotNull(loaded);
        Assert.Equal(trainer.WorkingHours, loaded.WorkingHours);
        Assert.Equal(client.Id, Assert.Single(loaded.Clients).ClientId);
    }

    [Fact]
    public async Task Replacing_working_hours_removes_old_rows()
    {
        var trainer = NewTrainer();
        trainer.SetWorkingHours([WorkingHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0))]);
        await SaveAsync<ITrainerRepository>(trainers => trainers.Add(trainer));

        await ChangeAsync<ITrainerRepository>(async trainers =>
        {
            var loaded = await trainers.GetByIdAsync(trainer.Id, Ct);
            loaded!.SetWorkingHours([WorkingHours.Create(DayOfWeek.Friday, new TimeOnly(10, 0), new TimeOnly(16, 0))]);
        });

        var reloaded = await ReadAsync<ITrainerRepository, Trainer?>(trainers => trainers.GetByIdAsync(trainer.Id, Ct));
        Assert.Equal(DayOfWeek.Friday, Assert.Single(reloaded!.WorkingHours).Day);
    }
}
