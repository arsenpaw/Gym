using System.Reflection;
using FitnessClub.Domain.Common;

namespace FitnessClub.ArchitectureTests;

public class DomainModelTests
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.Instance;

    private static IEnumerable<Type> DomainModelTypes() =>
        Layers.Domain.DeclaredTypes().Where(type =>
            !type.IsAbstract && !type.IsEnum && !type.IsInterface
            && (typeof(Entity).IsAssignableFrom(type) || type.Namespace == "FitnessClub.Domain.SharedKernel" || IsRecord(type)));

    [Fact]
    public void The_domain_model_is_found()
    {
        Assert.NotEmpty(DomainModelTypes());
    }

    [Fact]
    public void Entities_and_value_objects_have_no_public_setters_or_fields()
    {
        var offenders = DomainModelTypes()
            .SelectMany(type => type.GetProperties(Members).Where(property => property.SetMethod is { IsPublic: true }).Cast<MemberInfo>()
                .Concat(type.GetFields(Members)))
            .Select(member => $"{member.DeclaringType!.Name}.{member.Name}");

        Assert.Empty(offenders);
    }

    [Fact]
    public void Entities_and_value_objects_have_no_public_constructors()
    {
        var offenders = DomainModelTypes()
            .Where(type => type.GetConstructors(Members).Length > 0)
            .Select(type => type.Name);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Entities_and_value_objects_are_sealed()
    {
        var offenders = DomainModelTypes().Where(type => !type.IsSealed).Select(type => type.Name);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_aggregate_root_has_a_repository_interface_in_domain()
    {
        var offenders = AggregateRoots().Where(root => RepositoryInterfaceFor(root) is null).Select(root => root.Name);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_repository_interface_is_implemented_by_a_non_public_infrastructure_class()
    {
        var implementations = Layers.Infrastructure.ConcreteClasses().ToList();

        var offenders = AggregateRoots()
            .Select(RepositoryInterfaceFor)
            .OfType<Type>()
            .Where(contract => !implementations.Any(type => contract.IsAssignableFrom(type) && !type.IsPublic))
            .Select(contract => contract.Name);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Repositories_exist_only_for_aggregate_roots()
    {
        var offenders = Layers.Domain.DeclaredTypes()
            .Where(type => type.IsInterface)
            .SelectMany(type => type.GetInterfaces().Append(type))
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRepository<>))
            .Select(type => type.GetGenericArguments()[0])
            .Where(argument => !argument.IsGenericParameter && !typeof(AggregateRoot).IsAssignableFrom(argument))
            .Select(argument => argument.Name);

        Assert.Empty(offenders);
    }

    private static IEnumerable<Type> AggregateRoots() =>
        Layers.Domain.ConcreteClasses().Where(type => typeof(AggregateRoot).IsAssignableFrom(type));

    private static Type? RepositoryInterfaceFor(Type root) =>
        Layers.Domain.DeclaredTypes().FirstOrDefault(type =>
            type.IsInterface && typeof(IRepository<>).MakeGenericType(root).IsAssignableFrom(type) && type.Name == $"I{root.Name}Repository");

    private static bool IsRecord(Type type) => type.GetMethod("<Clone>$") is not null;
}
