using Pos.Identity.Application.Interfaces.Publisher;
using Pos.Identity.Infrastructure.Shared.Options;
using Pos.Identity.Infrastructure.Shared.Publisher;
using Pos.Identity.Infrastructure.Shared.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pos.Identity.Application.Interfaces.Clients;
using Pos.Identity.Application.Interfaces.Services;
using Pos.Identity.Infrastructure.Shared.Clients;
using Pos.Identity.Infrastructure.Shared.Seeders;
using Pos.Identity.Infrastructure.Shared.Services;
using Pos.Identity.Infrastructure.Shared.Settings;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pos.Identity.Infrastructure.Shared
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddSharedInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddOptions<OutboxPublisherOptions>()
                .Bind(configuration.GetSection("OutboxPublisher"))
                .Validate(x => x.PollingIntervalSeconds > 0 && x.BatchSize > 0 &&
                    x.PublishTimeoutSeconds > 0 && x.MaxAttempts > 0 &&
                    x.InitialRetryDelaySeconds > 0 &&
                    x.MaxRetryDelaySeconds >= x.InitialRetryDelaySeconds,
                    "Outbox intervals, batch size and retry limits must be positive.")
                .ValidateOnStart();
            services.AddOptions<RabbitMqOptions>()
                .Bind(configuration.GetSection("RabbitMq"))
                .Validate(x => !configuration.GetValue<bool>("OutboxPublisher:Enabled") ||
                    (!string.IsNullOrWhiteSpace(x.HostName) && x.Port > 0 && x.Port <= 65535 &&
                     !string.IsNullOrWhiteSpace(x.UserName) && !string.IsNullOrWhiteSpace(x.Password) &&
                     !string.IsNullOrWhiteSpace(x.VirtualHost) && !string.IsNullOrWhiteSpace(x.ExchangeName)),
                    "Configure RabbitMq before enabling the Identity outbox publisher.")
                .ValidateOnStart();
            services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
            services.AddScoped<OutboxDispatcher>();
            services.AddHostedService<OutboxPublisherWorker>();

            services.Configure<IdentityEmailLinkOptions>(configuration.GetSection("IdentityEmailLinks"));
            services.AddScoped<IEmailRequestService, EmailRequestService>();
            services.AddScoped<IUserAuthenticationService, UserAuthenticationService>();
            services.AddScoped<IPlatformAdminService, PlatformAdminService>();
            services.AddScoped<ITenantUserService, TenantUserService>();
            services.AddScoped<IUserInvitationService, UserInvitationService>();
            //seeder
            services.AddScoped<IdentityRoleSeeder>();
            services.AddMemoryCache();
            services.AddScoped<ITenantSubscriptionAccessService, TenantSubscriptionAccessService>();

            services.Configure<InternalApiKeyOptions>(
                     configuration.GetSection(nameof(InternalApiKeyOptions)));

            services.AddHttpClient<ITenantBillingClient, TenantBillingClient>(client =>
            {
                client.BaseAddress = new Uri(
                    configuration["Services:TenantBilling:BaseUrl"]!);
            })
            .RedactLoggedHeaders(new[]
            {
                "X-Internal-Api-Key",
                "Authorization"
            });

            return services;
        }
    }
}
