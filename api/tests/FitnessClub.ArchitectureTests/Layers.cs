using System.Reflection;
using System.Runtime.CompilerServices;
using FitnessClub.Domain.Common;

namespace FitnessClub.ArchitectureTests;

internal static class Layers
{
    public static readonly Assembly Domain = typeof(Entity).Assembly;
    public static readonly Assembly Application = typeof(FitnessClub.Application.DependencyInjection).Assembly;
    public static readonly Assembly Infrastructure = typeof(FitnessClub.Infrastructure.DependencyInjection).Assembly;
    public static readonly Assembly Api = typeof(Program).Assembly;

    public static IEnumerable<string> ReferencedAssemblyNames(this Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(reference => reference.Name!);

    public static IEnumerable<Type> DeclaredTypes(this Assembly assembly) =>
        assembly.GetTypes().Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) && !type.Name.Contains('<'));

    public static IEnumerable<Type> ConcreteClasses(this Assembly assembly) =>
        assembly.DeclaredTypes().Where(type => type is { IsClass: true, IsAbstract: false });

    public static string Describe(this NetArchTest.Rules.TestResult result) =>
        string.Join(", ", result.FailingTypeNames ?? []);
}
