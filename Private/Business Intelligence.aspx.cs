using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.UI;
using CodeCrafters_Major_Project_Website.Models;

namespace CodeCrafters_Major_Project_Website.Private
{
    public partial class Business_Intelligence : Page
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RoleBootstrapper.CanManage(User))
            {
                Response.Redirect("~/Account/Login.aspx?ReturnUrl=" + Server.UrlEncode(Request.RawUrl));
                return;
            }
            if (!IsPostBack) LoadDashboard();
        }

        private void LoadDashboard()
        {
            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);
            AsOfDate.Text = "<span class=\"dashboard-date\">" + HttpUtility.HtmlEncode(today.ToString("dddd, dd MMMM yyyy", CultureInfo.GetCultureInfo("en-ZA"))) + "</span>";
            PowerBiSection.Visible = User.IsInRole(RoleBootstrapper.Manager) || User.IsInRole(RoleBootstrapper.Admin);
            if (PowerBiSection.Visible) LoadPowerBiEmbed();

            try
            {
                LoadSummary(today, tomorrow);
                LoadPropertyRows(today, tomorrow);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Manager dashboard failed to load: " + ex);
                DashboardError.Text = "<div class=\"form-msg error\">Property figures could not be loaded. Please refresh or contact support.</div>";
            }
        }

        private void LoadPowerBiEmbed()
        {
            string configuredUrl = ConfigurationManager.AppSettings["PowerBI:SecureEmbedUrl"];
            Uri embedUri;
            bool valid = Uri.TryCreate(configuredUrl, UriKind.Absolute, out embedUri) &&
                String.Equals(embedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                String.Equals(embedUri.Host, "app.powerbi.com", StringComparison.OrdinalIgnoreCase) &&
                embedUri.AbsolutePath.StartsWith("/reportEmbed", StringComparison.OrdinalIgnoreCase);
            if (!valid)
            {
                PowerBiEmbed.Text = "<div class=\"powerbi-message\">Add the secure Power BI report embed URL to the <code>PowerBI:SecureEmbedUrl</code> app setting to show reports here.</div>";
                return;
            }

            PowerBiEmbed.Text = "<iframe class=\"powerbi-frame\" title=\"Regal Inn Power BI reports\" src=\"" +
                HttpUtility.HtmlAttributeEncode(embedUri.AbsoluteUri) + "\" allowfullscreen=\"true\"></iframe>";
        }

        private void LoadSummary(DateTime today, DateTime tomorrow)
        {
            const string sql = @"SELECT
 (SELECT COUNT(*) FROM Hotel_Room WHERE hotel_room_status IN ('Available','Booked')) AS InventoryRooms,
 (SELECT COUNT(DISTINCT ra.Hotel_Room_ID) FROM Room_Assignment ra
  INNER JOIN Booking b ON b.Booking_ID=ra.Booking_ID
  INNER JOIN Hotel_Room hr ON hr.Hotel_Room_ID=ra.Hotel_Room_ID
  WHERE hr.hotel_room_status IN ('Available','Booked') AND ra.Assignment_Status<>'Cancelled'
   AND b.Booking_Status IN ('Booked','Confirmed','CheckedIn') AND b.Checkin_Date<@Tomorrow AND b.Checkout_Date>@Today) AS OccupiedRooms,
 (SELECT COUNT(DISTINCT b.Booking_ID) FROM Booking b
  WHERE b.Checkin_Date>=@Today AND b.Checkin_Date<@Tomorrow AND b.Booking_Status IN ('Booked','Confirmed','CheckedIn')
   AND EXISTS (SELECT 1 FROM Room_Assignment ra WHERE ra.Booking_ID=b.Booking_ID AND ra.Assignment_Status<>'Cancelled')) AS Arrivals,
 (SELECT COUNT(DISTINCT b.Booking_ID) FROM Booking b
  WHERE b.Checkout_Date>=@Today AND b.Checkout_Date<@Tomorrow AND b.Booking_Status IN ('Booked','Confirmed','CheckedIn')
   AND EXISTS (SELECT 1 FROM Room_Assignment ra WHERE ra.Booking_ID=b.Booking_ID AND ra.Assignment_Status<>'Cancelled')) AS Departures,
 (SELECT COUNT(*) FROM Booking WHERE Booking_Status IN ('Booked','Confirmed','CheckedIn')) AS ActiveBookings,
 (SELECT COUNT(DISTINCT b.Booking_ID) FROM Booking b
  WHERE b.Booking_Status='Pending' AND EXISTS (SELECT 1 FROM Room_Assignment ra WHERE ra.Booking_ID=b.Booking_ID
   AND ra.Assignment_Status<>'Cancelled' AND ra.Assigned_Date>=DATEADD(MINUTE,-@HoldMinutes,GETDATE()))) AS PaymentHolds;";

            int holdMinutes;
            if (!Int32.TryParse(ConfigurationManager.AppSettings["Booking:HoldMinutes"], out holdMinutes) || holdMinutes < 1) holdMinutes = 15;
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Today", today);
                command.Parameters.AddWithValue("@Tomorrow", tomorrow);
                command.Parameters.AddWithValue("@HoldMinutes", holdMinutes);
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("The dashboard summary returned no data.");
                    int rooms = Convert.ToInt32(reader["InventoryRooms"]);
                    int occupied = Convert.ToInt32(reader["OccupiedRooms"]);
                    int available = Math.Max(0, rooms - occupied);
                    decimal occupancy = rooms == 0 ? 0m : Math.Min(100m, occupied * 100m / rooms);
                    OccupancyValue.Text = occupancy.ToString("0.#", CultureInfo.InvariantCulture) + "%";
                    OccupiedRoomsValue.Text = occupied.ToString(CultureInfo.InvariantCulture);
                    AvailableRoomsValue.Text = available.ToString(CultureInfo.InvariantCulture);
                    InventoryRoomsValue.Text = rooms.ToString(CultureInfo.InvariantCulture);
                    ArrivalsValue.Text = Convert.ToInt32(reader["Arrivals"]).ToString(CultureInfo.InvariantCulture);
                    DeparturesValue.Text = Convert.ToInt32(reader["Departures"]).ToString(CultureInfo.InvariantCulture);
                    ActiveBookingsValue.Text = Convert.ToInt32(reader["ActiveBookings"]).ToString(CultureInfo.InvariantCulture);
                    PaymentHoldsValue.Text = Convert.ToInt32(reader["PaymentHolds"]).ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        private void LoadPropertyRows(DateTime today, DateTime tomorrow)
        {
            const string sql = @"SELECT br.Branch_Name,
 COUNT(hr.Hotel_Room_ID) AS InventoryRooms,
 SUM(CASE WHEN hr.Hotel_Room_ID IS NOT NULL AND EXISTS (
  SELECT 1 FROM Room_Assignment ra INNER JOIN Booking b ON b.Booking_ID=ra.Booking_ID
  WHERE ra.Hotel_Room_ID=hr.Hotel_Room_ID AND ra.Assignment_Status<>'Cancelled'
   AND b.Booking_Status IN ('Booked','Confirmed','CheckedIn') AND b.Checkin_Date<@Tomorrow AND b.Checkout_Date>@Today
 ) THEN 1 ELSE 0 END) AS OccupiedRooms,
 (SELECT COUNT(DISTINCT b.Booking_ID) FROM Booking b WHERE b.Branch_ID=br.Branch_ID
  AND b.Checkin_Date>=@Today AND b.Checkin_Date<@Tomorrow AND b.Booking_Status IN ('Booked','Confirmed','CheckedIn')
  AND EXISTS (SELECT 1 FROM Room_Assignment ra WHERE ra.Booking_ID=b.Booking_ID AND ra.Assignment_Status<>'Cancelled')) AS Arrivals,
 (SELECT COUNT(DISTINCT b.Booking_ID) FROM Booking b WHERE b.Branch_ID=br.Branch_ID
  AND b.Checkout_Date>=@Today AND b.Checkout_Date<@Tomorrow AND b.Booking_Status IN ('Booked','Confirmed','CheckedIn')
  AND EXISTS (SELECT 1 FROM Room_Assignment ra WHERE ra.Booking_ID=b.Booking_ID AND ra.Assignment_Status<>'Cancelled')) AS Departures
FROM Branch br
LEFT JOIN Hotel_Room hr ON hr.Branch_ID=br.Branch_ID AND hr.hotel_room_status IN ('Available','Booked')
WHERE br.Branch_Status='Active'
GROUP BY br.Branch_ID,br.Branch_Name
ORDER BY br.Branch_Name;";

            var rows = new StringBuilder();
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Today", today);
                command.Parameters.AddWithValue("@Tomorrow", tomorrow);
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int total = Convert.ToInt32(reader["InventoryRooms"]);
                        int occupied = reader["OccupiedRooms"] == DBNull.Value ? 0 : Convert.ToInt32(reader["OccupiedRooms"]);
                        int available = Math.Max(0, total - occupied);
                        decimal percent = total == 0 ? 0m : Math.Min(100m, occupied * 100m / total);
                        string propertyName = HttpUtility.HtmlEncode(Convert.ToString(reader["Branch_Name"]));
                        rows.Append("<tr><td class=\"property-name\">").Append(propertyName).Append("</td><td class=\"occupancy-cell\">")
                            .Append(occupied.ToString(CultureInfo.InvariantCulture)).Append(" / ").Append(total.ToString(CultureInfo.InvariantCulture))
                            .Append(" <span class=\"stat-subtitle\">").Append(percent.ToString("0.#", CultureInfo.InvariantCulture)).Append("%</span>")
                            .Append("<div class=\"occupancy-track\"><div class=\"occupancy-fill\" style=\"width:")
                            .Append(percent.ToString("0.#", CultureInfo.InvariantCulture)).Append("%\"></div></div></td><td>")
                            .Append(available.ToString(CultureInfo.InvariantCulture)).Append("</td><td>")
                            .Append(Convert.ToInt32(reader["Arrivals"]).ToString(CultureInfo.InvariantCulture)).Append("</td><td>")
                            .Append(Convert.ToInt32(reader["Departures"]).ToString(CultureInfo.InvariantCulture)).Append("</td></tr>");
                    }
                }
            }
            PropertyRows.Text = rows.Length == 0
                ? "<tr><td colspan=\"5\">No active properties were found.</td></tr>"
                : rows.ToString();
        }
    }
}
