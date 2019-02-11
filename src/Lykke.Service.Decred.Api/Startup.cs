using System;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AzureStorage.Tables;
using Common.Log;
using DcrdClient;
using Decred.BlockExplorer;
using Lykke.Common.ApiLibrary.Middleware;
using Lykke.Common.ApiLibrary.Swagger;
using Lykke.Common.Log;
using Lykke.Logs;
using Lykke.Logs.Loggers.LykkeSlack;
using Lykke.Service.Decred.Api.Common;
using Lykke.Service.Decred.Api.Common.Entity;
using Lykke.Service.Decred.Api.Common.Services;
using Lykke.Service.Decred.Api.Middleware;
using Lykke.Service.Decred.Api.Repository;
using Lykke.Service.Decred.Api.Services;
using Lykke.SettingsReader;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using NDecred.Common;
using Newtonsoft.Json.Serialization;
using Npgsql;
using Lykke.MonitoringServiceApiCaller;
using Lykke.Service.Decred.Api.Repository.SpentOutputs;
using Lykke.Service.Decred.Api.Workflow.PeriodicalHandlers;

namespace Lykke.Service.Decred.Api
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; }
        public IHostingEnvironment Environment { get; }

        private IHealthNotifier _healthNotifier;
        private RemoveOldSpentOutputsPeriodicalHandler _removeOldSpentOutputsPeriodicalHandler;
        private string _monitoringServiceUrl;

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
            Environment = env;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<AppSettings>(Configuration);
            var reloadableSettings = Configuration.LoadSettings<AppSettings>();
            if (reloadableSettings.CurrentValue.MonitoringServiceClient != null)
                _monitoringServiceUrl = reloadableSettings.CurrentValue.MonitoringServiceClient.MonitoringServiceUrl;
            services.AddMvc()
                .AddJsonOptions(options =>
                {
                    options.SerializerSettings.SerializationBinder = new DefaultSerializationBinder();
                    options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
                });

            services.AddSwaggerGen(options =>
            {
                options.DefaultLykkeConfiguration("v1", "Decred API");
            });
            services.AddLykkeLogging(
                reloadableSettings.ConnectionString(s => s.ServiceSettings.Db.LogsConnString),
                "DecredApiLog",
                reloadableSettings.CurrentValue.SlackNotifications.AzureQueue.ConnectionString,
                reloadableSettings.CurrentValue.SlackNotifications.AzureQueue.QueueName,
                logging =>
                {
                    logging.AddAdditionalSlackChannel("BlockChainIntegration", channelOptions =>
                    {
                        channelOptions.MinLogLevel = Microsoft.Extensions.Logging.LogLevel.Information;
                        channelOptions.SpamGuard.DisableGuarding();
                        channelOptions.IncludeHealthNotifications();
                    });

                    logging.AddAdditionalSlackChannel("BlockChainIntegrationImportantMessages", channelOptions =>
                    {
                        channelOptions.MinLogLevel = Microsoft.Extensions.Logging.LogLevel.Warning;
                        channelOptions.SpamGuard.DisableGuarding();
                        channelOptions.IncludeHealthNotifications();
                    });
                });

            RegisterRepositories(reloadableSettings, services);

            // Register network dependency
            services.AddTransient(p =>
            {
                var networkType = reloadableSettings.CurrentValue.ServiceSettings.NetworkType.Trim().ToLower();
                var name =
                    networkType == "test" ? "testnet" :
                    networkType == "main" ? "mainnet" :
                    throw new Exception($"Unrecognized network type '{networkType}'");
                return Network.ByName(name);
            });

            services.AddHttpClient();

            services.Configure<HttpClientFactoryOptions>(opt =>
            {
                opt.HttpMessageHandlerBuilderActions.Add(builder =>
                {
                    var settings = reloadableSettings.CurrentValue;
                    var handler = (HttpClientHandler) builder.PrimaryHandler;
                    handler.Credentials = new NetworkCredential(
                        settings.ServiceSettings.Dcrd.RpcUser,
                        settings.ServiceSettings.Dcrd.RpcPass);
                });
            });

            services.AddTransient(p => new DcrdHttpConfig
            {
                ApiUrl = reloadableSettings.CurrentValue.ServiceSettings.Dcrd.RpcEndpoint,
                MinConfirmations = reloadableSettings.CurrentValue.ServiceSettings.ConfirmationDepth
            });

            services.AddTransient<IDcrdClient, DcrdHttpClient>();

            services.AddTransient<IReloadingManager<AppSettings>>(p => reloadableSettings);
            services.AddTransient<HttpClient>();
            services.AddTransient<TransactionHistoryService>();
            services.AddTransient<IHealthService, HealthService>();
            services.AddTransient<ITransactionBuilder, TransactionBuilder>();
            services.AddTransient<IUnsignedTransactionService, UnsignedTransactionService>();
            services.AddTransient<ITransactionFeeService, TransactionFeeService>();
            services.AddTransient<ITransactionBroadcastService, TransactionBroadcastService>();
            services.AddTransient<IAddressValidationService, AddressValidationService>();
            services.AddTransient<BalanceService>();

            services.AddSingleton(e =>
                new RemoveOldSpentOutputsPeriodicalHandler(e.GetService<ILogFactory>(),
                    reloadableSettings.CurrentValue.ServiceSettings.SpentOutputsExpirationTimerPeriod,
                    reloadableSettings.CurrentValue.ServiceSettings.SpentOutputsExpiration,
                    e.GetService<ISpentOutputRepository>()));
        }

        private void RegisterRepositories(IReloadingManager<AppSettings> config, IServiceCollection services)
        {
            // Wire up azure connections
            var connectionString = config.ConnectionString(a => a.ServiceSettings.Db.Azure);
            
            services.AddTransient
               <INosqlRepo<ObservableWalletEntity>, AzureRepo<ObservableWalletEntity>>(e =>
                    new AzureRepo<ObservableWalletEntity>(
                        AzureTableStorage<ObservableWalletEntity>.Create(connectionString, "ObservableWallet", e.GetService<ILogFactory>())
                    ));

            services.AddTransient
                <INosqlRepo<ObservableAddressEntity>, AzureRepo<ObservableAddressEntity>>(e =>
                    new AzureRepo<ObservableAddressEntity>(
                        AzureTableStorage<ObservableAddressEntity>.Create(connectionString, "ObservableAddress", e.GetService<ILogFactory>())
                    ));

            services.AddTransient
                <INosqlRepo<UnsignedTransactionEntity>, AzureRepo<UnsignedTransactionEntity>>(e =>
                    new AzureRepo<UnsignedTransactionEntity>(
                        AzureTableStorage<UnsignedTransactionEntity>.Create(connectionString, "UnsignedTransactionEntity", e.GetService<ILogFactory>())
                    ));

            services.AddTransient
                <INosqlRepo<BroadcastedTransactionByHash>, AzureRepo<BroadcastedTransactionByHash>>(e =>
                    new AzureRepo<BroadcastedTransactionByHash>(
                        AzureTableStorage<BroadcastedTransactionByHash>.Create(connectionString, "BroadcastedTransactionByHash", e.GetService<ILogFactory>())
                    ));

            services.AddTransient
                <INosqlRepo<BroadcastedTransaction>, AzureRepo<BroadcastedTransaction>>(e =>
                    new AzureRepo<BroadcastedTransaction>(
                        AzureTableStorage<BroadcastedTransaction>.Create(connectionString, "BroadcastedTransaction", e.GetService<ILogFactory>())
                    ));

            services.AddSingleton
                <ISpentOutputRepository>(e => 
                    new SpentOutputRepository(
                        AzureTableStorage<SpentOutputEntity>.Create(connectionString, "SpentOutputs", e.GetService<ILogFactory>())
                    ));

            services.AddScoped<IDbConnection, NpgsqlConnection>((p) =>
            {
                var dcrdataConnectionString = config.CurrentValue.ServiceSettings.Db.Dcrdata;
                var sqlClient = new NpgsqlConnection(dcrdataConnectionString);
                sqlClient.Open();
                return sqlClient;
            });

            services.AddTransient<IBlockRepository, BlockRepository>();
            services.AddTransient<IAddressRepository, AddressRepository>();
            services.AddTransient<ITransactionRepository, TransactionRepository>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime appLifetime)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            _healthNotifier = app.ApplicationServices.GetService<IHealthNotifier>();
            _removeOldSpentOutputsPeriodicalHandler = app.ApplicationServices.GetService<RemoveOldSpentOutputsPeriodicalHandler>();
            app.UseMiddleware(typeof(ApiErrorHandler));
            app.UseLykkeForwardedHeaders();
            app.UseMvc();
            app.UseSwagger();
            app.UseSwaggerUI(x =>
            {
                x.RoutePrefix = "swagger/ui";
                x.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
            });
            app.UseStaticFiles();

            appLifetime.ApplicationStarted.Register(() => StartApplication().GetAwaiter().GetResult());
            appLifetime.ApplicationStopped.Register(() => CleanUp().GetAwaiter().GetResult());
        }

        private async Task StartApplication()
        {
#if !DEBUG
            await Configuration.RegisterInMonitoringServiceAsync(_monitoringServiceUrl, _healthNotifier);
#endif
            _removeOldSpentOutputsPeriodicalHandler.Start();

            _healthNotifier.Notify("Started");
        }
        

        private Task CleanUp()
        {
            _healthNotifier?.Notify("Terminating");

            _removeOldSpentOutputsPeriodicalHandler.Stop();

            return Task.CompletedTask;
        }
        
    }
}
