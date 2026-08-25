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
            }
        }

        // Pulls a handful of available rooms to showcase on the homepage,
        // matched to your real Hotel_Room table in the GroupPmb2 database.
        private void BindFeaturedRooms()
        {
            var rooms = new List<FeaturedRoom>();

            string connStr = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
            const string sql = @"SELECT TOP 3 Hotel_Room_ID, hotel_room_type, Hotel_Room_Price,
                                         Max_Adults, Max_Children
                                  FROM Hotel_Room
                                  WHERE hotel_room_status = 'Available'
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

        // Max_Adults / Max_Children showed as NULL in your sample data —
        // this guards against that until those columns are populated.
        private int SafeInt(object dbValue)
        {
            return dbValue == DBNull.Value ? 0 : Convert.ToInt32(dbValue);
        }

        // Your Hotel_Room table has no ImageUrl column, so we pick a stock
        // photo based on keywords in hotel_room_type. Once you add real
        // room photos (e.g. to a Room_Images table or a fixed folder like
        // ~/images/rooms/), swap this out for a real lookup.
        public static string ImageForRoomType(string roomType)
        {
            string type = (roomType ?? string.Empty).ToLower();

            if (type.Contains("suite"))
                return ResolveImage("~/Images/Pictures/suite twin beds1.png");
            if (type.Contains("twin"))
                return ResolveImage("~/Images/Pictures/standard 2 double beds.jpg");
            if (type.Contains("king"))
                return ResolveImage("~/Images/Pictures/Standard king bed.jpg");

            return ResolveImage("~/Images/Pictures/Regal pic 1.jpg");
        }

        private static string ResolveImage(string url) { return VirtualPathUtility.ToAbsolute(url); }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            // Redirect to Rooms.aspx with the search criteria as query string,
            // so results are pre-filtered on load — no login required to search.
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

            // TODO: insert into a Newsletter table, or call your EmailService.
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
