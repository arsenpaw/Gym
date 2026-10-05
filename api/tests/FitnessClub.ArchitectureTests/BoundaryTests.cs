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
        var controllers = Layers.Api.ConcreteClasses().Where(type => typeof(ControllerBase).IsAssignableFrom(type)).ToList();

        var injected = controllers
            .SelectMany(type => type.GetConstructors().SelectMany(constructor => constructor.GetParameters()))
            .Concat(controllers
                .SelectMany(type => type.GetMethods())
                .SelectMany(method => method.GetParameters())
                .Where(parameter => parameter.IsDefined(typeof(FromServicesAttribute), inherit: false)));

        var offenders = injected
            .Where(parameter => !parameter.ParameterType.IsInterface
                || parameter.ParameterType.Assembly != Layers.Application
                || !parameter.ParameterType.Name.EndsWith("Service"))
            .Select(parameter => $"{parameter.Member.DeclaringType!.Name}({parameter.ParameterType.Name})");

        Assert.NotEmpty(controllers);
        Assert.Empty(offenders);
    }

    [Fact]
    public void Infrastructure_exposes_only_its_registration_entry_point()
    {
        var offenders = Layers.Infrastructure.DeclaredTypes()
            .Where(type => type.IsPublic && type != typeof(FitnessClub.Infrastructure.DependencyInjection))
            .Select(type => type.Name);

        Assert.Empty(offenders);
    }
}
