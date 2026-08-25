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
    public partial class Rooms : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Pre-fill from querystring if arriving from the homepage search widget
                if (Request.QueryString["checkin"] != null) txtCheckIn.Text = Request.QueryString["checkin"];
                if (Request.QueryString["checkout"] != null) txtCheckOut.Text = Request.QueryString["checkout"];
                if (Request.QueryString["type"] != null) ddlRoomType.SelectedValue = Request.QueryString["type"];

                BindRooms(ddlRoomType.SelectedValue, 0);
            }
        }

        // Matches your real Hotel_Room table: hotel_room_type holds free-text
        // like "Standard Room 1 King Bed" / "Suite Room Twin Beds", so the
        // dropdown filter (Standard/Deluxe/Suite) does a partial LIKE match
        // rather than an exact equals.
        private void BindRooms(string roomTypeFilter, decimal maxPrice)
        {
            var rooms = new List<RoomResult>();
            string connStr = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

            const string sql = @"SELECT Hotel_Room_ID, Branch_ID, hotel_room_number, hotel_room_type,
                                         Hotel_Room_Price, Max_Adults, Max_Children
                                  FROM Hotel_Room
                                  WHERE hotel_room_status = 'Available'
                                    AND (@RoomType = '' OR hotel_room_type LIKE '%' + @RoomType + '%')
                                    AND (@MaxPrice = 0 OR Hotel_Room_Price <= @MaxPrice)
                                  ORDER BY Hotel_Room_Price ASC";

            using (var conn = new SqlConnection(connStr))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@RoomType", roomTypeFilter ?? string.Empty);
                cmd.Parameters.AddWithValue("@MaxPrice", maxPrice);
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string roomType = reader["hotel_room_type"].ToString();
                        int maxAdults = SafeInt(reader["Max_Adults"]);
                        int maxChildren = SafeInt(reader["Max_Children"]);

                        rooms.Add(new RoomResult
                        {
                            RoomId = reader["Hotel_Room_ID"].ToString(),
                            RoomName = roomType,
                            RoomNumber = reader["hotel_room_number"].ToString(),
                            BranchId = reader["Branch_ID"].ToString(),
                            Blurb = BlurbForRoomType(roomType),
                            MaxGuests = (maxAdults + maxChildren) > 0 ? (maxAdults + maxChildren).ToString() : "2",
                            PricePerNight = Convert.ToDecimal(reader["Hotel_Room_Price"]),
                            ImageUrl = Default.ImageForRoomType(roomType)
                        });
                    }
                }
            }

            rptRooms.DataSource = rooms;
            rptRooms.DataBind();
            pnlNoResults.Visible = rooms.Count == 0;
        }

        private int SafeInt(object dbValue)
        {
            return dbValue == DBNull.Value ? 0 : Convert.ToInt32(dbValue);
        }

        // hotel_room_type has no separate description field, so we generate
        // a short one from the type text for the card copy.
        private string BlurbForRoomType(string roomType)
        {
            string type = (roomType ?? string.Empty).ToLower();
            if (type.Contains("suite"))
                return "Spacious suite with extra living space, ideal for longer stays.";
            if (type.Contains("twin"))
                return "Comfortable twin-bed room, perfect for friends or family sharing.";
            return "Well-appointed room with everything you need for a restful stay.";
        }

        protected void btnFilter_Click(object sender, EventArgs e)
        {
            decimal maxPrice = 0;
            decimal.TryParse(ddlMaxPrice.SelectedValue, out maxPrice);
            BindRooms(ddlRoomType.SelectedValue, maxPrice);
        }

        protected void rptRooms_ItemCommand(object source, System.Web.UI.WebControls.RepeaterCommandEventArgs e)
        {
            if (e.CommandName != "Book") return;

            string roomId = e.CommandArgument.ToString();
            hdnSelectedRoomId.Value = roomId;
            lblSelectedRoom.Text = "Room #" + roomId;
            txtModalCheckIn.Text = txtCheckIn.Text;
            txtModalCheckOut.Text = txtCheckOut.Text;

            // Gate at the point of commitment, not before — browsing stays public.
            pnlLoginPrompt.Visible = !User.Identity.IsAuthenticated;
            btnConfirmBooking.Visible = User.Identity.IsAuthenticated;

            pnlBookingModal.Style["display"] = "block";
        }

        protected void btnConfirmBooking_Click(object sender, EventArgs e)
        {
            if (!User.Identity.IsAuthenticated)
            {
                Response.Redirect("~/Account/Login.aspx?ReturnUrl=" + Server.UrlEncode(Request.RawUrl));
                return;
            }

            // TODO: insert a booking record, e.g.:
            // INSERT INTO Hotel_Booking (Hotel_Room_ID, UserId, CheckIn, CheckOut, Status)
            // and consider updating Hotel_Room.hotel_room_status to 'Occupied'
            // (or a 'Reserved' status if you add one) for the matching Hotel_Room_ID.

            Response.Redirect("~/Bookings/Confirmation.aspx?roomId=" + Server.UrlEncode(hdnSelectedRoomId.Value));
        }

        private class RoomResult
        {
            public string RoomId { get; set; }
            public string RoomName { get; set; }
            public string RoomNumber { get; set; }
            public string BranchId { get; set; }
            public string Blurb { get; set; }
            public string MaxGuests { get; set; }
            public decimal PricePerNight { get; set; }
            public string ImageUrl { get; set; }
        }
    }
}
