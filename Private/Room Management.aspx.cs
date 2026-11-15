using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using CodeCrafters_Major_Project_Website.Models;

namespace CodeCrafters_Major_Project_Website.Private
{
    public partial class Room_Management : Page
    {
        private string ConnectionString { get { return ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString; } }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RoleBootstrapper.CanManage(User))
            {
                Response.Redirect("~/Account/Login.aspx?ReturnUrl=" + Server.UrlEncode(Request.RawUrl));
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            if (!IsPostBack) BindRooms();
        }

        protected void SearchRooms_Click(object sender, EventArgs e)
        {
            ClearFeedback();
            BindRooms();
        }

        protected void RoomGrid_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow) return;
            var status = e.Row.FindControl("RoomStatusInput") as DropDownList;
            DataRowView room = e.Row.DataItem as DataRowView;
            if (status == null || room == null) return;

            string current = Convert.ToString(room["RoomStatus"]);
            ListItem selected = status.Items.FindByValue(current);
            if (selected == null && !String.IsNullOrWhiteSpace(current))
            {
                selected = new ListItem(current, current);
                selected.Attributes["disabled"] = "disabled";
                status.Items.Insert(0, selected);
            }
            if (selected != null) status.SelectedValue = current;

            bool hasActiveBooking = room["BookingId"] != DBNull.Value;
            status.Enabled = !hasActiveBooking;
            var save = e.Row.FindControl("SaveRoomButton") as Button;
            if (save != null) save.Enabled = !hasActiveBooking;
            var locked = e.Row.FindControl("RoomBookingLock") as Label;
            if (locked != null) locked.Visible = hasActiveBooking;
        }

        protected void RoomGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!String.Equals(e.CommandName, "SaveRoomStatus", StringComparison.Ordinal)) return;
            int rowIndex;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument), out rowIndex) || rowIndex < 0 || rowIndex >= RoomGrid.Rows.Count)
            {
                SetFeedback(false, "Select a room and try again.");
                return;
            }

            string roomId = Convert.ToString(RoomGrid.DataKeys[rowIndex].Value);
            var dropdown = RoomGrid.Rows[rowIndex].FindControl("RoomStatusInput") as DropDownList;
            if (dropdown == null)
            {
                SetFeedback(false, "The room status could not be read. Refresh and try again.");
                return;
            }

            try
            {
                UpdateRoomStatus(roomId, dropdown.SelectedValue);
                SetFeedback(true, "Room status was updated.");
                BindRooms();
            }
            catch (InvalidOperationException ex) { SetFeedback(false, ex.Message); }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Room status update failed: " + ex);
                SetFeedback(false, "The room status could not be saved. Refresh and try again.");
            }
        }

        private void BindRooms()
        {
            const string sql = @"SELECT hr.Hotel_Room_ID AS RoomId,br.Branch_Name AS BranchName,
hr.hotel_room_number AS RoomNumber,hr.hotel_room_type AS RoomType,hr.hotel_room_status AS RoomStatus,
hr.Hotel_Room_Price AS Rate,ISNULL(hr.Max_Adults,0)+ISNULL(hr.Max_Children,0) AS Capacity,
active.Booking_ID AS BookingId,active.Checkin_Date,active.Checkout_Date,active.Booking_Status
FROM Hotel_Room hr
INNER JOIN Branch br ON br.Branch_ID=hr.Branch_ID
OUTER APPLY (
 SELECT TOP (1) b.Booking_ID,b.Checkin_Date,b.Checkout_Date,b.Booking_Status
 FROM Room_Assignment ra INNER JOIN Booking b ON b.Booking_ID=ra.Booking_ID
 WHERE ra.Hotel_Room_ID=hr.Hotel_Room_ID AND ra.Assignment_Status<>'Cancelled'
   AND b.Booking_Status IN ('Pending','Booked','Confirmed','CheckedIn')
   AND (b.Booking_Status<>'Pending' OR ra.Assigned_Date>DATEADD(MINUTE,-@HoldMinutes,GETDATE()))
   AND b.Checkout_Date>@Today
 ORDER BY b.Checkin_Date,b.Booking_ID
) active
WHERE (@Status='All' OR hr.hotel_room_status=@Status)
 AND (@Search='' OR CONVERT(varchar(20),hr.hotel_room_number) LIKE '%'+@Search+'%'
   OR hr.hotel_room_type LIKE '%'+@Search+'%' OR br.Branch_Name LIKE '%'+@Search+'%')
ORDER BY br.Branch_Name,hr.hotel_room_number;";
            var table = new DataTable();
            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                using (var command = new SqlCommand(sql, connection))
                using (var adapter = new SqlDataAdapter(command))
                {
                    command.Parameters.Add("@Status", SqlDbType.VarChar, 30).Value = StatusFilter.SelectedValue;
                    command.Parameters.Add("@Search", SqlDbType.VarChar, 100).Value = (RoomSearch.Text ?? String.Empty).Trim();
                    command.Parameters.Add("@Today", SqlDbType.Date).Value = DateTime.Today;
                    int holdMinutes;
                    if (!Int32.TryParse(ConfigurationManager.AppSettings["Booking:HoldMinutes"], out holdMinutes) || holdMinutes < 1) holdMinutes = 15;
                    command.Parameters.Add("@HoldMinutes", SqlDbType.Int).Value = holdMinutes;
                    adapter.Fill(table);
                }
                RoomGrid.DataSource = table;
                RoomGrid.DataBind();
                RoomGrid.EmptyDataText = "No rooms match this search.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Manager room inventory failed to load: " + ex);
                RoomGrid.DataSource = null;
                RoomGrid.DataBind();
                SetFeedback(false, "Room inventory could not be loaded. Please refresh or contact support.");
            }
        }

        private void UpdateRoomStatus(string roomId, string requestedStatus)
        {
            string normalizedStatus = (requestedStatus ?? String.Empty).Trim();
            if (!String.Equals(normalizedStatus, "Available", StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(normalizedStatus, "Unavailable", StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(normalizedStatus, "Under Maintenance", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose Available, Unavailable, or Under Maintenance.");

            int id;
            if (!Int32.TryParse(roomId, out id) || id < 1) throw new InvalidOperationException("The selected room is invalid. Refresh and try again.");
            string canonicalStatus = String.Equals(normalizedStatus, "Available", StringComparison.OrdinalIgnoreCase) ? "Available" :
                String.Equals(normalizedStatus, "Unavailable", StringComparison.OrdinalIgnoreCase) ? "Unavailable" : "Under Maintenance";

            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        using (var room = new SqlCommand("SELECT Hotel_Room_ID FROM Hotel_Room WITH (UPDLOCK,HOLDLOCK) WHERE Hotel_Room_ID=@RoomId", connection, transaction))
                        {
                            room.Parameters.Add("@RoomId", SqlDbType.Int).Value = id;
                            if (room.ExecuteScalar() == null) throw new InvalidOperationException("That room no longer exists. Refresh the inventory.");
                        }

                        const string activeSql = @"SELECT COUNT(*) FROM Room_Assignment ra WITH (UPDLOCK,HOLDLOCK)
INNER JOIN Booking b WITH (UPDLOCK,HOLDLOCK) ON b.Booking_ID=ra.Booking_ID
WHERE ra.Hotel_Room_ID=@RoomId AND ra.Assignment_Status<>'Cancelled'
 AND b.Booking_Status IN ('Pending','Booked','Confirmed','CheckedIn')
 AND (b.Booking_Status<>'Pending' OR ra.Assigned_Date>DATEADD(MINUTE,-@HoldMinutes,GETDATE()))
 AND b.Checkout_Date>@Today;";
                        using (var active = new SqlCommand(activeSql, connection, transaction))
                        {
                            active.Parameters.Add("@RoomId", SqlDbType.Int).Value = id;
                            active.Parameters.Add("@Today", SqlDbType.Date).Value = DateTime.Today;
                            int holdMinutes;
                            if (!Int32.TryParse(ConfigurationManager.AppSettings["Booking:HoldMinutes"], out holdMinutes) || holdMinutes < 1) holdMinutes = 15;
                            active.Parameters.Add("@HoldMinutes", SqlDbType.Int).Value = holdMinutes;
                            if (Convert.ToInt32(active.ExecuteScalar()) > 0)
                                throw new InvalidOperationException("This room has an active or future booking and its status cannot be changed until the stay is complete or cancelled.");
                        }

                        using (var update = new SqlCommand("UPDATE Hotel_Room SET hotel_room_status=@Status WHERE Hotel_Room_ID=@RoomId", connection, transaction))
                        {
                            update.Parameters.Add("@Status", SqlDbType.VarChar, 30).Value = canonicalStatus;
                            update.Parameters.Add("@RoomId", SqlDbType.Int).Value = id;
                            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("The room status changed while you were editing. Refresh and try again.");
                        }
                        transaction.Commit();
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        private void SetFeedback(bool success, string message)
        {
            RoomFeedback.Text = "<div class=\"form-msg " + (success ? "success" : "error") + "\">" + HttpUtility.HtmlEncode(message) + "</div>";
            string script = "(function(){var show=function(){if(window.RegalDialog)window.RegalDialog.alert('" +
                HttpUtility.JavaScriptStringEncode(message) + "',{title:'" + (success ? "Room updated" : "Check room update") + "'});};if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',show);else show();})();";
            ClientScript.RegisterStartupScript(GetType(), "roomStatusFeedback", script, true);
        }

        private void ClearFeedback() { RoomFeedback.Text = String.Empty; }
    }
}
