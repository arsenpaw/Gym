using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Amount).HasMoneyConversion();
        builder.Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Client>().WithMany().HasForeignKey(p => p.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.PaidAt);
        builder.HasIndex(p => p.MembershipId).IsUnique();
    }
}
