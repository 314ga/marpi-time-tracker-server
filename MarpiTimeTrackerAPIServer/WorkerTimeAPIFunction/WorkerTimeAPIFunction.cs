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

namespace WorkerTimeAPIFunction
{
    public static class WorkerTimeAPIFunction
    {
        [FunctionName("WorkerTimeAPIFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "WorkerTimeAPI/{request}/{workerID}/{param1}/{param2}")] HttpRequest req,
            string request, int workerID, string param1, string param2, ILogger log)
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
                        case "present-work":
                            {
                                text = "INSERT INTO workdays(ID_worker,date_present) VALUES("+workerID+", '"+param1+"');";
                                break;
                            }
                        case "punch-work-zone":
                            {
                                text = "SET XACT_ABORT ON; "+
                                        "BEGIN TRANSACTION PunchWorkZone "+
                                        "USE[marpi - timetracker] "+
                                        "DECLARE @date AS DATETIME2(0) = CONVERT(datetime, '" + param1 + "') " +
                                        "DECLARE @ID_workday AS int = (SELECT ID_work_day FROM workdays WHERE ID_worker = " + workerID + " AND end_time IS NULL) " +
                                        "if ((SELECT end_time FROM workdays WHERE ID_work_day = @ID_workday ) IS NULL AND NOT "+
                                        "EXISTS(SELECT start_time FROM workzone_workday WHERE ID_work_day = @ID_workday)) BEGIN "+
                                        "UPDATE workdays SET start_time = @date WHERE ID_work_day = @ID_workday END "+
                                        "IF EXISTS(SELECT end_time FROM workzone_workday WHERE ID_work_day = @ID_workday AND end_time IS NULL) BEGIN "+
                                        "UPDATE workzone_workday SET end_time = @date WHERE ID_work_day = @ID_workday AND end_time IS NULL END " +
                                        "INSERT INTO workzone_workday(ID_work_day, ID_work_zone, start_time) " +
                                        "VALUES(@ID_workday, '" + param2 + "', @date); GO " +
                                        "IF(@@ERROR > 0) BEGIN Rollback Transaction PunchWorkZone " +
                                        "END ELSE BEGIN Commit Transaction PunchWorkZone END SET XACT_ABORT OFF; GO";
                                break;
                            }
                        case "punch-leaving-work":
                            {
                                text = "SET XACT_ABORT ON;"+
                                        " BEGIN TRANSACTION LeaveWork" +
                                        " USE[marpi - timetracker]"+
                                        " DECLARE @date AS DATETIME2(0) = CONVERT(datetime, '" + param1 + "')" +
                                        " DECLARE @ID_workday AS int = (SELECT ID_work_day FROM workdays WHERE ID_worker = " + workerID + " AND end_time IS NULL)" +
                                        " UPDATE workdays SET end_time = @date WHERE ID_work_day = @ID_workday"+
                                        " UPDATE workzone_workday SET end_time = @date WHERE ID_work_day = @ID_workday AND end_time IS NULL GO"+
                                        " IF(@@ERROR > 0) BEGIN Rollback Transaction LeaveWork"+
                                        " END ELSE BEGIN Commit Transaction LeaveWork END SET XACT_ABORT OFF; GO";
                                break;
                            }
                        case "punch-break":
                            {
                                text = "SET XACT_ABORT ON;"+
                                        " BEGIN TRANSACTION Breaks"+
                                        " USE[marpi - timetracker]"+
                                        " DECLARE @date AS DATETIME2(0) = CONVERT(datetime, '" + param1 + "')" +
                                        " DECLARE @ID_workday AS int = (SELECT ID_work_day FROM workdays WHERE ID_worker = " + workerID + " AND end_time IS NULL)" +
                                        " DECLARE @existingBreakID AS int = (SELECT ID_break FROM breaks WHERE break_end IS NULL AND ID_work_day = @ID_workday)"+
                                        " IF(@existingBreakID IS NOT NULL AND LEN(@existingBreakID) > 0) BEGIN"+
                                        " UPDATE breaks SET break_end = @date WHERE ID_break = @existingBreakID; END ELSE BEGIN"+
                                        " INSERT INTO breaks(ID_work_day, break_start) VALUES(@ID_workday, @date); END GO"+
                                        " IF(@@ERROR > 0) BEGIN Rollback Transaction Breaks"+
                                        " END ELSE BEGIN Commit Transaction Breaks END SET XACT_ABORT OFF; GO";
                                break;
                            }
                        case "select-day-time":
                            {
                                text = "SELECT workdays.start_time, workdays.end_time, hours_total, workzone_workday.ID_work_zone ,  workzone_workday.start_time,  workzone_workday.end_time" +
                                        " FROM workdays"+
                                        " INNER JOIN workzone_workday"+
                                        " ON workdays.ID_work_day = workzone_workday.ID_work_day WHERE workdays.date_present = '" + param1 + "' AND ID_worker = " + workerID + ";";
                                break;
                            }
                        case "select-month-time":
                            {
                                text = "SELECT workdays.start_time, workdays.end_time, hours_total, workzone_workday.ID_work_zone ,  workzone_workday.start_time,  workzone_workday.end_time"+
                                        " FROM workdays"+
                                        " INNER JOIN workzone_workday"+
                                        " ON workdays.ID_work_day = workzone_workday.ID_work_day WHERE MONTH(workdays.date_present) = " + param1 + " AND YEAR(workdays.date_present) = " + param2 + " AND ID_worker = " + workerID + "; ";
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
