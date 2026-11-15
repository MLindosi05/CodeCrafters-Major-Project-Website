using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CodeCrafters_Major_Project_Website.Models;


namespace CodeCrafters_Major_Project_Website
{
    public partial class Payments : System.Web.UI.Page
    {
        protected void Page_Init(object sender, EventArgs e) { ViewStateUserKey = Session.SessionID; }
        protected string PayPalClientId
        {
            get { return ConfigurationManager.AppSettings["PayPal:ClientId"]; }
        }

        protected string BookingCheckIn { get; private set; }
        protected string BookingCheckOut { get; private set; }
        protected string BookingGuestCount { get; private set; }
        protected string PaymentCsrfToken { get; private set; }
        protected string PaymentAttemptToken { get; private set; }
        protected string ResumeCartJson { get; private set; } = "[]";
        protected bool IsResumingBooking { get; private set; }
        protected string BookingTaxPercentage { get { return ConfigurationManager.AppSettings["Booking:TaxPercentage"] ?? "0"; } }
        protected bool SmsOtpConfigured
        {
            get { return !String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["Sms:TextbeeApiKey"]); }
        }
        protected bool MailConfigured
        {
            get { return !String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["Mail:SmtpHost"]) && !String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["Mail:From"]) && !String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["Mail:SmtpPort"]); }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            // Gate on the server, not just the client — so someone can't
            // reach this page just by typing the URL, even without clicking
            // "Continue to Booking" while logged out.
            if (!User.Identity.IsAuthenticated)
            {
                Response.Redirect("~/Account/Login.aspx?ReturnUrl=" + Server.UrlEncode(Request.RawUrl));
                return;
            }
            int resumeBookingId;
            bool requestedResume = Int32.TryParse(Request.QueryString["resumeBookingId"], out resumeBookingId) && resumeBookingId > 0;
            if (!requestedResume) Session.Remove("Payment.ResumeBookingId");
            if (requestedResume)
            {
                try { PreparePendingBookingResume(resumeBookingId); }
                catch (InvalidOperationException ex)
                {
                    Session["MyBookings.Notice"] = ex.Message;
                    Response.Redirect("~/Bookings/MyBookings.aspx?tab=all", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("Pending booking resume failed: " + ex);
                    Session["MyBookings.Notice"] = "We could not continue this payment. Refresh My Bookings and try again.";
                    Response.Redirect("~/Bookings/MyBookings.aspx?tab=all", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
            }
            var checkIn = Session["Booking.CheckIn"] as DateTime?;
            var checkOut = Session["Booking.CheckOut"] as DateTime?;
            if (!checkIn.HasValue || !checkOut.HasValue || checkOut.Value.Date <= checkIn.Value.Date)
            {
                Response.Redirect("~/Rooms.aspx");
                return;
            }
            BookingCheckIn = checkIn.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            BookingCheckOut = checkOut.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            BookingGuestCount = Convert.ToString(Session["Booking.GuestCount"] ?? 1, CultureInfo.InvariantCulture);
            if (Session["Payment.Attempt"] == null || Convert.ToBoolean(Session["Payment.AttemptCompleted"] ?? false))
            {
                using (var rng = RandomNumberGenerator.Create())
                {
                    var bytes = new byte[24]; rng.GetBytes(bytes);
                    Session["Payment.Attempt"] = Convert.ToBase64String(bytes);
                    Session["Payment.AttemptCompleted"] = false;
                }
            }
            PaymentAttemptToken = Convert.ToString(Session["Payment.Attempt"]);
            if (Session["Payment.Csrf"] == null)
            {
                using (var rng = RandomNumberGenerator.Create())
                {
                    var bytes = new byte[32]; rng.GetBytes(bytes);
                    Session["Payment.Csrf"] = Convert.ToBase64String(bytes);
                }
            }
            PaymentCsrfToken = Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(Convert.ToString(Session["Payment.Csrf"]))));

            int currentResumeBookingId;
            if (Int32.TryParse(Convert.ToString(Session["Payment.ResumeBookingId"]), out currentResumeBookingId) && currentResumeBookingId > 0 && IsResumingBooking)
            {
                var payment = BookingSupportStore.ReadPayment(currentResumeBookingId);
                if (payment != null && !String.IsNullOrWhiteSpace(payment.PayPalOrderId))
                {
                    Session["Payment.Order." + PaymentAttemptToken] = payment.PayPalOrderId;
                    SetPayPalSession(payment, User.Identity.Name);
                }
            }
        }

        private void PreparePendingBookingResume(int bookingId)
        {
            var summary = new MyBookingsService().GetBookingsForUser(User.Identity.Name)
                .FirstOrDefault(booking => booking.BookingId == bookingId && booking.Status == "Pending" && booking.CanContinuePayment);
            if (summary == null) throw new InvalidOperationException("This pending booking is no longer available to continue. Check its status in My Bookings.");

            var payment = BookingSupportStore.ReadPayment(bookingId);
            if (payment == null || payment.Status != "PendingPayment" ||
                !String.Equals(payment.OwnerEmail, User.Identity.Name, StringComparison.OrdinalIgnoreCase) ||
                payment.Rooms == null || payment.Rooms.Count == 0)
                throw new InvalidOperationException("The payment details for this pending booking could not be verified. Contact the hotel before starting another payment.");

            var roomIds = payment.Rooms.Select(room => room.RoomId).ToList();
            int guestCount = payment.GuestCount > 0 ? payment.GuestCount : summary.GuestCount;
            new AvailabilityService().RenewPendingPaymentHold(User.Identity.Name, bookingId, roomIds,
                payment.CheckIn, payment.CheckOut, guestCount);

            Session["Booking.CheckIn"] = payment.CheckIn.Date;
            Session["Booking.CheckOut"] = payment.CheckOut.Date;
            Session["Booking.GuestCount"] = guestCount;
            Session["Payment.ResumeBookingId"] = bookingId;
            IsResumingBooking = true;
            ResumeCartJson = JsonConvert.SerializeObject(payment.Rooms.Select(room => new
            {
                roomId = room.RoomId.ToString(CultureInfo.InvariantCulture),
                roomName = room.RoomType,
                branchName = payment.BranchName,
                pricePerNight = room.RatePerNight
            }));
        }

        private void SetPayPalSession(BookingSupportStore.PaymentRecord payment, string email)
        {
            var roomIds = payment.Rooms.Select(room => room.RoomId).ToList();
            var paypalContext = new JObject
            {
                ["roomIds"] = new JArray(roomIds),
                ["checkIn"] = payment.CheckIn.Date,
                ["checkOut"] = payment.CheckOut.Date,
                ["user"] = email,
                ["zarTotal"] = payment.ZarAmount,
                ["usd"] = payment.UsdAmount
            };
            Session["pp_" + payment.PayPalOrderId] = paypalContext.ToString(Formatting.None);
            Session["pp_bookingIds_" + payment.PayPalOrderId] = new List<int> { payment.BookingId };
        }
    }
}
