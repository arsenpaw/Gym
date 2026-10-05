using NetArchTest.Rules;

namespace FitnessClub.ArchitectureTests;

public class LayerDependencyTests
{
    private static readonly string[] QueryableAssemblies = ["System.Linq.Queryable", "System.Linq.Expressions"];

    [Fact]
    public void Domain_depends_only_on_the_base_class_library()
    {
        var offenders = Layers.Domain.ReferencedAssemblyNames()
            .Where(name => !IsBaseClassLibrary(name) || QueryableAssemblies.Contains(name));

        Assert.Empty(offenders);
    }

    [Fact]
    public void Application_depends_only_on_domain_and_dependency_injection_abstractions()
    {
        string[] allowed = ["FitnessClub.Domain", "Microsoft.Extensions.DependencyInjection.Abstractions"];

        var offenders = Layers.Application.ReferencedAssemblyNames()
            .Where(name => (!IsBaseClassLibrary(name) && !allowed.Contains(name)) || QueryableAssemblies.Contains(name));

        Assert.Empty(offenders);
    }

    [Fact]
    public void Api_does_not_reference_persistence_or_job_frameworks()
    {
        var offenders = Layers.Api.ReferencedAssemblyNames()
            .Where(name => name.StartsWith("Microsoft.EntityFrameworkCore") || name.StartsWith("Hangfire"));

        Assert.Empty(offenders);
    }

    [Fact]
    public void Only_the_composition_root_uses_infrastructure()
    {
        var result = Types.InAssembly(Layers.Api)
            .That().DoNotHaveName("Program")
            .ShouldNot().HaveDependencyOn("FitnessClub.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, result.Describe());
    }

    [Fact]
    public void Api_uses_domain_only_to_translate_domain_errors()
    {
        var result = Types.InAssembly(Layers.Api)
            .That().DoNotHaveName("ExceptionToProblemDetailsHandler")
            .ShouldNot().HaveDependencyOn("FitnessClub.Domain")
            .GetResult();

        Assert.True(result.IsSuccessful, result.Describe());
    }

    [Fact]
    public void Application_types_do_not_depend_on_outer_layers_or_frameworks()
    {
        var result = Types.InAssembly(Layers.Application)
            .ShouldNot().HaveDependencyOnAny("FitnessClub.Infrastructure", "FitnessClub.Api", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, result.Describe());
    }

    private static bool IsBaseClassLibrary(string name) =>
        name is "netstandard" or "mscorlib" or "System" || name.StartsWith("System.");
}
