using System.ComponentModel.DataAnnotations;

namespace FitnessClub.Application.Clients;

public sealed record VisitPageQuery
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = 10;
}
