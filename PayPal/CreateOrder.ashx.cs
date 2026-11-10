using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.SessionState;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CodeCrafters_Major_Project_Website.PayPal
{
    public class CreateOrder : HttpTaskAsyncHandler, IRequiresSessionState
    {
        public override async Task ProcessRequestAsync(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json";

            if (!ctx.User.Identity.IsAuthenticated)
            {
                Fail(ctx, 401, "Please log in first.");
                return;
            }

            try
            {
                string body = new StreamReader(ctx.Request.InputStream).ReadToEnd();
                var req = JObject.Parse(body);

                var roomIds = req["roomIds"].ToObject<List<string>>().Distinct().ToList();
                DateTime checkIn = DateTime.Parse((string)req["checkIn"]);
                DateTime checkOut = DateTime.Parse((string)req["checkOut"]);
                int nights = (checkOut.Date - checkIn.Date).Days;

                if (roomIds.Count == 0) { Fail(ctx, 400, "Your cart is empty."); return; }
                if (checkIn.Date < DateTime.Today || nights < 1) { Fail(ctx, 400, "Please choose valid dates."); return; }

                // Price comes from the DATABASE, never from the browser.
                decimal zarTotal = 0;
                string connStr = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    foreach (string id in roomIds)
                    {
                        using (var cmd = new SqlCommand(
                            "SELECT Hotel_Room_Price FROM Hotel_Room WHERE Hotel_Room_ID = @Id AND hotel_room_status = 'Available'", conn))
                        {
                            cmd.Parameters.AddWithValue("@Id", id);
                            object price = cmd.ExecuteScalar();
                            if (price == null) { Fail(ctx, 400, "Room " + id + " is no longer available."); return; }
                            zarTotal += Convert.ToDecimal(price) * nights;
                        }
                    }
                }

                decimal usd = PayPalClient.ZarToUsd(zarTotal);
                var order = await PayPalClient.CreateOrderAsync(usd, "The Regal Inn booking (" + nights + " night(s))");
                string orderId = (string)order["id"];

                // Remember what this order was for, on the server, so capture can't be tampered with.
                ctx.Session["pp_" + orderId] = JsonConvert.SerializeObject(new
                {
                    roomIds,
                    checkIn,
                    checkOut,
                    user = ctx.User.Identity.Name,
                    zarTotal,
                    usd
                });

                ctx.Response.Write(new JObject { ["id"] = orderId }.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex); // check the Output window while testing
                Fail(ctx, 500, "Could not start payment.");
            }
        }

        private static void Fail(HttpContext ctx, int code, string msg)
        {
            ctx.Response.StatusCode = code;
            ctx.Response.Write(new JObject { ["error"] = msg }.ToString());
        }
    }
}