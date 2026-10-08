---
tags: [plan, api, ui, notifications]
date: 2026-10-08
---

# Client Messages over Twilio SendGrid Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the club's own SMTP sender with Twilio SendGrid, add two branded HTML email templates (expiry reminder, 10% promotion), and let Admin/Receptionist preview and send them from the client page.

**Architecture:**
- Domain:
  - `Notification` stores a subject, a plain-text body and an optional HTML body.
  - Factories take a `NotificationContent` value object instead of composing the wording.
- Infrastructure:
  - `EmailTemplates` renders embedded HTML files into that content.
  - `SendGridNotificationSender` posts it to SendGrid's v3 REST API through a typed `HttpClient`.
- Application:
  - A new `ClientMessageService` composes, previews, sends and lists messages per client.
  - A new `ClientMessagesController` exposes it.
- UI: the client page gets a Messages card with an iframe preview, send-with-confirm and the history table.

**Tech Stack:** .NET 10, EF Core (SQL Server), xUnit v3, React 19 + Mantine 9, Orval, TanStack Query, MSW, Vitest.

**Spec:** [[2026-10-08-client-messages-design]] (`docs/Code/Specs/2026-10-08-client-messages-design.md`)

## Global Constraints

- **SendGrid only:** no SMTP, no MailKit, no SendGrid SDK package. Use `POST https://api.sendgrid.com/v3/mail/send` with `Authorization: Bearer <SendGrid:ApiKey>`.
- **Settings:** `SendGrid:ApiKey`, `SendGrid:FromAddress` (required when `ApiKey` is set, checked at startup), `SendGrid:FromName` (default `Fitness Club`). An empty `ApiKey` means `LoggingNotificationSender`.
- **Roles:** Admin and Receptionist on `api/clients/{id}/messages`. Trainer gets 403, anonymous 401.
- **Promotion:** 10% off any membership, valid until the last day of the current (club-local) month. There is no staff input.
- **Content limits:**
  - Subject: required, ≤ 200.
  - Text: required, ≤ 1000 (`Notification.MessageMaxLength`).
  - HTML: optional, unlimited (`nvarchar(max)`).
- **Template style:**
  - Layout: 600px table layout with inline CSS only, and no SVG or images.
  - Colors: teal `#0ca678` header with a "Fitness Club" wordmark, a white rounded card on `#f1f3f5`, a grey footer.
  - Fonts: `'Inter', system-ui, -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif`.
- **C# rules:**
  - No comments in C# (`SourceCodeTests`).
  - Infrastructure types are `internal`.
  - Package versions only in `api/Directory.Packages.props`.
  - Warnings are errors, so pass `TestContext.Current.CancellationToken` in tests.
- **UI rules:**
  - Use the generated Orval hooks only, and never edit `src/api/generated/`.
  - No new UI libraries. A native `<iframe>` is fine.
- **Dates in emails** use the format `d MMMM yyyy` (invariant culture), e.g. `3 November 2026`.

## Review Focus

1. **A client or plan name containing HTML or `{{Token}}`** (e.g. `<b>Olena</b>`, `{{PlanName}}`). The email must show it literally: encoded, never re-substituted, never a crash. The test is in Task 1.
2. **A membership ending today or tomorrow.** The reminder must say "today" / "tomorrow", never "in 0 days" or "in 1 days". The test is in Task 1.
3. **SendGrid rejecting the request** (bad key 401, unverified sender 403). The send returns 200 with status `Failed` and SendGrid's reason, and the reason is retryable later. Tests are in Task 3 (sender) and Task 4 (service).
4. **Retrying an old notice with no HTML** (rows created before this change have `HtmlBody = null`). It must go out as text only, with no empty `text/html` part. The test is in Task 3.
5. **The promotion sent on the last day of a month or in February of a leap year** must name that month's last day (31 December, 29 February). The test is in Task 4.

---

## File map

| File | Responsibility |
|---|---|
| `api/src/FitnessClub.Domain/Notifications/NotificationContent.cs` (new) | Subject/text/html value object with limits |
| `api/src/FitnessClub.Domain/Notifications/Notification.cs` | `Subject`, `HtmlBody`, the three factories |
| `api/src/FitnessClub.Domain/Notifications/NotificationType.cs` | `ExpiryReminder`, `Promotion` |
| `api/src/FitnessClub.Domain/Notifications/INotificationRepository.cs` | `ListForClientAsync` |
| `api/src/FitnessClub.Application/Abstractions/IEmailTemplates.cs` (new) | Template port |
| `api/src/FitnessClub.Application/Abstractions/INotificationSender.cs` | `SendAsync(Notification, ct)` |
| `api/src/FitnessClub.Application/Notifications/ExpiryReminderEmail.cs`, `PromotionEmail.cs` (new) | Template models |
| `api/src/FitnessClub.Application/Notifications/ExpiryNotificationService.cs` | Uses the reminder template and the new sender signature |
| `api/src/FitnessClub.Application/Notifications/NotificationResponse.cs` | `Subject` |
| `api/src/FitnessClub.Application/ClientMessages/*` (new) | Service, interface, request/response |
| `api/src/FitnessClub.Infrastructure/Notifications/EmailTemplates.cs` (new) + `Templates/*.html` (new) | Rendering |
| `api/src/FitnessClub.Infrastructure/Notifications/SendGridOptions.cs`, `SendGridNotificationSender.cs` (new) | Delivery |
| `api/src/FitnessClub.Infrastructure/Notifications/LoggingNotificationSender.cs` | New signature |
| `api/src/FitnessClub.Infrastructure/Notifications/SmtpNotificationSender.cs`, `SmtpOptions.cs` | **Delete** |
| `api/src/FitnessClub.Infrastructure/DependencyInjection.cs` | Templates, SendGrid/Logging choice |
| `api/src/FitnessClub.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs` + new migration | Columns |
| `api/src/FitnessClub.Infrastructure/Persistence/Repositories/NotificationRepository.cs` | `ListForClientAsync` |
| `api/src/FitnessClub.Api/Controllers/ClientMessagesController.cs` (new) | HTTP |
| `ui/src/features/clients/ClientMessagesCard.tsx` (new) | Card |
| `ui/src/features/notifications/notificationLabels.ts` (new) | Shared type labels and status colors |
| `ui/src/features/notifications/NotificationsPage.tsx` | Type + Subject columns |

---

### Task 1: Notification content and the two email templates

**Files:**
- Create: `api/src/FitnessClub.Domain/Notifications/NotificationContent.cs`
- Create: `api/src/FitnessClub.Application/Abstractions/IEmailTemplates.cs`
- Create: `api/src/FitnessClub.Application/Notifications/ExpiryReminderEmail.cs`
- Create: `api/src/FitnessClub.Application/Notifications/PromotionEmail.cs`
- Create: `api/src/FitnessClub.Infrastructure/Notifications/EmailTemplates.cs`
- Create: `api/src/FitnessClub.Infrastructure/Notifications/Templates/ExpiryReminder.html`
- Create: `api/src/FitnessClub.Infrastructure/Notifications/Templates/Promotion.html`
- Modify: `api/src/FitnessClub.Infrastructure/FitnessClub.Infrastructure.csproj` (embed the HTML)
- Modify: `api/src/FitnessClub.Infrastructure/DependencyInjection.cs` (register templates)
- Test: `api/tests/FitnessClub.UnitTests/Domain/NotificationContentTests.cs`
- Test: `api/tests/FitnessClub.IntegrationTests/Notifications/EmailTemplatesTests.cs`

**Interfaces:**
- Produces:
  - `NotificationContent.Create(string subject, string text, string? html = null)` with `.Subject`, `.Text`, `.Html` and `NotificationContent.SubjectMaxLength = 200`
  - `IEmailTemplates.ExpiryReminder(ExpiryReminderEmail) : NotificationContent` and `IEmailTemplates.Promotion(PromotionEmail) : NotificationContent`
  - `ExpiryReminderEmail(string FirstName, string PlanName, DateOnly EndsOn, int DaysLeft)` with `static For(Client, Membership, DateOnly today)`
  - `PromotionEmail(string FirstName, int DiscountPercent, DateOnly ValidUntil)`
  - `EmailTemplates` (internal, singleton, registered as `IEmailTemplates`)

- [ ] **Step 1: Write the failing domain tests**

`api/tests/FitnessClub.UnitTests/Domain/NotificationContentTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run them and see them fail to compile**

Run: `cd api && dotnet test --project tests/FitnessClub.UnitTests --filter-class "FitnessClub.UnitTests.Domain.NotificationContentTests"`
Expected: build error `The type or namespace name 'NotificationContent' could not be found`.

- [ ] **Step 3: Implement `NotificationContent`**

`api/src/FitnessClub.Domain/Notifications/NotificationContent.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Notifications;

public sealed record NotificationContent
{
    public const int SubjectMaxLength = 200;

    public string Subject { get; }
    public string Text { get; }
    public string? Html { get; }

    private NotificationContent(string subject, string text, string? html)
    {
        Subject = subject;
        Text = text;
        Html = html;
    }

    public static NotificationContent Create(string subject, string text, string? html = null)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new DomainException("A subject is required.");

        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("A message is required.");

        var trimmedSubject = subject.Trim();
        if (trimmedSubject.Length > SubjectMaxLength)
            throw new DomainException($"A subject must be at most {SubjectMaxLength} characters.");

        var trimmedText = text.Trim();
        if (trimmedText.Length > Notification.MessageMaxLength)
            throw new DomainException($"A message must be at most {Notification.MessageMaxLength} characters.");

        return new NotificationContent(trimmedSubject, trimmedText, string.IsNullOrWhiteSpace(html) ? null : html);
    }
}
```

- [ ] **Step 4: Run the domain tests**

Run: `cd api && dotnet test --project tests/FitnessClub.UnitTests --filter-class "FitnessClub.UnitTests.Domain.NotificationContentTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Write the failing template tests**

`api/tests/FitnessClub.IntegrationTests/Notifications/EmailTemplatesTests.cs`. These need no database: they construct the internal class directly.

```csharp
using FitnessClub.Application.Notifications;
using FitnessClub.Infrastructure.Notifications;

namespace FitnessClub.IntegrationTests.Notifications;

public class EmailTemplatesTests
{
    private readonly EmailTemplates _templates = new();

    [Fact]
    public void ExpiryReminder_fills_subject_text_and_html()
    {
        var content = _templates.ExpiryReminder(new ExpiryReminderEmail("Olena", "Monthly", new DateOnly(2026, 11, 3), 5));

        Assert.Equal("Your membership expires soon", content.Subject);
        Assert.Equal(
            "Dear Olena, your membership 'Monthly' ends in 5 days, on 3 November 2026. Renew at the reception desk to keep training without a break.",
            content.Text);
        Assert.Contains("Your membership ends in 5 days", content.Html);
        Assert.Contains("Monthly", content.Html);
        Assert.Contains("3 November 2026", content.Html);
        Assert.Contains("Dear Olena", content.Html);
        Assert.Contains("#0ca678", content.Html);
        Assert.DoesNotContain("{{", content.Html);
    }

    [Theory]
    [InlineData(0, "today")]
    [InlineData(1, "tomorrow")]
    [InlineData(2, "in 2 days")]
    public void ExpiryReminder_says_when_in_words(int daysLeft, string when)
    {
        var content = _templates.ExpiryReminder(new ExpiryReminderEmail("Olena", "Monthly", new DateOnly(2026, 11, 3), daysLeft));

        Assert.Contains($"Your membership ends {when}", content.Html);
        Assert.Contains($"ends {when}, on 3 November 2026", content.Text);
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
        var content = _templates.ExpiryReminder(new ExpiryReminderEmail("<b>Olena</b>", "{{EndsOn}} & Co", new DateOnly(2026, 11, 3), 5));

        Assert.Contains("&lt;b&gt;Olena&lt;/b&gt;", content.Html);
        Assert.DoesNotContain("<b>Olena</b>", content.Html);
        Assert.Contains("{{EndsOn}} &amp; Co", content.Html);
    }
}
```

- [ ] **Step 6: Run them and see them fail to compile**

Run: `cd api && dotnet test --project tests/FitnessClub.IntegrationTests --filter-class "FitnessClub.IntegrationTests.Notifications.EmailTemplatesTests"`
Expected: build error, `EmailTemplates` / `ExpiryReminderEmail` not found.

- [ ] **Step 7: Add the Application port and models**

`api/src/FitnessClub.Application/Abstractions/IEmailTemplates.cs`:

```csharp
using FitnessClub.Application.Notifications;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.Application.Abstractions;

public interface IEmailTemplates
{
    NotificationContent ExpiryReminder(ExpiryReminderEmail email);

    NotificationContent Promotion(PromotionEmail email);
}
```

`api/src/FitnessClub.Application/Notifications/ExpiryReminderEmail.cs`:

```csharp
using FitnessClub.Domain.Clients;

namespace FitnessClub.Application.Notifications;

public sealed record ExpiryReminderEmail(string FirstName, string PlanName, DateOnly EndsOn, int DaysLeft)
{
    public static ExpiryReminderEmail For(Client client, Membership membership, DateOnly today) =>
        new(client.Name.FirstName, membership.PlanName, membership.EndsOn, membership.EndsOn.DayNumber - today.DayNumber);
}
```

`api/src/FitnessClub.Application/Notifications/PromotionEmail.cs`:

```csharp
namespace FitnessClub.Application.Notifications;

public sealed record PromotionEmail(string FirstName, int DiscountPercent, DateOnly ValidUntil);
```

- [ ] **Step 8: Add the HTML templates**

`api/src/FitnessClub.Infrastructure/Notifications/Templates/ExpiryReminder.html`:

```html
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="color-scheme" content="light">
<meta name="supported-color-schemes" content="light">
<title>Your membership expires soon</title>
</head>
<body style="margin:0;padding:0;background-color:#f1f3f5;">
<div style="display:none;max-height:0;overflow:hidden;opacity:0;">Your {{PlanName}} membership ends {{When}}. Renew at the reception desk.</div>
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#f1f3f5;">
  <tr>
    <td align="center" style="padding:32px 16px;">
      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:600px;background-color:#ffffff;border-radius:16px;overflow:hidden;font-family:'Inter',system-ui,-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#212529;">
        <tr>
          <td style="background-color:#0ca678;padding:24px 32px;">
            <span style="font-size:20px;font-weight:700;letter-spacing:0.5px;color:#ffffff;">Fitness Club</span>
          </td>
        </tr>
        <tr>
          <td style="padding:40px 32px 8px;">
            <p style="margin:0 0 8px;font-size:13px;font-weight:600;letter-spacing:1px;text-transform:uppercase;color:#0ca678;">Membership reminder</p>
            <h1 style="margin:0 0 16px;font-size:26px;line-height:1.3;font-weight:700;color:#212529;">Your membership ends {{When}}</h1>
            <p style="margin:0;font-size:16px;line-height:1.6;color:#495057;">Dear {{FirstName}}, we would love to keep seeing you at the club. Renew before your membership runs out to keep training without a break.</p>
          </td>
        </tr>
        <tr>
          <td style="padding:24px 32px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#e6fcf5;border:1px solid #96f2d7;border-radius:12px;">
              <tr>
                <td style="padding:20px 24px;">
                  <p style="margin:0 0 4px;font-size:13px;color:#087f5b;">Plan</p>
                  <p style="margin:0 0 16px;font-size:18px;font-weight:700;color:#212529;">{{PlanName}}</p>
                  <p style="margin:0 0 4px;font-size:13px;color:#087f5b;">Valid until</p>
                  <p style="margin:0;font-size:18px;font-weight:700;color:#212529;">{{EndsOn}}</p>
                </td>
              </tr>
            </table>
          </td>
        </tr>
        <tr>
          <td style="padding:0 32px 40px;">
            <table role="presentation" cellpadding="0" cellspacing="0" border="0">
              <tr>
                <td style="background-color:#0ca678;border-radius:8px;padding:12px 24px;font-size:15px;font-weight:600;color:#ffffff;">Renew at the reception desk</td>
              </tr>
            </table>
          </td>
        </tr>
        <tr>
          <td style="padding:24px 32px;border-top:1px solid #e9ecef;font-size:12px;line-height:1.6;color:#868e96;">You are receiving this email because you are a member of Fitness Club.</td>
        </tr>
      </table>
    </td>
  </tr>
</table>
</body>
</html>
```

`api/src/FitnessClub.Infrastructure/Notifications/Templates/Promotion.html`:

```html
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="color-scheme" content="light">
<meta name="supported-color-schemes" content="light">
<title>{{Discount}}% off your next membership</title>
</head>
<body style="margin:0;padding:0;background-color:#f1f3f5;">
<div style="display:none;max-height:0;overflow:hidden;opacity:0;">This month only: {{Discount}}% off any membership, until {{ValidUntil}}.</div>
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#f1f3f5;">
  <tr>
    <td align="center" style="padding:32px 16px;">
      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:600px;background-color:#ffffff;border-radius:16px;overflow:hidden;font-family:'Inter',system-ui,-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#212529;">
        <tr>
          <td style="background-color:#0ca678;padding:24px 32px;">
            <span style="font-size:20px;font-weight:700;letter-spacing:0.5px;color:#ffffff;">Fitness Club</span>
          </td>
        </tr>
        <tr>
          <td align="center" style="padding:40px 32px 8px;">
            <p style="margin:0 0 8px;font-size:13px;font-weight:600;letter-spacing:1px;text-transform:uppercase;color:#0ca678;">Members' offer</p>
            <h1 style="margin:0 0 24px;font-size:26px;line-height:1.3;font-weight:700;color:#212529;">A little something for you, {{FirstName}}</h1>
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="background-color:#e6fcf5;border:2px dashed #20c997;border-radius:16px;">
              <tr>
                <td align="center" style="padding:24px 40px;">
                  <p style="margin:0;font-size:56px;line-height:1;font-weight:800;color:#0ca678;">{{Discount}}% OFF</p>
                  <p style="margin:8px 0 0;font-size:15px;font-weight:600;color:#087f5b;">on any membership</p>
                </td>
              </tr>
            </table>
          </td>
        </tr>
        <tr>
          <td align="center" style="padding:24px 32px 8px;">
            <p style="margin:0;font-size:16px;line-height:1.6;color:#495057;">This month only, renew or upgrade with {{Discount}}% off. Offer valid until <strong style="color:#212529;">{{ValidUntil}}</strong>.</p>
          </td>
        </tr>
        <tr>
          <td align="center" style="padding:24px 32px 40px;">
            <table role="presentation" cellpadding="0" cellspacing="0" border="0">
              <tr>
                <td style="background-color:#0ca678;border-radius:8px;padding:12px 24px;font-size:15px;font-weight:600;color:#ffffff;">Show this email at the reception desk</td>
              </tr>
            </table>
          </td>
        </tr>
        <tr>
          <td style="padding:24px 32px;border-top:1px solid #e9ecef;font-size:12px;line-height:1.6;color:#868e96;">One discount per membership. You are receiving this email because you are a member of Fitness Club.</td>
        </tr>
      </table>
    </td>
  </tr>
</table>
</body>
</html>
```

- [ ] **Step 9: Embed the templates**

In `api/src/FitnessClub.Infrastructure/FitnessClub.Infrastructure.csproj`, add an item group next to the existing ones:

```xml
  <ItemGroup>
    <EmbeddedResource Include="Notifications\Templates\*.html" />
  </ItemGroup>
```

- [ ] **Step 10: Implement `EmailTemplates`**

`api/src/FitnessClub.Infrastructure/Notifications/EmailTemplates.cs`:

```csharp
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Notifications;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.Infrastructure.Notifications;

internal sealed partial class EmailTemplates : IEmailTemplates
{
    private static readonly string ExpiryReminderHtml = Load("ExpiryReminder.html");
    private static readonly string PromotionHtml = Load("Promotion.html");

    public NotificationContent ExpiryReminder(ExpiryReminderEmail email)
    {
        var endsOn = Format(email.EndsOn);
        var when = email.DaysLeft switch
        {
            <= 0 => "today",
            1 => "tomorrow",
            _ => $"in {email.DaysLeft} days",
        };

        return NotificationContent.Create(
            "Your membership expires soon",
            $"Dear {email.FirstName}, your membership '{email.PlanName}' ends {when}, on {endsOn}. Renew at the reception desk to keep training without a break.",
            Fill(ExpiryReminderHtml, new Dictionary<string, string>
            {
                ["FirstName"] = email.FirstName,
                ["PlanName"] = email.PlanName,
                ["EndsOn"] = endsOn,
                ["When"] = when,
            }));
    }

    public NotificationContent Promotion(PromotionEmail email)
    {
        var validUntil = Format(email.ValidUntil);
        var discount = email.DiscountPercent.ToString(CultureInfo.InvariantCulture);

        return NotificationContent.Create(
            $"{discount}% off your next membership",
            $"Dear {email.FirstName}, this month only: get {discount}% off any membership. Show this email at the reception desk. Valid until {validUntil}.",
            Fill(PromotionHtml, new Dictionary<string, string>
            {
                ["FirstName"] = email.FirstName,
                ["Discount"] = discount,
                ["ValidUntil"] = validUntil,
            }));
    }

    private static string Format(DateOnly date) => date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    private static string Fill(string template, IReadOnlyDictionary<string, string> values) =>
        Placeholder().Replace(template, match =>
            values.TryGetValue(match.Groups[1].Value, out var value)
                ? WebUtility.HtmlEncode(value)
                : throw new InvalidOperationException($"The email template has no value for '{match.Groups[1].Value}'."));

    private static string Load(string name)
    {
        var resource = $"{typeof(EmailTemplates).Namespace}.Templates.{name}";
        using var stream = typeof(EmailTemplates).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"The email template '{resource}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex Placeholder();
}
```

`Regex.Replace` is a single pass over the template, so text inserted from a value is never scanned again. That is what makes Review Focus #1 safe.

- [ ] **Step 11: Register the templates**

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, after `services.AddSingleton(provider => provider.GetRequiredService<IOptions<ExpiryNotificationOptions>>().Value);` add:

```csharp
        services.AddSingleton<IEmailTemplates, EmailTemplates>();
```

- [ ] **Step 12: Run the template, domain and architecture tests**

Run:
```bash
cd api
dotnet test --project tests/FitnessClub.IntegrationTests --filter-class "FitnessClub.IntegrationTests.Notifications.EmailTemplatesTests"
dotnet test --project tests/FitnessClub.UnitTests
dotnet test --project tests/FitnessClub.ArchitectureTests
```
Expected: all PASS.

- [ ] **Step 13: Commit**

```bash
git add api/src/FitnessClub.Domain/Notifications/NotificationContent.cs api/src/FitnessClub.Application/Abstractions/IEmailTemplates.cs api/src/FitnessClub.Application/Notifications/ExpiryReminderEmail.cs api/src/FitnessClub.Application/Notifications/PromotionEmail.cs api/src/FitnessClub.Infrastructure/Notifications/EmailTemplates.cs api/src/FitnessClub.Infrastructure/Notifications/Templates api/src/FitnessClub.Infrastructure/FitnessClub.Infrastructure.csproj api/src/FitnessClub.Infrastructure/DependencyInjection.cs api/tests/FitnessClub.UnitTests/Domain/NotificationContentTests.cs api/tests/FitnessClub.IntegrationTests/Notifications/EmailTemplatesTests.cs
git commit -m "feat: branded expiry reminder and promotion email templates"
```

---

### Task 2: Notification subject, HTML body and the new message types

**Files:**
- Modify: `api/src/FitnessClub.Domain/Notifications/Notification.cs`
- Modify: `api/src/FitnessClub.Domain/Notifications/NotificationType.cs`
- Modify: `api/src/FitnessClub.Domain/Notifications/INotificationRepository.cs`
- Modify: `api/src/FitnessClub.Application/Notifications/ExpiryNotificationService.cs`
- Modify: `api/src/FitnessClub.Application/Notifications/NotificationResponse.cs`
- Modify: `api/src/FitnessClub.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs`
- Modify: `api/src/FitnessClub.Infrastructure/Persistence/Repositories/NotificationRepository.cs`
- Create: `api/src/FitnessClub.Infrastructure/Persistence/Migrations/<timestamp>_AddNotificationSubjectAndHtml.cs` (+ Designer, snapshot)
- Modify: `api/tests/FitnessClub.UnitTests/Domain/TestData.cs`
- Modify tests: `api/tests/FitnessClub.UnitTests/Domain/NotificationTests.cs`, `api/tests/FitnessClub.IntegrationTests/Notifications/NotificationSeeder.cs`, `api/tests/FitnessClub.IntegrationTests/Persistence/NotificationRepositoryTests.cs`, `api/tests/FitnessClub.IntegrationTests/Services/ExpiryNotificationServiceTests.cs`, `api/tests/FitnessClub.IntegrationTests/Notifications/NotificationsEndpointsTests.cs`

**Interfaces:**
- Consumes: `NotificationContent`, `IEmailTemplates`, `ExpiryReminderEmail.For` (Task 1).
- Produces:
  - `Notification.Subject : string` and `Notification.HtmlBody : string?`
  - `Notification.MembershipExpiring(Client, Membership, NotificationContent, DateTimeOffset)`
  - `Notification.ExpiryReminder(Client, Membership, NotificationContent, DateTimeOffset)`
  - `Notification.Promotion(Client, NotificationContent, DateTimeOffset)`
  - `NotificationType.ExpiryReminder` and `NotificationType.Promotion`
  - `INotificationRepository.ListForClientAsync(Guid clientId, CancellationToken) : Task<IReadOnlyList<Notification>>`, newest first
  - `NotificationResponse.Subject`
  - `TestData.Content(string? html = "<p>Hello</p>") : NotificationContent`
  - `ExpiryNotificationService` gains the constructor parameter `IEmailTemplates templates` right after `INotificationSender sender`.

- [ ] **Step 1: Add the `TestData.Content` helper**

In `api/tests/FitnessClub.UnitTests/Domain/TestData.cs` add `using FitnessClub.Domain.Notifications;` and:

```csharp
    public static NotificationContent Content(string? html = "<p>Hello</p>") =>
        NotificationContent.Create("Your membership expires soon", "Dear Olena, your membership ends soon.", html);
```

- [ ] **Step 2: Rewrite the domain tests for the new factories**

Replace `api/tests/FitnessClub.UnitTests/Domain/NotificationTests.cs` with the following. The existing rules are kept, and every call passes `TestData.Content()`.

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.UnitTests.Domain;

public class NotificationTests
{
    [Fact]
    public void MembershipExpiring_uses_email_and_the_given_content()
    {
        var client = TestData.Client(email: "olena@example.com");
        var membership = TestData.Buy(client, TestData.Plan(validityDays: 30));

        var notification = Notification.MembershipExpiring(client, membership, TestData.Content(), TestData.Now);

        Assert.Equal(NotificationType.MembershipExpiring, notification.Type);
        Assert.Equal(NotificationChannel.Email, notification.Channel);
        Assert.Equal("olena@example.com", notification.Recipient);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(membership.Id, notification.MembershipId);
        Assert.Equal("Your membership expires soon", notification.Subject);
        Assert.Equal("Dear Olena, your membership ends soon.", notification.Message);
        Assert.Equal("<p>Hello</p>", notification.HtmlBody);
        Assert.Equal(TestData.Now, notification.CreatedAt);
    }

    [Fact]
    public void MembershipExpiring_for_client_without_email_throws()
    {
        var client = TestData.ClientWithMembership(email: null);

        Assert.Throws<DomainException>(() => Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Content(), TestData.Now));
    }

    [Fact]
    public void MembershipExpiring_for_cancelled_membership_throws()
    {
        var client = TestData.ClientWithMembership();
        var membership = client.Memberships.Single();
        client.CancelMembership(membership.Id, TestData.Now);

        Assert.Throws<DomainException>(() => Notification.MembershipExpiring(client, membership, TestData.Content(), TestData.Now));
    }

    [Fact]
    public void MembershipExpiring_after_renewal_throws()
    {
        var client = TestData.ClientWithMembership(validityDays: 30);
        var current = client.Memberships.Single();
        TestData.Buy(client, TestData.Plan(), TestData.Today.AddDays(30));

        Assert.Throws<DomainException>(() => Notification.MembershipExpiring(client, current, TestData.Content(), TestData.Now));
    }

    [Fact]
    public void MembershipExpiring_for_another_clients_membership_throws()
    {
        var owner = TestData.ClientWithMembership();

        Assert.Throws<DomainException>(() => Notification.MembershipExpiring(TestData.Client(), owner.Memberships.Single(), TestData.Content(), TestData.Now));
    }

    [Fact]
    public void ExpiryReminder_is_a_manual_email_without_membership_id()
    {
        var client = TestData.ClientWithMembership();

        var notification = Notification.ExpiryReminder(client, client.Memberships.Single(), TestData.Content(), TestData.Now);

        Assert.Equal(NotificationType.ExpiryReminder, notification.Type);
        Assert.Equal(NotificationChannel.Email, notification.Channel);
        Assert.Equal("olena@example.com", notification.Recipient);
        Assert.Null(notification.MembershipId);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
    }

    [Fact]
    public void ExpiryReminder_for_a_membership_not_active_today_throws()
    {
        var client = TestData.Client("olena@example.com");
        var future = TestData.Buy(client, TestData.Plan(), TestData.Today.AddDays(3));

        var exception = Assert.Throws<DomainException>(() => Notification.ExpiryReminder(client, future, TestData.Content(), TestData.Now));
        Assert.Equal("The client has no active membership to remind about.", exception.Message);
    }

    [Fact]
    public void ExpiryReminder_for_another_clients_membership_throws()
    {
        var owner = TestData.ClientWithMembership();

        Assert.Throws<DomainException>(() =>
            Notification.ExpiryReminder(TestData.Client("other@example.com"), owner.Memberships.Single(), TestData.Content(), TestData.Now));
    }

    [Fact]
    public void ExpiryReminder_for_client_without_email_throws()
    {
        var client = TestData.ClientWithMembership(email: null);

        var exception = Assert.Throws<DomainException>(() => Notification.ExpiryReminder(client, client.Memberships.Single(), TestData.Content(), TestData.Now));
        Assert.Equal("The client has no email address.", exception.Message);
    }

    [Fact]
    public void Promotion_is_a_manual_email_without_membership_id()
    {
        var client = TestData.Client("olena@example.com");

        var notification = Notification.Promotion(client, TestData.Content(), TestData.Now);

        Assert.Equal(NotificationType.Promotion, notification.Type);
        Assert.Equal("olena@example.com", notification.Recipient);
        Assert.Null(notification.MembershipId);
    }

    [Fact]
    public void Promotion_for_client_without_email_throws()
    {
        Assert.Throws<DomainException>(() => Notification.Promotion(TestData.Client(), TestData.Content(), TestData.Now));
    }

    [Fact]
    public void Content_without_html_leaves_html_body_empty()
    {
        var notification = Notification.Promotion(TestData.Client("olena@example.com"), TestData.Content(html: null), TestData.Now);

        Assert.Null(notification.HtmlBody);
    }

    [Fact]
    public void MarkSent_records_time_and_cannot_be_repeated()
    {
        var client = TestData.ClientWithMembership();
        var notification = Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Content(), TestData.Now);

        notification.MarkSent(TestData.Now.AddMinutes(1));

        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal(TestData.Now.AddMinutes(1), notification.SentAt);
        Assert.Throws<DomainException>(() => notification.MarkFailed("late failure"));
    }

    [Fact]
    public void MarkFailed_truncates_long_reason()
    {
        var client = TestData.ClientWithMembership();
        var notification = Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Content(), TestData.Now);

        notification.MarkFailed(new string('x', Notification.FailureReasonMaxLength + 50));

        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Equal(Notification.FailureReasonMaxLength, notification.FailureReason!.Length);
    }

    [Fact]
    public void Retry_returns_failed_notification_to_pending()
    {
        var client = TestData.ClientWithMembership();
        var notification = Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Content(), TestData.Now);
        Assert.Throws<DomainException>(() => notification.Retry());
        notification.MarkFailed("Mailbox unavailable.");

        notification.Retry();

        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Null(notification.FailureReason);
    }
}
```

- [ ] **Step 3: Run them and see them fail to compile**

Run: `cd api && dotnet test --project tests/FitnessClub.UnitTests --filter-class "FitnessClub.UnitTests.Domain.NotificationTests"`
Expected: build errors (no `ExpiryReminder`/`Promotion`/`Subject`, wrong `MembershipExpiring` arity).

- [ ] **Step 4: Implement the domain changes**

`api/src/FitnessClub.Domain/Notifications/NotificationType.cs`:

```csharp
namespace FitnessClub.Domain.Notifications;

public enum NotificationType
{
    MembershipExpiring,
    ExpiryReminder,
    Promotion,
}
```

In `api/src/FitnessClub.Domain/Notifications/Notification.cs`:

- Remove `using System.Globalization;`.
- Add the properties `public string Subject { get; private set; } = null!;` (after `Recipient`) and `public string? HtmlBody { get; private set; }` (after `Message`).
- Replace the `MembershipExpiring` method with:

```csharp
    public static Notification MembershipExpiring(Client client, Membership membership, NotificationContent content, DateTimeOffset now)
    {
        if (!client.Owns(membership))
            throw new DomainException("The membership does not belong to the client.");

        if (!client.NeedsExpiryNotice(membership, now.ToDateOnly()))
            throw new DomainException("The membership does not need an expiry notice.");

        return Email(client, membership.Id, NotificationType.MembershipExpiring, content, now);
    }

    public static Notification ExpiryReminder(Client client, Membership membership, NotificationContent content, DateTimeOffset now)
    {
        if (!client.Owns(membership))
            throw new DomainException("The membership does not belong to the client.");

        if (!membership.IsActiveOn(now.ToDateOnly()))
            throw new DomainException("The client has no active membership to remind about.");

        return Email(client, null, NotificationType.ExpiryReminder, content, now);
    }

    public static Notification Promotion(Client client, NotificationContent content, DateTimeOffset now) =>
        Email(client, null, NotificationType.Promotion, content, now);

    private static Notification Email(Client client, Guid? membershipId, NotificationType type, NotificationContent content, DateTimeOffset now)
    {
        var email = client.Email ?? throw new DomainException("The client has no email address.");
        return new Notification
        {
            ClientId = client.Id,
            MembershipId = membershipId,
            Type = type,
            Channel = NotificationChannel.Email,
            Recipient = email.Value,
            Subject = content.Subject,
            Message = content.Text,
            HtmlBody = content.Html,
            Status = NotificationStatus.Pending,
            CreatedAt = now,
        };
    }
```

In `api/src/FitnessClub.Domain/Notifications/INotificationRepository.cs` add:

```csharp
    Task<IReadOnlyList<Notification>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken);
```

- [ ] **Step 5: Run the domain tests**

Run: `cd api && dotnet test --project tests/FitnessClub.UnitTests`
Expected: PASS. The rest of the solution doesn't compile yet; the next steps fix that.

- [ ] **Step 6: Use the template in `ExpiryNotificationService`**

In `api/src/FitnessClub.Application/Notifications/ExpiryNotificationService.cs`, add `IEmailTemplates templates,` to the primary constructor after `INotificationSender sender,`, and replace the `Add` line:

```csharp
                notifications.Add(Notification.MembershipExpiring(
                    client, membership, templates.ExpiryReminder(ExpiryReminderEmail.For(client, membership, today)), now));
```

`NotificationResponse` gains `Subject`. In `api/src/FitnessClub.Application/Notifications/NotificationResponse.cs`, insert `string Subject,` after `string Recipient,` in the record and `notification.Subject,` after `notification.Recipient,` in `FromEntity`.

- [ ] **Step 7: Persistence**

In `api/src/FitnessClub.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs`, after the `Message` line:

```csharp
        builder.Property(n => n.Subject).HasMaxLength(NotificationContent.SubjectMaxLength).IsRequired();
        builder.Property(n => n.HtmlBody);
        builder.HasIndex(n => new { n.ClientId, n.CreatedAt });
```

In `api/src/FitnessClub.Infrastructure/Persistence/Repositories/NotificationRepository.cs`:

```csharp
    public async Task<IReadOnlyList<Notification>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        await Set.Where(n => n.ClientId == clientId).OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);
```

- [ ] **Step 8: Fix the remaining test call sites**

`api/tests/FitnessClub.IntegrationTests/Notifications/NotificationSeeder.cs`:
- Add `using FitnessClub.UnitTests.Domain;`.
- Change the factory call to `Notification.MembershipExpiring(client, membership, TestData.Content(), now)`.
- Add a client helper for Task 5 (`NewClient` gains a `withEmail` parameter):

```csharp
    public static async Task<Client> ClientWithMembershipAsync(this FitnessClubApiFactory factory, bool withEmail = true)
    {
        var (client, _, plan) = NewClient(TimeProvider.System.GetLocalNow(), withEmail);

        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IMembershipPlanRepository>().Add(plan);
        scope.ServiceProvider.GetRequiredService<IClientRepository>().Add(client);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return client;
    }
```

and change `NewClient` to:

```csharp
    private static (Client Client, Membership Membership, MembershipPlan Plan) NewClient(DateTimeOffset now, bool withEmail = true)
    {
        var plan = MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(800m), ValidityDays, null);
        var client = Client.Register(
            PersonName.Create("Olena", "Shevchenko", null),
            new DateOnly(1995, 3, 14),
            PhoneNumber.Create($"+380{Random.Shared.NextInt64(100_000_000, 999_999_999)}"),
            withEmail ? EmailAddress.Create($"{Guid.NewGuid():N}@example.com") : null,
            now);
        var payment = client.PurchaseMembership(plan, DateOnly.FromDateTime(now.DateTime), PaymentMethod.Cash, now);
        return (client, client.Memberships.Single(m => m.Id == payment.MembershipId), plan);
    }
```

`api/tests/FitnessClub.IntegrationTests/Persistence/NotificationRepositoryTests.cs`:
- Add `using FitnessClub.UnitTests.Domain;`.
- Change the factory call to `Notification.MembershipExpiring(client, membership, TestData.Content(), Now)`.
- After loading, add:

```csharp
        Assert.Equal("Your membership expires soon", loaded.Subject);
        Assert.Equal("<p>Hello</p>", loaded.HtmlBody);
```

Then add the client-listing test:

```csharp
    [Fact]
    public async Task ListForClientAsync_returns_only_that_clients_notifications_newest_first()
    {
        var olena = Client.Register(PersonName.Create("Olena", "Shevchenko", null), new DateOnly(1995, 3, 14), PhoneNumber.Create(UniquePhone()), EmailAddress.Create("olena.list@example.com"), Now);
        var ivan = Client.Register(PersonName.Create("Ivan", "Koval", null), new DateOnly(1990, 1, 2), PhoneNumber.Create(UniquePhone()), EmailAddress.Create("ivan.list@example.com"), Now);
        var older = Notification.Promotion(olena, TestData.Content(), Now);
        var newer = Notification.Promotion(olena, TestData.Content(html: null), Now.AddHours(1));
        var other = Notification.Promotion(ivan, TestData.Content(), Now);
        await SaveAsync<IClientRepository>(clients => { clients.Add(olena); clients.Add(ivan); });
        await SaveAsync<INotificationRepository>(notifications => { notifications.Add(older); notifications.Add(newer); notifications.Add(other); });

        var list = await ReadAsync<INotificationRepository, IReadOnlyList<Notification>>(n => n.ListForClientAsync(olena.Id, Ct));

        Assert.Equal([newer.Id, older.Id], list.Select(n => n.Id));
        Assert.Null(list[0].HtmlBody);
    }
```

`api/tests/FitnessClub.IntegrationTests/Services/ExpiryNotificationServiceTests.cs`:
- Add `using FitnessClub.Application.Abstractions;`.
- Change `Service`:

```csharp
    private ExpiryNotificationService Service(int expiryNoticeDays = 7) =>
        new(Get<IClientRepository>(), Get<INotificationRepository>(), _sender, Get<IEmailTemplates>(), UnitOfWork, _time,
            new ExpiryNotificationOptions { ExpiryNoticeDays = expiryNoticeDays });
```

- In `CreateDueNoticesAsync_creates_a_pending_notice_for_a_membership_ending_within_the_window`, replace `Assert.Contains("2026-10-09", notice.Message);` with:

```csharp
        Assert.Equal("Your membership expires soon", notice.Subject);
        Assert.Contains("ends in 4 days, on 9 October 2026", notice.Message);
        Assert.Contains("Your membership ends in 4 days", notice.HtmlBody);
```

`api/tests/FitnessClub.IntegrationTests/Notifications/NotificationsEndpointsTests.cs`: in `List_as_admin_returns_notifications`, add `Assert.Equal(seeded.Subject, notification.Subject);`.

(`FakeNotificationSender` and `SendPendingAsync` still use the old sender signature. Task 3 changes both.)

- [ ] **Step 9: Add the migration**

Run from `api/`:

```bash
dotnet build
ConnectionStrings__FitnessClub="Server=localhost;TrustServerCertificate=True" dotnet ef migrations add AddNotificationSubjectAndHtml --no-build --project src/FitnessClub.Infrastructure --startup-project src/FitnessClub.Api --output-dir Persistence/Migrations
```

Then edit the generated files:
- Remove `// <auto-generated />` and `/// <inheritdoc />` from the migration and its Designer.
- Make both partial classes `internal`.
- In `Up`, set the `Subject` default so existing rows get a real subject:

```csharp
            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Your membership expires soon");
```

`HtmlBody` is `nvarchar(max)`, nullable. `Down` drops both columns and the `(ClientId, CreatedAt)` index; EF generates that already.

Run: `ConnectionStrings__FitnessClub="Server=localhost;TrustServerCertificate=True" dotnet ef migrations has-pending-model-changes --project src/FitnessClub.Infrastructure --startup-project src/FitnessClub.Api`
Expected: "No changes have been made to the model since the last migration."

- [ ] **Step 10: Run everything except the sender tests**

The sender signature doesn't change until Task 3, so the solution builds cleanly here.

Run: `cd api && dotnet build && dotnet test`
Expected: PASS (the Mailpit SMTP tests still run here; Task 3 deletes them).

- [ ] **Step 11: Commit**

```bash
git add api/src api/tests
git commit -m "feat: notification subject, html body, reminder and promotion types"
```

---

### Task 3: SendGrid sender replaces SMTP

**Files:**
- Modify: `api/src/FitnessClub.Application/Abstractions/INotificationSender.cs`
- Modify: `api/src/FitnessClub.Application/Notifications/ExpiryNotificationService.cs` (the `SendPendingAsync` call)
- Modify: `api/src/FitnessClub.Infrastructure/Notifications/LoggingNotificationSender.cs`
- Create: `api/src/FitnessClub.Infrastructure/Notifications/SendGridOptions.cs`
- Create: `api/src/FitnessClub.Infrastructure/Notifications/SendGridNotificationSender.cs`
- Delete: `api/src/FitnessClub.Infrastructure/Notifications/SmtpNotificationSender.cs`, `SmtpOptions.cs`
- Modify: `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`
- Modify: `api/src/FitnessClub.Infrastructure/FitnessClub.Infrastructure.csproj` (remove MailKit)
- Modify: `api/Directory.Packages.props` (remove MailKit)
- Modify: `api/src/FitnessClub.Api/appsettings.json`, `api/src/FitnessClub.Api/appsettings.Development.json`
- Delete: `api/tests/FitnessClub.IntegrationTests/Notifications/MailpitFixture.cs`, `SmtpNotificationSenderTests.cs`
- Create: `api/tests/FitnessClub.IntegrationTests/Notifications/SendGridNotificationSenderTests.cs`
- Modify: `api/tests/FitnessClub.IntegrationTests/Services/FakeNotificationSender.cs`, `ExpiryNotificationServiceTests.cs`, `api/tests/FitnessClub.IntegrationTests/HostConfigurationTests.cs`

**Interfaces:**
- Consumes: `Notification.Subject`, `.Message`, `.HtmlBody`, `.Recipient`, `.Channel` (Task 2).
- Produces:
  - `INotificationSender.SendAsync(Notification notification, CancellationToken cancellationToken) : Task`
  - `SendGridNotificationSender(HttpClient, IOptions<SendGridOptions>)` with `static Uri BaseAddress` and `static TimeSpan Timeout`
  - `FakeNotificationSender.Sent : IReadOnlyList<Notification>`

- [ ] **Step 1: Write the failing sender tests**

`api/tests/FitnessClub.IntegrationTests/Notifications/SendGridNotificationSenderTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using FitnessClub.Domain.Notifications;
using FitnessClub.Infrastructure.Notifications;
using FitnessClub.UnitTests.Domain;
using Microsoft.Extensions.Options;

namespace FitnessClub.IntegrationTests.Notifications;

public class SendGridNotificationSenderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly StubHandler _handler = new();

    private SendGridNotificationSender Sender() =>
        new(
            new HttpClient(_handler) { BaseAddress = SendGridNotificationSender.BaseAddress },
            Options.Create(new SendGridOptions { ApiKey = "SG.test-key", FromAddress = "club@example.com", FromName = "Fitness Club" }));

    private static Notification Promotion(string? html = "<p>10% OFF</p>") =>
        Notification.Promotion(TestData.Client("olena@example.com"), NotificationContent.Create("10% off", "Dear Olena, 10% off.", html), TestData.Now);

    [Fact]
    public async Task SendAsync_posts_the_email_to_sendgrid_with_text_and_html()
    {
        await Sender().SendAsync(Promotion(), Ct);

        Assert.Equal(HttpMethod.Post, _handler.Request!.Method);
        Assert.Equal("https://api.sendgrid.com/v3/mail/send", _handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", _handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("SG.test-key", _handler.Request.Headers.Authorization.Parameter);
        using var body = JsonDocument.Parse(_handler.Body!);
        var root = body.RootElement;
        Assert.Equal("olena@example.com", root.GetProperty("personalizations")[0].GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("club@example.com", root.GetProperty("from").GetProperty("email").GetString());
        Assert.Equal("Fitness Club", root.GetProperty("from").GetProperty("name").GetString());
        Assert.Equal("10% off", root.GetProperty("subject").GetString());
        var content = root.GetProperty("content");
        Assert.Equal(2, content.GetArrayLength());
        Assert.Equal("text/plain", content[0].GetProperty("type").GetString());
        Assert.Equal("Dear Olena, 10% off.", content[0].GetProperty("value").GetString());
        Assert.Equal("text/html", content[1].GetProperty("type").GetString());
        Assert.Equal("<p>10% OFF</p>", content[1].GetProperty("value").GetString());
    }

    [Fact]
    public async Task SendAsync_without_html_sends_only_text()
    {
        await Sender().SendAsync(Promotion(html: null), Ct);

        using var body = JsonDocument.Parse(_handler.Body!);
        var content = Assert.Single(body.RootElement.GetProperty("content").EnumerateArray());
        Assert.Equal("text/plain", content.GetProperty("type").GetString());
    }

    [Fact]
    public async Task SendAsync_throws_with_sendgrids_reason_when_rejected()
    {
        _handler.Response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"errors":[{"message":"The provided authorization grant is invalid, expired, or revoked"}]}""", Encoding.UTF8, "application/json"),
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Sender().SendAsync(Promotion(), Ct));

        Assert.Contains("401", exception.Message);
        Assert.Contains("authorization grant is invalid", exception.Message);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.Accepted);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return Response;
        }
    }
}
```

- [ ] **Step 2: Run them and see them fail to compile**

Run: `cd api && dotnet test --project tests/FitnessClub.IntegrationTests --filter-class "FitnessClub.IntegrationTests.Notifications.SendGridNotificationSenderTests"`
Expected: build error, `SendGridNotificationSender` not found.

- [ ] **Step 3: Change the sender port and its callers**

`api/src/FitnessClub.Application/Abstractions/INotificationSender.cs`:

```csharp
using FitnessClub.Domain.Notifications;

namespace FitnessClub.Application.Abstractions;

public interface INotificationSender
{
    Task SendAsync(Notification notification, CancellationToken cancellationToken);
}
```

In `ExpiryNotificationService.SendPendingAsync`, replace the send line with `await sender.SendAsync(notification, cancellationToken);`.

`api/src/FitnessClub.Infrastructure/Notifications/LoggingNotificationSender.cs`:

```csharp
using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace FitnessClub.Infrastructure.Notifications;

internal sealed partial class LoggingNotificationSender(ILogger<LoggingNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        LogNotification(notification.Channel, notification.Recipient, notification.Subject, notification.Message);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sending {Channel} notification to {Recipient}: {Subject}. {Message}")]
    private partial void LogNotification(NotificationChannel channel, string recipient, string subject, string message);
}
```

`api/tests/FitnessClub.IntegrationTests/Services/FakeNotificationSender.cs`:

```csharp
using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.IntegrationTests.Services;

internal sealed class FakeNotificationSender : INotificationSender
{
    public const string FailureMessage = "Mailbox unavailable.";

    private readonly List<Notification> _sent = [];

    public IReadOnlyList<Notification> Sent => _sent;

    public HashSet<string> FailingRecipients { get; } = [];

    public Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (FailingRecipients.Contains(notification.Recipient))
            throw new InvalidOperationException(FailureMessage);

        _sent.Add(notification);
        return Task.CompletedTask;
    }
}
```

In `ExpiryNotificationServiceTests.SendPendingAsync_sends_and_marks_notices_sent`, replace the `_sender.Sent` assertion with:

```csharp
        Assert.Equal(notice.Id, Assert.Single(_sender.Sent).Id);
```

- [ ] **Step 4: Implement SendGrid**

`api/src/FitnessClub.Infrastructure/Notifications/SendGridOptions.cs`:

```csharp
namespace FitnessClub.Infrastructure.Notifications;

internal sealed class SendGridOptions
{
    public const string SectionName = "SendGrid";

    public string? ApiKey { get; set; }

    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "Fitness Club";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
```

`api/src/FitnessClub.Infrastructure/Notifications/SendGridNotificationSender.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.Notifications;
using Microsoft.Extensions.Options;

namespace FitnessClub.Infrastructure.Notifications;

internal sealed class SendGridNotificationSender(HttpClient http, IOptions<SendGridOptions> options) : INotificationSender
{
    public static readonly Uri BaseAddress = new("https://api.sendgrid.com/");

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (notification.Channel != NotificationChannel.Email)
            throw new InvalidOperationException($"{notification.Channel} notifications can't be sent: only email is supported.");

        var sendGrid = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/mail/send") { Content = JsonContent.Create(Mail(notification, sendGrid)) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sendGrid.ApiKey);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var reason = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"SendGrid rejected the email ({(int)response.StatusCode} {response.StatusCode}): {reason}");
        }
    }

    private static SendGridMail Mail(Notification notification, SendGridOptions sendGrid) =>
        new(
            [new SendGridPersonalization([new SendGridAddress(notification.Recipient, null)])],
            new SendGridAddress(sendGrid.FromAddress!, sendGrid.FromName),
            notification.Subject,
            notification.HtmlBody is null
                ? [new SendGridContent("text/plain", notification.Message)]
                : [new SendGridContent("text/plain", notification.Message), new SendGridContent("text/html", notification.HtmlBody)]);

    private sealed record SendGridMail(
        [property: JsonPropertyName("personalizations")] IReadOnlyList<SendGridPersonalization> Personalizations,
        [property: JsonPropertyName("from")] SendGridAddress From,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("content")] IReadOnlyList<SendGridContent> Content);

    private sealed record SendGridPersonalization([property: JsonPropertyName("to")] IReadOnlyList<SendGridAddress> To);

    private sealed record SendGridAddress(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name);

    private sealed record SendGridContent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("value")] string Value);
}
```

`text/plain` must come before `text/html` in `content`: SendGrid rejects the other order.

- [ ] **Step 5: Replace SMTP in DI and config**

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, replace the whole `SmtpOptions` block, from `services.AddOptions<SmtpOptions>()` through `services.AddScoped<INotificationSender, SmtpNotificationSender>();`, with:

```csharp
        services.AddOptions<SendGridOptions>()
            .Bind(configuration.GetSection(SendGridOptions.SectionName))
            .Validate(
                sendGrid => !sendGrid.IsConfigured || !string.IsNullOrWhiteSpace(sendGrid.FromAddress),
                $"{SendGridOptions.SectionName}:FromAddress is required when {SendGridOptions.SectionName}:ApiKey is set.")
            .ValidateOnStart();
        if (string.IsNullOrWhiteSpace(configuration[$"{SendGridOptions.SectionName}:ApiKey"]))
            services.AddScoped<INotificationSender, LoggingNotificationSender>();
        else
            services.AddHttpClient<INotificationSender, SendGridNotificationSender>(http =>
            {
                http.BaseAddress = SendGridNotificationSender.BaseAddress;
                http.Timeout = SendGridNotificationSender.Timeout;
            });
```

Then:
- Delete `SmtpNotificationSender.cs` and `SmtpOptions.cs`.
- Remove `<PackageReference Include="MailKit" />` from the Infrastructure csproj and `<PackageVersion Include="MailKit" Version="4.18.1" />` from `api/Directory.Packages.props`.
- Delete `tests/FitnessClub.IntegrationTests/Notifications/MailpitFixture.cs` and `SmtpNotificationSenderTests.cs`.

`api/src/FitnessClub.Api/appsettings.json`: replace the `"Smtp": { ... }` object with

```json
  "SendGrid": {
    "ApiKey": "",
    "FromAddress": "",
    "FromName": "Fitness Club"
  }
```

`api/src/FitnessClub.Api/appsettings.Development.json`: replace the uncommitted `"Smtp": { ... }` block with the same `SendGrid` block.

- [ ] **Step 6: Update the host configuration tests**

In `api/tests/FitnessClub.IntegrationTests/HostConfigurationTests.cs`, replace the three SMTP tests with:

```csharp
    [Fact]
    public void Notifications_are_only_logged_without_a_sendgrid_api_key()
    {
        using var scope = factory.Services.CreateScope();

        Assert.IsType<LoggingNotificationSender>(scope.ServiceProvider.GetRequiredService<INotificationSender>());
    }

    [Fact]
    public void Notifications_go_through_sendgrid_when_an_api_key_is_set()
    {
        using var baseFactory = new FitnessClubApiFactory();
        using var sendGrid = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("SendGrid:ApiKey", "SG.test-key");
            builder.UseSetting("SendGrid:FromAddress", "club@example.com");
        });
        using var scope = sendGrid.Services.CreateScope();

        Assert.IsType<SendGridNotificationSender>(scope.ServiceProvider.GetRequiredService<INotificationSender>());
    }

    [Fact]
    public void Sendgrid_api_key_without_from_address_fails_at_startup()
    {
        using var baseFactory = new FitnessClubApiFactory();
        using var invalid = baseFactory.WithWebHostBuilder(builder => builder.UseSetting("SendGrid:ApiKey", "SG.test-key"));

        var exception = Assert.Throws<OptionsValidationException>(() => invalid.CreateClient());
        Assert.Contains("SendGrid:FromAddress", exception.Message);
    }
```

- [ ] **Step 7: Run all API tests**

Run: `cd api && dotnet build && dotnet test`
Expected: all PASS, with no Mailpit container started. `grep -rn "Smtp\|MailKit\|Mailpit" api/src api/tests --include='*.cs' --include='*.csproj' --include='*.json' --include='*.props'` prints nothing.

- [ ] **Step 8: Commit**

```bash
git add -A api
git commit -m "feat: send email through Twilio SendGrid instead of SMTP"
```

---

### Task 4: Client message service

**Files:**
- Create: `api/src/FitnessClub.Application/ClientMessages/IClientMessageService.cs`
- Create: `api/src/FitnessClub.Application/ClientMessages/ClientMessageService.cs`
- Create: `api/src/FitnessClub.Application/ClientMessages/ClientMessageRequest.cs`
- Create: `api/src/FitnessClub.Application/ClientMessages/ClientMessagePreviewResponse.cs`
- Create: `api/src/FitnessClub.Application/ClientMessages/MessageTemplate.cs`
- Modify: `api/src/FitnessClub.Application/DependencyInjection.cs`
- Test: `api/tests/FitnessClub.IntegrationTests/Services/ClientMessageServiceTests.cs`

**Interfaces:**
- Consumes:
  - `IEmailTemplates`, `ExpiryReminderEmail.For`, `PromotionEmail` (Task 1)
  - `Notification.ExpiryReminder/Promotion`, `ListForClientAsync`, `NotificationResponse` (Task 2)
  - `INotificationSender.SendAsync(Notification, ct)` and `FakeNotificationSender` (Task 3)
- Produces:
  - `IClientMessageService`:
    - `PreviewAsync(Guid clientId, ClientMessageRequest request, CancellationToken) : Task<ClientMessagePreviewResponse>`
    - `SendAsync(Guid clientId, ClientMessageRequest request, CancellationToken) : Task<NotificationResponse>`
    - `ListAsync(Guid clientId, CancellationToken) : Task<IReadOnlyList<NotificationResponse>>`
  - `ClientMessageRequest { string? Template }`: `[Required]`, regex `ExpiryReminder|Promotion`, case-insensitive
  - `ClientMessagePreviewResponse(string Template, string Recipient, string Subject, string Html)`
  - `ClientMessageService.PromotionDiscountPercent = 10`

- [ ] **Step 1: Write the failing service tests**

`api/tests/FitnessClub.IntegrationTests/Services/ClientMessageServiceTests.cs`:

```csharp
using FitnessClub.Application.Abstractions;
using FitnessClub.Application.ClientMessages;
using FitnessClub.Application.Common;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Notifications;
using FitnessClub.IntegrationTests.Infrastructure;
using FitnessClub.UnitTests.Domain;

namespace FitnessClub.IntegrationTests.Services;

public class ClientMessageServiceTests(FitnessClubApiFactory factory) : ServiceTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeNotificationSender _sender = new();
    private readonly FakeTimeProvider _time = new(TestData.Now);

    private ClientMessageService Service() =>
        new(Get<IClientRepository>(), Get<INotificationRepository>(), _sender, Get<IEmailTemplates>(), UnitOfWork, _time);

    private static ClientMessageRequest Request(string template) => new() { Template = template };

    private Task<IReadOnlyList<Notification>> AllNotificationsAsync() => Get<INotificationRepository>().ListAsync(null, Ct);

    private async Task<Client> SeedClientAsync(string? email = "olena@example.com", bool withMembership = true)
    {
        var client = TestData.Client(email);
        if (withMembership)
            TestData.Buy(client, await SeedPlanAsync());
        Get<IClientRepository>().Add(client);
        await SeedAsync();
        return client;
    }

    [Fact]
    public async Task PreviewAsync_renders_the_reminder_for_the_active_membership_and_saves_nothing()
    {
        var client = await SeedClientAsync();
        var membership = client.Memberships.Single();

        var preview = await Service().PreviewAsync(client.Id, Request("ExpiryReminder"), Ct);

        Assert.Equal("ExpiryReminder", preview.Template);
        Assert.Equal("olena@example.com", preview.Recipient);
        Assert.Equal("Your membership expires soon", preview.Subject);
        Assert.Contains(membership.PlanName, preview.Html);
        Assert.Contains("3 November 2026", preview.Html);
        Assert.Equal(0, UnitOfWork.SaveCount);
        Assert.Empty(await AllNotificationsAsync());
    }

    [Fact]
    public async Task PreviewAsync_accepts_the_template_name_in_any_case()
    {
        var client = await SeedClientAsync();

        var preview = await Service().PreviewAsync(client.Id, Request("promotion"), Ct);

        Assert.Equal("Promotion", preview.Template);
    }

    [Fact]
    public async Task PreviewAsync_renders_the_promotion_valid_until_the_end_of_the_month()
    {
        var client = await SeedClientAsync(withMembership: false);

        var preview = await Service().PreviewAsync(client.Id, Request("Promotion"), Ct);

        Assert.Equal("10% off your next membership", preview.Subject);
        Assert.Contains("10% OFF", preview.Html);
        Assert.Contains("31 October 2026", preview.Html);
    }

    [Fact]
    public async Task PreviewAsync_reminder_without_an_active_membership_throws_domain_exception()
    {
        var client = await SeedClientAsync(withMembership: false);

        var exception = await Assert.ThrowsAsync<DomainException>(() => Service().PreviewAsync(client.Id, Request("ExpiryReminder"), Ct));
        Assert.Equal("The client has no active membership to remind about.", exception.Message);
    }

    [Fact]
    public async Task PreviewAsync_for_a_client_without_email_throws_domain_exception()
    {
        var client = await SeedClientAsync(email: null);

        var exception = await Assert.ThrowsAsync<DomainException>(() => Service().PreviewAsync(client.Id, Request("Promotion"), Ct));
        Assert.Equal("The client has no email address.", exception.Message);
    }

    [Fact]
    public async Task PreviewAsync_for_a_missing_client_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Service().PreviewAsync(Guid.NewGuid(), Request("Promotion"), Ct));
    }

    [Fact]
    public async Task SendAsync_stores_the_message_sends_it_and_marks_it_sent()
    {
        var client = await SeedClientAsync();
        _time.Now = TestData.Now.AddMinutes(5);

        var result = await Service().SendAsync(client.Id, Request("Promotion"), Ct);

        Assert.Equal("Sent", result.Status);
        Assert.Equal("Promotion", result.Type);
        Assert.Equal("10% off your next membership", result.Subject);
        Assert.Equal(TestData.Now.AddMinutes(5), result.SentAt);
        var sent = Assert.Single(_sender.Sent);
        Assert.Equal(result.Id, sent.Id);
        Assert.Contains("10% OFF", sent.HtmlBody);
        var stored = Assert.Single(await AllNotificationsAsync());
        Assert.Equal(NotificationStatus.Sent, stored.Status);
        Assert.Null(stored.MembershipId);
        Assert.Equal(2, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task SendAsync_records_the_failure_reason_when_sending_fails()
    {
        var client = await SeedClientAsync();
        _sender.FailingRecipients.Add("olena@example.com");

        var result = await Service().SendAsync(client.Id, Request("ExpiryReminder"), Ct);

        Assert.Equal("Failed", result.Status);
        Assert.Equal(FakeNotificationSender.FailureMessage, result.FailureReason);
        Assert.Null(result.SentAt);
        Assert.Equal(NotificationStatus.Failed, Assert.Single(await AllNotificationsAsync()).Status);
    }

    [Theory]
    [InlineData(2026, 12, 31, "31 December 2026")]
    [InlineData(2028, 2, 10, "29 February 2028")]
    [InlineData(2027, 2, 1, "28 February 2027")]
    public async Task SendAsync_promotion_is_valid_until_the_last_day_of_the_month(int year, int month, int day, string validUntil)
    {
        var client = await SeedClientAsync(withMembership: false);
        _time.Now = new DateTimeOffset(year, month, day, 20, 0, 0, TimeSpan.Zero);

        await Service().SendAsync(client.Id, Request("Promotion"), Ct);

        Assert.Contains(validUntil, Assert.Single(_sender.Sent).HtmlBody);
    }

    [Fact]
    public async Task SendAsync_for_a_client_without_email_saves_nothing()
    {
        var client = await SeedClientAsync(email: null);

        await Assert.ThrowsAsync<DomainException>(() => Service().SendAsync(client.Id, Request("Promotion"), Ct));
        Assert.Equal(0, UnitOfWork.SaveCount);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task ListAsync_returns_only_the_clients_messages_newest_first()
    {
        var olena = await SeedClientAsync();
        var other = await SeedClientAsync(email: "ivan@example.com", withMembership: false);
        var promotion = await Service().SendAsync(olena.Id, Request("Promotion"), Ct);
        _time.Now = TestData.Now.AddHours(1);
        var reminder = await Service().SendAsync(olena.Id, Request("ExpiryReminder"), Ct);
        await Service().SendAsync(other.Id, Request("Promotion"), Ct);

        var list = await Service().ListAsync(olena.Id, Ct);

        Assert.Equal([reminder.Id, promotion.Id], list.Select(n => n.Id));
    }

    [Fact]
    public async Task ListAsync_for_a_missing_client_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Service().ListAsync(Guid.NewGuid(), Ct));
    }
}
```

- [ ] **Step 2: Run them and see them fail to compile**

Run: `cd api && dotnet test --project tests/FitnessClub.IntegrationTests --filter-class "FitnessClub.IntegrationTests.Services.ClientMessageServiceTests"`
Expected: build error, namespace `FitnessClub.Application.ClientMessages` not found.

- [ ] **Step 3: Implement the request/response types**

`MessageTemplate.cs`:

```csharp
namespace FitnessClub.Application.ClientMessages;

public enum MessageTemplate
{
    ExpiryReminder,
    Promotion,
}
```

`ClientMessageRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace FitnessClub.Application.ClientMessages;

public sealed record ClientMessageRequest
{
    [Required(ErrorMessage = "Template is required.")]
    [RegularExpression("^(?i:expiryreminder|promotion)$", ErrorMessage = "Template must be ExpiryReminder or Promotion.")]
    public string? Template { get; init; }
}
```

`ClientMessagePreviewResponse.cs`:

```csharp
namespace FitnessClub.Application.ClientMessages;

public sealed record ClientMessagePreviewResponse(string Template, string Recipient, string Subject, string Html);
```

`IClientMessageService.cs`:

```csharp
using FitnessClub.Application.Notifications;

namespace FitnessClub.Application.ClientMessages;

public interface IClientMessageService
{
    Task<ClientMessagePreviewResponse> PreviewAsync(Guid clientId, ClientMessageRequest request, CancellationToken cancellationToken);

    Task<NotificationResponse> SendAsync(Guid clientId, ClientMessageRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationResponse>> ListAsync(Guid clientId, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement the service**

`ClientMessageService.cs`:

```csharp
using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Application.Notifications;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.Application.ClientMessages;

internal sealed class ClientMessageService(
    IClientRepository clients,
    INotificationRepository notifications,
    INotificationSender sender,
    IEmailTemplates templates,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IClientMessageService
{
    public const int PromotionDiscountPercent = 10;

    public async Task<ClientMessagePreviewResponse> PreviewAsync(Guid clientId, ClientMessageRequest request, CancellationToken cancellationToken)
    {
        var template = Parse(request);
        var notification = Compose(await GetClientAsync(clientId, cancellationToken), template, timeProvider.GetLocalNow());
        return new ClientMessagePreviewResponse(template.ToString(), notification.Recipient, notification.Subject, notification.HtmlBody!);
    }

    public async Task<NotificationResponse> SendAsync(Guid clientId, ClientMessageRequest request, CancellationToken cancellationToken)
    {
        var notification = Compose(await GetClientAsync(clientId, cancellationToken), Parse(request), timeProvider.GetLocalNow());
        notifications.Add(notification);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await sender.SendAsync(notification, cancellationToken);
            notification.MarkSent(timeProvider.GetLocalNow());
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            notification.MarkFailed(string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return NotificationResponse.FromEntity(notification);
    }

    public async Task<IReadOnlyList<NotificationResponse>> ListAsync(Guid clientId, CancellationToken cancellationToken)
    {
        await GetClientAsync(clientId, cancellationToken);
        var list = await notifications.ListForClientAsync(clientId, cancellationToken);
        return list.Select(NotificationResponse.FromEntity).ToList();
    }

    private Notification Compose(Client client, MessageTemplate template, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.DateTime);
        switch (template)
        {
            case MessageTemplate.ExpiryReminder:
                var membership = client.ActiveMembershipOn(today)
                    ?? throw new DomainException("The client has no active membership to remind about.");
                return Notification.ExpiryReminder(
                    client, membership, templates.ExpiryReminder(ExpiryReminderEmail.For(client, membership, today)), now);
            case MessageTemplate.Promotion:
                var validUntil = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                return Notification.Promotion(
                    client, templates.Promotion(new PromotionEmail(client.Name.FirstName, PromotionDiscountPercent, validUntil)), now);
            default:
                throw new DomainException($"Unknown message template '{template}'.");
        }
    }

    private static MessageTemplate Parse(ClientMessageRequest request) =>
        Enum.TryParse<MessageTemplate>(request.Template, ignoreCase: true, out var template) && Enum.IsDefined(template)
            ? template
            : throw new DomainException("Template must be ExpiryReminder or Promotion.");

    private async Task<Client> GetClientAsync(Guid id, CancellationToken cancellationToken) =>
        await clients.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Client '{id}' was not found.");
}
```

Register it in `api/src/FitnessClub.Application/DependencyInjection.cs` after the `IExpiryNotificationService` line: `services.AddScoped<IClientMessageService, ClientMessageService>();`, and add `using FitnessClub.Application.ClientMessages;`.

- [ ] **Step 5: Run the service and architecture tests**

Run:
```bash
cd api
dotnet test --project tests/FitnessClub.IntegrationTests --filter-class "FitnessClub.IntegrationTests.Services.ClientMessageServiceTests"
dotnet test --project tests/FitnessClub.ArchitectureTests
```
Expected: all PASS.

- [ ] **Step 6: Commit**

```bash
git add api/src/FitnessClub.Application api/tests/FitnessClub.IntegrationTests/Services/ClientMessageServiceTests.cs
git commit -m "feat: preview, send and list client messages"
```

---

### Task 5: Client messages endpoints

**Files:**
- Create: `api/src/FitnessClub.Api/Controllers/ClientMessagesController.cs`
- Test: `api/tests/FitnessClub.IntegrationTests/Notifications/ClientMessagesEndpointsTests.cs`
- Modify (generated): `ui/openapi/fitnessclub.json`

**Interfaces:**
- Consumes: `IClientMessageService`, `ClientMessageRequest`, `ClientMessagePreviewResponse` (Task 4); `factory.ClientWithMembershipAsync(bool withEmail)` (Task 2).
- Produces: OpenAPI operations `ClientMessages_Preview` (GET `/api/clients/{id}/messages/preview?Template=`), `ClientMessages_Send` (POST `/api/clients/{id}/messages`, body `{ template }`) and `ClientMessages_List` (GET `/api/clients/{id}/messages`).

- [ ] **Step 1: Write the failing HTTP tests**

`api/tests/FitnessClub.IntegrationTests/Notifications/ClientMessagesEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FitnessClub.Application.ClientMessages;
using FitnessClub.Application.Common;
using FitnessClub.Application.Notifications;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.IntegrationTests.Notifications;

public class ClientMessagesEndpointsTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Url(Guid clientId) => $"/api/clients/{clientId}/messages";

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Receptionist)]
    public async Task Preview_returns_the_rendered_email(string role)
    {
        var client = await factory.ClientWithMembershipAsync();

        var preview = await factory.CreateClientWithRoles(role)
            .GetFromJsonAsync<ClientMessagePreviewResponse>($"{Url(client.Id)}/preview?template=Promotion", Ct);

        Assert.Equal("Promotion", preview!.Template);
        Assert.Equal(client.Email!.Value, preview.Recipient);
        Assert.Equal("10% off your next membership", preview.Subject);
        Assert.Contains("Olena", preview.Html);
    }

    [Theory]
    [InlineData("?template=Birthday")]
    [InlineData("")]
    public async Task Preview_with_an_unknown_or_missing_template_returns_400(string query)
    {
        var client = await factory.ClientWithMembershipAsync();

        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync($"{Url(client.Id)}/preview{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Preview_for_a_client_without_email_returns_400_with_the_reason()
    {
        var client = await factory.ClientWithMembershipAsync(withEmail: false);

        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync($"{Url(client.Id)}/preview?template=Promotion", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal("The client has no email address.", problem!.Detail);
    }

    [Fact]
    public async Task Preview_for_an_unknown_client_returns_404()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync($"{Url(Guid.NewGuid())}/preview?template=Promotion", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Send_returns_the_sent_message_and_it_is_listed_for_the_client()
    {
        var client = await factory.ClientWithMembershipAsync();
        var http = factory.CreateClientWithRoles(Roles.Receptionist);

        var response = await http.PostAsJsonAsync(Url(client.Id), new { template = "ExpiryReminder" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = await response.Content.ReadFromJsonAsync<NotificationResponse>(Ct);
        Assert.Equal("Sent", sent!.Status);
        Assert.Equal("ExpiryReminder", sent.Type);
        Assert.Equal("Your membership expires soon", sent.Subject);
        var list = await http.GetFromJsonAsync<List<NotificationResponse>>(Url(client.Id), Ct);
        Assert.Equal(sent.Id, Assert.Single(list!).Id);
    }

    [Fact]
    public async Task Send_with_an_unknown_template_returns_400()
    {
        var client = await factory.ClientWithMembershipAsync();

        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsJsonAsync(Url(client.Id), new { template = "Birthday" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_for_an_unknown_client_returns_404()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync(Url(Guid.NewGuid()), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_for_a_trainer_return_403()
    {
        var trainer = factory.CreateClientWithRoles(Roles.Trainer);
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await trainer.GetAsync($"{Url(id)}/preview?template=Promotion", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await trainer.PostAsJsonAsync(Url(id), new { template = "Promotion" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await trainer.GetAsync(Url(id), Ct)).StatusCode);
    }

    [Fact]
    public async Task Endpoints_without_a_user_return_401()
    {
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url(Guid.NewGuid()), Ct)).StatusCode);
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `cd api && dotnet test --project tests/FitnessClub.IntegrationTests --filter-class "FitnessClub.IntegrationTests.Notifications.ClientMessagesEndpointsTests"`
Expected: FAIL with 404s (no route yet).

- [ ] **Step 3: Implement the controller**

`api/src/FitnessClub.Api/Controllers/ClientMessagesController.cs`:

```csharp
using FitnessClub.Application.ClientMessages;
using FitnessClub.Application.Common;
using FitnessClub.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.Api.Controllers;

[ApiController]
[Route("api/clients/{id:guid}/messages")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Receptionist}")]
public sealed class ClientMessagesController(IClientMessageService service) : ControllerBase
{
    [HttpGet("preview")]
    public Task<ClientMessagePreviewResponse> Preview(Guid id, [FromQuery] ClientMessageRequest request, CancellationToken cancellationToken) =>
        service.PreviewAsync(id, request, cancellationToken);

    [HttpPost]
    public Task<NotificationResponse> Send(Guid id, ClientMessageRequest request, CancellationToken cancellationToken) =>
        service.SendAsync(id, request, cancellationToken);

    [HttpGet]
    public Task<IReadOnlyList<NotificationResponse>> List(Guid id, CancellationToken cancellationToken) =>
        service.ListAsync(id, cancellationToken);
}
```

- [ ] **Step 4: Run the endpoint tests and the full suite**

Run: `cd api && dotnet test`
Expected: all PASS, including `OpenApiDocumentTests`.

- [ ] **Step 5: Regenerate the OpenAPI document**

Run: `cd api && dotnet build`, then `git diff --stat ui/openapi/fitnessclub.json`.
Expected: the document now contains `ClientMessages_Preview`, `ClientMessages_Send`, `ClientMessages_List`, `ClientMessagePreviewResponse`, `ClientMessageRequest`, and `subject` on `NotificationResponse`.

- [ ] **Step 6: Commit**

```bash
git add api/src/FitnessClub.Api api/tests/FitnessClub.IntegrationTests/Notifications/ClientMessagesEndpointsTests.cs ui/openapi/fitnessclub.json
git commit -m "feat: client messages endpoints"
```

---

### Task 6: Messages card on the client page

**Files:**
- Create: `ui/src/features/notifications/notificationLabels.ts`
- Create: `ui/src/features/clients/ClientMessagesCard.tsx`
- Test: `ui/src/features/clients/ClientMessagesCard.test.tsx`
- Modify: `ui/src/features/clients/ClientDetailsPage.tsx`, `ClientDetailsPage.test.tsx`
- Modify: `ui/src/features/notifications/NotificationsPage.tsx`, `NotificationsPage.test.tsx`

**Interfaces:**
- Consumes the generated client after `npm run generate`. Check the exact names in `src/api/generated/endpoints/client-messages/client-messages.ts` and `.msw.ts`; the expected names are:
  - `useClientMessagesPreview(id, { Template }, options)`
  - `useClientMessagesSend()` with variables `{ id, data: { template } }`
  - `useClientMessagesList(id)` and `getClientMessagesListQueryKey(id)`
  - `getClientMessagesPreviewMockHandler`, `getClientMessagesSendMockHandler`, `getClientMessagesListMockHandler`
  - types `ClientMessagePreviewResponse` and `NotificationResponse` (now with `subject`)
- Produces: `<ClientMessagesCard clientId={string} />`, plus `notificationTypeLabels` and `notificationStatusColors` from `notificationLabels.ts`.

- [ ] **Step 1: Generate the client**

Run: `cd ui && npm run generate`
Expected: `src/api/generated/endpoints/client-messages/` exists. If the hook names differ from the list above, use the generated ones everywhere below.

- [ ] **Step 2: Write the failing card tests**

`ui/src/features/clients/ClientMessagesCard.test.tsx`:

```tsx
import { screen, waitFor } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import {
  getClientMessagesListMockHandler,
  getClientMessagesPreviewMockHandler,
  getClientMessagesSendMockHandler,
} from '../../api/generated/endpoints/client-messages/client-messages.msw';
import type { ClientMessageRequest, ClientMessagePreviewResponse, NotificationResponse } from '../../api/generated/model';
import dayjs from '../../lib/dayjs';
import { signInAs } from '../../test/auth';
import { ids } from '../../test/fixtures';
import { renderPage } from '../../test/render';
import { server } from '../../test/server';
import { ClientMessagesCard } from './ClientMessagesCard';

const previews: Record<string, ClientMessagePreviewResponse> = {
  ExpiryReminder: { template: 'ExpiryReminder', recipient: 'olena@example.com', subject: 'Your membership expires soon', html: '<p>Dear Olena, your membership ends in 4 days</p>' },
  Promotion: { template: 'Promotion', recipient: 'olena@example.com', subject: '10% off your next membership', html: '<p>10% OFF</p>' },
};

const message = (overrides: Partial<NotificationResponse> = {}): NotificationResponse => ({
  id: ids.notification,
  clientId: ids.client,
  membershipId: null,
  type: 'Promotion',
  channel: 'Email',
  recipient: 'olena@example.com',
  subject: '10% off your next membership',
  message: 'Dear Olena, this month only: get 10% off any membership.',
  status: 'Sent',
  createdAt: dayjs().format(),
  sentAt: dayjs().format(),
  failureReason: null,
  ...overrides,
});

const previewHandler = getClientMessagesPreviewMockHandler(({ request }) => previews[new URL(request.url).searchParams.get('Template') ?? '']);

const renderCard = () => renderPage(<ClientMessagesCard clientId={ids.client} />);

describe('ClientMessagesCard', () => {
  beforeEach(() => signInAs('Receptionist'));

  it('previews the reminder and switches to the promotion', async () => {
    server.use(previewHandler, getClientMessagesListMockHandler([]));
    const { user } = renderCard();

    expect(await screen.findByText('Your membership expires soon')).toBeInTheDocument();
    expect(screen.getByText('olena@example.com')).toBeInTheDocument();
    expect(screen.getByTitle('Email preview')).toHaveAttribute('srcdoc', previews.ExpiryReminder.html);

    await user.click(screen.getByRole('radio', { name: 'Promotion' }));

    expect(await screen.findByText('10% off your next membership')).toBeInTheDocument();
    expect(screen.getByTitle('Email preview')).toHaveAttribute('srcdoc', previews.Promotion.html);
  });

  it('explains why the email cannot be sent and disables Send', async () => {
    server.use(
      http.get('*/api/clients/:id/messages/preview', () =>
        HttpResponse.json({ title: 'Invalid request', detail: 'The client has no active membership to remind about.' }, { status: 400 }),
      ),
      getClientMessagesListMockHandler([]),
    );
    renderCard();

    expect(await screen.findByText('The client has no active membership to remind about.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Send email' })).toBeDisabled();
  });

  it('asks for confirmation, sends the selected template and refreshes the history', async () => {
    let sent: ClientMessageRequest | undefined;
    let listLoads = 0;
    server.use(
      previewHandler,
      getClientMessagesListMockHandler(() => {
        listLoads += 1;
        return listLoads === 1 ? [] : [message()];
      }),
      getClientMessagesSendMockHandler(async ({ request }) => {
        sent = (await request.json()) as ClientMessageRequest;
        return message();
      }),
    );
    const { user } = renderCard();

    await user.click(await screen.findByRole('radio', { name: 'Promotion' }));
    await screen.findByText('10% off your next membership');
    await user.click(screen.getByRole('button', { name: 'Send email' }));
    expect(await screen.findByText('Send this email to olena@example.com?')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText('Email sent')).toBeInTheDocument();
    expect(sent).toEqual({ template: 'Promotion' });
    await waitFor(() => expect(listLoads).toBe(2));
  });

  it('shows the reason when the email could not be delivered', async () => {
    server.use(
      previewHandler,
      getClientMessagesListMockHandler([]),
      getClientMessagesSendMockHandler(message({ status: 'Failed', sentAt: null, failureReason: 'SendGrid rejected the email (401 Unauthorized)' })),
    );
    const { user } = renderCard();

    await user.click(await screen.findByRole('button', { name: 'Send email' }));
    await user.click(await screen.findByRole('button', { name: 'Send' }));

    expect(await screen.findByText('SendGrid rejected the email (401 Unauthorized)')).toBeInTheDocument();
  });

  it('lists the messages already sent to the client', async () => {
    server.use(
      previewHandler,
      getClientMessagesListMockHandler([message({ type: 'MembershipExpiring', subject: 'Your membership expires soon', status: 'Failed', sentAt: null })]),
    );
    renderCard();

    expect(await screen.findByText('Expiry notice')).toBeInTheDocument();
    expect(screen.getByText('Failed')).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run them and see them fail**

Run: `cd ui && npm test -- src/features/clients/ClientMessagesCard.test.tsx`
Expected: FAIL, `ClientMessagesCard` cannot be resolved.

- [ ] **Step 4: Add the shared labels**

`ui/src/features/notifications/notificationLabels.ts`:

```ts
export const notificationTypeLabels: Record<string, string> = {
  MembershipExpiring: 'Expiry notice',
  ExpiryReminder: 'Reminder',
  Promotion: 'Promotion',
};

export const notificationStatusColors: Record<string, string> = { Pending: 'yellow', Sent: 'teal', Failed: 'red' };
```

- [ ] **Step 5: Implement the card**

`ui/src/features/clients/ClientMessagesCard.tsx`:

```tsx
import { Alert, Badge, Button, Card, Center, Group, Loader, SegmentedControl, Stack, Table, Text, Title, Tooltip } from '@mantine/core';
import { modals } from '@mantine/modals';
import { notifications } from '@mantine/notifications';
import { IconSend } from '@tabler/icons-react';
import { useQueryClient } from '@tanstack/react-query';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import {
  getClientMessagesListQueryKey,
  useClientMessagesList,
  useClientMessagesPreview,
  useClientMessagesSend,
} from '../../api/generated/endpoints/client-messages/client-messages';
import { getNotificationsListQueryKey } from '../../api/generated/endpoints/notifications/notifications';
import { problemMessage } from '../../api/problem';
import { QueryErrorAlert } from '../../api/QueryErrorAlert';
import { formatDateTime } from '../../lib/format';
import { notificationStatusColors, notificationTypeLabels } from '../notifications/notificationLabels';

type Template = 'ExpiryReminder' | 'Promotion';

const templates: { value: Template; label: string }[] = [
  { value: 'ExpiryReminder', label: 'Expiry reminder' },
  { value: 'Promotion', label: 'Promotion' },
];

export const ClientMessagesCard = ({ clientId }: { clientId: string }) => {
  const queryClient = useQueryClient();
  const [template, setTemplate] = useState<Template>('ExpiryReminder');
  const preview = useClientMessagesPreview(clientId, { Template: template }, { query: { retry: false } });
  const history = useClientMessagesList(clientId);
  const send = useClientMessagesSend({
    mutation: {
      meta: { errorTitle: 'The email was not sent' },
      onSuccess: async (result) => {
        if (result.status === 'Sent') notifications.show({ color: 'teal', message: 'Email sent' });
        else notifications.show({ color: 'red', title: 'The email was not sent', message: result.failureReason ?? 'Unknown error' });
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: getClientMessagesListQueryKey(clientId) }),
          queryClient.invalidateQueries({ queryKey: getNotificationsListQueryKey() }),
        ]);
      },
    },
  });

  const confirmSend = (recipient: string) =>
    modals.openConfirmModal({
      title: 'Send this email?',
      children: <Text size="sm">Send this email to {recipient}?</Text>,
      labels: { confirm: 'Send', cancel: 'Cancel' },
      onConfirm: () => send.mutate({ id: clientId, data: { template } }),
    });

  return (
    <Card withBorder radius="md">
      <Group justify="space-between" mb="sm">
        <Title order={4}>Messages</Title>
        <Group>
          <SegmentedControl data={templates} value={template} onChange={(value) => setTemplate(value as Template)} aria-label="Template" />
          <Button
            leftSection={<IconSend size={16} />}
            disabled={!preview.isSuccess}
            loading={send.isPending}
            onClick={() => preview.data && confirmSend(preview.data.recipient)}
          >
            Send email
          </Button>
        </Group>
      </Group>
      <Stack>
        {preview.isPending ? (
          <Center h={120}><Loader /></Center>
        ) : preview.isError ? (
          <Alert color="orange" title="This email can't be sent">{problemMessage(preview.error)}</Alert>
        ) : (
          <>
            <Table variant="vertical" layout="fixed">
              <Table.Tbody>
                <Table.Tr><Table.Th w={120}>To</Table.Th><Table.Td>{preview.data.recipient}</Table.Td></Table.Tr>
                <Table.Tr><Table.Th>Subject</Table.Th><Table.Td>{preview.data.subject}</Table.Td></Table.Tr>
              </Table.Tbody>
            </Table>
            <iframe
              title="Email preview"
              srcDoc={preview.data.html}
              sandbox=""
              style={{ width: '100%', height: 520, border: '1px solid var(--mantine-color-default-border)', borderRadius: 'var(--mantine-radius-md)' }}
            />
          </>
        )}
        <Title order={5}>Sent messages</Title>
        {history.isError ? (
          <QueryErrorAlert title="Could not load messages" error={history.error} />
        ) : (
          <DataTable
            minHeight={120}
            fetching={history.isFetching}
            records={history.data ?? []}
            noRecordsText="No messages yet"
            columns={[
              { accessor: 'createdAt', title: 'Created', render: (n) => formatDateTime(n.createdAt) },
              { accessor: 'type', title: 'Type', render: (n) => notificationTypeLabels[n.type] ?? n.type },
              { accessor: 'subject', title: 'Subject', ellipsis: true },
              {
                accessor: 'status',
                title: 'Status',
                render: (n) => (
                  <Tooltip label={n.failureReason} disabled={!n.failureReason}>
                    <Badge variant="light" color={notificationStatusColors[n.status] ?? 'gray'}>{n.status}</Badge>
                  </Tooltip>
                ),
              },
              { accessor: 'sentAt', title: 'Sent', render: (n) => (n.sentAt ? formatDateTime(n.sentAt) : '—') },
            ]}
          />
        )}
      </Stack>
    </Card>
  );
};
```

- [ ] **Step 6: Run the card tests**

Run: `cd ui && npm test -- src/features/clients/ClientMessagesCard.test.tsx`
Expected: PASS (5 tests).

- [ ] **Step 7: Put the card on the client page**

In `ClientDetailsPage.tsx`:
- Import `import { ClientMessagesCard } from './ClientMessagesCard';`.
- Add `<ClientMessagesCard clientId={data.id} />` right after the Visits `</Card>`.

In `ClientDetailsPage.test.tsx`, every test now also loads the card. Import the two handlers and extend `beforeEach`:

```tsx
import { getClientMessagesListMockHandler, getClientMessagesPreviewMockHandler } from '../../api/generated/endpoints/client-messages/client-messages.msw';
```

```tsx
  beforeEach(() => {
    signInAs('Receptionist');
    server.use(
      getClientMessagesPreviewMockHandler({ template: 'ExpiryReminder', recipient: 'olena@example.com', subject: 'Your membership expires soon', html: '<p>Hi</p>' }),
      getClientMessagesListMockHandler([]),
    );
  });
```

- [ ] **Step 8: Notifications page labels**

In `NotificationsPage.tsx`:
- Import `notificationStatusColors` and `notificationTypeLabels` from `./notificationLabels`, and delete the local `statusColors` (use `notificationStatusColors` in the badge).
- Change the title to `Notifications` and the dimmed text to `Expiry notices go out automatically every morning. Reminders and promotions are sent from a client's page.`
- Replace the `message` column with:

```tsx
            { accessor: 'type', title: 'Type', render: (n) => notificationTypeLabels[n.type] ?? n.type },
            { accessor: 'subject', title: 'Subject', ellipsis: true, width: 280 },
```

In `NotificationsPage.test.tsx`:
- Add `subject: 'Your membership expires soon',` to the `failed` fixture.
- If a test looks for the heading `Expiry notifications`, change it to `Notifications`.

- [ ] **Step 9: Run all UI checks**

Run: `cd ui && npm run typecheck && npm run lint && npm test`
Expected: all PASS.

- [ ] **Step 10: Commit**

```bash
git add ui/src
git commit -m "feat: messages card with email preview on the client page"
```

---

### Task 7: Deploy settings and docs

**Files:**
- Modify: `deploy/docker-compose.yml`, `deploy/docker-compose.server.yml`, `deploy/.env.example`, `deploy/.env.server.example`, `deploy/CLAUDE.md`
- Modify: `api/CLAUDE.md`, `README.md`, `CLAUDE.md`
- Modify: `docs/Code/Specs/2026-10-07-expiry-notifications-design.md`, `docs/Code/Requirements Coverage.md`

- [ ] **Step 1: Compose**

`deploy/docker-compose.yml`: replace the six `Smtp__*` lines with

```yaml
      SendGrid__ApiKey: ${SENDGRID_API_KEY:-}
      SendGrid__FromAddress: ${SENDGRID_FROM_ADDRESS:-}
      SendGrid__FromName: ${SENDGRID_FROM_NAME:-Fitness Club}
```

`deploy/docker-compose.server.yml`: replace the six `Smtp__*` lines with

```yaml
      SendGrid__ApiKey: ${SENDGRID_API_KEY:?set it in .env - the twilio sendgrid api key with mail send access}
      SendGrid__FromAddress: ${SENDGRID_FROM_ADDRESS:?set it in .env - a verified sender in sendgrid, e.g. club@yourdomain.com}
      SendGrid__FromName: ${SENDGRID_FROM_NAME:?set it in .env - the sender name clients see, e.g. Fitness Club}
```

- [ ] **Step 2: Env examples**

`deploy/.env.example`: replace the SMTP comment and variables with

```sh
# Email (expiry notices, reminders and promotions) goes out through Twilio SendGrid.
# Leave SENDGRID_API_KEY empty to only write emails to the API log.
# Create a key with "Mail Send" access (SendGrid -> Settings -> API Keys). The from-address must be a
# verified single sender or on an authenticated domain (SendGrid -> Settings -> Sender Authentication).
SENDGRID_API_KEY=
SENDGRID_FROM_ADDRESS=
SENDGRID_FROM_NAME=Fitness Club
```

`deploy/.env.server.example`: replace the email section with

```sh
# ---------------------------------------------------------------------------
# Email (Twilio SendGrid)
# ---------------------------------------------------------------------------

# An API key with "Mail Send" access, and a from-address that is verified in SendGrid
# (Settings -> Sender Authentication).
SENDGRID_API_KEY="SG.your-api-key"
SENDGRID_FROM_ADDRESS="club@yourdomain.com"
SENDGRID_FROM_NAME="Fitness Club"
```

- [ ] **Step 3: `deploy/CLAUDE.md`**

Replace the five `SMTP_*` table rows with

```markdown
| `SENDGRID_API_KEY` | no | empty | `SendGrid__ApiKey`. Empty means emails are only logged. Set it to send through Twilio SendGrid |
| `SENDGRID_FROM_ADDRESS` | if `SENDGRID_API_KEY` is set | empty | `SendGrid__FromAddress`. Must be a verified sender in SendGrid. Without it, a set key fails startup |
| `SENDGRID_FROM_NAME` | no | `Fitness Club` | `SendGrid__FromName` |
```

and the server bullet with `- **Email is required:** every \`SENDGRID_*\` variable must be set, so the server always emails clients.`

- [ ] **Step 4: `api/CLAUDE.md`**

Replace the "Email (expiry notices)" section with:

```markdown
## Email (Twilio SendGrid)

- Expiry notices, reminders and promotions are sent by `SendGridNotificationSender` (SendGrid v3 REST API, typed `HttpClient`). Without `SendGrid:ApiKey`, they go to `LoggingNotificationSender` and only appear in the log. That is the default locally and in tests.
- The two HTML templates are embedded resources in `src/FitnessClub.Infrastructure/Notifications/Templates/`, filled by `EmailTemplates` (`{{Token}}`, values HTML-encoded). Inline CSS only, because email clients drop `<style>` and SVG.
- To send real email locally, set the SendGrid settings in user secrets. The from-address must be verified in SendGrid:

  ```sh
  dotnet user-secrets --project src/FitnessClub.Api set "SendGrid:ApiKey" "SG.<key>"
  dotnet user-secrets --project src/FitnessClub.Api set "SendGrid:FromAddress" "<verified sender>"
  ```

- `SendGridNotificationSenderTests` use a stub `HttpMessageHandler`, so no network or container is needed.
```

- [ ] **Step 5: `README.md` and the root `CLAUDE.md`**

In `README.md`, change the adapter line to: `- **Strategy / adapter:** \`INotificationSender\`: \`SendGridNotificationSender\` (Twilio SendGrid) when an API key is configured, otherwise \`LoggingNotificationSender\`.`

In the root `CLAUDE.md` Status → Backend, replace `email over SMTP via MailKit when \`Smtp:Host\` is set, otherwise only logged` with `email through Twilio SendGrid when \`SendGrid:ApiKey\` is set, otherwise only logged`. After the expiry notifications clause, add `; staff can also send a branded expiry reminder or a 10% promotion from a client's page`.

- [ ] **Step 6: Specs and coverage**

In `docs/Code/Specs/2026-10-07-expiry-notifications-design.md`:
- Add at the top under the title: `> Email delivery moved to Twilio SendGrid with HTML templates in [[2026-10-08-client-messages-design]]. The SMTP sections below are superseded.`
- Update:
  - the `INotificationSender` rows to `SendAsync(notification)`
  - the sender row to `SendGridNotificationSender`
  - the config block to the `SendGrid` section
  - the SMTP test row to `SendGridNotificationSenderTests`
- Remove the "Each notice opens its own SMTP connection" gap.

In `docs/Code/Requirements Coverage.md`:
- Row 6 notes: `✅ (email through Twilio SendGrid; clients without an email address get no notice). Staff can also send a reminder or promotion: \`GET /api/clients/{id}/messages/preview\`, \`POST/GET /api/clients/{id}/messages\` ([[2026-10-08-client-messages-design]])`.
- Line 52: `- Notifications are emailed by \`SendGridNotificationSender\` when \`SendGrid:ApiKey\` is set, otherwise only written to the log by \`LoggingNotificationSender\`. There is no SMS channel, so clients without an email address get no notice.`

- [ ] **Step 7: Check that no SMTP mention is left**

Run: `grep -rniE "smtp|mailkit|mailpit" --exclude-dir=node_modules --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git . | grep -v "docs/Code/Plans/2026-10-0[5-7]\|docs/Code/Specs/2026-10-0[5-7]\|docs/Code/Plans/2026-10-08-sql"`
Expected: no hits outside older plans/specs (those are history). The "superseded" note covers the expiry spec.

- [ ] **Step 8: Validate compose**

Run: `docker compose -f deploy/docker-compose.yml config --quiet && echo ok`
Expected: `ok`.

- [ ] **Step 9: Commit**

```bash
git add deploy api/CLAUDE.md README.md CLAUDE.md docs
git commit -m "docs: twilio sendgrid configuration and client messages"
```

---

## Manual verification (after all tasks)

1. Start the stack without a key: `docker compose -f deploy/docker-compose.yml up -d --build`.
2. Sign in as a receptionist, open a client with an email and an active membership, then:
   - Toggle Expiry reminder / Promotion. Both previews render as branded emails, in light and dark portal theme.
   - Send one. Expect the "Email sent" toast, a row in Sent messages and on `/notifications`, and a `Sending Email notification` line in the API log.
   - Open a client without an email. Expect the orange alert, and Send is disabled.
3. Set `SENDGRID_API_KEY` and a verified `SENDGRID_FROM_ADDRESS` in `deploy/.env`, restart `api`, and send both templates to a real inbox. Check Gmail web and mobile rendering.
4. Set a wrong key and send. Expect a red toast with SendGrid's 401 reason and a `Failed` row. Fix the key, then use Retry on `/notifications` and **Run now**: the row becomes `Sent`.
