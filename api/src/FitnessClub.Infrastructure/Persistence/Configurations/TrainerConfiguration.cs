using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Trainers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal sealed class TrainerConfiguration : IEntityTypeConfiguration<Trainer>
{
    public void Configure(EntityTypeBuilder<Trainer> builder)
    {
        builder.ToTable("Trainers");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.OwnsPersonName(t => t.Name);
        builder.Navigation(t => t.Name).IsRequired();
        builder.Property(t => t.Phone).HasPhoneConversion().IsRequired();
        builder.HasIndex(t => t.Phone).IsUnique();
        builder.Property(t => t.Email).HasEmailConversion();
        builder.Property(t => t.Specialization).HasMaxLength(Trainer.SpecializationMaxLength).IsRequired();
        builder.Property(t => t.IdentityUserId).HasMaxLength(Trainer.IdentityUserIdMaxLength);
        builder.HasIndex(t => t.IdentityUserId).IsUnique().HasFilter("[IdentityUserId] IS NOT NULL");

        builder.OwnsMany(t => t.WorkingHours, hours =>
        {
            hours.ToTable("TrainerWorkingHours");
            hours.WithOwner().HasForeignKey("TrainerId");
            hours.Property<int>("Id");
            hours.HasKey("Id");
            hours.Property(h => h.Day).HasConversion<string>().HasMaxLength(10);
        });
        builder.Navigation(t => t.WorkingHours).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(t => t.Clients, assignment =>
        {
            assignment.ToTable("TrainerClients");
            assignment.WithOwner().HasForeignKey("TrainerId");
            assignment.HasKey("TrainerId", nameof(ClientAssignment.ClientId));
            assignment.HasOne<Client>().WithMany().HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(t => t.Clients).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
