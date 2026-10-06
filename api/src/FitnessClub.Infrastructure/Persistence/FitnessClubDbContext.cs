using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Notifications;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.Trainers;
using FitnessClub.Domain.Training;
using FitnessClub.Domain.Visits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FitnessClub.Infrastructure.Persistence;

internal sealed class FitnessClubDbContext(DbContextOptions<FitnessClubDbContext> options) : DbContext(options)
{
    public const string VersionProperty = "Version";

    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Trainer> Trainers => Set<Trainer>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FitnessClubDbContext).Assembly);

        var aggregateRoots = modelBuilder.Model.GetEntityTypes()
            .Where(type => !type.IsOwned() && typeof(AggregateRoot).IsAssignableFrom(type.ClrType))
            .Select(type => type.ClrType)
            .ToList();

        foreach (var root in aggregateRoots)
            modelBuilder.Entity(root).Property<Guid>(VersionProperty).IsConcurrencyToken();
    }

    private static readonly SemaphoreSlim NonRelationalSaveLock = new(1, 1);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampChangedAggregates();
        if (Database.IsRelational())
            return base.SaveChanges(acceptAllChangesOnSuccess);

        NonRelationalSaveLock.Wait();
        try
        {
            foreach (var root in StoredAggregateRootsBeingChanged())
                EnsureNotStale(root, root.GetDatabaseValues());

            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        finally
        {
            NonRelationalSaveLock.Release();
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampChangedAggregates();
        if (Database.IsRelational())
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        await NonRelationalSaveLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var root in StoredAggregateRootsBeingChanged())
                EnsureNotStale(root, await root.GetDatabaseValuesAsync(cancellationToken));

            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        finally
        {
            NonRelationalSaveLock.Release();
        }
    }

    private List<EntityEntry> StoredAggregateRootsBeingChanged() =>
        ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted)
            .Where(entry => !entry.Metadata.IsOwned() && entry.Entity is AggregateRoot)
            .ToList();

    private static void EnsureNotStale(EntityEntry root, PropertyValues? storedValues)
    {
        var expectedVersion = root.Property(VersionProperty).OriginalValue;
        if (storedValues is null || !Equals(storedValues[VersionProperty], expectedVersion))
            throw new DbUpdateConcurrencyException($"{root.Metadata.ClrType.Name} was changed or deleted since it was loaded.");
    }

    private void StampChangedAggregates()
    {
        ChangeTracker.DetectChanges();

        var changedRoots = ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(AggregateRootOf)
            .OfType<EntityEntry>()
            .Where(root => root.State != EntityState.Deleted)
            .DistinctBy(root => root.Entity)
            .ToList();

        foreach (var root in changedRoots)
            root.Property(VersionProperty).CurrentValue = Guid.NewGuid();
    }

    private EntityEntry? AggregateRootOf(EntityEntry entry)
    {
        var current = entry;
        while (current.Metadata.IsOwned())
        {
            var ownership = current.Metadata.FindOwnership()!;
            var ownerKey = ownership.Properties.Select(property => current.Property(property.Name).CurrentValue).ToList();
            current = ChangeTracker.Entries().FirstOrDefault(candidate =>
                candidate.Metadata == ownership.PrincipalEntityType
                && ownership.PrincipalKey.Properties.Select(property => candidate.Property(property.Name).CurrentValue).SequenceEqual(ownerKey));

            if (current is null)
                return null;
        }

        return current;
    }
}
