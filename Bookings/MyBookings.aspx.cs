using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.UI;

namespace CodeCrafters_Major_Project_Website.Bookings
{
    public partial class MyBookings : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack) BindBookings();
        }

        private void BindBookings()
        {
            const string sql = @"SELECT b.Booking_ID AS BookingId, br.Branch_Name AS BranchName, hr.hotel_room_type AS RoomType,
b.Checkin_Date AS CheckIn, b.Checkout_Date AS CheckOut, b.Booking_Total_Amount AS Total, b.Booking_Status AS Status
FROM Booking b INNER JOIN Client c ON c.Client_ID=b.Client_ID INNER JOIN Branch br ON br.Branch_ID=b.Branch_ID
LEFT JOIN Room_Assignment ra ON ra.Booking_ID=b.Booking_ID LEFT JOIN Hotel_Room hr ON hr.Hotel_Room_ID=ra.Hotel_Room_ID
WHERE c.Email_Address=@Email ORDER BY b.Checkin_Date DESC";
            using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Email", User.Identity.Name);
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    BookingsRepeater.DataSource = reader;
                    BookingsRepeater.DataBind();
                    if (!reader.HasRows) Message.Text = "<div class='form-msg error'>You do not have any bookings yet.</div>";
                }
            }
        }
    }
}
