using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(n => n.Channel).HasConversion<string>().HasMaxLength(10);
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(n => n.Recipient).HasMaxLength(Notification.RecipientMaxLength).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(Notification.MessageMaxLength).IsRequired();
        builder.Property(n => n.Subject).HasMaxLength(NotificationContent.SubjectMaxLength).IsRequired();
        builder.Property(n => n.HtmlBody);
        builder.HasIndex(n => new { n.ClientId, n.CreatedAt });
        builder.Property(n => n.FailureReason).HasMaxLength(Notification.FailureReasonMaxLength);
        builder.HasOne<Client>().WithMany().HasForeignKey(n => n.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(n => new { n.MembershipId, n.Type }).IsUnique().HasFilter("[MembershipId] IS NOT NULL");
        builder.HasIndex(n => new { n.Status, n.CreatedAt });
    }
}
