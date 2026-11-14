using System;
using System.Collections.Generic;
using CodeCrafters_Major_Project_Website.Models;

namespace CodeCrafters_Major_Project_Website.Models
{
    public class BookingService
    {
        public int CreateBooking(string email, int roomId, DateTime checkIn, DateTime checkOut, int adults)
        {
            var reservation = new AvailabilityService().CreateBooking(email, new List<int> { roomId }, checkIn, checkOut,
                adults, 0, BookingStatus.Confirmed);
            return reservation.BookingId;
        }

        // Kept for the existing payment handler API: one Booking row now owns all selected room assignments.
        public List<int> ReserveBookings(string email, IList<int> roomIds, DateTime checkIn, DateTime checkOut, int adults)
        {
            var reservation = new AvailabilityService().CreateBooking(email, roomIds, checkIn, checkOut,
                adults, 0, BookingStatus.PendingPayment);
            return new List<int> { reservation.BookingId };
        }

        public void ExtendPaymentHold(IList<int> bookingIds)
        {
            new AvailabilityService().ExtendPendingHold(bookingIds);
        }
        public void SetPaymentReservationStatus(IList<int> bookingIds, string status)
        {
            BookingStatus parsed;
            if (!Enum.TryParse(status, true, out parsed) || (parsed != BookingStatus.Confirmed && parsed != BookingStatus.Cancelled))
                throw new ArgumentException("Invalid payment booking status.", "status");
            new AvailabilityService().SetBookingStatus(bookingIds, parsed);
        }
    }
}