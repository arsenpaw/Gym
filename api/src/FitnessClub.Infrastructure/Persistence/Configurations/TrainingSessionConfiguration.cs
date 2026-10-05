using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.Trainers;
using FitnessClub.Domain.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal sealed class TrainingSessionConfiguration : IEntityTypeConfiguration<TrainingSession>
{
    public void Configure(EntityTypeBuilder<TrainingSession> builder)
    {
        builder.ToTable("TrainingSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Title).HasMaxLength(TrainingSession.TitleMaxLength).IsRequired();
        builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Trainer>().WithMany().HasForeignKey(s => s.TrainerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>().WithMany().HasForeignKey(s => s.RoomId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(s => s.Slot, slot =>
        {
            slot.Property(t => t.Start).HasColumnName("StartsAt");
            slot.Property(t => t.End).HasColumnName("EndsAt");
            slot.HasIndex(t => t.Start);
        });
        builder.Navigation(s => s.Slot).IsRequired();

        builder.OwnsMany(s => s.Bookings, booking =>
        {
            booking.ToTable("Bookings");
            booking.WithOwner().HasForeignKey("TrainingSessionId");
            booking.HasKey(b => b.Id);
            booking.Property(b => b.Id).ValueGeneratedNever();
            booking.HasOne<Client>().WithMany().HasForeignKey(b => b.ClientId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(s => s.Bookings).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
