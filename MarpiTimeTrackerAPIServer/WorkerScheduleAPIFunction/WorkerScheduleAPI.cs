using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Data.SqlClient;
using System.Data;

namespace WorkerScheduleAPIFunction
{
    public static class WorkerScheduleAPI
    {
        [FunctionName("WorkerScheduleAPI")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "WorkerScheduleAPI/{request}/{workerID}/{startDate}/{endDate}")] HttpRequest req,
            string request, int workerID, string startDate, string endDate, ILogger log)
        {

            string responseMessage = "";
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            if (!string.IsNullOrEmpty(request))
            {
                var str = Environment.GetEnvironmentVariable("jdbc_scheduleDB_connection");
                using (SqlConnection conn = new SqlConnection(str))
                {
                    var text = "";
                    conn.Open();
                    switch (request)
                    {
                        case "get-worker-zone":
                            {
                                text = "SELECT zone_name FROM workzones INNER JOIN workzones_workers ON workzones.ID_work_zone ="+
                                    "workzones_workers.ID_work_zone AND workzones_workers.ID_workers =" + workerID + " FOR JSON PATH; ";
                                break;
                            }
                        case "get-worker-schedule":
                            {
                                text = "SELECT schedule.time_start, schedule.time_end, workerstates.worker_state FROM schedule "+
                                    "INNER JOIN workerstates ON workerstates.ID_worker_state = schedule.ID_worker_state AND "+
                                    "schedule.ID_workers =" + workerID + " AND schedule.time_start >= '" + startDate + "' AND schedule.time_start <= '"+ endDate + "'  FOR JSON PATH; ";
                                break;
                            }
                        default:
                            {
                                text = "error";
                                break;
                            }

                    }
                    if (text != "error" || text != "")
                    {
                        using (SqlCommand cmd = new SqlCommand(text, conn))
                        {
                            SqlDataReader reader = await cmd.ExecuteReaderAsync();
                            // Execute the command and log the # rows affected.
                            while (reader.Read())
                            {
                                IDataRecord result = (IDataRecord)reader;
                                responseMessage += String.Format("{0}", result[0]);
                            }
                            // Call Close when done reading.
                            reader.Close();

                        }
                    }
                    else
                    {
                        return new NotFoundObjectResult(request);
                    }

                }
                return new OkObjectResult(responseMessage);
            }
            else
            {
                return new NotFoundObjectResult(request);
            }
            // Get the connection string from app settings and use it to create a connection.

        }
    }
}
