namespace FitnessClub.Application.Clients;

public sealed record VisitPageResponse(IReadOnlyList<VisitResponse> Items, int TotalCount);
