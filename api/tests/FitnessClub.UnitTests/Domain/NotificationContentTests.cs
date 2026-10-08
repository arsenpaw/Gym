using FitnessClub.Domain.Common;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.UnitTests.Domain;

public class NotificationContentTests
{
    [Fact]
    public void Create_trims_subject_and_text_and_keeps_html()
    {
        var content = NotificationContent.Create("  Hello  ", "  Body  ", "<p>Body</p>");

        Assert.Equal("Hello", content.Subject);
        Assert.Equal("Body", content.Text);
        Assert.Equal("<p>Body</p>", content.Html);
    }

    [Fact]
    public void Create_without_html_leaves_it_empty()
    {
        Assert.Null(NotificationContent.Create("Hello", "Body", "   ").Html);
    }

    [Theory]
    [InlineData("", "Body")]
    [InlineData("Hello", " ")]
    public void Create_requires_subject_and_text(string subject, string text)
    {
        Assert.Throws<DomainException>(() => NotificationContent.Create(subject, text));
    }

    [Fact]
    public void Create_rejects_a_too_long_subject_or_text()
    {
        Assert.Throws<DomainException>(() => NotificationContent.Create(new string('s', NotificationContent.SubjectMaxLength + 1), "Body"));
        Assert.Throws<DomainException>(() => NotificationContent.Create("Hello", new string('t', Notification.MessageMaxLength + 1)));
    }
}
