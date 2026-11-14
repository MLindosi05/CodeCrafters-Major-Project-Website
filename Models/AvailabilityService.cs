using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace CodeCrafters_Major_Project_Website.Models
{
    public enum BookingStatus { PendingPayment, Confirmed, CheckedIn, Completed, Cancelled, Expired }

    public sealed class AvailableRoom
    {
        public int RoomId { get; set; }
        public string BranchId { get; set; }
        public string BranchName { get; set; }
        public int RoomNumber { get; set; }
        public string RoomType { get; set; }
        public decimal PricePerNight { get; set; }
        public int MaxAdults { get; set; }
        public int MaxChildren { get; set; }
        public int MaxGuests { get { return MaxAdults + MaxChildren; } }
    }

    public sealed class BookingReservation
    {
        public int BookingId { get; set; }
        public string BranchId { get; set; }
        public string BranchName { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public int GuestCount { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public List<BookingRoomRate> Rooms { get; set; } = new List<BookingRoomRate>();
        public DateTime? HoldExpiresUtc { get; set; }
    }

    public sealed class BookingRoomRate
    {
        public int RoomId { get; set; }
        public string RoomNumber { get; set; }
        public string RoomType { get; set; }
        public decimal RatePerNight { get; set; }
        public decimal Subtotal { get; set; }
    }

    /// <summary>Availability and booking operations using the existing shared database schema.</summary>
    public sealed class AvailabilityService
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        public List<AvailableRoom> GetAvailableRooms(string branchId, string roomType, decimal maxPrice,
            DateTime? checkIn, DateTime? checkOut, int guestCount)
        {
            ValidateDates(checkIn, checkOut);
            const string sql = @"
SELECT hr.Hotel_Room_ID,hr.Branch_ID,b.Branch_Name,hr.hotel_room_number,hr.hotel_room_type,
       hr.Hotel_Room_Price,hr.Max_Adults,hr.Max_Children
FROM Hotel_Room hr INNER JOIN Branch b ON b.Branch_ID=hr.Branch_ID
WHERE (@BranchId='' OR hr.Branch_ID=@BranchId)
  AND (@RoomType='' OR hr.hotel_room_type LIKE '%' + @RoomType + '%')
  AND (@MaxPrice=0 OR hr.Hotel_Room_Price<=@MaxPrice)
  AND ((ISNULL(hr.Max_Adults,0)+ISNULL(hr.Max_Children,0)>=@GuestCount) OR (ISNULL(hr.Max_Adults,0)+ISNULL(hr.Max_Children,0)=0) OR @GuestCount<=0)
  AND ((@CheckIn IS NULL AND hr.hotel_room_status='Available') OR
       (@CheckIn IS NOT NULL AND NOT EXISTS (
          SELECT 1 FROM Room_Assignment ra
          INNER JOIN Booking bk ON bk.Booking_ID=ra.Booking_ID
          WHERE ra.Hotel_Room_ID=hr.Hotel_Room_ID
            AND ra.Assignment_Status<>'Cancelled'
            AND bk.Booking_Status IN ('Pending','Booked','Confirmed','CheckedIn')
            AND (bk.Booking_Status<>'Pending' OR ra.Assigned_Date>DATEADD(MINUTE,-@HoldMinutes,GETDATE()))
            AND bk.Checkin_Date<@CheckOut AND bk.Checkout_Date>@CheckIn
       )))
ORDER BY hr.Hotel_Room_Price ASC";
            var rooms = new List<AvailableRoom>();
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@BranchId", branchId ?? String.Empty);
                command.Parameters.AddWithValue("@RoomType", roomType ?? String.Empty);
                command.Parameters.AddWithValue("@MaxPrice", maxPrice);
                command.Parameters.AddWithValue("@GuestCount", guestCount);
                command.Parameters.AddWithValue("@HoldMinutes", ReadHoldMinutes());
                command.Parameters.Add("@CheckIn", SqlDbType.Date).Value = (object)(checkIn.HasValue ? checkIn.Value.Date : (DateTime?)null) ?? DBNull.Value;
                command.Parameters.Add("@CheckOut", SqlDbType.Date).Value = (object)(checkOut.HasValue ? checkOut.Value.Date : (DateTime?)null) ?? DBNull.Value;
                connection.Open();
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) rooms.Add(new AvailableRoom {
                        RoomId = Convert.ToInt32(reader["Hotel_Room_ID"]), BranchId = Convert.ToString(reader["Branch_ID"]),
                        BranchName = Convert.ToString(reader["Branch_Name"]), RoomNumber = Convert.ToInt32(reader["hotel_room_number"]),
                        RoomType = Convert.ToString(reader["hotel_room_type"]), PricePerNight = Convert.ToDecimal(reader["Hotel_Room_Price"]),
                        MaxAdults = reader["Max_Adults"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Max_Adults"]),
                        MaxChildren = reader["Max_Children"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Max_Children"])
                    });
            }
            return rooms;
        }

        public BookingReservation CreateBooking(string email, IList<int> roomIds, DateTime checkIn, DateTime checkOut,
            int adults, int children, BookingStatus initialStatus)
        {
            ValidateDates(checkIn, checkOut);
            if (String.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("Please log in before booking.");
            if (roomIds == null || roomIds.Count == 0) throw new InvalidOperationException("Your cart is empty.");
            if (initialStatus != BookingStatus.PendingPayment && initialStatus != BookingStatus.Confirmed)
                throw new ArgumentException("A new booking must be pending payment or confirmed.", "initialStatus");

            var uniqueRoomIds = roomIds.Distinct().ToList();
            if (uniqueRoomIds.Count != roomIds.Count) throw new InvalidOperationException("Your cart contains the same room more than once.");
            int holdMinutes = ReadHoldMinutes();
            ExpirePendingHolds();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        int clientId = GetClientId(connection, transaction, email);
                        int nights = (checkOut.Date - checkIn.Date).Days;
                        decimal total = 0;
                        string bookingBranchId = null;
                        string bookingBranchName = null;
                        var roomDetails = new List<BookingRoomRate>();
                        foreach (int roomId in uniqueRoomIds)
                        {
                            using (var room = new SqlCommand(@"SELECT hr.Hotel_Room_Price,hr.Branch_ID,hr.hotel_room_number,hr.hotel_room_type,br.Branch_Name
FROM Hotel_Room hr WITH (UPDLOCK,HOLDLOCK) INNER JOIN Branch br ON br.Branch_ID=hr.Branch_ID WHERE hr.Hotel_Room_ID=@RoomId", connection, transaction))
                            {
                                room.Parameters.AddWithValue("@RoomId", roomId);
                                using (var reader = room.ExecuteReader())
                                {
                                    if (!reader.Read()) throw new InvalidOperationException("A selected room no longer exists.");
                                    decimal rate = Convert.ToDecimal(reader["Hotel_Room_Price"]);
                                    string branch = Convert.ToString(reader["Branch_ID"]);
                                    string branchName = Convert.ToString(reader["Branch_Name"]);
                                    if (bookingBranchId != null && !String.Equals(bookingBranchId, branch, StringComparison.OrdinalIgnoreCase))
                                        throw new InvalidOperationException("Rooms in one booking must be at the same branch. Remove rooms from another branch and try again.");
                                    bookingBranchId = branch;
                                    bookingBranchName = branchName;
                                    roomDetails.Add(new BookingRoomRate { RoomId = roomId, RoomNumber = Convert.ToString(reader["hotel_room_number"]),
                                        RoomType = Convert.ToString(reader["hotel_room_type"]), RatePerNight = rate, Subtotal = rate * nights });
                                    total += rate * nights;
                                }
                            }
                            using (var overlap = new SqlCommand(@"SELECT COUNT(*) FROM Room_Assignment ra WITH (UPDLOCK,HOLDLOCK)
INNER JOIN Booking bk WITH (UPDLOCK,HOLDLOCK) ON bk.Booking_ID=ra.Booking_ID
WHERE ra.Hotel_Room_ID=@RoomId AND ra.Assignment_Status<>'Cancelled'
  AND bk.Booking_Status IN ('Pending','Booked','Confirmed','CheckedIn')
  AND (bk.Booking_Status<>'Pending' OR ra.Assigned_Date>DATEADD(MINUTE,-@HoldMinutes,GETDATE()))
  AND bk.Checkin_Date<@CheckOut AND bk.Checkout_Date>@CheckIn", connection, transaction))
                            {
                                overlap.Parameters.AddWithValue("@RoomId", roomId); overlap.Parameters.AddWithValue("@CheckIn", checkIn.Date);
                                overlap.Parameters.AddWithValue("@CheckOut", checkOut.Date); overlap.Parameters.AddWithValue("@HoldMinutes", holdMinutes);
                                if (Convert.ToInt32(overlap.ExecuteScalar()) > 0)
                                    throw new InvalidOperationException("This room is unavailable for those dates. Please choose another room or date.");
                            }
                        }

                        int bookingId;
                        using (var booking = new SqlCommand(@"INSERT INTO Booking
(Client_ID,Branch_ID,Booking_Date,Checkin_Date,Checkout_Date,Booking_Total_Amount,Booking_Status,Number_Adults,Number_Children,Booking_Type)
VALUES (@ClientId,@BranchId,CAST(GETDATE() AS date),@CheckIn,@CheckOut,@Total,@Status,@Adults,@Children,'Online'); SELECT CAST(SCOPE_IDENTITY() AS int);", connection, transaction))
                        {
                            booking.Parameters.AddWithValue("@ClientId", clientId); booking.Parameters.AddWithValue("@BranchId", bookingBranchId);
                            booking.Parameters.AddWithValue("@CheckIn", checkIn.Date); booking.Parameters.AddWithValue("@CheckOut", checkOut.Date);
                            booking.Parameters.AddWithValue("@Total", total); booking.Parameters.AddWithValue("@Status", DbStatus(initialStatus));
                            booking.Parameters.AddWithValue("@Adults", Math.Max(1, adults)); booking.Parameters.AddWithValue("@Children", Math.Max(0, children));
                            bookingId = Convert.ToInt32(booking.ExecuteScalar());
                        }
                        foreach (var room in roomDetails)
                        using (var assignment = new SqlCommand("INSERT INTO Room_Assignment (Booking_ID,Hotel_Room_ID,Assignment_Status,Assigned_Date) VALUES (@BookingId,@RoomId,'Reserved',GETDATE())", connection, transaction))
                        {
                            assignment.Parameters.AddWithValue("@BookingId", bookingId); assignment.Parameters.AddWithValue("@RoomId", room.RoomId); assignment.ExecuteNonQuery();
                        }
                        decimal subtotal = total;
                        decimal taxAmount = Decimal.Round(subtotal * ReadTaxPercentage() / 100m, 2, MidpointRounding.AwayFromZero);
                        total = subtotal + taxAmount;
                        using (var updateTotal = new SqlCommand("UPDATE Booking SET Booking_Total_Amount=@Total WHERE Booking_ID=@Id", connection, transaction))
                        { updateTotal.Parameters.AddWithValue("@Total", total); updateTotal.Parameters.AddWithValue("@Id", bookingId); updateTotal.ExecuteNonQuery(); }
                        SyncRoomStatus(connection, transaction, uniqueRoomIds);
                        transaction.Commit();
                        return new BookingReservation { BookingId = bookingId, BranchId = bookingBranchId, BranchName = bookingBranchName, TotalAmount = total,
                            Subtotal = subtotal, TaxAmount = taxAmount, GuestCount = Math.Max(1, adults) + Math.Max(0, children),
                            CheckIn = checkIn.Date, CheckOut = checkOut.Date, Rooms = roomDetails,
                            HoldExpiresUtc = initialStatus == BookingStatus.PendingPayment ? (DateTime?)DateTime.UtcNow.AddMinutes(holdMinutes) : null };
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public void ValidateRoomAvailability(IList<int> roomIds, DateTime checkIn, DateTime checkOut, int guestCount)
        {
            if (roomIds == null || roomIds.Count == 0) throw new InvalidOperationException("Your cart is empty.");
            var requested = roomIds.Distinct().ToList();
            if (requested.Count != roomIds.Count) throw new InvalidOperationException("Your cart contains the same room more than once.");
            var available = new HashSet<int>(GetAvailableRooms(String.Empty, String.Empty, 0, checkIn, checkOut, guestCount)
                .Select(room => room.RoomId));
            var unavailable = requested.Where(id => !available.Contains(id)).ToList();
            if (unavailable.Count > 0) throw new InvalidOperationException("One or more rooms in your cart are no longer available for these dates or guest count. Return to Rooms and select available rooms.");
        }

        public decimal ModifyBookingDates(string email, int bookingId, DateTime checkIn, DateTime checkOut)
        {
            ValidateDates(checkIn, checkOut);
            if (String.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("Please log in before changing a booking.");
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        int clientId = GetClientId(connection, transaction, email);
                        string status;
                        decimal oldTotal;
                        using (var booking = new SqlCommand("SELECT Booking_Status,Booking_Total_Amount FROM Booking WITH (UPDLOCK,HOLDLOCK) WHERE Booking_ID=@Id AND Client_ID=@ClientId", connection, transaction))
                        {
                            booking.Parameters.AddWithValue("@Id", bookingId); booking.Parameters.AddWithValue("@ClientId", clientId);
                            using (var reader = booking.ExecuteReader())
                            {
                                if (!reader.Read()) throw new InvalidOperationException("That booking was not found on your account.");
                                status = Convert.ToString(reader["Booking_Status"]); oldTotal = Convert.ToDecimal(reader["Booking_Total_Amount"]);
                            }
                        }
                        if (status != "Confirmed" && status != "Booked") throw new InvalidOperationException("Only confirmed bookings can have their dates changed.");
                        var roomIds = new List<int>();
                        using (var rooms = new SqlCommand("SELECT Hotel_Room_ID FROM Room_Assignment WITH (UPDLOCK,HOLDLOCK) WHERE Booking_ID=@Id AND Assignment_Status<>'Cancelled'", connection, transaction))
                        { rooms.Parameters.AddWithValue("@Id", bookingId); using (var reader = rooms.ExecuteReader()) while (reader.Read()) roomIds.Add(Convert.ToInt32(reader[0])); }
                        if (roomIds.Count == 0) throw new InvalidOperationException("This booking has no active room assignments.");

                        decimal subtotal = 0;
                        int nights = (checkOut.Date - checkIn.Date).Days;
                        foreach (int roomId in roomIds)
                        {
                            decimal rate;
                            using (var room = new SqlCommand("SELECT Hotel_Room_Price FROM Hotel_Room WITH (UPDLOCK,HOLDLOCK) WHERE Hotel_Room_ID=@RoomId", connection, transaction))
                            { room.Parameters.AddWithValue("@RoomId", roomId); object value = room.ExecuteScalar(); if (value == null) throw new InvalidOperationException("A room in this booking is no longer available."); rate = Convert.ToDecimal(value); }
                            using (var overlap = new SqlCommand(@"SELECT COUNT(*) FROM Room_Assignment ra WITH (UPDLOCK,HOLDLOCK)
INNER JOIN Booking other WITH (UPDLOCK,HOLDLOCK) ON other.Booking_ID=ra.Booking_ID
WHERE ra.Hotel_Room_ID=@RoomId AND ra.Assignment_Status<>'Cancelled' AND other.Booking_ID<>@BookingId
AND other.Booking_Status IN ('Pending','Booked','Confirmed','CheckedIn')
AND (other.Booking_Status<>'Pending' OR ra.Assigned_Date>DATEADD(MINUTE,-@HoldMinutes,GETDATE()))
AND other.Checkin_Date<@CheckOut AND other.Checkout_Date>@CheckIn", connection, transaction))
                            {
                                overlap.Parameters.AddWithValue("@RoomId", roomId); overlap.Parameters.AddWithValue("@BookingId", bookingId);
                                overlap.Parameters.AddWithValue("@CheckIn", checkIn.Date); overlap.Parameters.AddWithValue("@CheckOut", checkOut.Date);
                                overlap.Parameters.AddWithValue("@HoldMinutes", ReadHoldMinutes());
                                if (Convert.ToInt32(overlap.ExecuteScalar()) > 0) throw new InvalidOperationException("At least one room is unavailable for the new dates. No changes were made.");
                            }
                            subtotal += rate * nights;
                        }
                        decimal newTotal = Decimal.Round(subtotal * (1m + ReadTaxPercentage() / 100m), 2, MidpointRounding.AwayFromZero);
                        decimal priceDifference = newTotal - oldTotal;
                        if (priceDifference != 0)
                            throw new InvalidOperationException("The new dates change the booking total by R" + Math.Abs(priceDifference).ToString("N2") + ". No changes were made because this project has no safe payment adjustment or refund API yet.");
                        using (var update = new SqlCommand("UPDATE Booking SET Checkin_Date=@CheckIn,Checkout_Date=@CheckOut,Booking_Total_Amount=@Total WHERE Booking_ID=@Id AND Client_ID=@ClientId", connection, transaction))
                        {
                            update.Parameters.AddWithValue("@CheckIn", checkIn.Date); update.Parameters.AddWithValue("@CheckOut", checkOut.Date);
                            update.Parameters.AddWithValue("@Total", newTotal); update.Parameters.AddWithValue("@Id", bookingId); update.Parameters.AddWithValue("@ClientId", clientId);
                            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("The booking changed while you were editing it. Refresh and try again.");
                        }
                        SyncRoomStatus(connection, transaction, roomIds);
                        transaction.Commit();
                        return newTotal - oldTotal;
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public void ExtendPendingHold(IList<int> bookingIds)
        {
            if (bookingIds == null || bookingIds.Count == 0) throw new InvalidOperationException("The booking hold could not be found.");
            ExpirePendingHolds();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                try
                {
                    foreach (int id in bookingIds.Distinct())
                    using (var command = new SqlCommand(@"UPDATE ra SET Assigned_Date=GETDATE() FROM Room_Assignment ra
INNER JOIN Booking b ON b.Booking_ID=ra.Booking_ID WHERE b.Booking_ID=@Id AND b.Booking_Status='Pending'", connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        if (command.ExecuteNonQuery() == 0) throw new InvalidOperationException("The payment hold expired. Return to your cart and start checkout again.");
                    }
                    transaction.Commit();
                }
                catch { transaction.Rollback(); throw; }
                }
            }
        }

        public void SetBookingStatus(IList<int> bookingIds, BookingStatus newStatus)
        {
            if (bookingIds == null || bookingIds.Count == 0) return;
            ExpirePendingHolds();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                try
                {
                    var roomIds = new List<int>();
                    foreach (int bookingId in bookingIds.Distinct())
                    {
                        string currentStatus;
                        using (var current = new SqlCommand("SELECT Booking_Status FROM Booking WITH (UPDLOCK,HOLDLOCK) WHERE Booking_ID=@Id", connection, transaction))
                        { current.Parameters.AddWithValue("@Id", bookingId); currentStatus = Convert.ToString(current.ExecuteScalar()); }
                        if (String.IsNullOrEmpty(currentStatus)) throw new InvalidOperationException("The booking could not be found.");
                        if (!CanTransition(currentStatus, newStatus))
                        {
                            if (currentStatus == DbStatus(newStatus)) continue;
                            throw new InvalidOperationException("The booking status changed. Refresh and try again.");
                        }
                        using (var getRooms = new SqlCommand("SELECT Hotel_Room_ID FROM Room_Assignment WITH (UPDLOCK,HOLDLOCK) WHERE Booking_ID=@Id", connection, transaction))
                        { getRooms.Parameters.AddWithValue("@Id", bookingId); using (var reader = getRooms.ExecuteReader()) while (reader.Read()) roomIds.Add(Convert.ToInt32(reader[0])); }
                        using (var update = new SqlCommand("UPDATE Booking SET Booking_Status=@Status WHERE Booking_ID=@Id", connection, transaction))
                        { update.Parameters.AddWithValue("@Status", DbStatus(newStatus)); update.Parameters.AddWithValue("@Id", bookingId); update.ExecuteNonQuery(); }
                        using (var updateItems = new SqlCommand("UPDATE Room_Assignment SET Assignment_Status=@AssignmentStatus WHERE Booking_ID=@Id", connection, transaction))
                        { updateItems.Parameters.AddWithValue("@AssignmentStatus", newStatus == BookingStatus.Cancelled || newStatus == BookingStatus.Expired ? "Cancelled" : (newStatus == BookingStatus.CheckedIn || newStatus == BookingStatus.Completed ? "Assigned" : "Reserved")); updateItems.Parameters.AddWithValue("@Id", bookingId); updateItems.ExecuteNonQuery(); }
                    }
                    SyncRoomStatus(connection, transaction, roomIds.Distinct().ToList());
                    transaction.Commit();
                }
                catch { transaction.Rollback(); throw; }
                }
            }
        }

        public void RunMaintenance() { ExpirePendingHolds(); }

        /// <summary>Refreshes the legacy current-status field from confirmed stays covering today.</summary>
        public void RefreshCurrentRoomStatuses()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try { SyncRoomStatus(connection, transaction, null); transaction.Commit(); }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public void ExpirePendingHolds()
        {
            int minutes = ReadHoldMinutes();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        using (var expire = new SqlCommand(@"UPDATE b SET Booking_Status='Cancelled'
FROM Booking b WITH (UPDLOCK,ROWLOCK)
WHERE b.Booking_Status='Pending' AND EXISTS (SELECT 1 FROM Room_Assignment ra WHERE ra.Booking_ID=b.Booking_ID AND ra.Assigned_Date<=DATEADD(MINUTE,-@Minutes,GETDATE()))", connection, transaction))
                        { expire.Parameters.AddWithValue("@Minutes", minutes); expire.ExecuteNonQuery(); }
                        using (var updateAssignments = new SqlCommand(@"UPDATE ra SET Assignment_Status='Cancelled'
FROM Room_Assignment ra INNER JOIN Booking b ON b.Booking_ID=ra.Booking_ID
WHERE b.Booking_Status='Cancelled' AND ra.Assignment_Status='Reserved' AND ra.Assigned_Date<=DATEADD(MINUTE,-@Minutes,GETDATE())", connection, transaction))
                        { updateAssignments.Parameters.AddWithValue("@Minutes", minutes); updateAssignments.ExecuteNonQuery(); }
                        using (var complete = new SqlCommand("UPDATE Booking SET Booking_Status='Completed' WHERE Booking_Status IN ('Confirmed','CheckedIn','Booked') AND Checkout_Date<=@HotelDate", connection, transaction))
                        { complete.Parameters.Add("@HotelDate", SqlDbType.Date).Value = CurrentHotelDate(); complete.ExecuteNonQuery(); }
                        using (var completeAssignments = new SqlCommand(@"UPDATE ra SET Assignment_Status='Assigned'
FROM Room_Assignment ra INNER JOIN Booking b ON b.Booking_ID=ra.Booking_ID
WHERE b.Booking_Status='Completed' AND ra.Assignment_Status='Reserved'", connection, transaction))
                        { completeAssignments.ExecuteNonQuery(); }
                        SyncRoomStatus(connection, transaction, null);
                        transaction.Commit();
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        private static bool CanTransition(string from, BookingStatus to)
        {
            if (from == "Pending") return to == BookingStatus.Confirmed || to == BookingStatus.Cancelled || to == BookingStatus.Expired;
            if (from == "Booked" || from == "Confirmed") return to == BookingStatus.CheckedIn || to == BookingStatus.Cancelled || to == BookingStatus.Completed;
            if (from == "CheckedIn") return to == BookingStatus.Completed;
            return false;
        }
        private static string DbStatus(BookingStatus status)
        { return status == BookingStatus.PendingPayment ? "Pending" : status == BookingStatus.Expired ? "Cancelled" : status.ToString(); }

        private static void ValidateDates(DateTime? checkIn, DateTime? checkOut)
        {
            if (checkIn.HasValue != checkOut.HasValue) throw new InvalidOperationException("Enter both check-in and check-out dates.");
            if (!checkIn.HasValue) return;
            int maxStay;
            if (!Int32.TryParse(ConfigurationManager.AppSettings["Booking:MaxStayNights"], out maxStay) || maxStay < 1) maxStay = 30;
            if (checkIn.Value.Date < CurrentHotelDate() || checkOut.Value.Date <= checkIn.Value.Date || (checkOut.Value.Date - checkIn.Value.Date).TotalDays > maxStay)
                throw new InvalidOperationException("Choose valid stay dates within the maximum stay of " + maxStay + " nights.");
        }
        private static int ReadHoldMinutes()
        {
            int minutes;
            if (!Int32.TryParse(ConfigurationManager.AppSettings["Booking:HoldMinutes"], out minutes) || minutes < 1) throw new ConfigurationErrorsException("Booking:HoldMinutes must be a positive integer.");
            return minutes;
        }
        private static decimal ReadTaxPercentage()
        {
            decimal percentage;
            if (!Decimal.TryParse(ConfigurationManager.AppSettings["Booking:TaxPercentage"], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out percentage) || percentage < 0 || percentage > 100)
                throw new ConfigurationErrorsException("Booking:TaxPercentage must be between 0 and 100.");
            return percentage;
        }
        private static TimeZoneInfo GetHotelTimeZone()
        {
            string zoneId = ConfigurationManager.AppSettings["Hotel:TimeZoneId"];
            if (String.IsNullOrWhiteSpace(zoneId)) throw new ConfigurationErrorsException("Hotel:TimeZoneId is required.");
            return TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        }
        private static DateTime CurrentHotelDate() { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetHotelTimeZone()).Date; }
        internal static void SyncRoomStatus(SqlConnection connection, SqlTransaction transaction, IList<int> roomIds)
        {
            string filter = roomIds == null ? String.Empty : " WHERE CHARINDEX(','+CONVERT(varchar(20),hr.Hotel_Room_ID)+',',','+@RoomIds+',')>0";
            using (var command = new SqlCommand(@"UPDATE hr SET hotel_room_status=CASE WHEN EXISTS (
 SELECT 1 FROM Room_Assignment ra INNER JOIN Booking b ON b.Booking_ID=ra.Booking_ID
 WHERE ra.Hotel_Room_ID=hr.Hotel_Room_ID AND ra.Assignment_Status<>'Cancelled'
   AND b.Booking_Status IN ('Booked','Confirmed','CheckedIn')
   AND b.Checkin_Date<=@HotelDate AND b.Checkout_Date>@HotelDate
 ) THEN 'Booked' WHEN hr.hotel_room_status='Booked' THEN 'Available' ELSE hr.hotel_room_status END
FROM Hotel_Room hr
WHERE hr.hotel_room_status IN ('Available','Booked')" + (String.IsNullOrEmpty(filter) ? String.Empty : " AND CHARINDEX(','+CONVERT(varchar(20),hr.Hotel_Room_ID)+',',','+@RoomIds+',')>0"), connection, transaction))
            {
                command.Parameters.Add("@HotelDate", SqlDbType.Date).Value = CurrentHotelDate();
                if (roomIds != null) command.Parameters.AddWithValue("@RoomIds", String.Join(",", roomIds));
                command.ExecuteNonQuery();
            }
        }
        private static int GetClientId(SqlConnection connection, SqlTransaction transaction, string email)
        {
            using (var command = new SqlCommand("SELECT Client_ID FROM Client WHERE Email_Address=@Email AND Client_Status='Active'", connection, transaction))
            { command.Parameters.AddWithValue("@Email", email); object id = command.ExecuteScalar(); if (id == null) throw new InvalidOperationException("Your guest profile is unavailable. Please contact the hotel."); return Convert.ToInt32(id); }
        }
    }
}
