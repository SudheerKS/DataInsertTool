using Common.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSS.Customers.Model;
using MongoDB.Bson;
using DSS.Customers;

namespace DataInsertTool
{
    public interface IService
    {
        Task Process();
    }

    public class Service : IService
    {
        private readonly DbConfig _dbConfig;
        private readonly AppSettings _appSettings;
        private readonly Config _Config;
        private readonly ILogger _logger;
        public Service(ILogger<Service> logger, IOptions<DbConfig> dbconfig, IOptions<Config> config,IOptions<AppSettings> appSettings)
        {
            _logger = logger;
            _dbConfig = dbconfig.Value;
            _Config = config.Value;
            _appSettings = appSettings.Value;
        }

        public async Task Process()
        {
            if (!Directory.Exists(_appSettings.DocumentsStorage)) return;
            string textFilePath = Directory.EnumerateFiles(_appSettings.DocumentsStorage, "*.txt").FirstOrDefault();

            Console.WriteLine($"Process Started  for {Path.GetFileNameWithoutExtension(textFilePath)}");
            _logger?.LogInformation($"Processing Started  for {Path.GetFileNameWithoutExtension(textFilePath)}");
            var lines = File.ReadLines(textFilePath);
            int linescount = lines.Count();
            var batchSize = Convert.ToInt32(_appSettings.OtherSettings["BillProcessorBatchSize"]);
            List<Customer> lstCustomer = new List<Customer>();
            var customerService = new CustomerService(_dbConfig);
            long totalDBCount = 0;
            for (int i = 0; i < linescount; i += batchSize)
            {
                try
                {
                    var batchlines = lines.Skip(i).Take(batchSize);
                    Parallel.ForEach(batchlines, (line) =>
                    {
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(line) || !string.IsNullOrEmpty(line))
                            {
                                string[] fields = line.Split("|");

                                //lstCustomer.Add(new Customer()
                                //{
                                //    Id = ObjectId.GenerateNewId().ToString(),
                                //    MobileNumber = "",
                                //    AccountNumber = (fields[0] ?? "").Trim(),
                                //    BillingEmailId = (fields[1] ?? "").Trim(),
                                //    ContactEmailId = (fields[2] ?? "").Trim(),
                                //    ContactId = (fields[3] ?? "").Trim(),
                                //    Status = "Active",
                                //    IsDeleted = false
                                //});

                                Customer customer = new Customer()
                                {
                                    Id = ObjectId.GenerateNewId().ToString(),
                                    MobileNumber = "",
                                    AccountNumber = (fields[0] ?? "").Trim(),
                                    BillingEmailId = (fields[1] ?? "").Trim(),
                                    ContactEmailId = (fields[2] ?? "").Trim(),
                                    ContactId = (fields[3] ?? "").Trim(),
                                    Status = "Active",
                                    IsDeleted = false
                                };
                                customerService.SaveCustomer(customer).Wait();
                                totalDBCount += 1;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error " + line);
                        }
                    });

                    //if (lstCustomer.Count > 0)
                    //{
                    //    customerService.SaveCustomers(lstCustomer).Wait();
                    //    totalDBCount += lstCustomer.Count;
                    //}
                }

                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error");
                }
            }
            _logger?.LogInformation($"total {totalDBCount} files saved in DB");
            Console.WriteLine($"Process Finished");
            _logger?.LogInformation($"Process Finished");
            await Task.Delay(500);
        }
    }
}
