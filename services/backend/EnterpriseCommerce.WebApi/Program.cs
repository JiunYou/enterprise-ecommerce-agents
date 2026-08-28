using EnterpriseCommerce.Application;
using EnterpriseCommerce.Infrastructure;
using EnterpriseCommerce.WebApi.Extensions;
using EnterpriseCommerce.WebApi.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Logging Framework Setup (Serilog)
builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration.ReadFrom.Configuration(context.Configuration);
});

// 2. Configuration Loading
// builder.Configuration.AddJsonFile(...);

// 3. Dependency Injection Setup
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add Extension Configs
builder.Services.AddApiVersioningConfig();
builder.Services.AddSwaggerConfig();
builder.Services.AddJwtAuthentication();
builder.Services.AddAuthorization();

// Add Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks(); // Health Check

// Global Exception Handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// 4. Security Framework (CORS)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// 5. Middleware Pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Security Headers & Exception Handling
app.UseExceptionHandler(); // Uses the registered GlobalExceptionHandler
app.UseHttpsRedirection();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// 6. Endpoints
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapControllers();

app.Run();

public partial class Program { }
