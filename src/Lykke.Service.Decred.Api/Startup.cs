using System;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using AzureStorage.Tables;
using Common.Log;
using DcrdClient;
using Decred.BlockExplorer;
using Lykke.Common.ApiLibrary.Middleware;
using Lykke.Logs;
using Lykke.Service.Decred.Api.Common;
using Lykke.Service.Decred.Api.Common.Entity;
using Lykke.Service.Decred.Api.Common.Services;
using Lykke.Service.Decred.Api.Middleware;
using Lykke.Service.Decred.Api.Repository;
using Lykke.Service.Decred.Api.Services;
using Lykke.SettingsReader;
using Lykke.SlackNotification.AzureQueue;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NDecred.Common;
using Newtonsoft.Json.Serialization;
using Npgsql;

namespace Lykke.Service.Decred.Api
{
    public class Startup
    {
        private ILog _log;
        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

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

            services.AddMvc()
                .AddJsonOptions(options =>
                {
                    options.SerializerSettings.SerializationBinder = new DefaultSerializationBinder();
                    options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
                });

            _log = CreateLogWithSlack(reloadableSettings, services);
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
            
            services.AddTransient<IDcrdClient, DcrdHttpClient>(s =>
            {
                var settings = reloadableSettings.CurrentValue;
                var minConfirmations = settings.ServiceSettings.ConfirmationDepth;
                return new DcrdHttpClient(
                    settings.ServiceSettings.Dcrd.RpcEndpoint,
                    new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                        Credentials = new NetworkCredential(
                            settings.ServiceSettings.Dcrd.RpcUser,
                            settings.ServiceSettings.Dcrd.RpcPass)
                    }, minConfirmations);
            });
            
            services.AddSingleton(p => _log);
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
        }

        private void RegisterRepositories(IReloadingManager<AppSettings> config, IServiceCollection services)
        {
            var consoleLogger = new LogToConsole();

            // Wire up azure connections            
            var connectionString = config.ConnectionString(a => a.ServiceSettings.Db.Azure);
            
            services.AddTransient
               <INosqlRepo<ObservableWalletEntity>, AzureRepo<ObservableWalletEntity>>(e => 
                    new AzureRepo<ObservableWalletEntity>(
                        AzureTableStorage<ObservableWalletEntity>.Create(connectionString, "ObservableWallet", consoleLogger)
                    ));
            
            services.AddTransient
                <INosqlRepo<ObservableAddressEntity>, AzureRepo<ObservableAddressEntity>>(e => 
                    new AzureRepo<ObservableAddressEntity>(
                        AzureTableStorage<ObservableAddressEntity>.Create(connectionString, "ObservableAddress", consoleLogger)
                    ));

            services.AddTransient
                <INosqlRepo<UnsignedTransactionEntity>, AzureRepo<UnsignedTransactionEntity>>(e => 
                    new AzureRepo<UnsignedTransactionEntity>(
                        AzureTableStorage<UnsignedTransactionEntity>.Create(connectionString, "UnsignedTransactionEntity", consoleLogger)
                    ));

            services.AddTransient
                <INosqlRepo<BroadcastedTransactionByHash>, AzureRepo<BroadcastedTransactionByHash>>(e => 
                    new AzureRepo<BroadcastedTransactionByHash>(
                        AzureTableStorage<BroadcastedTransactionByHash>.Create(connectionString, "BroadcastedTransactionByHash", consoleLogger)
                    ));

            services.AddTransient
                <INosqlRepo<BroadcastedTransaction>, AzureRepo<BroadcastedTransaction>>(e => 
                    new AzureRepo<BroadcastedTransaction>(
                        AzureTableStorage<BroadcastedTransaction>.Create(connectionString, "BroadcastedTransaction", consoleLogger)
                    ));

            services.AddTransient
                <INosqlRepo<BroadcastedOutpoint>, AzureRepo<BroadcastedOutpoint>>(e => 
                    new AzureRepo<BroadcastedOutpoint>(
                        AzureTableStorage<BroadcastedOutpoint>.Create(connectionString, "BroadcastedOutpoint", consoleLogger)
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

            app.UseMiddleware(typeof(ApiErrorHandler));
            app.UseLykkeForwardedHeaders();
            app.UseMvc();
            
            appLifetime.ApplicationStarted.Register(() => StartApplication().GetAwaiter().GetResult());
            appLifetime.ApplicationStopping.Register(() => StopApplication().GetAwaiter().GetResult());
            appLifetime.ApplicationStopped.Register(() => CleanUp().GetAwaiter().GetResult());
        }
        
        private async Task StartApplication()
        {
            await _log.WriteMonitorAsync("", $"Env: {Program.EnvInfo}", "Started");
        }

        private async Task StopApplication()
        {
            await _log.WriteMonitorAsync("", $"Env: {Program.EnvInfo}", "Stopped");
        }

        private async Task CleanUp()
        {
            await _log.WriteMonitorAsync("", $"Env: {Program.EnvInfo}", "Terminating");
        }

        private static ILog CreateLogWithSlack(IReloadingManager<AppSettings> settings, IServiceCollection services)
        {
            var consoleLogger = new LogToConsole();
            var aggregateLogger = new AggregateLogger();

            aggregateLogger.AddLog(consoleLogger);

            var dbLogConnectionStringManager = settings.Nested(x => x.ServiceSettings.Db.LogsConnString);
            var dbLogConnectionString = dbLogConnectionStringManager.CurrentValue;

            if (string.IsNullOrEmpty(dbLogConnectionString))
            {
                consoleLogger.WriteWarningAsync(nameof(Startup), nameof(CreateLogWithSlack), "Table loggger is not inited").Wait();
                return aggregateLogger;
            }

            if (dbLogConnectionString.StartsWith("${") && dbLogConnectionString.EndsWith("}"))
                throw new InvalidOperationException($"LogsConnString {dbLogConnectionString} is not filled in settings");

            var persistenceManager = new LykkeLogToAzureStoragePersistenceManager(
                AzureTableStorage<LogEntity>.Create(dbLogConnectionStringManager, "LykkeServiceLog", consoleLogger),
                consoleLogger);

            // Creating slack notification service, which logs own azure queue processing messages to aggregate log
            var slackService = services.UseSlackNotificationsSenderViaAzureQueue(new AzureQueueIntegration.AzureQueueSettings
            {
                ConnectionString = settings.CurrentValue.SlackNotifications.AzureQueue.ConnectionString,
                QueueName = settings.CurrentValue.SlackNotifications.AzureQueue.QueueName
            }, aggregateLogger);

            var slackNotificationsManager = new LykkeLogToAzureSlackNotificationsManager(slackService, consoleLogger);

            // Creating azure storage logger, which logs own messages to concole log
            var azureStorageLogger = new LykkeLogToAzureStorage(
                persistenceManager,
                slackNotificationsManager,
                consoleLogger);

            azureStorageLogger.Start();

            aggregateLogger.AddLog(azureStorageLogger);

            return aggregateLogger;
        }
    }
}
