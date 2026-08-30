using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using System.Data.SqlClient;

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
            }
        }

        // Now pulls every available room instead of just the top 3.
        private void BindFeaturedRooms()
        {
            var rooms = new List<FeaturedRoom>();

            string connStr = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

            // Picks one room per distinct hotel_room_type — the cheapest available
            // room of each type, so Featured Rooms shows variety, not duplicates.
            const string sql = @"
        SELECT Hotel_Room_ID, hotel_room_type, Hotel_Room_Price, Max_Adults, Max_Children
        FROM (
            SELECT *,
                   ROW_NUMBER() OVER (PARTITION BY hotel_room_type ORDER BY Hotel_Room_Price ASC) AS rn
            FROM Hotel_Room
            WHERE hotel_room_status = 'Available'
        ) ranked
        WHERE rn = 1
        ORDER BY Hotel_Room_Price DESC";

            using (var conn = new SqlConnection(connStr))
            using (var cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string roomType = reader["hotel_room_type"].ToString();
                        rooms.Add(new FeaturedRoom
                        {
                            RoomId = reader["Hotel_Room_ID"].ToString(),
                            RoomName = roomType,
                            PricePerNight = Convert.ToDecimal(reader["Hotel_Room_Price"]),
                            MaxGuests = SafeInt(reader["Max_Adults"]) + SafeInt(reader["Max_Children"]),
                            ImageUrl = ImageForRoomType(roomType)
                        });
                    }
                }
            }

            rptFeaturedRooms.DataSource = rooms;
            rptFeaturedRooms.DataBind();
        }

        // Populates the search widget's Room Type dropdown from whatever
        // distinct room types actually exist in the database right now.
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

        // Matches each room type to the actual filenames sitting in
        // ~/Images/. Ordered from most-specific to least-specific so
        // "standard...king" and "standard...double" don't collide.
        public static string ImageForRoomType(string roomType)
        {
            string type = (roomType ?? string.Empty).ToLower();

            if (type.Contains("suite") && type.Contains("twin"))
                return ResolveImage("~/Images/SUITE ROOM TWIN BEDS.jpeg");
            if (type.Contains("suite"))
                return ResolveImage("~/Images/SUITE ROOM 1 KING BED.jpeg");

            if (type.Contains("executive"))
                return ResolveImage("~/Images/EXECUTIVE ROOM I KING BED.jpeg");

            if (type.Contains("deluxe"))
                return ResolveImage("~/Images/DELUXE ROOM 1 KING BED.jpeg");

            if (type.Contains("standard") && type.Contains("double"))
                return ResolveImage("~/Images/STANDARD ROOM 2 DOUBLE BEDS.jpeg");
            if (type.Contains("standard"))
                return ResolveImage("~/Images/STANDARD ROOM 1 KING BED.jpeg");

            // Fallback for any type that doesn't match a known keyword
            return ResolveImage("~/Images/regall inn home page.jpg");
        }

        private static string ResolveImage(string url) { return VirtualPathUtility.ToAbsolute(url); }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
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