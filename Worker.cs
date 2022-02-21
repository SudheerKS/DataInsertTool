using Common.Config;
using Common.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataInsertTool
{
    class Worker : BackgroundService
    {

        private static readonly IConfigurationRoot Configuration;
        private static DbConfig _dbConfig;

        static Worker()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            Configuration = builder.Build();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddOptions();

            _dbConfig = GetDbConfig();

            serviceCollection.Configure<DbConfig>(config =>
            {
                config.ConnectionString = _dbConfig.ConnectionString;
                config.Database = _dbConfig.Database;
            });

            var serilogLogger = new LoggerConfiguration()
                    .WriteTo.File(Configuration["LogFilesStorage"] + @"/" + string.Format("log_{0}.txt", DateTime.Now.ToString("yyyy-dd-M")))

              .CreateLogger();
            serviceCollection.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddSerilog(logger: serilogLogger, dispose: true);
            });

            serviceCollection.Configure<AppSettings>(appSettings =>
            {
                appSettings.DocumentsStorage = Configuration["DocumentsStorage"];
                appSettings.OtherSettings = Configuration.GetSection("AppSettings:OtherSettings")
                .GetChildren().ToDictionary(x => x.Key, x => x.Value);
            });

            serviceCollection.AddScoped<AppSettings>();
            serviceCollection.AddScoped<IService, Service>();
            serviceCollection.AddScoped<DbConfig>();
            IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            var service = serviceProvider.GetService<IService>();
            await service.Process();

        }

        public static DbConfig GetDbConfig()
        {
            var dbConfig = Configuration.GetSection("DatabaseSettings").Get<DbConfig>();
            var encPassword = string.Empty;

            if (dbConfig.DataProvider.ToLower().Trim() == "mongodb")
            {
                var endIndex = dbConfig.ConnectionString.IndexOf("@");
                var startIndex = dbConfig.ConnectionString.LastIndexOf(":", endIndex) + 1;
                encPassword = dbConfig.ConnectionString[startIndex..endIndex];
            }
            else
                encPassword = dbConfig.ConnectionString.Split(';')[3].Substring(10);

            var password = SecurityHelper.DecryptWithEmbedKey(encPassword, 15);
            dbConfig.ConnectionString = dbConfig.ConnectionString.Replace(encPassword, password);
            return dbConfig;
        }


    }
}
