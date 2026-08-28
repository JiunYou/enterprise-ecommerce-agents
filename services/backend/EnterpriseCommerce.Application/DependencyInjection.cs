using EnterpriseCommerce.Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace EnterpriseCommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);

            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<Events.IDomainEventDispatcher, Events.DomainEventDispatcher>();
        services.AddScoped<Events.IIntegrationEventMapper, Events.IntegrationEventMapper>();
        
        // Register all domain event handlers dynamically
        var domainEventHandlers = assembly.GetTypes()
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Events.IDomainEventHandler<>)))
            .ToList();
        
        foreach (var handler in domainEventHandlers)
        {
            var interfaceType = handler.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Events.IDomainEventHandler<>));
            services.AddScoped(interfaceType, handler);
        }

        return services;
    }
}
