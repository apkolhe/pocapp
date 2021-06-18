using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PoCAppRestApi.Models;
using Microsoft.Azure.Cosmos.Table;
using System.Collections.Generic;
using System.Linq;

namespace PoCAppRestApi
{
    public static class PoCApi
    {
        private static readonly string tableName = "Tutorials";
        private static readonly string storageConnectionString = Environment.GetEnvironmentVariable("StorageConnectionString");

        // Retrieve storage account information from connection string.
        static CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);

        // Create a table client for interacting with the table service
        static CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());

        // Create a table client for interacting with the table service 
        static CloudTable table = tableClient.GetTableReference(tableName);

        [FunctionName("GetTutorials")]
        public static async Task<IActionResult> GetTutorials([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tutorials")] HttpRequest req, ILogger log)
        {
            log.LogInformation("Get All Tutorials");

            var result = await RetrieveOperation();

            var tutorials = result.Item2.Where(x => CheckIfInt(x.RowKey) == true);

            return tutorials != null ? (ActionResult)new OkObjectResult(tutorials) : new NotFoundObjectResult(new { message = "No Tutorials Found" });
        }

        private static bool CheckIfInt(string rowKey)
        {
            bool isNumeric = int.TryParse(rowKey, out _);
            return isNumeric;
        }

        [FunctionName("GetTutorialsById")]
        public static async Task<IActionResult> GetTutorial([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tutorials/{id}")] HttpRequest req, string id, ILogger log)
        {
            log.LogInformation("Get All Tutorial by id");

            var tutorials = await RetrieveOperation(id);

            return tutorials != null ? (ActionResult)new OkObjectResult(tutorials.Item2) : new NotFoundObjectResult(new { message = $"No Tutorial Found with id - {id}" });

        }

        [FunctionName("CreateTutorials")]
        public static async Task<IActionResult> Create([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tutorials")] HttpRequest req, ILogger log)
        {
            log.LogInformation("Add a Tutorial");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (requestBody == null)
            {
                throw new ArgumentNullException("Empty Tutorial");
            }

            var tutorial = JsonConvert.DeserializeObject<Tutorial>(requestBody);
            tutorial.PartitionKey = "Tutorial";
            var uniqueId = GetNextId();
            tutorial.UniqueId = uniqueId;
            tutorial.RowKey = uniqueId.ToString(); // save entity with id

            var successWithId = await InsertOrMergeOperation(tutorial);

            tutorial.RowKey = tutorial.Title; // save entity with title

            var successWithTitle = await InsertOrMergeOperation(tutorial);

            if (successWithId.Item1 && successWithTitle.Item1)
            {
                return (ActionResult)new OkObjectResult(successWithId.Item2);
            }
            else
            {
                return new BadRequestObjectResult(new { message = $"Failed to add Tutorial" });
            }

        }

        [FunctionName("UpdateTutorials")]
        public static async Task<IActionResult> Update([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "tutorials/{id}")] HttpRequest req, string id, ILogger log)
        {
            log.LogInformation("Update a Tutorial info.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updateTutorial = JsonConvert.DeserializeObject<Tutorial>(requestBody);

            try
            {
                TableOperation retrieveOperation = TableOperation.Retrieve<Tutorial>("Tutorial", updateTutorial.UniqueId.ToString());
                TableResult result = await table.ExecuteAsync(retrieveOperation);
                Tutorial tutorial = result.Result as Tutorial;

                if (tutorial == null)
                {
                    return new NotFoundObjectResult(new { message = "Tutorial Not Found" });
                }
                else
                {
                    tutorial.Title = updateTutorial.Title;
                    tutorial.Description = updateTutorial.Description;
                    tutorial.Published = updateTutorial.Published;
                }

                var successWithId = await InsertOrMergeOperation(tutorial);

                if (successWithId.Item1)
                {
                    return (ActionResult)new OkObjectResult(successWithId.Item2);
                }
                else
                {
                    return new BadRequestObjectResult(new { message = $"Failed to add Tutorial" });
                }
            }
            catch (StorageException e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                throw;
            }
        }

        [FunctionName("DeleteTutorialsById")]
        public static async Task<IActionResult> DeleteById([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tutorials/{id}")] HttpRequest req, string id, ILogger log)
        {
            log.LogInformation("Delete Tutorial by Id");

            TableOperation retrieveOperation = TableOperation.Retrieve<Tutorial>("Tutorial", id.ToString());
            TableResult response = await table.ExecuteAsync(retrieveOperation);
            Tutorial deleteTutorial = response.Result as Tutorial;

            deleteTutorial.PartitionKey = "Tutorial";
            deleteTutorial.RowKey = id;
            

            try
            {
                TableOperation deleteOperation = TableOperation.Delete(deleteTutorial);
                TableResult result = await table.ExecuteAsync(deleteOperation);

                return new OkResult();                
                
            }
            catch (StorageException e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                throw;
            }
        }

        [FunctionName("DeleteTutorials")]
        public static async Task<IActionResult> Delete([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tutorials")] HttpRequest req, ILogger log)
        {
            log.LogInformation("Delete all Tutorials");
                        
            var query = new TableQuery<Tutorial>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "Tutorial")).Select(new[] {"RowKey"});
            var entities = table.ExecuteQuery(query).ToList();
            var offset = 0;

            while (offset < entities.Count)
            {
                TableBatchOperation batchOperation = new TableBatchOperation();
                var rows = entities.Skip(offset).Take(100).ToList();

                foreach (var row in rows)
                {
                    batchOperation.Delete(row);
                }

                table.ExecuteBatch(batchOperation);
                offset += rows.Count;
            }

            return new OkResult();
        }

        [FunctionName("SearchTutorials")]
        public static async Task<IActionResult> Search([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tutorials/search/{title?}")] HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string title = req.Query["title"];

            var tutorials = await RetrieveOperation(title);

            return tutorials != null ? (ActionResult)new OkObjectResult(tutorials.Item2) : new NotFoundObjectResult(new { message = $"No Tutorial Found with title - {title}" });
        }

        private static async Task<Tuple<bool, Tutorial>> InsertOrMergeOperation(Tutorial tutorial)
        {
            try
            {
                // Create the InsertOrReplace table operation
                TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(tutorial);

                // Execute the operation.
                TableResult result = await table.ExecuteAsync(insertOrMergeOperation);
                Tutorial insertedTutorial = result.Result as Tutorial;

                if (result.RequestCharge.HasValue)
                {
                    Console.WriteLine("Request Charge of InsertOrMerge Operation: " + result.RequestCharge);
                }

                return new Tuple<bool, Tutorial>(true, insertedTutorial);
            }
            catch (StorageException e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                throw;
            }

        }

        private static async Task<Tuple<bool, List<Tutorial>>> RetrieveOperation(string rowKey = "default")
        {
            try
            {
                TableContinuationToken token = null;
                var tutorials = new List<Tutorial>();
                TableQuery<Tutorial> query;

                if (rowKey != "default")
                {
                    query = new TableQuery<Tutorial>().Where(TableQuery.CombineFilters(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "Tutorial"), TableOperators.And, TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, rowKey)));
                }
                else
                {
                    query = new TableQuery<Tutorial>();
                }

                do
                {
                    var queryResult = await table.ExecuteQuerySegmentedAsync(query, token);
                    tutorials.AddRange(queryResult.Results);

                } while (token != null);

                return new Tuple<bool, List<Tutorial>>(true, tutorials);
            }
            catch (StorageException e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                throw;
            }

        }

        private static int GetNextId()
        {
            var query = new TableQuery<Tutorial>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "Tutorial")).Select(new[] { "RowKey" });
            var entities = table.ExecuteQuery(query).ToList();

            var maxId = entities.Where(x => CheckIfInt(x.RowKey) == true).Max(x => x.RowKey);

            return maxId == null ? 1 : Convert.ToInt32(maxId) + 1;
        }
    }
}
