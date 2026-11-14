using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using CodeCrafters_Major_Project_Website.Models;

namespace CodeCrafters_Major_Project_Website
{
    public partial class Rooms : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (Request.QueryString["checkin"] != null) txtCheckIn.Text = Request.QueryString["checkin"];
                if (Request.QueryString["checkout"] != null) txtCheckOut.Text = Request.QueryString["checkout"];
                if (Request.QueryString["guests"] != null && ddlGuests.Items.FindByValue(Request.QueryString["guests"]) != null)
                    ddlGuests.SelectedValue = Request.QueryString["guests"];
                BindBranches();
                BindRoomTypes();
                if (Request.QueryString["branch"] != null && ddlBranch.Items.FindByValue(Request.QueryString["branch"]) != null)
                    ddlBranch.SelectedValue = Request.QueryString["branch"];
                if (Request.QueryString["type"] != null && ddlRoomType.Items.FindByValue(Request.QueryString["type"]) != null)
                    ddlRoomType.SelectedValue = Request.QueryString["type"];

                SaveStaySelection();
                BindRooms(ddlBranch.SelectedValue, ddlRoomType.SelectedValue, 0);
            }
        }

        private void BindRooms(string branchFilter, string roomTypeFilter, decimal maxPrice)
        {
            var rooms = new List<RoomResult>();
            DateTime checkIn, checkOut;
            bool hasDateInput = !String.IsNullOrWhiteSpace(txtCheckIn.Text) || !String.IsNullOrWhiteSpace(txtCheckOut.Text);
            DateTime? dateIn = null, dateOut = null;
            if (hasDateInput)
            {
                if (!DateTime.TryParse(txtCheckIn.Text, out checkIn) || !DateTime.TryParse(txtCheckOut.Text, out checkOut))
                {
                    rptRooms.DataSource = rooms; rptRooms.DataBind(); pnlNoResults.Visible = true; return;
                }
                dateIn = checkIn.Date; dateOut = checkOut.Date;
            }
            int guests;
            Int32.TryParse(ddlGuests.SelectedValue, out guests);
            List<AvailableRoom> availableRooms;
            try { availableRooms = new AvailabilityService().GetAvailableRooms(branchFilter, roomTypeFilter, maxPrice, dateIn, dateOut, guests); }
            catch (InvalidOperationException)
            {
                rptRooms.DataSource = rooms; rptRooms.DataBind(); pnlNoResults.Visible = true; return;
            }
            foreach (var room in availableRooms)
            {
                int maxGuests = room.MaxGuests > 0 ? room.MaxGuests : 2;
                RoomTypePresentation presentation = RoomTypePresentation.For(room.RoomType, maxGuests.ToString());
                rooms.Add(new RoomResult
                {
                    RoomId = room.RoomId.ToString(), RoomName = room.RoomType,
                    RoomNumber = room.RoomNumber.ToString(), BranchId = room.BranchId, BranchName = room.BranchName,
                    Blurb = presentation.Description, MaxGuests = maxGuests.ToString(),
                    AdditionalAmenities = presentation.AdditionalAmenities, PricePerNight = room.PricePerNight,
                    ImageUrl = Default.ImageForRoomType(room.RoomType)
                });
            }
            rptRooms.DataSource = rooms; rptRooms.DataBind(); pnlNoResults.Visible = rooms.Count == 0;
        }
        protected void btnFilter_Click(object sender, EventArgs e)
        {
            decimal maxPrice = 0;
            decimal.TryParse(ddlMaxPrice.SelectedValue, out maxPrice);
            SaveStaySelection();
            BindRooms(ddlBranch.SelectedValue, ddlRoomType.SelectedValue, maxPrice);
            pnlBookingModal.Style["display"] = "none";
        }

        private static int ReadMaxStayNights()
        {
            int max;
            return Int32.TryParse(ConfigurationManager.AppSettings["Booking:MaxStayNights"], out max) && max > 0 ? max : 30;
        }

        private void SaveStaySelection()
        {
            DateTime checkIn, checkOut;
            int guestCount;
            if (DateTime.TryParse(txtCheckIn.Text, out checkIn) && DateTime.TryParse(txtCheckOut.Text, out checkOut) &&
                checkIn.Date >= DateTime.Today && checkOut.Date > checkIn.Date &&
                (checkOut.Date - checkIn.Date).TotalDays <= ReadMaxStayNights())
            {
                Session["Booking.CheckIn"] = checkIn.Date;
                Session["Booking.CheckOut"] = checkOut.Date;
                Session["Booking.GuestCount"] = Int32.TryParse(ddlGuests.SelectedValue, out guestCount) ? guestCount : 1;
            }
            else
            {
                Session.Remove("Booking.CheckIn");
                Session.Remove("Booking.CheckOut");
                Session.Remove("Booking.GuestCount");
            }
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
            public string[] AdditionalAmenities { get; set; }
            public decimal PricePerNight { get; set; }
            public string ImageUrl { get; set; }
        }
    }
}

namespace CodeCrafters_Major_Project_Website.Models
{
    public sealed class RoomTypePresentation
    {
        private RoomTypePresentation(string description, string[] additionalAmenities)
        {
            Description = description;
            AdditionalAmenities = additionalAmenities;
        }

        public string Description { get; private set; }
        public string[] AdditionalAmenities { get; private set; }

        public static RoomTypePresentation For(string roomType, string maxGuests)
        {
            string type = (roomType ?? string.Empty).Trim().ToLowerInvariant();
            string capacity = string.IsNullOrWhiteSpace(maxGuests) ? "2" : maxGuests;

            if (type.Contains("suite") && type.Contains("twin"))
                return new RoomTypePresentation("A suite with a separate living area and twin beds, accommodating up to " + capacity + " guests.", new[] { "Separate Lounge Area", "Mini Bar" });
            if (type.Contains("suite") && type.Contains("king"))
                return new RoomTypePresentation("A suite with a separate living area and one king bed, accommodating up to " + capacity + " guests.", new[] { "Separate Lounge Area", "Mini Bar" });
            if (type.Contains("executive"))
                return new RoomTypePresentation("An executive-category room with one king bed, suited to business or leisure stays for up to " + capacity + " guests.", new[] { "Work Desk", "Tea & Coffee Station" });
            if (type.Contains("deluxe"))
                return new RoomTypePresentation("A deluxe room with one king bed, offering a comfortable stay for up to " + capacity + " guests.", new[] { "Mini Bar", "In-room Safe" });
            if (type.Contains("standard") && (type.Contains("double") || type.Contains("2 double")))
                return new RoomTypePresentation("A practical standard room with two double beds, suitable for up to " + capacity + " guests.", new[] { "Air Conditioning", "Flat-screen TV" });
            if (type.Contains("standard") && type.Contains("king"))
                return new RoomTypePresentation("A comfortable, practical standard room with one king bed for up to " + capacity + " guests.", new[] { "Air Conditioning", "En-suite Bathroom" });

            return new RoomTypePresentation("A comfortable room for up to " + capacity + " guests. The room name lists its bed configuration.", new[] { "Air Conditioning", "Flat-screen TV" });
        }
    }
}
