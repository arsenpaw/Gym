namespace FitnessClub.Application.ClientMessages;

public sealed record ClientMessagePreviewResponse(string Template, string Recipient, string Subject, string Html);
