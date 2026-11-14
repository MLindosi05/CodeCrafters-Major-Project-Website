using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.SessionState;
using CodeCrafters_Major_Project_Website.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CodeCrafters_Major_Project_Website.PayPal
{
    public class CreateOrder : HttpTaskAsyncHandler, IRequiresSessionState
    {
        public override async Task ProcessRequestAsync(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json";
            if (ctx.User == null || ctx.User.Identity == null || !ctx.User.Identity.IsAuthenticated) { Fail(ctx, 401, "Please log in first."); return; }
            try
            {
                string body = new StreamReader(ctx.Request.InputStream).ReadToEnd();
                var req = JObject.Parse(body);
                ValidateCsrf(ctx, req);
                string action = Convert.ToString(ctx.Request.QueryString["action"]);
                if (String.Equals(action, "otp-send", StringComparison.OrdinalIgnoreCase))
                {
                    string orderId = (string)req["orderID"];
                    string savedOrder = Convert.ToString(ctx.Session["pp_" + orderId]);
                    var orderDetails = String.IsNullOrWhiteSpace(savedOrder) ? null : JObject.Parse(savedOrder);
                    if (orderDetails == null || !String.Equals((string)orderDetails["user"], ctx.User.Identity.Name, StringComparison.OrdinalIgnoreCase))
                    { Fail(ctx, 403, "This payment does not belong to your account or has expired."); return; }
                    await BookingOtpService.SendAsync(ctx.User.Identity.Name, orderId, ctx);
                    ctx.Response.Write(new JObject { ["sent"] = true }.ToString()); return;
                }
                if (String.Equals(action, "otp-verify", StringComparison.OrdinalIgnoreCase))
                { BookingOtpService.Verify((string)req["orderID"], (string)req["code"], ctx); ctx.Response.Write(new JObject { ["verified"] = true }.ToString()); return; }
                if (String.Equals(ctx.Request.QueryString["action"], "validate", StringComparison.OrdinalIgnoreCase))
                { ValidateCart(ctx, req); return; }
                if (String.Equals(ctx.Request.QueryString["action"], "capture", StringComparison.OrdinalIgnoreCase))
                { await CaptureOrder(ctx, (string)req["orderID"]); return; }
                await CreatePayPalOrder(ctx, req);
            }
            catch (InvalidOperationException ex) { Fail(ctx, 400, ex.Message); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); Fail(ctx, 500, "We could not complete the payment request. Please try again or contact the hotel before retrying."); }
        }

        private static async Task CreatePayPalOrder(HttpContext ctx, JObject req)
        {
            string attemptToken = (string)req["attemptToken"];
            if (String.IsNullOrEmpty(attemptToken) || !String.Equals(attemptToken, Convert.ToString(ctx.Session["Payment.Attempt"]), StringComparison.Ordinal))
                throw new InvalidOperationException("This payment attempt is invalid. Reload the payment page and try again.");
            var previous = ctx.Session["Payment.Order." + attemptToken] as string;
            if (!String.IsNullOrEmpty(previous))
            {
                ctx.Response.Write(new JObject { ["id"] = previous }.ToString());
                return;
            }
            var rawIds = req["roomIds"] as JArray;
            DateTime checkIn, checkOut;
            if (rawIds == null || !(ctx.Session["Booking.CheckIn"] is DateTime) || !(ctx.Session["Booking.CheckOut"] is DateTime))
                throw new InvalidOperationException("Please choose valid rooms and stay dates.");
            checkIn = ((DateTime)ctx.Session["Booking.CheckIn"]).Date;
            checkOut = ((DateTime)ctx.Session["Booking.CheckOut"]).Date;
            DateTime postedIn, postedOut;
            if (!DateTime.TryParse((string)req["checkIn"], CultureInfo.InvariantCulture, DateTimeStyles.None, out postedIn) ||
                !DateTime.TryParse((string)req["checkOut"], CultureInfo.InvariantCulture, DateTimeStyles.None, out postedOut) ||
                postedIn.Date != checkIn || postedOut.Date != checkOut)
                throw new InvalidOperationException("Your stay dates changed. Return to Rooms and search again.");
            var roomIds = new List<int>();
            foreach (var token in rawIds)
            {
                int id;
                if (!Int32.TryParse(Convert.ToString(token, CultureInfo.InvariantCulture), out id))
                    throw new InvalidOperationException("A selected room is invalid. Please refresh your cart.");
                roomIds.Add(id);
            }

            var availability = new AvailabilityService();
            int guests = Convert.ToInt32(ctx.Session["Booking.GuestCount"] ?? 1);
            var reservation = availability.CreateBooking(ctx.User.Identity.Name, roomIds, checkIn, checkOut,
                guests, 0, BookingStatus.PendingPayment);
            try
            {
                int nights = (checkOut.Date - checkIn.Date).Days;
                decimal usd = PayPalClient.ZarToUsd(reservation.TotalAmount);
                var order = await PayPalClient.CreateOrderAsync(usd, "The Regal Inn booking (" + nights + " night(s))");
                string orderId = (string)order["id"];
                var bookingIds = new List<int> { reservation.BookingId };
                BookingSupportStore.SavePayment(new BookingSupportStore.PaymentRecord
                {
                    BookingId = reservation.BookingId, OwnerEmail = ctx.User.Identity.Name, PayPalOrderId = orderId,
                    ZarAmount = reservation.TotalAmount, UsdAmount = usd, Currency = "USD", Status = "PendingPayment",
                    BranchId = reservation.BranchId, BranchName = reservation.BranchName, CheckIn = reservation.CheckIn,
                    CheckOut = reservation.CheckOut, GuestCount = reservation.GuestCount, Subtotal = reservation.Subtotal,
                    TaxAmount = reservation.TaxAmount, Rooms = reservation.Rooms
                });
                ctx.Session["Payment.Order." + attemptToken] = orderId;
                ctx.Session["pp_" + orderId] = JsonConvert.SerializeObject(new
                {
                    roomIds,
                    checkIn = checkIn.Date,
                    checkOut = checkOut.Date,
                    user = ctx.User.Identity.Name,
                    zarTotal = reservation.TotalAmount,
                    usd
                });
                ctx.Session["pp_bookingIds_" + orderId] = bookingIds;
                ctx.Response.Write(new JObject { ["id"] = orderId }.ToString());
            }
            catch
            {
                availability.SetBookingStatus(new List<int> { reservation.BookingId }, BookingStatus.Cancelled);
                throw;
            }
        }

        private static void ValidateCart(HttpContext ctx, JObject req)
        {
            var ids = ReadRoomIds(req["roomIds"] as JArray);
            if (!(ctx.Session["Booking.CheckIn"] is DateTime) || !(ctx.Session["Booking.CheckOut"] is DateTime))
                throw new InvalidOperationException("Choose stay dates in Rooms before payment.");
            int guests = Convert.ToInt32(ctx.Session["Booking.GuestCount"] ?? 1);
            new AvailabilityService().ValidateRoomAvailability(ids, (DateTime)ctx.Session["Booking.CheckIn"],
                (DateTime)ctx.Session["Booking.CheckOut"], guests);
            ctx.Response.Write(new JObject { ["valid"] = true }.ToString());
        }

        private static List<int> ReadRoomIds(JArray rawIds)
        {
            if (rawIds == null || rawIds.Count == 0) throw new InvalidOperationException("Your cart is empty.");
            var roomIds = new List<int>();
            foreach (var token in rawIds)
            {
                int id;
                if (!Int32.TryParse(Convert.ToString(token, CultureInfo.InvariantCulture), out id) || id < 1)
                    throw new InvalidOperationException("A selected room is invalid. Please refresh your cart.");
                roomIds.Add(id);
            }
            return roomIds;
        }
        private static async Task CaptureOrder(HttpContext ctx, string orderId)
        {
            if (String.IsNullOrWhiteSpace(orderId)) { Fail(ctx, 400, "The PayPal order reference is missing."); return; }
            if (!BookingOtpService.IsVerified(orderId, ctx)) { Fail(ctx, 403, "Verify the SMS code before payment can be captured and the booking confirmed."); return; }
            var completedIds = ctx.Session["pp_done_" + orderId] as List<int>;
            if (completedIds != null) { WriteBookingIds(ctx, completedIds); return; }
            var saved = ctx.Session["pp_" + orderId] as string;
            if (String.IsNullOrEmpty(saved)) { Fail(ctx, 400, "This payment session has expired. Please return to your cart and try again."); return; }
            JObject info = JObject.Parse(saved);
            if (!String.Equals((string)info["user"], ctx.User.Identity.Name, StringComparison.OrdinalIgnoreCase)) { Fail(ctx, 403, "This payment belongs to a different account."); return; }
            var ids = ctx.Session["pp_bookingIds_" + orderId] as List<int>;
            if (ids == null) { Fail(ctx, 400, "This payment hold has expired. Return to your cart and start checkout again."); return; }
            new BookingService().ExtendPaymentHold(ids);
            JObject paypalOrder;
            try { paypalOrder = await PayPalClient.CaptureOrderAsync(orderId); }
            catch
            {
                // Capture may have succeeded even when its response was lost. Query PayPal before retrying or releasing rooms.
                paypalOrder = await PayPalClient.GetOrderAsync(orderId);
            }
            if (!IsCompletedAndMatches(paypalOrder, Convert.ToDecimal(info["usd"])))
            { Fail(ctx, 402, "PayPal has not confirmed this payment. Your selected rooms are held while you retry or contact the hotel."); return; }

            var capture = paypalOrder["purchase_units"] == null ? null : paypalOrder["purchase_units"].FirstOrDefault()?["payments"]?["captures"]?.FirstOrDefault();
            string captureId = capture == null ? null : (string)capture["id"];
            // Persist the verified capture outside Session before confirming the shared-schema booking.
            var payment = BookingSupportStore.ReadPayment(ids[0]);
            if (payment == null || !String.Equals(payment.OwnerEmail, ctx.User.Identity.Name, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(payment.PayPalOrderId, orderId, StringComparison.Ordinal))
                throw new InvalidOperationException("The payment record for this booking is unavailable. Contact the hotel before retrying.");
            payment.CaptureId = captureId;
            payment.ZarAmount = Convert.ToDecimal(info["zarTotal"]);
            payment.UsdAmount = Convert.ToDecimal(info["usd"]);
            payment.Currency = "USD";
            payment.Status = "Paid";
            payment.CapturedUtc = DateTime.UtcNow;
            payment.RefundStatus = "NotRequested";
            payment.Captures = payment.Captures ?? new List<BookingSupportStore.PaymentCapture>();
            if (!payment.Captures.Any(c => c.CaptureId == captureId))
                payment.Captures.Add(new BookingSupportStore.PaymentCapture { CaptureId = captureId, ZarAmount = payment.ZarAmount, UsdAmount = payment.UsdAmount });
            BookingSupportStore.SavePayment(payment);
            new BookingService().SetPaymentReservationStatus(ids, "Confirmed");
            string confirmationBody = "Your booking #" + ids[0] + " has been paid and confirmed. Check-in: " + Convert.ToDateTime(info["checkIn"]).ToString("dd MMM yyyy", CultureInfo.InvariantCulture) +
                "; check-out: " + Convert.ToDateTime(info["checkOut"]).ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + ". Please keep this email for your records.";
            try
            {
                string invoice = new InvoiceService().GetOrCreate(ctx.User.Identity.Name, ids[0], false);
                BookingSupportStore.EnqueueEmail(ctx.User.Identity.Name, "Your Regal Inn booking is confirmed", confirmationBody, invoice);
            }
            catch (Exception invoiceError)
            {
                System.Diagnostics.Trace.TraceError("Confirmation invoice generation failed: " + invoiceError);
                BookingSupportStore.EnqueueEmail(ctx.User.Identity.Name, "Your Regal Inn booking is confirmed", confirmationBody);
            }
            ctx.Session["pp_done_" + orderId] = ids;
            var confirmed = ctx.Session["Payment.ConfirmedBookingIds"] as List<int> ?? new List<int>();
            foreach (int id in ids) if (!confirmed.Contains(id)) confirmed.Add(id);
            ctx.Session["Payment.ConfirmedBookingIds"] = confirmed;
            ctx.Session["Payment.Owner"] = ctx.User.Identity.Name;
            ctx.Session["Payment.AttemptCompleted"] = true;
            ctx.Session.Remove("pp_" + orderId);
            ctx.Session.Remove("pp_bookingIds_" + orderId);
            WriteBookingIds(ctx, ids);
        }

        private static bool IsCompletedAndMatches(JObject order, decimal expectedUsd)
        {
            if (!String.Equals((string)order["status"], "COMPLETED", StringComparison.OrdinalIgnoreCase)) return false;
            var unit = order["purchase_units"] == null ? null : order["purchase_units"].FirstOrDefault();
            var capture = unit == null || unit["payments"] == null || unit["payments"]["captures"] == null ? null : unit["payments"]["captures"].FirstOrDefault();
            if (capture == null || String.IsNullOrWhiteSpace((string)capture["id"])) return false;
            var amount = capture["amount"];
            if (amount == null || !String.Equals((string)amount["currency_code"], "USD", StringComparison.OrdinalIgnoreCase)) return false;
            decimal captured;
            return Decimal.TryParse((string)amount["value"], NumberStyles.Number, CultureInfo.InvariantCulture, out captured) && captured == expectedUsd;
        }

        private static void ValidateCsrf(HttpContext ctx, JObject req)
        {
            string sessionToken = Convert.ToString(ctx.Session["Payment.Csrf"]);
            if (String.IsNullOrEmpty(sessionToken)) throw new InvalidOperationException("Your payment page expired. Reload it and try again.");
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                string expected = Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sessionToken)));
                if (!String.Equals(expected, (string)req["csrfToken"], StringComparison.Ordinal))
                    throw new InvalidOperationException("The payment request expired. Reload the page and try again.");
            }
        }

        private static void WriteBookingIds(HttpContext ctx, IList<int> ids)
        { ctx.Response.Write(new JObject { ["bookingIds"] = new JArray(ids) }.ToString()); }
        private static void Fail(HttpContext ctx, int code, string message)
        { ctx.Response.StatusCode = code; ctx.Response.TrySkipIisCustomErrors = true; ctx.Response.Write(new JObject { ["error"] = message }.ToString()); }
    }
}
