using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Visits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal sealed class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        builder.ToTable("Visits");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.HasOne<Client>().WithMany().HasForeignKey(v => v.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(v => new { v.ClientId, v.CheckedInAt });
    }
}
