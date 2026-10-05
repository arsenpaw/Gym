using FitnessClub.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.OwnsPersonName(c => c.Name);
        builder.Navigation(c => c.Name).IsRequired();
        builder.Property(c => c.Phone).HasPhoneConversion().IsRequired();
        builder.HasIndex(c => c.Phone).IsUnique();
        builder.Property(c => c.Email).HasEmailConversion();

        builder.OwnsMany(c => c.Memberships, membership =>
        {
            membership.ToTable("Memberships");
            membership.WithOwner().HasForeignKey("ClientId");
            membership.HasKey(m => m.Id);
            membership.Property(m => m.Id).ValueGeneratedNever();
            membership.Property(m => m.PlanName).HasMaxLength(Domain.MembershipPlans.MembershipPlan.NameMaxLength).IsRequired();
            membership.Property(m => m.Price).HasMoneyConversion();
            membership.HasIndex(m => m.EndsOn);
        });
        builder.Navigation(c => c.Memberships).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
