using FitnessClub.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal static class ValueConversions
{
    public static PropertyBuilder<Money> HasMoneyConversion(this PropertyBuilder<Money> property) =>
        property.HasConversion(money => money.Amount, amount => Money.Of(amount)).HasPrecision(18, 2);

    public static PropertyBuilder<PhoneNumber> HasPhoneConversion(this PropertyBuilder<PhoneNumber> property) =>
        property.HasConversion(phone => phone.Value, value => PhoneNumber.Create(value)).HasMaxLength(PhoneNumber.MaxLength);

    public static PropertyBuilder<PhoneNumber?> HasOptionalPhoneConversion(this PropertyBuilder<PhoneNumber?> property) =>
        property.HasConversion(phone => phone!.Value, value => PhoneNumber.Create(value)).HasMaxLength(PhoneNumber.MaxLength);

    public static PropertyBuilder<EmailAddress> HasEmailConversion(this PropertyBuilder<EmailAddress> property) =>
        property.HasConversion(email => email.Value, value => EmailAddress.Create(value)).HasMaxLength(EmailAddress.MaxLength);

    public static PropertyBuilder<EmailAddress?> HasOptionalEmailConversion(this PropertyBuilder<EmailAddress?> property) =>
        property.HasConversion(email => email!.Value, value => EmailAddress.Create(value)).HasMaxLength(EmailAddress.MaxLength);

    public static void OwnsPersonName<TOwner>(this EntityTypeBuilder<TOwner> builder, System.Linq.Expressions.Expression<Func<TOwner, PersonName?>> navigation)
        where TOwner : class =>
        builder.OwnsOne(navigation, name =>
        {
            name.Property(n => n.FirstName).HasColumnName("FirstName").HasMaxLength(PersonName.PartMaxLength).IsRequired();
            name.Property(n => n.LastName).HasColumnName("LastName").HasMaxLength(PersonName.PartMaxLength).IsRequired();
            name.Property(n => n.MiddleName).HasColumnName("MiddleName").HasMaxLength(PersonName.PartMaxLength);
        });
}
