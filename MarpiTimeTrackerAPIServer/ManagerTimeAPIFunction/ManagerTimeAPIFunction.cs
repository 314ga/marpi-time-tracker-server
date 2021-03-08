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

namespace ManagerTimeAPIFunction
{
    public static class ManagerTimeAPIFunction
    {
        [FunctionName("ManagerTimeAPIFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "ManagerTimeAPI/{request}/{workerID}/{param1}/{param2}/{param3}/{param4}/{param5}/{param6}")] HttpRequest req,
            string request, int workerID, string param1, string param2, string param3, string param4, string param5, string param6, ILogger log)
        {

            string responseMessage = "";
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            if (!string.IsNullOrEmpty(request))
            {
                var str = Environment.GetEnvironmentVariable("jdbc_timeDB_connection");
                using (SqlConnection conn = new SqlConnection(str))
                {
                    var text = "";
                    conn.Open();
                    switch (request)
                    {
                        case "fix-worker-zone":
                            {
                                text = "DECLARE @workday_ID AS int = (SELECT ID_work_day FROM workdays WHERE ID_worker = " + workerID + " AND date_present = '"+param1+"') " +
                                       " UPDATE workzone_workday SET ID_work_zone = '"+param2+"', start_time = '"+param3+"', " +
                                       "end_time = '"+param4 + "' WHERE ID_work_day = @workday_ID AND start_time = '"+param5+ "' AND end_time = '" + param6 + "';";
                                break;
                            }
                        case "fix-break":
                            {
                                text = "DECLARE @workday_ID AS int = (SELECT ID_work_day FROM workdays WHERE ID_worker = " + workerID + " AND date_present = '" + param1 + "') " +
                                       " UPDATE breaks SET break_start = '" + param2 + "', break_end = '" + param3 + "'" +
                                       " WHERE ID_work_day = @workday_ID AND break_start = '" + param5 + "' AND break_end = '" + param6 + "';";
                                break;
                            }
                        case "fix-day":
                            {
                                text = "UPDATE workdays SET end_time = '"+param1+"' WHERE ID_worker = "+workerID+" AND start_time = '"+param2+"';";
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
