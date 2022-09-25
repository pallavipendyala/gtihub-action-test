using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using System.Collections.Generic;
using System.Diagnostics;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage.Auth;
using TableEntity = Microsoft.WindowsAzure.Storage.Table.TableEntity;


namespace Company.Function
{
    class MyEntity : TableEntity
    {
        public MyEntity() { }
        public MyEntity(string Fileformat, string FileRange)
        {
            this.PartitionKey = Fileformat;
            this.RowKey = FileRange;
        }
        public String auto_user { get; set; }
        public String auto_version { get; set; }
        public DateTime auto_start_time { get; set; }
        public int city_flag { get; set; }
        public int day_flag { get; set; }
        public int RHDF { get; set; }
        public String e_user { get; set; }
        public String e_version { get; set; }
        public String e_task_status { get; set; }
        public int e_error_code { get; set; }
        public String e_output_location { get; set; }
        public String e_error_string { get; set; }
        public DateTime e_last_update { get; set; }
        public String w_task_status { get; set; }
        public String w_output_location { get; set; }
        public int w_error_code { get; set; }
        public String w_error_string { get; set; }
        public DateTime w_last_update { get; set; }
        public String l_task_status { get; set; }
        public String l_output_location { get; set; }
        public int l_error_code { get; set; }
        public String l_error_string { get; set; }
        public DateTime l_last_update { get; set; }
        public String c_task_status { get; set; }
        public String c_output_location { get; set; }
        public int c_error_code { get; set; }
        public String c_error_string { get; set; }
        public DateTime c_last_update { get; set; }
    }

    public class CreateCPUTaskTimerTrigger
    {
        [FunctionName("CreateCPUTaskTimerTrigger")]
        public void Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
       // public static void Run()
        {
            // {second} {minute} {hour} {day} {month} {day-of-week}
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            string BatchAccountUrl = "https://autolabellinggpubatch.westeurope.batch.azure.com";
            string BatchAccountName = "autolabellinggpubatch";  
            string BatchAccountKey = "4zfp4Ip8igqpzA+2MschhBgH1IBzsoNz9MKuSFDo+xkDmEeh8KJ8snHC0Wri6YVb/ZVdLlrSlOHq+ABauSGUCg==";

            string AzureDataTableName = "ProdAutoLabellingAutomation";
            string CPUJobID = "Testing_Windows_CPU_Job";
            string queueMesssageSeperator = "$";
            string queueClientConnectionString = "DefaultEndpointsProtocol=https;AccountName=cloudautomations;AccountKey=TBzKz4+8R4RkVHAMKJyI76nEMibVE/JK5nTB0W5L/VN7eZTROauqkRMyomKpv8lJqQNbJ8M2XRNf+ASt8s9omA==;EndpointSuffix=core.windows.net";
            string queueName = "prod-auto-labelling-automation-queue";
            System.Int32 MAX_MESSAGES = 32;

            QueueClient queueClient = new QueueClient(queueClientConnectionString, queueName);

            if(queueClient.Exists())
            {
                QueueMessage[] receivedMessages = queueClient.ReceiveMessages(MAX_MESSAGES);
                
                if(receivedMessages.Length > 0)
                {
                    log.LogInformation($"Number of received messages : {receivedMessages.Length}");

                    BatchSharedKeyCredentials credentials = new BatchSharedKeyCredentials(BatchAccountUrl, BatchAccountName, BatchAccountKey);

                    List<CloudTask> tasks = new List<CloudTask>();

                    foreach (QueueMessage item in receivedMessages)
                    {
                        string receivedMessage = item.Body.ToString();
                        // var base64EncodedBytes = System.Convert.FromBase64String0(receivedMessageBase64);
                        // string receivedMessage = System.Text.Encoding.UTF8.GetString(base64EncodedBytes);

                        var storageAccount = CloudStorageAccount.Parse(queueClientConnectionString);
                        CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

                        CloudTable table = tableClient.GetTableReference(AzureDataTableName);
                        //var All_entities = table.ExecuteQuery(new TableQuery<MyEntity>()).ToList();  // all table entities to list
                        try
                        {
                            string[] messageElements = receivedMessage.Split(queueMesssageSeperator);

                            string cpuTaskID = messageElements[0];
                            string cpuTaskCmd = messageElements[1];   

                            int range_index = cpuTaskCmd.IndexOf("-er");
                            int file_index = cpuTaskCmd.IndexOf("-ef");
                            var Range_data = (cpuTaskCmd.Substring(range_index, 20)).Split(' ');
                            var file_format = (cpuTaskCmd.Substring(file_index, 50)).Split(' ');
                            if(Range_data[1].Contains('_'))
                            {
                                var range_values = Range_data[1].Split('-');
                                var upper_range =range_values[0].Split('_');
                                var Lower_range =range_values[1].Split('_');
                                Range_data[1] = upper_range[0] +"-" + Lower_range[0];
                            }
                    
                            var file_range = Range_data[1];
                            string partitionFilter = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, file_format[1]);
                            string rowKey_filter = TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, file_range);
                            string finalFilter = TableQuery.CombineFilters(partitionFilter, TableOperators.And, rowKey_filter);
                            TableQuery<MyEntity> query1 = new TableQuery<MyEntity>().Where(finalFilter);
                            var Entity1 = table.ExecuteQuerySegmentedAsync(query1,null);
                            //MyEntity en= null;
                            foreach (MyEntity en in Entity1.Result)
                            {
                                CloudTask task = new CloudTask(cpuTaskID,cpuTaskCmd); 
                                tasks.Add(task);

                                en.c_task_status = "Active";

                                var Res = table.ExecuteAsync(TableOperation.Replace(en));

                            }
                        
                            log.LogInformation($"Received Message is : {receivedMessage}");

                            queueClient.DeleteMessage(item.MessageId,item.PopReceipt);
                        }
                        catch (System.Exception)
                        {
                            log.LogInformation($"Received Message is : {receivedMessage}");
                            log.LogInformation("Received message is in incorrect format.");
                        }
                    }

                    if(tasks.Count > 0)
                    {
                        using(BatchClient batchClient = BatchClient.Open(credentials))
                        {
                            log.LogInformation($"Adding the tasks to CPU Job ID : {CPUJobID}");  
                            batchClient.JobOperations.AddTask(CPUJobID,tasks);
                        }
                    }
                    else
                    {
                        log.LogInformation("No tasks added to the CPU Job ID.");
                    }
                }
                else
                {
                    log.LogInformation("There are no messages in the Queue.");
                }
            }
            else
            {
                log.LogInformation("Quere Client doesn't exists.");
            }

        }
    }

    
}

