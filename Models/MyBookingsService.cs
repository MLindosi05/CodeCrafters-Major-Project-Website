using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;

namespace CodeCrafters_Major_Project_Website.Models
{
    public sealed class BookingSummaryVm
    {
        public int BookingId { get; set; }
        public DateTime BookingDate { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int Nights { get { return Math.Max(0, (CheckOut.Date - CheckIn.Date).Days); } }
        public string Status { get; set; }
        public string BranchName { get; set; }
        public string PaymentStatus { get; set; }
        public string RefundStatus { get; set; }
        public bool HasRefund { get; set; }
        public decimal Total { get; set; }
        public int GuestCount { get; set; }
        public decimal RefundAmount { get; set; }
        public int RefundPercentage { get; set; }
        public bool CanCancel { get; set; }
        public bool CanModifyDates { get; set; }
        public bool CanMobileCheckIn { get; set; }
        public bool CanMobileCheckOut { get; set; }
        public bool CanDownloadInvoice { get; set; }
        public bool CanDownloadCreditNote { get; set; }
        public bool CanDownloadAmendedInvoice { get; set; }
        public bool CanResendConfirmation { get; set; }
        public List<BookingRoomVm> Rooms { get; set; } = new List<BookingRoomVm>();
    }

    public sealed class BookingRoomVm
    {
        public int RoomId { get; set; }
        public string RoomNumber { get; set; }
        public string RoomType { get; set; }
        public string AssignmentStatus { get; set; }
    }

    /// <summary>Reads and updates booking data using columns already present in the shared database.</summary>
    public sealed class MyBookingsService
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        public List<BookingSummaryVm> GetBookingsForUser(string email)
        {
            if (String.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("Please sign in to view bookings.");
            const string sql = @"
SELECT b.Booking_ID,b.Booking_Date,b.Checkin_Date,b.Checkout_Date,b.Booking_Total_Amount,b.Booking_Status,
       b.Credit_Amount,b.Number_Adults,b.Number_Children,b.Client_ID,br.Branch_Name,ra.Hotel_Room_ID,ra.Assignment_Status,
       hr.hotel_room_number,hr.hotel_room_type
FROM Booking b
INNER JOIN Client c ON c.Client_ID=b.Client_ID
LEFT JOIN Branch br ON br.Branch_ID=b.Branch_ID
LEFT JOIN Room_Assignment ra ON ra.Booking_ID=b.Booking_ID
LEFT JOIN Hotel_Room hr ON hr.Hotel_Room_ID=ra.Hotel_Room_ID
WHERE c.Email_Address=@Email AND c.Client_Status='Active'
ORDER BY b.Booking_Date DESC,b.Booking_ID DESC,hr.hotel_room_number";
            var bookings = new Dictionary<int, BookingSummaryVm>();
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = email;
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int id = Convert.ToInt32(reader["Booking_ID"]);
                        BookingSummaryVm summary;
                        if (!bookings.TryGetValue(id, out summary))
                        {
                            string status = Convert.ToString(reader["Booking_Status"]);
                            var payment = BookingSupportStore.ReadPayment(id);
                            decimal total = reader["Booking_Total_Amount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Booking_Total_Amount"]);
                            DateTime checkIn = reader["Checkin_Date"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["Checkin_Date"]);
                            DateTime checkOut = reader["Checkout_Date"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["Checkout_Date"]);
                            bool cancellable = status == "Pending" || status == "Booked" || status == "Confirmed";
                            summary = new BookingSummaryVm
                            {
                                BookingId = id,
                                BookingDate = reader["Booking_Date"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["Booking_Date"]),
                                CheckIn = checkIn,
                                CheckOut = checkOut,
                                Status = status,
                                BranchName = reader["Branch_Name"] == DBNull.Value ? "Branch details unavailable" : Convert.ToString(reader["Branch_Name"]),
                                Total = total,
                                GuestCount = (reader["Number_Adults"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Number_Adults"])) +
                                    (reader["Number_Children"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Number_Children"])),
                                RefundAmount = status == "Cancelled" && reader["Credit_Amount"] != DBNull.Value
                                    ? Convert.ToDecimal(reader["Credit_Amount"])
                                    : (status == "Confirmed" || status == "Booked" ? CalculateRefund(total, checkIn) : 0),
                                RefundPercentage = status == "Confirmed" || status == "Booked"
                                    ? (total <= 0 ? 0 : (int)Math.Round(CalculateRefund(total, checkIn) * 100m / total, MidpointRounding.AwayFromZero)) : 0,
                                PaymentStatus = payment != null && payment.Status == "Paid" ? "Paid by PayPal" : status == "Confirmed" || status == "Booked" || status == "CheckedIn" || status == "Completed" ? "Paid / confirmed" :
                                    status == "Cancelled" ? (reader["Credit_Amount"] != DBNull.Value && Convert.ToDecimal(reader["Credit_Amount"]) > 0 ? "Cancelled · refund credit pending" : "Cancelled") : "Payment pending",
                                RefundStatus = payment == null ? (status == "Cancelled" ? "Manual review" : "—") : payment.RefundStatus,
                                HasRefund = status == "Cancelled" && reader["Credit_Amount"] != DBNull.Value && Convert.ToDecimal(reader["Credit_Amount"]) > 0,
                                CanCancel = cancellable,
                                CanModifyDates = status == "Confirmed" || status == "Booked",
                                CanMobileCheckIn = IsWithinMobileCheckInWindow(status, checkIn, checkOut),
                                CanMobileCheckOut = status == "CheckedIn",
                                CanDownloadInvoice = status == "Confirmed" || status == "Booked" || status == "CheckedIn" || status == "Completed" ||
                                    (status == "Cancelled" && ((reader["Credit_Amount"] != DBNull.Value && Convert.ToDecimal(reader["Credit_Amount"]) > 0) ||
                                        (payment != null && payment.Status == "Paid"))),
                                CanDownloadCreditNote = status == "Cancelled" && reader["Credit_Amount"] != DBNull.Value && Convert.ToDecimal(reader["Credit_Amount"]) > 0
                                ,CanResendConfirmation = status == "Confirmed" || status == "Booked" || status == "CheckedIn" || status == "Completed"
                            };
                            bookings.Add(id, summary);
                        }
                        if (reader["Hotel_Room_ID"] != DBNull.Value)
                        {
                            int roomId = Convert.ToInt32(reader["Hotel_Room_ID"]);
                            if (!summary.Rooms.Any(room => room.RoomId == roomId))
                                summary.Rooms.Add(new BookingRoomVm
                                {
                                    RoomId = roomId,
                                    RoomNumber = reader["hotel_room_number"] == DBNull.Value ? "—" : Convert.ToString(reader["hotel_room_number"]),
                                    RoomType = reader["hotel_room_type"] == DBNull.Value ? "Room" : Convert.ToString(reader["hotel_room_type"]),
                                    AssignmentStatus = Convert.ToString(reader["Assignment_Status"])
                                });
                        }
                    }
                }
            }
            var result = bookings.Values.ToList();
            foreach (var booking in result)
                booking.CanDownloadAmendedInvoice = new InvoiceService().HasAmendedInvoice(booking.BookingId);
            return result;
        }

        public decimal CancelBooking(string email, int bookingId)
        {
            if (String.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("Please sign in before cancelling a booking.");
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        int clientId;
                        using (var client = new SqlCommand("SELECT Client_ID FROM Client WHERE Email_Address=@Email AND Client_Status='Active'", connection, transaction))
                        { client.Parameters.AddWithValue("@Email", email); object value = client.ExecuteScalar(); if (value == null) throw new InvalidOperationException("Your guest profile could not be found."); clientId = Convert.ToInt32(value); }

                        string status; decimal total;
                        using (var booking = new SqlCommand("SELECT Booking_Status,Booking_Total_Amount FROM Booking WITH (UPDLOCK,HOLDLOCK) WHERE Booking_ID=@Id AND Client_ID=@ClientId", connection, transaction))
                        {
                            booking.Parameters.AddWithValue("@Id", bookingId); booking.Parameters.AddWithValue("@ClientId", clientId);
                            using (var reader = booking.ExecuteReader())
                            { if (!reader.Read()) throw new InvalidOperationException("That booking was not found on your account."); status = Convert.ToString(reader["Booking_Status"]); total = Convert.ToDecimal(reader["Booking_Total_Amount"]); }
                        }
                        if (status != "Pending" && status != "Booked" && status != "Confirmed")
                            throw new InvalidOperationException("This booking can no longer be cancelled online.");

                        DateTime checkInDate;
                        using (var dates = new SqlCommand("SELECT Checkin_Date FROM Booking WHERE Booking_ID=@Id", connection, transaction))
                        { dates.Parameters.AddWithValue("@Id", bookingId); checkInDate = Convert.ToDateTime(dates.ExecuteScalar()).Date; }
                        decimal refund = status == "Pending" ? 0 : CalculateRefund(total, checkInDate);
                        decimal penalty = status == "Pending" ? 0 : total - refund;
                        var roomIds = new List<int>();
                        using (var rooms = new SqlCommand("SELECT Hotel_Room_ID FROM Room_Assignment WITH (UPDLOCK,HOLDLOCK) WHERE Booking_ID=@Id", connection, transaction))
                        { rooms.Parameters.AddWithValue("@Id", bookingId); using (var reader = rooms.ExecuteReader()) while (reader.Read()) roomIds.Add(Convert.ToInt32(reader[0])); }

                        using (var update = new SqlCommand(@"UPDATE Booking SET Booking_Status='Cancelled',Cancellation_Date=GETDATE(),
Cancellation_Reason=@Reason,Penalty_Amount=@Penalty,Credit_Amount=@Refund
WHERE Booking_ID=@Id AND Client_ID=@ClientId", connection, transaction))
                        {
                            update.Parameters.AddWithValue("@Reason", "Cancelled by guest"); update.Parameters.AddWithValue("@Penalty", penalty);
                            update.Parameters.AddWithValue("@Refund", refund); update.Parameters.AddWithValue("@Id", bookingId); update.Parameters.AddWithValue("@ClientId", clientId);
                            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("The booking changed while you were cancelling it. Refresh and try again.");
                        }
                        using (var assignments = new SqlCommand("UPDATE Room_Assignment SET Assignment_Status='Cancelled' WHERE Booking_ID=@Id", connection, transaction))
                        { assignments.Parameters.AddWithValue("@Id", bookingId); assignments.ExecuteNonQuery(); }
                        AvailabilityService.SyncRoomStatus(connection, transaction, roomIds);
                        transaction.Commit();
                        return refund;
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public void QueueConfirmationEmail(string email, int bookingId)
        {
            if (String.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("Please sign in to resend a confirmation.");
            const string sql = @"SELECT b.Checkin_Date,b.Checkout_Date,b.Booking_Status FROM Booking b INNER JOIN Client c ON c.Client_ID=b.Client_ID WHERE b.Booking_ID=@Id AND c.Email_Address=@Email AND c.Client_Status='Active'";
            DateTime checkIn, checkOut;
            string status;
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", bookingId); command.Parameters.AddWithValue("@Email", email); connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("That booking was not found on your account.");
                    status = Convert.ToString(reader["Booking_Status"]);
                    checkIn = Convert.ToDateTime(reader["Checkin_Date"]);
                    checkOut = Convert.ToDateTime(reader["Checkout_Date"]);
                }
            }
            if (status != "Confirmed" && status != "Booked" && status != "CheckedIn" && status != "Completed")
                throw new InvalidOperationException("Only paid bookings have a confirmation email.");
            string invoice = new InvoiceService().GetOrCreate(email, bookingId, false);
            string body = "Booking #" + bookingId + " is " + status + ". Check-in: " + checkIn.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) +
                "; check-out: " + checkOut.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + ". Your invoice and receipt are attached.";
            BookingSupportStore.EnqueueEmail(email, "Your Regal Inn booking confirmation", body, invoice);
        }

        public bool MobileCheckIn(string email, int bookingId, string documentNumber, string arrivalTime, string requests)
        {
            if (String.IsNullOrWhiteSpace(documentNumber) || documentNumber.Trim().Length > 80) throw new InvalidOperationException("Enter a valid ID or passport number.");
            if (!String.IsNullOrWhiteSpace(arrivalTime) && arrivalTime.Trim().Length > 40) throw new InvalidOperationException("Arrival time must be 40 characters or fewer.");
            if (!String.IsNullOrWhiteSpace(requests) && requests.Trim().Length > 500) throw new InvalidOperationException("Special requests must be 500 characters or fewer.");
            int window;
            if (!Int32.TryParse(ConfigurationManager.AppSettings["Booking:MobileCheckInWindow"], out window) || window < 0) window = 1;
            string receptionEmail = String.Empty, branchName = String.Empty, guestName = String.Empty;
            DateTime checkInDate = DateTime.MinValue;
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        string status; DateTime checkIn, checkOut;
                        using (var command = new SqlCommand(@"SELECT b.Booking_Status,b.Checkin_Date,b.Checkout_Date,br.Branch_Name,br.Branch_Email,c.First_Name,c.Last_Name
FROM Booking b WITH (UPDLOCK,HOLDLOCK) INNER JOIN Client c ON c.Client_ID=b.Client_ID LEFT JOIN Branch br ON br.Branch_ID=b.Branch_ID
WHERE b.Booking_ID=@Id AND c.Email_Address=@Email AND c.Client_Status='Active'", connection, transaction))
                        {
                            command.Parameters.AddWithValue("@Id", bookingId); command.Parameters.AddWithValue("@Email", email);
                            using (var reader = command.ExecuteReader())
                            {
                                if (!reader.Read()) throw new InvalidOperationException("That booking was not found on your account.");
                                status = Convert.ToString(reader["Booking_Status"]);
                                checkIn = Convert.ToDateTime(reader["Checkin_Date"]).Date;
                                checkOut = Convert.ToDateTime(reader["Checkout_Date"]).Date;
                                checkInDate = checkIn;
                                receptionEmail = Convert.ToString(reader["Branch_Email"]);
                                branchName = Convert.ToString(reader["Branch_Name"]);
                                guestName = (Convert.ToString(reader["First_Name"]) + " " + Convert.ToString(reader["Last_Name"])).Trim();
                            }
                        }
                        DateTime today = HotelToday();
                        if (status != "Confirmed" && status != "Booked") throw new InvalidOperationException("Mobile check-in is unavailable for this booking status.");
                        if (today < checkIn.AddDays(-window) || today >= checkOut) throw new InvalidOperationException("Mobile check-in opens " + window + " day(s) before arrival and closes at check-out.");
                        using (var update = new SqlCommand("UPDATE Booking SET Booking_Status='CheckedIn' WHERE Booking_ID=@Id AND Booking_Status IN ('Confirmed','Booked')", connection, transaction))
                        { update.Parameters.AddWithValue("@Id", bookingId); if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("This booking changed. Refresh and try again."); }
                        using (var assignments = new SqlCommand("UPDATE Room_Assignment SET Assignment_Status='Assigned',Actual_CheckIn_Time=GETDATE() WHERE Booking_ID=@Id AND Assignment_Status<>'Cancelled'", connection, transaction))
                        { assignments.Parameters.AddWithValue("@Id", bookingId); assignments.ExecuteNonQuery(); }
                        BookingSupportStore.SaveMobileCheckIn(bookingId, documentNumber.Trim(), (arrivalTime ?? String.Empty).Trim(), (requests ?? String.Empty).Trim());
                        transaction.Commit();
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
            if (String.IsNullOrWhiteSpace(receptionEmail)) return false;
            try
            {
                string message = "Mobile check-in completed for booking #" + bookingId + ". Guest: " + guestName + ". Branch: " + branchName +
                    ". Check-in date: " + checkInDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) +
                    ". Arrival time: " + (arrivalTime ?? String.Empty).Trim() + ". Special requests: " + (String.IsNullOrWhiteSpace(requests) ? "None provided." : requests.Trim());
                BookingSupportStore.EnqueueEmail(receptionEmail, "Mobile check-in and guest requests · Booking #" + bookingId, message);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Reception mobile check-in notification failed for booking #" + bookingId + ": " + ex);
                return false;
            }
        }

        public void MobileCheckOut(string email, int bookingId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                try
                {
                    using (var update = new SqlCommand(@"UPDATE b SET Booking_Status='Completed' FROM Booking b WITH (UPDLOCK,HOLDLOCK) INNER JOIN Client c ON c.Client_ID=b.Client_ID WHERE b.Booking_ID=@Id AND c.Email_Address=@Email AND c.Client_Status='Active' AND b.Booking_Status='CheckedIn'", connection, transaction))
                    { update.Parameters.AddWithValue("@Id", bookingId); update.Parameters.AddWithValue("@Email", email); if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Mobile check-out is available only for a booking you own that is checked in."); }
                    using (var assignments = new SqlCommand("UPDATE Room_Assignment SET Assignment_Status='Assigned',Actual_CheckOut_Time=GETDATE() WHERE Booking_ID=@Id AND Assignment_Status<>'Cancelled'", connection, transaction))
                    { assignments.Parameters.AddWithValue("@Id", bookingId); assignments.ExecuteNonQuery(); }
                    AvailabilityService.SyncRoomStatus(connection, transaction, null);
                    transaction.Commit();
                }
                catch { transaction.Rollback(); throw; }
                }
            }
        }

        private static DateTime HotelToday()
        {
            string zoneId = ConfigurationManager.AppSettings["Hotel:TimeZoneId"];
            if (String.IsNullOrWhiteSpace(zoneId)) zoneId = "South Africa Standard Time";
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zoneId)).Date;
        }

        private static bool IsWithinMobileCheckInWindow(string status, DateTime checkIn, DateTime checkOut)
        {
            if (status != "Confirmed" && status != "Booked") return false;
            int window;
            if (!Int32.TryParse(ConfigurationManager.AppSettings["Booking:MobileCheckInWindow"], out window) || window < 0) window = 1;
            DateTime today = HotelToday();
            return today >= checkIn.Date.AddDays(-window) && today < checkOut.Date;
        }

        private static decimal CalculateRefund(decimal total, DateTime checkIn)
        {
            DateTime arrival = checkIn.Date.Add(ReadCheckInTime());
            string zoneId = ConfigurationManager.AppSettings["Hotel:TimeZoneId"] ?? "South Africa Standard Time";
            DateTime hotelNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zoneId));
            TimeSpan remaining = arrival - hotelNow;
            decimal percentage = remaining.TotalDays >= 7 ? 100m : remaining.TotalHours >= 48 ? 50m : 0m;
            return Decimal.Round(total * percentage / 100m, 2, MidpointRounding.AwayFromZero);
        }

        private static TimeSpan ReadCheckInTime()
        {
            TimeSpan time;
            return TimeSpan.TryParse(ConfigurationManager.AppSettings["Hotel:CheckInTime"], out time) ? time : TimeSpan.FromHours(14);
        }
    }
}
