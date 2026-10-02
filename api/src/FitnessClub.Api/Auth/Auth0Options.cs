using System.ComponentModel.DataAnnotations;

namespace FitnessClub.Api.Auth;

public sealed class Auth0Options
{
    public const string SectionName = "Auth0";

    [Required]
    public string Domain { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "";

    [Required]
    public string RolesClaim { get; set; } = "https://fitnessclub/roles";
}
