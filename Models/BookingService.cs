using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace CodeCrafters_Major_Project_Website.Models
{
    public class BookingService
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        public int CreateBooking(string email, int roomId, DateTime checkIn, DateTime checkOut, int adults)
        {
            if (checkIn.Date < DateTime.Today || checkOut.Date <= checkIn.Date)
                throw new InvalidOperationException("Choose a future check-in date and a check-out date after it.");

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        int clientId = GetClientId(connection, transaction, email);
                        decimal nightlyRate;
                        string branchId;
                        using (var room = new SqlCommand(@"SELECT Hotel_Room_Price, Branch_ID FROM Hotel_Room WITH (UPDLOCK, HOLDLOCK)
WHERE Hotel_Room_ID = @RoomId AND hotel_room_status = 'Available'", connection, transaction))
                        {
                            room.Parameters.AddWithValue("@RoomId", roomId);
                            using (var reader = room.ExecuteReader())
                            {
                                if (!reader.Read()) throw new InvalidOperationException("That room is no longer available.");
                                nightlyRate = Convert.ToDecimal(reader["Hotel_Room_Price"]);
                                branchId = Convert.ToString(reader["Branch_ID"]);
                            }
                        }

                        using (var overlap = new SqlCommand(@"SELECT COUNT(*) FROM Room_Assignment ra WITH (UPDLOCK, HOLDLOCK)
INNER JOIN Booking b ON b.Booking_ID = ra.Booking_ID
WHERE ra.Hotel_Room_ID = @RoomId
  AND ISNULL(b.Booking_Status, 'Confirmed') NOT IN ('Cancelled', 'Archived')
  AND b.Checkin_Date < @CheckOut AND b.Checkout_Date > @CheckIn", connection, transaction))
                        {
                            overlap.Parameters.AddWithValue("@RoomId", roomId);
                            overlap.Parameters.AddWithValue("@CheckIn", checkIn.Date);
                            overlap.Parameters.AddWithValue("@CheckOut", checkOut.Date);
                            if (Convert.ToInt32(overlap.ExecuteScalar()) > 0)
                                throw new InvalidOperationException("This room was just booked for those dates. Please choose another room or date.");
                        }

                        int nights = (checkOut.Date - checkIn.Date).Days;
                        int bookingId;
                        using (var booking = new SqlCommand(@"INSERT INTO Booking
(Client_ID, Branch_ID, Booking_Date, Checkin_Date, Checkout_Date, Booking_Total_Amount, Booking_Status, Number_Adults, Number_Children, Booking_Type)
VALUES (@ClientId, @BranchId, CAST(GETDATE() AS date), @CheckIn, @CheckOut, @Total, 'Confirmed', @Adults, 0, 'Online');
SELECT CAST(SCOPE_IDENTITY() AS int);", connection, transaction))
                        {
                            booking.Parameters.AddWithValue("@ClientId", clientId);
                            booking.Parameters.AddWithValue("@BranchId", branchId);
                            booking.Parameters.AddWithValue("@CheckIn", checkIn.Date);
                            booking.Parameters.AddWithValue("@CheckOut", checkOut.Date);
                            booking.Parameters.AddWithValue("@Total", nightlyRate * nights);
                            booking.Parameters.AddWithValue("@Adults", adults);
                            bookingId = Convert.ToInt32(booking.ExecuteScalar());
                        }
                        using (var assignment = new SqlCommand(@"INSERT INTO Room_Assignment
(Booking_ID, Hotel_Room_ID, Assignment_Status, Assigned_Date)
VALUES (@BookingId, @RoomId, 'Reserved', GETDATE())", connection, transaction))
                        {
                            assignment.Parameters.AddWithValue("@BookingId", bookingId);
                            assignment.Parameters.AddWithValue("@RoomId", roomId);
                            assignment.ExecuteNonQuery();
                        }
                        transaction.Commit();
                        return bookingId;
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        private static int GetClientId(SqlConnection connection, SqlTransaction transaction, string email)
        {
            using (var command = new SqlCommand("SELECT Client_ID FROM Client WHERE Email_Address = @Email AND Client_Status = 'Active'", connection, transaction))
            {
                command.Parameters.AddWithValue("@Email", email);
                object id = command.ExecuteScalar();
                if (id == null) throw new InvalidOperationException("Your guest profile is unavailable. Please contact the hotel.");
                return Convert.ToInt32(id);
            }
        }
    }
}
