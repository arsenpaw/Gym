using FitnessClub.Api.Auth;
using FitnessClub.Api.ErrorHandling;
using FitnessClub.Application;
using FitnessClub.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAuth0Authentication(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionToProblemDetailsHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

await app.UseInfrastructureAsync();

await app.RunAsync();

public partial class Program;
