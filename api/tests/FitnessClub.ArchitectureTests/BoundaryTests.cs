using System.Reflection;
using FitnessClub.Domain.Common;
using FitnessClub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.ArchitectureTests;

public class BoundaryTests
{
    [Fact]
    public void DbContext_exposes_only_aggregate_roots()
    {
        var offenders = typeof(FitnessClubDbContext).GetProperties()
            .Where(property => property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(property => property.PropertyType.GetGenericArguments()[0])
            .Where(entity => !typeof(AggregateRoot).IsAssignableFrom(entity))
            .Select(entity => entity.Name);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Application_services_are_internal_and_exposed_through_interfaces()
    {
        var services = Layers.Application.ConcreteClasses().Where(type => type.Name.EndsWith("Service")).ToList();

        var offenders = services
            .Where(type => type.IsPublic || type.GetInterface($"I{type.Name}") is not { IsPublic: true })
            .Select(type => type.Name);

        Assert.NotEmpty(services);
        Assert.Empty(offenders);
    }

    [Fact]
    public void Controllers_depend_only_on_application_service_interfaces()
    {
        var controllers = Layers.Api.DeclaredTypes().Where(type => type.IsClass && typeof(ControllerBase).IsAssignableFrom(type)).ToList();

        const BindingFlags declaredInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        var constructorParameters = controllers
            .SelectMany(type => type.GetConstructors(declaredInstance).SelectMany(constructor => constructor.GetParameters()))
            .Select(parameter => (Owner: parameter.Member.DeclaringType!, Type: parameter.ParameterType));

        var serviceProperties = controllers
            .SelectMany(type => type.GetProperties(declaredInstance))
            .Where(property => property.IsDefined(typeof(FromServicesAttribute), inherit: false))
            .Select(property => (Owner: property.DeclaringType!, Type: property.PropertyType));

        var actionParameters = controllers
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetParameters());

        var strictOffenders = constructorParameters
            .Concat(serviceProperties)
            .Concat(actionParameters
                .Where(parameter => parameter.IsDefined(typeof(FromServicesAttribute), inherit: false))
                .Select(parameter => (Owner: parameter.Member.DeclaringType!, Type: parameter.ParameterType)))
            .Where(item => item.Type.IsInterface && !IsApplicationServiceInterface(item.Type));

        var actionOffenders = actionParameters
            .Where(parameter => !parameter.IsDefined(typeof(FromServicesAttribute), inherit: false))
            .Select(parameter => (Owner: parameter.Member.DeclaringType!, Type: parameter.ParameterType))
            .Where(item => !IsAllowedActionParameterType(item.Type));

        var offenders = strictOffenders
            .Concat(actionOffenders)
            .Select(item => $"{item.Owner.Name}({item.Type.Name})");

        Assert.NotEmpty(controllers);
        Assert.Empty(offenders);
    }

    private static bool IsApplicationServiceInterface(Type type) =>
        type.IsInterface && type.Assembly == Layers.Application && type.Name.EndsWith("Service");

    private static bool IsAllowedActionParameterType(Type type) =>
        !type.IsInterface
        || IsApplicationServiceInterface(type)
        || (IsFrameworkType(type) && type.GenericTypeArguments.All(IsAllowedActionParameterType));

    private static bool IsFrameworkType(Type type) =>
        type.Namespace is { } ns && (ns.StartsWith("System") || ns.StartsWith("Microsoft.AspNetCore"));

    [Fact]
    public void Infrastructure_exposes_only_its_registration_entry_point()
    {
        var offenders = Layers.Infrastructure.DeclaredTypes()
            .Where(type => type.IsVisible && type != typeof(FitnessClub.Infrastructure.DependencyInjection))
            .Select(type => type.Name);

        Assert.Empty(offenders);
    }
}
