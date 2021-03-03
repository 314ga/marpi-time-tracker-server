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

namespace ManagerScheduleAPIFunction
{
    public static class ManagerScheduleAPI
    {
        [FunctionName("ManagerScheduleAPI")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "ManagerScheduleAPI/{request}/{workerID}/{param1}/{param2}")] HttpRequest req,
            string request, int workerID, string param1, string param2, ILogger log)
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
                                text = "SELECT zone_name FROM workzones INNER JOIN workzones_workers ON workzones.ID_work_zone =" +
                                    "workzones_workers.ID_work_zone AND workzones_workers.ID_workers =" + workerID + " FOR JSON PATH; ";
                                break;
                            }
                        case "get-worker-schedule":
                            {
                                text = "SELECT schedule.time_start, schedule.time_end, workerstates.worker_state FROM schedule " +
                                    "INNER JOIN workerstates ON workerstates.ID_worker_state = schedule.ID_worker_state AND " +
                                    "schedule.ID_workers =" + workerID + " AND schedule.time_start >= '" + param1 + "' AND schedule.time_start <= '" + param2 + "'  FOR JSON PATH; ";
                                break;
                            }
                        case "get-schedule":
                            {
                                text = "SELECT schedule.time_start, schedule.time_end, workerstates.worker_state FROM schedule "+
                                       "INNER JOIN workerstates ON workerstates.ID_worker_state = schedule.ID_worker_state AND "+
                                       " schedule.time_start >= '" + param1 + "' AND schedule.time_start <= '" + param2 + "' FOR JSON PATH;";
                                break;
                            }
                        case "add-worker-to-schedule":
                            {
                                text = "SET XACT_ABORT ON; "
                                + " BEGIN TRANSACTION AddWorkerToSchedule"
                                + " USE[marpi-schedule]"
                                + " DECLARE @date AS date = '"+param1+"';"
                                + " DECLARE @start_hours AS time;"
                                + " DECLARE @end_hours AS time;"
                                + " DECLARE @worker_ID AS smallint ="+workerID+";"
                                + " DECLARE @team AS varchar(3) = (SELECT ID_team FROM workers WHERE ID_workers = @worker_ID);"
                                + " DECLARE @contract AS varchar(2) = (SELECT ID_contract_type FROM workers WHERE ID_workers = @worker_ID);"
                                + " SET @start_hours = (SELECT start_hours from teamhours WHERE ID_team = @team"
                                + " AND odd_even_week = (SELECT datepart(isowk, @date) % 2) "
	                            + " AND ID_contract_type = @contract"
                                + " AND ID_week_name = (SELECT FORMAT(@date, 'ddd')));"
                                + " SET @end_hours = (SELECT end_hours from teamhours where ID_team = @team"
                                + " AND odd_even_week = (SELECT datepart(isowk, @date) % 2) "
	                            + " AND ID_contract_type = @contract"
                                + " AND ID_week_name = (SELECT FORMAT(@date, 'ddd')));"
                                + " INSERT INTO schedule VALUES((select cast(concat(@date, ' ', @start_hours) as datetime2(7))),"
	                            + " (select cast(concat(@date, ' ', @end_hours) as datetime2(7))),'"+param2+ "',@worker_ID);"
                                + " IF(@@ERROR > 0) BEGIN Rollback Transaction AddWorkerToSchedule"
                                + " END ELSE BEGIN Commit Transaction AddWorkerToSchedule END SET XACT_ABORT OFF;";
                                break;
                            }
                        case "update-contract":
                            {
                                text = "UPDATE workers SET ID_contract_type = '"+param1+"' WHERE ID_workers = " + workerID + "; ";
                                break;
                            }
                        case "update-workerteam":
                            {
                                text = "UPDATE workers SET ID_Team = '" + param1 + "' WHERE ID_workers = " + workerID + "; ";
                                break;
                            }
                        case "remove-worker-zone":
                            {
                                text = "DELETE FROM workzones_workers WHERE ID_workers = " + workerID + " AND ID_work_zone = '"+param1+"';";
                                break;
                            }
                        case "add-worker-zone":
                            {
                                text = "INSERT INTO workzones_workers VALUES('"+param1+"',"+workerID+");";
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
