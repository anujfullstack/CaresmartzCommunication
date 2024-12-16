using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

[assembly: FunctionsStartup(typeof(Caresmartz.Services.SchedulerListeners.Startup))]
namespace Caresmartz.Services.SchedulerListeners
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            builder.ConfigurationBuilder
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

        }
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var hostConfig = builder.GetContext().Configuration;
            ConfigureServices(builder.Services, hostConfig);
            builder.Services.AddHttpClient(); // Example: Add HttpClient

        }
        private void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Built-in & Libraries
            services.AddHttpContextAccessor();
            services.AddAzureClients(builder => builder.AddServiceBusClient(configuration.GetValue<string>("ServiceBusConnectionString")));
        }
    }
}
