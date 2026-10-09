using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
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
        builder.Property(c => c.Email).HasEmailConversion().IsRequired();
        builder.HasIndex(c => c.Email).IsUnique();
        builder.Property(c => c.Phone).HasOptionalPhoneConversion();

        builder.OwnsMany(c => c.Memberships, membership =>
        {
            membership.ToTable("Memberships");
            membership.WithOwner().HasForeignKey("ClientId");
            membership.HasKey(m => m.Id);
            membership.Property(m => m.Id).ValueGeneratedNever();
            membership.HasOne<MembershipPlan>().WithMany().HasForeignKey(m => m.PlanId).OnDelete(DeleteBehavior.Restrict);
            membership.Property(m => m.PlanName).HasMaxLength(MembershipPlan.NameMaxLength).IsRequired();
            membership.Property(m => m.Price).HasMoneyConversion();
            membership.HasIndex(m => m.EndsOn);
        });
        builder.Navigation(c => c.Memberships).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
