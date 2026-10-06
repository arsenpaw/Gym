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

        var injected = controllers
            .SelectMany(type => type.GetConstructors(declaredInstance).SelectMany(constructor => constructor.GetParameters()))
            .Concat(controllers
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .SelectMany(method => method.GetParameters()))
            .Select(parameter => (Owner: parameter.Member.DeclaringType!, Type: parameter.ParameterType))
            .Concat(controllers
                .SelectMany(type => type.GetProperties(declaredInstance))
                .Where(property => property.IsDefined(typeof(FromServicesAttribute), inherit: false))
                .Select(property => (Owner: property.DeclaringType!, Type: property.PropertyType)));

        var offenders = injected
            .Where(item => item.Type.IsInterface
                && (item.Type.Assembly != Layers.Application || !item.Type.Name.EndsWith("Service")))
            .Select(item => $"{item.Owner.Name}({item.Type.Name})");

        Assert.NotEmpty(controllers);
        Assert.Empty(offenders);
    }

    [Fact]
    public void Infrastructure_exposes_only_its_registration_entry_point()
    {
        var offenders = Layers.Infrastructure.DeclaredTypes()
            .Where(type => type.IsVisible && type != typeof(FitnessClub.Infrastructure.DependencyInjection))
            .Select(type => type.Name);

        Assert.Empty(offenders);
    }
}
