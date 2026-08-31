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
    public partial class Rooms : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Pre-fill from querystring if arriving from the homepage search widget
                if (Request.QueryString["checkin"] != null) txtCheckIn.Text = Request.QueryString["checkin"];
                if (Request.QueryString["checkout"] != null) txtCheckOut.Text = Request.QueryString["checkout"];
                BindBranches();
                BindRoomTypes();
                if (Request.QueryString["branch"] != null && ddlBranch.Items.FindByValue(Request.QueryString["branch"]) != null)
                    ddlBranch.SelectedValue = Request.QueryString["branch"];
                if (Request.QueryString["type"] != null && ddlRoomType.Items.FindByValue(Request.QueryString["type"]) != null)
                    ddlRoomType.SelectedValue = Request.QueryString["type"];

                BindRooms(ddlBranch.SelectedValue, ddlRoomType.SelectedValue, 0);
            }
        }

        // Matches your real Hotel_Room table: hotel_room_type holds free-text
        // like "Standard Room 1 King Bed" / "Suite Room Twin Beds", so the
        // dropdown filter (Standard/Deluxe/Suite) does a partial LIKE match
        // rather than an exact equals.
        private void BindRooms(string branchFilter, string roomTypeFilter, decimal maxPrice)
        {
            var rooms = new List<RoomResult>();
            string connStr = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

            const string sql = @"SELECT hr.Hotel_Room_ID, hr.Branch_ID, b.Branch_Name, hr.hotel_room_number, hr.hotel_room_type,
                                         hr.Hotel_Room_Price, hr.Max_Adults, hr.Max_Children
                                  FROM Hotel_Room hr
                                  INNER JOIN Branch b ON b.Branch_ID = hr.Branch_ID
                                  WHERE hr.hotel_room_status = 'Available'
                                    AND (@BranchId = '' OR hr.Branch_ID = @BranchId)
                                    AND (@RoomType = '' OR hr.hotel_room_type LIKE '%' + @RoomType + '%')
                                    AND (@MaxPrice = 0 OR Hotel_Room_Price <= @MaxPrice)
                                  ORDER BY hr.Hotel_Room_Price ASC";

            using (var conn = new SqlConnection(connStr))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@RoomType", roomTypeFilter ?? string.Empty);
                cmd.Parameters.AddWithValue("@BranchId", branchFilter ?? string.Empty);
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
                            BranchName = reader["Branch_Name"].ToString(),
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
            BindRooms(ddlBranch.SelectedValue, ddlRoomType.SelectedValue, maxPrice);

            // Make sure a previously-opened booking modal doesn't carry over
            // into the new search results via ViewState.
            pnlBookingModal.Style["display"] = "none";
        }

        private void BindBranches()
        {
            ddlBranch.Items.Clear();
            ddlBranch.Items.Add(new ListItem("All branches", ""));
            using (var conn = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString))
            using (var cmd = new SqlCommand("SELECT Branch_ID, Branch_Name FROM Branch WHERE Branch_Status = 'Active' ORDER BY Branch_Name", conn))
            {
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    ddlBranch.DataSource = reader;
                    ddlBranch.DataTextField = "Branch_Name";
                    ddlBranch.DataValueField = "Branch_ID";
                    ddlBranch.DataBind();
                }
            }
        }

        // Populates Room Type with every distinct type currently in Hotel_Room.
        private void BindRoomTypes()
        {
            ddlRoomType.Items.Clear();
            ddlRoomType.Items.Add(new ListItem("Any", ""));

            using (var conn = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString))
            using (var cmd = new SqlCommand("SELECT DISTINCT hotel_room_type FROM Hotel_Room ORDER BY hotel_room_type", conn))
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

            DateTime checkIn, checkOut;
            int roomId;
            if (!int.TryParse(hdnSelectedRoomId.Value, out roomId) ||
                !DateTime.TryParse(txtModalCheckIn.Text, out checkIn) ||
                !DateTime.TryParse(txtModalCheckOut.Text, out checkOut))
            {
                pnlBookingModal.Style["display"] = "block";
                lblSelectedRoom.Text = "Please enter valid booking dates.";
                return;
            }
            try
            {
                int bookingId = new BookingService().CreateBooking(User.Identity.Name, roomId, checkIn, checkOut, 2);
                Response.Redirect("~/Bookings/Confirmation.aspx?bookingId=" + bookingId);
            }
            catch (InvalidOperationException ex)
            {
                pnlBookingModal.Style["display"] = "block";
                lblSelectedRoom.Text = Server.HtmlEncode(ex.Message);
            }
        }

        private class RoomResult
        {
            public string RoomId { get; set; }
            public string RoomName { get; set; }
            public string RoomNumber { get; set; }
            public string BranchId { get; set; }
            public string BranchName { get; set; }
            public string Blurb { get; set; }
            public string MaxGuests { get; set; }
            public decimal PricePerNight { get; set; }
            public string ImageUrl { get; set; }
        }
    }
}