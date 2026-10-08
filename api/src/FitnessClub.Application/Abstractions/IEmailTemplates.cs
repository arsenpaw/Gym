using FitnessClub.Application.Notifications;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.Application.Abstractions;

public interface IEmailTemplates
{
    NotificationContent ExpiryReminder(ExpiryReminderEmail email);

    NotificationContent Promotion(PromotionEmail email);
}
