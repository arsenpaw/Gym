using FitnessClub.Application.Notifications;
using FitnessClub.Infrastructure.Notifications;

namespace FitnessClub.IntegrationTests.Notifications;

public class EmailTemplatesTests
{
    private readonly EmailTemplates _templates = new();

    [Fact]
    public void ExpiryReminder_fills_subject_text_and_html_with_the_end_date()
    {
        var content = _templates.ExpiryReminder(new ExpiryReminderEmail("Olena", "Monthly", new DateOnly(2026, 11, 3)));

        Assert.Equal("Your membership expires soon", content.Subject);
        Assert.Equal(
            "Dear Olena, your membership 'Monthly' ends on 3 November 2026. Renew at the reception desk to keep training without a break.",
            content.Text);
        Assert.Contains("Your membership ends on 3 November 2026", content.Html);
        Assert.Contains("Monthly", content.Html);
        Assert.Contains("Dear Olena", content.Html);
        Assert.Contains("#0ca678", content.Html);
        Assert.DoesNotContain("{{", content.Html);
        Assert.DoesNotContain(" days", content.Html);
    }

    [Fact]
    public void Promotion_fills_subject_text_and_html()
    {
        var content = _templates.Promotion(new PromotionEmail("Olena", 10, new DateOnly(2026, 10, 31)));

        Assert.Equal("10% off your next membership", content.Subject);
        Assert.Equal(
            "Dear Olena, this month only: get 10% off any membership. Show this email at the reception desk. Valid until 31 October 2026.",
            content.Text);
        Assert.Contains("10% OFF", content.Html);
        Assert.Contains("31 October 2026", content.Html);
        Assert.Contains("Olena", content.Html);
        Assert.DoesNotContain("{{", content.Html);
    }

    [Fact]
    public void Values_are_html_encoded_and_never_substituted_twice()
    {
        var content = _templates.ExpiryReminder(new ExpiryReminderEmail("<b>Olena</b>", "{{EndsOn}} & Co", new DateOnly(2026, 11, 3)));

        Assert.Contains("&lt;b&gt;Olena&lt;/b&gt;", content.Html);
        Assert.DoesNotContain("<b>Olena</b>", content.Html);
        Assert.Contains("{{EndsOn}} &amp; Co", content.Html);
    }
}
