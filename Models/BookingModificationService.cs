using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;

namespace CodeCrafters_Major_Project_Website.Models
{
    public sealed class BookingChangeRequest
    {
        public int BookingId { get; set; }
        public string Mode { get; set; } // Dates, Upgrade, Branch
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int SourceRoomId { get; set; }
        public int TargetRoomId { get; set; }
        public string TargetBranchId { get; set; }
    }

    public sealed class BookingChangeQuote
    {
        public int BookingId { get; set; }
        public decimal OldTotal { get; set; }
        public decimal NewTotal { get; set; }
        public decimal Difference { get { return NewTotal - OldTotal; } }
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public string BranchId { get; set; }
        public string BranchName { get; set; }
        public List<BookingRoomRate> Rooms { get; set; } = new List<BookingRoomRate>();
        public Dictionary<int, int> RoomMap { get; set; } = new Dictionary<int, int>();
    }

    public sealed class BookingModificationService
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        public BookingChangeQuote Quote(string email, BookingChangeRequest request)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try { return Calculate(connection, transaction, email, request); }
                    finally { transaction.Rollback(); }
                }
            }
        }

        public BookingChangeQuote Apply(string email, BookingChangeRequest request, decimal expectedNewTotal)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        var quote = Calculate(connection, transaction, email, request);
                        if (Decimal.Round(quote.NewTotal, 2, MidpointRounding.AwayFromZero) != Decimal.Round(expectedNewTotal, 2, MidpointRounding.AwayFromZero))
                            throw new InvalidOperationException("Room availability or pricing changed while payment was being confirmed. No booking changes were applied.");

                        foreach (var pair in quote.RoomMap.Where(p => p.Key != p.Value))
                        using (var assign = new SqlCommand(@"UPDATE Room_Assignment SET Hotel_Room_ID=@NewRoom
WHERE Booking_ID=@BookingId AND Hotel_Room_ID=@OldRoom AND Assignment_Status<>'Cancelled'", connection, transaction))
                        {
                            assign.Parameters.AddWithValue("@NewRoom", pair.Value); assign.Parameters.AddWithValue("@BookingId", request.BookingId); assign.Parameters.AddWithValue("@OldRoom", pair.Key);
                            if (assign.ExecuteNonQuery() != 1) throw new InvalidOperationException("A room assignment changed before this update. Refresh and retry.");
                        }

                        string branchId = quote.BranchId;
                        using (var update = new SqlCommand(@"UPDATE Booking SET Checkin_Date=@CheckIn,Checkout_Date=@CheckOut,
Branch_ID=@BranchId,Booking_Total_Amount=@Total WHERE Booking_ID=@BookingId AND Booking_Status IN ('Booked','Confirmed')", connection, transaction))
                        {
                            update.Parameters.AddWithValue("@CheckIn", quote.CheckIn.Date); update.Parameters.AddWithValue("@CheckOut", quote.CheckOut.Date);
                            update.Parameters.AddWithValue("@BranchId", branchId); update.Parameters.AddWithValue("@Total", quote.NewTotal); update.Parameters.AddWithValue("@BookingId", request.BookingId);
                            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Only confirmed bookings can be changed.");
                        }
                        AvailabilityService.SyncRoomStatus(connection, transaction, quote.RoomMap.Keys.Concat(quote.RoomMap.Values).Distinct().ToList());
                        transaction.Commit();
                        return quote;
                    }
                    catch { try { transaction.Rollback(); } catch { } throw; }
                }
            }
        }

        public bool IsChangeApplied(string email, BookingChangeQuote expected)
        {
            if (expected == null) return false;
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"SELECT b.Checkin_Date,b.Checkout_Date,b.Booking_Total_Amount,b.Branch_ID
FROM Booking b INNER JOIN Client c ON c.Client_ID=b.Client_ID
WHERE b.Booking_ID=@Id AND c.Email_Address=@Email AND c.Client_Status='Active' AND b.Booking_Status IN ('Booked','Confirmed')", connection))
                {
                    command.Parameters.AddWithValue("@Id", expected.BookingId); command.Parameters.AddWithValue("@Email", email ?? String.Empty);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) return false;
                        if (Convert.ToDateTime(reader["Checkin_Date"]).Date != expected.CheckIn.Date || Convert.ToDateTime(reader["Checkout_Date"]).Date != expected.CheckOut.Date ||
                            Convert.ToDecimal(reader["Booking_Total_Amount"]) != expected.NewTotal || !String.Equals(Convert.ToString(reader["Branch_ID"]), expected.BranchId, StringComparison.OrdinalIgnoreCase)) return false;
                    }
                }
                var roomIds = new List<int>();
                using (var command = new SqlCommand("SELECT Hotel_Room_ID FROM Room_Assignment WHERE Booking_ID=@Id AND Assignment_Status<>'Cancelled'", connection))
                { command.Parameters.AddWithValue("@Id", expected.BookingId); using (var reader = command.ExecuteReader()) while (reader.Read()) roomIds.Add(Convert.ToInt32(reader[0])); }
                return roomIds.OrderBy(id => id).SequenceEqual(expected.Rooms.Select(r => r.RoomId).OrderBy(id => id));
            }
        }

        public List<AvailableRoom> GetOptions(string email, int bookingId, int sourceRoomId, string mode, string targetBranchId)
        {
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(@"SELECT b.Client_ID,b.Booking_Status,b.Branch_ID,b.Checkin_Date,b.Checkout_Date,
 b.Number_Adults,b.Number_Children,old.hotel_room_type AS OldType,old.Hotel_Room_Price AS OldPrice,old.Branch_ID AS OldBranch
FROM Booking b INNER JOIN Client c ON c.Client_ID=b.Client_ID
INNER JOIN Room_Assignment ra ON ra.Booking_ID=b.Booking_ID AND ra.Hotel_Room_ID=@SourceRoom AND ra.Assignment_Status<>'Cancelled'
INNER JOIN Hotel_Room old ON old.Hotel_Room_ID=ra.Hotel_Room_ID
WHERE b.Booking_ID=@BookingId AND c.Email_Address=@Email AND c.Client_Status='Active'", connection))
            {
                command.Parameters.AddWithValue("@SourceRoom", sourceRoomId); command.Parameters.AddWithValue("@BookingId", bookingId); command.Parameters.AddWithValue("@Email", email ?? String.Empty);
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("The selected room is not part of a booking on your account.");
                    string status = Convert.ToString(reader["Booking_Status"]);
                    if (status != "Booked" && status != "Confirmed") throw new InvalidOperationException("Only upcoming confirmed bookings can change rooms.");
                    string oldType = Convert.ToString(reader["OldType"]), oldBranch = Convert.ToString(reader["OldBranch"]);
                    decimal oldPrice = Convert.ToDecimal(reader["OldPrice"]);
                    DateTime checkIn = Convert.ToDateTime(reader["Checkin_Date"]).Date, checkOut = Convert.ToDateTime(reader["Checkout_Date"]).Date;
                    int guests = Convert.ToInt32(reader["Number_Adults"]) + (reader["Number_Children"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Number_Children"]));
                    reader.Close();
                    string sql = @"SELECT hr.Hotel_Room_ID,hr.Branch_ID,br.Branch_Name,hr.hotel_room_number,hr.hotel_room_type,hr.Hotel_Room_Price,hr.Max_Adults,hr.Max_Children
FROM Hotel_Room hr INNER JOIN Branch br ON br.Branch_ID=hr.Branch_ID
WHERE hr.Hotel_Room_ID<>@SourceRoom AND
 ((@Mode='Upgrade' AND hr.Branch_ID=@OldBranch AND hr.Hotel_Room_Price>@OldPrice) OR
  (@Mode='Branch' AND hr.Branch_ID=@TargetBranch AND hr.hotel_room_type=@OldType))
 AND (ISNULL(hr.Max_Adults,0)+ISNULL(hr.Max_Children,0)=0 OR ISNULL(hr.Max_Adults,0)+ISNULL(hr.Max_Children,0)>=@Guests)
 AND NOT EXISTS (SELECT 1 FROM Room_Assignment own WHERE own.Booking_ID=@BookingId AND own.Hotel_Room_ID=hr.Hotel_Room_ID AND own.Assignment_Status<>'Cancelled')
 AND NOT EXISTS (SELECT 1 FROM Room_Assignment ra INNER JOIN Booking bk ON bk.Booking_ID=ra.Booking_ID
   WHERE ra.Hotel_Room_ID=hr.Hotel_Room_ID AND ra.Assignment_Status<>'Cancelled' AND bk.Booking_ID<>@BookingId
   AND bk.Booking_Status IN ('Pending','Booked','Confirmed','CheckedIn')
   AND (bk.Booking_Status<>'Pending' OR ra.Assigned_Date>DATEADD(MINUTE,-@HoldMinutes,GETDATE()))
   AND bk.Checkin_Date<@CheckOut AND bk.Checkout_Date>@CheckIn)
ORDER BY hr.Hotel_Room_Price";
                    var options = new List<AvailableRoom>();
                    using (var list = new SqlCommand(sql, connection))
                    {
                        list.Parameters.AddWithValue("@SourceRoom", sourceRoomId); list.Parameters.AddWithValue("@BookingId", bookingId);
                        list.Parameters.AddWithValue("@Mode", mode ?? String.Empty); list.Parameters.AddWithValue("@OldBranch", oldBranch);
                        list.Parameters.AddWithValue("@TargetBranch", targetBranchId ?? String.Empty); list.Parameters.AddWithValue("@OldType", oldType);
                        list.Parameters.AddWithValue("@OldPrice", oldPrice); list.Parameters.AddWithValue("@Guests", Math.Max(1, guests));
                        list.Parameters.AddWithValue("@CheckIn", checkIn); list.Parameters.AddWithValue("@CheckOut", checkOut);
                        list.Parameters.AddWithValue("@HoldMinutes", ReadHoldMinutes());
                        using (var rows = list.ExecuteReader()) while (rows.Read()) options.Add(new AvailableRoom
                        {
                            RoomId = Convert.ToInt32(rows["Hotel_Room_ID"]), BranchId = Convert.ToString(rows["Branch_ID"]), BranchName = Convert.ToString(rows["Branch_Name"]),
                            RoomNumber = Convert.ToInt32(rows["hotel_room_number"]), RoomType = Convert.ToString(rows["hotel_room_type"]),
                            PricePerNight = Convert.ToDecimal(rows["Hotel_Room_Price"]), MaxAdults = rows["Max_Adults"] == DBNull.Value ? 0 : Convert.ToInt32(rows["Max_Adults"]),
                            MaxChildren = rows["Max_Children"] == DBNull.Value ? 0 : Convert.ToInt32(rows["Max_Children"])
                        });
                    }
                    return options;
                }
            }
        }

        public List<KeyValuePair<string, string>> GetBranches(string email, int bookingId)
        {
            const string sql = @"SELECT DISTINCT br.Branch_ID,br.Branch_Name FROM Branch br
INNER JOIN Hotel_Room hr ON hr.Branch_ID=br.Branch_ID
WHERE EXISTS (SELECT 1 FROM Booking b INNER JOIN Client c ON c.Client_ID=b.Client_ID WHERE b.Booking_ID=@Id AND c.Email_Address=@Email AND c.Client_Status='Active' AND b.Booking_Status IN ('Booked','Confirmed'))
ORDER BY br.Branch_Name";
            var result = new List<KeyValuePair<string, string>>();
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", bookingId); command.Parameters.AddWithValue("@Email", email ?? String.Empty); connection.Open();
                using (var reader = command.ExecuteReader()) while (reader.Read()) result.Add(new KeyValuePair<string, string>(Convert.ToString(reader["Branch_ID"]), Convert.ToString(reader["Branch_Name"])));
            }
            return result;
        }

        private BookingChangeQuote Calculate(SqlConnection connection, SqlTransaction transaction, string email, BookingChangeRequest request)
        {
            ValidateDates(request.CheckIn, request.CheckOut);
            if (request.BookingId < 1 || String.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("Sign in and choose a valid booking.");
            string status, originalBranch; DateTime originalIn, originalOut; decimal oldTotal; int clientId, adults, children;
            using (var cmd = new SqlCommand(@"SELECT b.Client_ID,b.Booking_Status,b.Branch_ID,b.Checkin_Date,b.Checkout_Date,b.Booking_Total_Amount,b.Number_Adults,b.Number_Children
FROM Booking b WITH (UPDLOCK,HOLDLOCK) INNER JOIN Client c ON c.Client_ID=b.Client_ID
WHERE b.Booking_ID=@Id AND c.Email_Address=@Email AND c.Client_Status='Active'", connection, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", request.BookingId); cmd.Parameters.AddWithValue("@Email", email);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) throw new InvalidOperationException("That booking was not found on your account.");
                    clientId = Convert.ToInt32(r["Client_ID"]); status = Convert.ToString(r["Booking_Status"]); originalBranch = Convert.ToString(r["Branch_ID"]);
                    originalIn = Convert.ToDateTime(r["Checkin_Date"]).Date; originalOut = Convert.ToDateTime(r["Checkout_Date"]).Date;
                    oldTotal = Convert.ToDecimal(r["Booking_Total_Amount"]); adults = Convert.ToInt32(r["Number_Adults"]); children = r["Number_Children"] == DBNull.Value ? 0 : Convert.ToInt32(r["Number_Children"]);
                }
            }
            if (status != "Booked" && status != "Confirmed") throw new InvalidOperationException("Only bookings in Booked or Confirmed status can be changed.");
            var oldRooms = new List<BookingRoomRate>();
            using (var cmd = new SqlCommand(@"SELECT hr.Hotel_Room_ID,hr.hotel_room_number,hr.hotel_room_type,hr.Hotel_Room_Price,hr.Branch_ID
FROM Room_Assignment ra WITH (UPDLOCK,HOLDLOCK) INNER JOIN Hotel_Room hr WITH (UPDLOCK,HOLDLOCK) ON hr.Hotel_Room_ID=ra.Hotel_Room_ID
WHERE ra.Booking_ID=@Id AND ra.Assignment_Status<>'Cancelled'", connection, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", request.BookingId);
                using (var r = cmd.ExecuteReader()) while (r.Read()) oldRooms.Add(new BookingRoomRate { RoomId = Convert.ToInt32(r["Hotel_Room_ID"]), RoomNumber = Convert.ToString(r["hotel_room_number"]), RoomType = Convert.ToString(r["hotel_room_type"]), RatePerNight = Convert.ToDecimal(r["Hotel_Room_Price"]) });
            }
            if (oldRooms.Count == 0) throw new InvalidOperationException("This booking has no active room assignments.");
            DateTime checkIn = request.Mode == "Dates" ? request.CheckIn.Date : originalIn;
            DateTime checkOut = request.Mode == "Dates" ? request.CheckOut.Date : originalOut;
            string targetBranch = request.Mode == "Branch" ? request.TargetBranchId : originalBranch;
            var map = oldRooms.ToDictionary(room => room.RoomId, room => room.RoomId);
            if (request.Mode == "Upgrade")
            {
                if (!map.ContainsKey(request.SourceRoomId)) throw new InvalidOperationException("Select a room in this booking to upgrade.");
                map[request.SourceRoomId] = request.TargetRoomId;
            }
            else if (request.Mode == "Branch")
            {
                if (String.IsNullOrWhiteSpace(targetBranch) || String.Equals(targetBranch, originalBranch, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Choose a different branch.");
                map = MapEquivalentRooms(connection, transaction, request.BookingId, oldRooms, targetBranch, checkIn, checkOut, adults + children);
            }
            else if (request.Mode != "Dates") throw new InvalidOperationException("Unknown booking change type.");

            if (map.Values.Distinct().Count() != oldRooms.Count) throw new InvalidOperationException("Each booking room must map to a different available room.");
            int nights = (checkOut - checkIn).Days;
            decimal subtotal = 0; var newRooms = new List<BookingRoomRate>();
            foreach (var old in oldRooms)
            {
                int targetId = map[old.RoomId];
                string type, roomNumber, branch; decimal rate; int maxAdults, maxChildren;
                using (var cmd = new SqlCommand("SELECT hotel_room_type,hotel_room_number,Branch_ID,Hotel_Room_Price,Max_Adults,Max_Children FROM Hotel_Room WITH (UPDLOCK,HOLDLOCK) WHERE Hotel_Room_ID=@Room", connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@Room", targetId);
                    using (var r = cmd.ExecuteReader())
                    { if (!r.Read()) throw new InvalidOperationException("A selected room is no longer available."); type = Convert.ToString(r["hotel_room_type"]); roomNumber = Convert.ToString(r["hotel_room_number"]); branch = Convert.ToString(r["Branch_ID"]); rate = Convert.ToDecimal(r["Hotel_Room_Price"]); maxAdults = r["Max_Adults"] == DBNull.Value ? 0 : Convert.ToInt32(r["Max_Adults"]); maxChildren = r["Max_Children"] == DBNull.Value ? 0 : Convert.ToInt32(r["Max_Children"]); }
                }
                if (!String.Equals(branch, targetBranch, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The selected rooms must all belong to the target branch.");
                if (request.Mode == "Upgrade" && targetId != old.RoomId && rate <= old.RatePerNight) throw new InvalidOperationException("An upgrade must be a higher-priced room in the same branch.");
                if (request.Mode == "Branch" && !String.Equals(type, old.RoomType, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Branch changes require equivalent room types.");
                if ((maxAdults + maxChildren) > 0 && maxAdults + maxChildren < adults + children) throw new InvalidOperationException("A replacement room does not have enough guest capacity.");
                using (var overlap = new SqlCommand(@"SELECT COUNT(*) FROM Room_Assignment ra WITH (UPDLOCK,HOLDLOCK) INNER JOIN Booking bk WITH (UPDLOCK,HOLDLOCK) ON bk.Booking_ID=ra.Booking_ID
WHERE ra.Hotel_Room_ID=@Room AND ra.Assignment_Status<>'Cancelled' AND bk.Booking_ID<>@BookingId
AND bk.Booking_Status IN ('Pending','Booked','Confirmed','CheckedIn') AND (bk.Booking_Status<>'Pending' OR ra.Assigned_Date>DATEADD(MINUTE,-@HoldMinutes,GETDATE()))
AND bk.Checkin_Date<@CheckOut AND bk.Checkout_Date>@CheckIn", connection, transaction))
                {
                    overlap.Parameters.AddWithValue("@Room", targetId); overlap.Parameters.AddWithValue("@BookingId", request.BookingId); overlap.Parameters.AddWithValue("@CheckIn", checkIn); overlap.Parameters.AddWithValue("@CheckOut", checkOut); overlap.Parameters.AddWithValue("@HoldMinutes", ReadHoldMinutes());
                    if (Convert.ToInt32(overlap.ExecuteScalar()) > 0) throw new InvalidOperationException("A requested room is no longer available for these dates.");
                }
                newRooms.Add(new BookingRoomRate { RoomId = targetId, RoomNumber = roomNumber, RoomType = type, RatePerNight = rate, Subtotal = rate * nights });
                subtotal += rate * nights;
            }
            decimal tax = Decimal.Round(subtotal * ReadTaxPercentage() / 100m, 2, MidpointRounding.AwayFromZero);
            string branchName;
            using (var cmd = new SqlCommand("SELECT Branch_Name FROM Branch WHERE Branch_ID=@Id", connection, transaction))
            { cmd.Parameters.AddWithValue("@Id", targetBranch); object name = cmd.ExecuteScalar(); branchName = name == null ? "Branch details unavailable" : Convert.ToString(name); }
            return new BookingChangeQuote { BookingId = request.BookingId, OldTotal = oldTotal, NewTotal = subtotal + tax, CheckIn = checkIn, CheckOut = checkOut, BranchId = targetBranch, BranchName = branchName, Rooms = newRooms, RoomMap = map };
        }

        private Dictionary<int, int> MapEquivalentRooms(SqlConnection connection, SqlTransaction transaction, int bookingId, IList<BookingRoomRate> rooms, string branch, DateTime checkIn, DateTime checkOut, int guests)
        {
            var map = new Dictionary<int, int>(); var used = new HashSet<int>();
            foreach (var old in rooms.OrderBy(r => r.RoomType).ThenBy(r => r.RoomId))
            using (var cmd = new SqlCommand(@"SELECT TOP 1 hr.Hotel_Room_ID FROM Hotel_Room hr WITH (UPDLOCK,HOLDLOCK)
WHERE hr.Branch_ID=@Branch AND hr.hotel_room_type=@Type AND (ISNULL(hr.Max_Adults,0)+ISNULL(hr.Max_Children,0)=0 OR ISNULL(hr.Max_Adults,0)+ISNULL(hr.Max_Children,0)>=@Guests)
AND NOT EXISTS (SELECT 1 FROM Room_Assignment own WHERE own.Booking_ID=@BookingId AND own.Hotel_Room_ID=hr.Hotel_Room_ID AND own.Assignment_Status<>'Cancelled')
AND NOT EXISTS (SELECT 1 FROM Room_Assignment ra WITH (UPDLOCK,HOLDLOCK) INNER JOIN Booking b WITH (UPDLOCK,HOLDLOCK) ON b.Booking_ID=ra.Booking_ID
 WHERE ra.Hotel_Room_ID=hr.Hotel_Room_ID AND ra.Assignment_Status<>'Cancelled' AND b.Booking_ID<>@BookingId
 AND b.Booking_Status IN ('Pending','Booked','Confirmed','CheckedIn') AND (b.Booking_Status<>'Pending' OR ra.Assigned_Date>DATEADD(MINUTE,-@HoldMinutes,GETDATE()))
 AND b.Checkin_Date<@CheckOut AND b.Checkout_Date>@CheckIn)
ORDER BY hr.Hotel_Room_Price,hr.Hotel_Room_ID", connection, transaction))
            {
                cmd.Parameters.AddWithValue("@Branch", branch); cmd.Parameters.AddWithValue("@Type", old.RoomType); cmd.Parameters.AddWithValue("@Guests", Math.Max(1, guests)); cmd.Parameters.AddWithValue("@BookingId", bookingId);
                cmd.Parameters.AddWithValue("@CheckIn", checkIn); cmd.Parameters.AddWithValue("@CheckOut", checkOut); cmd.Parameters.AddWithValue("@HoldMinutes", ReadHoldMinutes());
                object candidate = cmd.ExecuteScalar();
                if (candidate == null || candidate == DBNull.Value) throw new InvalidOperationException("No equivalent " + old.RoomType + " room is available at that branch for your dates.");
                int target = Convert.ToInt32(candidate);
                if (!used.Add(target)) throw new InvalidOperationException("There are not enough equivalent rooms at that branch for this booking.");
                map[old.RoomId] = target;
            }
            return map;
        }

        private static void ValidateDates(DateTime checkIn, DateTime checkOut)
        {
            int maxStay; if (!Int32.TryParse(ConfigurationManager.AppSettings["Booking:MaxStayNights"], out maxStay) || maxStay < 1) maxStay = 30;
            string zone = ConfigurationManager.AppSettings["Hotel:TimeZoneId"] ?? "South Africa Standard Time";
            DateTime today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zone)).Date;
            if (checkIn.Date < today || checkOut.Date <= checkIn.Date || (checkOut.Date - checkIn.Date).TotalDays > maxStay)
                throw new InvalidOperationException("Choose valid stay dates within " + maxStay + " nights.");
        }
        private static int ReadHoldMinutes() { int m; return Int32.TryParse(ConfigurationManager.AppSettings["Booking:HoldMinutes"], out m) && m > 0 ? m : 15; }
        private static decimal ReadTaxPercentage() { decimal p; return Decimal.TryParse(ConfigurationManager.AppSettings["Booking:TaxPercentage"], NumberStyles.Number, CultureInfo.InvariantCulture, out p) && p >= 0 && p <= 100 ? p : 0m; }
    }
}
