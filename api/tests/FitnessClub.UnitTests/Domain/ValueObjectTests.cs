using System.Globalization;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.UnitTests.Domain;

public class ValueObjectTests
{
    [Theory]
    [InlineData("-1")]
    [InlineData("10.001")]
    public void Money_rejects_negative_or_sub_cent_amounts(string amount)
    {
        Assert.Throws<DomainException>(() => Money.Of(decimal.Parse(amount, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Money_with_same_amount_is_equal()
    {
        Assert.Equal(Money.Of(10.5m), Money.Of(10.50m));
    }

    [Theory]
    [InlineData("+38 (067) 123-45-67", "+380671234567")]
    [InlineData("0671234567", "0671234567")]
    public void PhoneNumber_is_normalized_to_digits(string input, string expected)
    {
        Assert.Equal(expected, PhoneNumber.Create(input).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("+38067abc4567")]
    [InlineData("1234567890123456")]
    [InlineData("38+0671234567")]
    public void PhoneNumber_rejects_invalid_input(string input)
    {
        Assert.Throws<DomainException>(() => PhoneNumber.Create(input));
    }

    [Fact]
    public void EmailAddress_is_trimmed_and_lowercased()
    {
        Assert.Equal("olena@example.com", EmailAddress.Create("  Olena@Example.COM ").Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("olena")]
    [InlineData("olena@localhost")]
    [InlineData("Olena <olena@example.com>")]
    public void EmailAddress_rejects_invalid_input(string input)
    {
        Assert.Throws<DomainException>(() => EmailAddress.Create(input));
    }

    [Fact]
    public void PersonName_trims_parts_and_builds_full_name()
    {
        var name = PersonName.Create(" Olena ", " Shevchenko ", " Petrivna ");

        Assert.Equal("Shevchenko Olena Petrivna", name.FullName);
    }

    [Fact]
    public void PersonName_treats_blank_middle_name_as_missing()
    {
        Assert.Null(PersonName.Create("Olena", "Shevchenko", "  ").MiddleName);
    }

    [Theory]
    [InlineData("", "Shevchenko")]
    [InlineData("Olena", " ")]
    public void PersonName_requires_first_and_last_name(string firstName, string lastName)
    {
        Assert.Throws<DomainException>(() => PersonName.Create(firstName, lastName, null));
    }

    [Fact]
    public void TimeSlot_must_end_after_start()
    {
        var start = TestData.Now;

        Assert.Throws<DomainException>(() => TimeSlot.Create(start, start));
    }

    [Fact]
    public void TimeSlots_touching_at_the_edge_do_not_overlap()
    {
        var first = TimeSlot.Create(TestData.Now, TestData.Now.AddHours(1));
        var second = TimeSlot.Create(TestData.Now.AddHours(1), TestData.Now.AddHours(2));

        Assert.False(first.Overlaps(second));
        Assert.True(first.Overlaps(TimeSlot.Create(TestData.Now.AddMinutes(30), TestData.Now.AddHours(2))));
    }
}
