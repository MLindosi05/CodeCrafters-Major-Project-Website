using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using System.Data.SqlClient;
using CodeCrafters_Major_Project_Website.Models;

namespace CodeCrafters_Major_Project_Website
{
    public partial class Default : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindFeaturedRooms();
                BindRoomTypeDropdown();

                // Set minimum selectable date attribute on the textbox control server-side
                txtCheckIn.Attributes["min"] = DateTime.Today.ToString("yyyy-MM-dd");
            }
        }

        // Pulls every available room representative for featured listings
        private void BindFeaturedRooms()
        {
            var rooms = new AvailabilityService().GetAvailableRooms(String.Empty, String.Empty, 0, null, null, 0)
                .GroupBy(room => room.RoomType)
                .Select(group => group.OrderBy(room => room.PricePerNight).First())
                .OrderByDescending(room => room.PricePerNight)
                .Select(room => new FeaturedRoom
                {
                    RoomId = room.RoomId.ToString(),
                    RoomName = room.RoomType,
                    PricePerNight = room.PricePerNight,
                    MaxGuests = room.MaxGuests,
                    ImageUrl = ImageForRoomType(room.RoomType)
                }).ToList();
            rptFeaturedRooms.DataSource = rooms;
            rptFeaturedRooms.DataBind();
        }

        // Populates the search widget's Room Type dropdown from distinct room types in the database
        private void BindRoomTypeDropdown()
        {
            string connStr = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            const string sql = @"SELECT DISTINCT hotel_room_type FROM Hotel_Room ORDER BY hotel_room_type";

            ddlRoomType.Items.Clear();
            ddlRoomType.Items.Add(new ListItem("Any", ""));

            using (var conn = new SqlConnection(connStr))
            using (var cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string type = reader["hotel_room_type"].ToString();
                        ddlRoomType.Items.Add(new ListItem(type, type));
                    }
                }
            }
        }

        private int SafeInt(object dbValue)
        {
            return dbValue == DBNull.Value ? 0 : Convert.ToInt32(dbValue);
        }

        // Matches each room type to the actual filenames sitting in ~/Images/
        public static string ImageForRoomType(string roomType)
        {
            string type = (roomType ?? string.Empty).ToLower();

            if (type.Contains("suite") && type.Contains("twin"))
                return ResolveImage("~/Images/SUITE ROOM TWIN BEDS.png");
            if (type.Contains("suite"))
                return ResolveImage("~/Images/SUITE ROOM 1 KING BED.png");

            if (type.Contains("executive"))
                return ResolveImage("~/Images/EXECUTIVE ROOM I KING BED.png");

            if (type.Contains("deluxe"))
                return ResolveImage("~/Images/DELUXE ROOM 1 KING BED.png");

            if (type.Contains("standard") && type.Contains("double"))
                return ResolveImage("~/Images/STANDARD ROOM 2 DOUBLE BEDS.png");
            if (type.Contains("standard"))
                return ResolveImage("~/Images/STANDARD ROOM 1 KING BED.png");

            return ResolveImage("~/Images/regall inn home page.jpg");
        }

        private static string ResolveImage(string url) { return VirtualPathUtility.ToAbsolute(url); }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            DateTime checkInDate, checkOutDate;

            // Business Validation 1: Check-in date cannot be in the past or invalid
            if (!DateTime.TryParse(txtCheckIn.Text, out checkInDate) || checkInDate.Date < DateTime.Today)
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "valError1", "alert('Error: Check-in date cannot be selected from the past.');", true);
                return;
            }

            // Business Validation 2: Check-out date cannot be less than or equal to check-in date
            if (!DateTime.TryParse(txtCheckOut.Text, out checkOutDate) || checkOutDate.Date <= checkInDate.Date)
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "valError2", "alert('Error: Check-out date must be strictly after the check-in date.');", true);
                return;
            }

            string url = string.Format(
                "~/Rooms.aspx?checkin={0}&checkout={1}&guests={2}&type={3}",
                Server.UrlEncode(txtCheckIn.Text),
                Server.UrlEncode(txtCheckOut.Text),
                Server.UrlEncode(ddlGuests.SelectedValue),
                Server.UrlEncode(ddlRoomType.SelectedValue));

            Response.Redirect(url);
        }

        protected void btnSubscribe_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNewsletterEmail.Text) || !txtNewsletterEmail.Text.Contains("@"))
            {
                litNewsletterMsg.Text = "<div class='form-msg error'>Please enter a valid email address.</div>";
                return;
            }

            litNewsletterMsg.Text = "<div class='form-msg success'>Thanks — you're subscribed!</div>";
            txtNewsletterEmail.Text = string.Empty;
        }

        private class FeaturedRoom
        {
            public string RoomId { get; set; }
            public string RoomName { get; set; }
            public decimal PricePerNight { get; set; }
            public int MaxGuests { get; set; }
            public string ImageUrl { get; set; }
        }
    }
}