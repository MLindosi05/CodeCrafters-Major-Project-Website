using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.SessionState;
using CodeCrafters_Major_Project_Website.Models;
using Newtonsoft.Json.Linq;

namespace CodeCrafters_Major_Project_Website.PayPal
{
    public sealed class BookingChange : HttpTaskAsyncHandler, IRequiresSessionState
    {
        public override async Task ProcessRequestAsync(HttpContext context)
        {
            context.Response.ContentType = "application/json";
            if (context.User == null || context.User.Identity == null || !context.User.Identity.IsAuthenticated)
            { Fail(context, 401, "Please sign in first."); return; }
            try
            {
                JObject input = JObject.Parse(new StreamReader(context.Request.InputStream).ReadToEnd());
                ValidateCsrf(context, (string)input["csrfToken"]);
                string action = Convert.ToString(context.Request.QueryString["action"]);
                var service = new BookingModificationService();
                if (action == "quote")
                {
                    var quote = service.Quote(context.User.Identity.Name, ReadRequest(input));
                    WriteQuote(context, quote);
                    return;
                }
                if (action == "options")
                {
                    int bookingId, sourceRoom;
                    if (!Int32.TryParse((string)input["bookingId"], out bookingId) || !Int32.TryParse((string)input["sourceRoomId"], out sourceRoom))
                        throw new InvalidOperationException("Choose a booking and room first.");
                    string mode = (string)input["mode"];
                    var options = service.GetOptions(context.User.Identity.Name, bookingId, sourceRoom, mode, (string)input["targetBranchId"]);
                    var array = new JArray(options.Select(r => new JObject { ["roomId"] = r.RoomId, ["roomNumber"] = r.RoomNumber, ["type"] = r.RoomType, ["branch"] = r.BranchName, ["rate"] = r.PricePerNight }));
                    context.Response.Write(new JObject { ["options"] = array }.ToString());
                    return;
                }
                if (action == "branches")
                {
                    int bookingId;
                    if (!Int32.TryParse((string)input["bookingId"], out bookingId)) throw new InvalidOperationException("Choose a valid booking.");
                    var branches = service.GetBranches(context.User.Identity.Name, bookingId);
                    context.Response.Write(new JObject { ["branches"] = new JArray(branches.Select(b => new JObject { ["id"] = b.Key, ["name"] = b.Value })) }.ToString());
                    return;
                }
                if (action == "apply")
                {
                    var request = ReadRequest(input);
                    var quote = service.Quote(context.User.Identity.Name, request);
                    decimal expectedTotal;
                    if (!Decimal.TryParse(Convert.ToString(input["expectedNewTotal"], CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out expectedTotal) || Decimal.Round(expectedTotal, 2) != Decimal.Round(quote.NewTotal, 2))
                        throw new InvalidOperationException("The price or availability changed. Check the updated quote before confirming.");
                    if (quote.Difference > 0) throw new InvalidOperationException("This change needs PayPal approval for the additional R" + quote.Difference.ToString("N2") + ".");
                    quote = service.Apply(context.User.Identity.Name, request, quote.NewTotal);
                    SaveSnapshot(request.BookingId, quote, false, 0m, null);
                    if (quote.Difference < 0) BookingSupportStore.RecordDemoRefund(request.BookingId, -quote.Difference);
                    QueueModifiedEmail(context.User.Identity.Name, request.BookingId, quote);
                    context.Response.Write(new JObject { ["success"] = true, ["difference"] = quote.Difference, ["message"] = ChangeMessage(quote, quote.Difference < 0) }.ToString());
                    return;
                }
                if (action == "createOrder")
                {
                    var request = ReadRequest(input);
                    var quote = service.Quote(context.User.Identity.Name, request);
                    decimal expectedTotal;
                    if (!Decimal.TryParse(Convert.ToString(input["expectedNewTotal"], CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out expectedTotal) || Decimal.Round(expectedTotal, 2) != Decimal.Round(quote.NewTotal, 2))
                        throw new InvalidOperationException("The price or availability changed. Check the updated quote before paying.");
                    if (quote.Difference <= 0) throw new InvalidOperationException("No additional payment is required; apply this change directly.");
                    var order = await PayPalClient.CreateOrderAsync(PayPalClient.ZarToUsd(quote.Difference), "Regal Inn booking change #" + request.BookingId);
                    string orderId = (string)order["id"];
                    BookingSupportStore.SaveChangeAttempt(new BookingSupportStore.BookingChangeAttempt
                    {
                        OrderId = orderId, BookingId = request.BookingId, OwnerEmail = context.User.Identity.Name,
                        Request = request, Quote = quote, OldTotal = quote.OldTotal, NewTotal = quote.NewTotal, Difference = quote.Difference,
                        ExpectedUsd = PayPalClient.ZarToUsd(quote.Difference), Status = "PendingPayment", CreatedUtc = DateTime.UtcNow
                    });
                    context.Response.Write(new JObject { ["id"] = orderId, ["difference"] = quote.Difference }.ToString());
                    return;
                }
                if (action == "capture")
                {
                    string orderId = (string)input["orderID"];
                    var attempt = BookingSupportStore.ReadChangeAttempt(orderId);
                    if (attempt == null || !String.Equals(attempt.OwnerEmail, context.User.Identity.Name, StringComparison.OrdinalIgnoreCase))
                    { Fail(context, 403, "This payment does not belong to your account."); return; }
                    if (attempt.Status == "Completed") { context.Response.Write(new JObject { ["success"] = true, ["message"] = "Booking change completed." }.ToString()); return; }
                    if (attempt.Status == "Captured" && service.IsChangeApplied(context.User.Identity.Name, attempt.Quote))
                    {
                        SaveSnapshot(attempt.BookingId, attempt.Quote, true, attempt.Difference, new BookingSupportStore.PaymentCapture { CaptureId = attempt.CaptureId, ZarAmount = attempt.Difference, UsdAmount = attempt.ExpectedUsd });
                        attempt.Status = "Completed"; BookingSupportStore.SaveChangeAttempt(attempt);
                        QueueModifiedEmail(context.User.Identity.Name, attempt.BookingId, attempt.Quote);
                        context.Response.Write(new JObject { ["success"] = true, ["message"] = ChangeMessage(attempt.Quote, false) }.ToString());
                        return;
                    }
                    JObject order;
                    try { order = await PayPalClient.CaptureOrderAsync(orderId); }
                    catch { order = await PayPalClient.GetOrderAsync(orderId); }
                    string captureId;
                    if (!TryReadCapture(order, attempt.ExpectedUsd, out captureId))
                    { Fail(context, 402, "PayPal has not confirmed the full additional payment."); return; }
                    attempt.Status = "Captured"; attempt.CaptureId = captureId; BookingSupportStore.SaveChangeAttempt(attempt);
                    try
                    {
                        var quote = service.Apply(context.User.Identity.Name, attempt.Request, attempt.NewTotal);
                        attempt.Quote = quote;
                        SaveSnapshot(attempt.BookingId, quote, true, attempt.Difference, new BookingSupportStore.PaymentCapture
                        { CaptureId = captureId, ZarAmount = attempt.Difference, UsdAmount = attempt.ExpectedUsd });
                        attempt.Status = "Completed"; BookingSupportStore.SaveChangeAttempt(attempt);
                        QueueModifiedEmail(context.User.Identity.Name, attempt.BookingId, quote);
                        context.Response.Write(new JObject { ["success"] = true, ["message"] = ChangeMessage(quote, false) }.ToString());
                    }
                    catch (Exception applyError)
                    {
                        SaveSnapshot(attempt.BookingId, null, true, attempt.Difference, new BookingSupportStore.PaymentCapture
                        { CaptureId = captureId, ZarAmount = attempt.Difference, UsdAmount = attempt.ExpectedUsd });
                        BookingSupportStore.RecordDemoRefund(attempt.BookingId, attempt.Difference);
                        attempt.Status = "CapturedDemoCreditRecorded";
                        BookingSupportStore.SaveChangeAttempt(attempt);
                        System.Diagnostics.Trace.TraceError("Booking change could not apply after PayPal capture. " + applyError);
                        Fail(context, 409, "PayPal captured the adjustment, but the booking could not be updated. A demo refund credit has been recorded; no PayPal refund was sent.");
                    }
                    return;
                }
                Fail(context, 400, "Unknown booking change action.");
            }
            catch (InvalidOperationException ex) { Fail(context, 400, ex.Message); }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Booking change request failed: " + ex);
                Fail(context, 500, "We could not complete this booking change. Please refresh and try again.");
            }
        }

        private static BookingChangeRequest ReadRequest(JObject input)
        {
            int bookingId, sourceRoom = 0, targetRoom = 0;
            DateTime checkIn, checkOut;
            if (!Int32.TryParse((string)input["bookingId"], out bookingId) || bookingId < 1) throw new InvalidOperationException("Choose a valid booking.");
            if (!DateTime.TryParse((string)input["checkIn"], CultureInfo.InvariantCulture, DateTimeStyles.None, out checkIn) ||
                !DateTime.TryParse((string)input["checkOut"], CultureInfo.InvariantCulture, DateTimeStyles.None, out checkOut))
                throw new InvalidOperationException("Enter valid check-in and check-out dates.");
            Int32.TryParse((string)input["sourceRoomId"], out sourceRoom);
            Int32.TryParse((string)input["targetRoomId"], out targetRoom);
            return new BookingChangeRequest { BookingId = bookingId, Mode = (string)input["mode"], CheckIn = checkIn.Date,
                CheckOut = checkOut.Date, SourceRoomId = sourceRoom, TargetRoomId = targetRoom, TargetBranchId = (string)input["targetBranchId"] };
        }

        private static void SaveSnapshot(int bookingId, BookingChangeQuote quote, bool addCapture, decimal addedZar, BookingSupportStore.PaymentCapture capture)
        {
            var payment = BookingSupportStore.ReadPayment(bookingId);
            if (payment == null) return;
            payment.Captures = payment.Captures ?? new System.Collections.Generic.List<BookingSupportStore.PaymentCapture>();
            if (addCapture && capture != null && !payment.Captures.Any(c => c.CaptureId == capture.CaptureId))
            {
                payment.Captures.Add(capture);
                payment.ZarAmount += addedZar;
                payment.UsdAmount += capture.UsdAmount;
            }
            if (quote != null)
            {
                payment.CheckIn = quote.CheckIn; payment.CheckOut = quote.CheckOut; payment.BranchId = quote.BranchId; payment.BranchName = quote.BranchName;
                payment.Rooms = quote.Rooms; payment.Subtotal = quote.Rooms.Sum(r => r.Subtotal);
                payment.TaxAmount = quote.NewTotal - payment.Subtotal;
            }
            BookingSupportStore.SavePayment(payment);
        }

        private static void QueueModifiedEmail(string email, int bookingId, BookingChangeQuote quote)
        {
            try
            {
                string invoice = new InvoiceService().GetOrCreateAmended(email, bookingId);
                BookingSupportStore.EnqueueEmail(email, "Your Regal Inn booking was modified", "Booking #" + bookingId + " was updated. New total: R" + quote.NewTotal.ToString("N2") + ".", invoice);
            }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("Amended invoice generation failed: " + ex); }
        }

        private static string ChangeMessage(BookingChangeQuote quote, bool refundQueued)
        { return "Booking #" + quote.BookingId + " was updated. New total: R" + quote.NewTotal.ToString("N2") + (refundQueued ? ". The price reduction is recorded as a demo credit; no PayPal refund was sent." : "."); }

        private static bool TryReadCapture(JObject order, decimal expectedUsd, out string captureId)
        {
            captureId = null;
            if (!String.Equals((string)order["status"], "COMPLETED", StringComparison.OrdinalIgnoreCase)) return false;
            var unit = order["purchase_units"] == null ? null : order["purchase_units"].FirstOrDefault();
            var capture = unit == null ? null : unit["payments"] == null || unit["payments"]["captures"] == null ? null : unit["payments"]["captures"].FirstOrDefault();
            if (capture == null || String.IsNullOrWhiteSpace((string)capture["id"])) return false;
            var amount = capture["amount"];
            decimal actual;
            if (amount == null || !String.Equals((string)amount["currency_code"], "USD", StringComparison.OrdinalIgnoreCase) ||
                !Decimal.TryParse((string)amount["value"], NumberStyles.Number, CultureInfo.InvariantCulture, out actual) || actual != expectedUsd) return false;
            captureId = (string)capture["id"];
            return true;
        }

        private static void ValidateCsrf(HttpContext context, string supplied)
        {
            string token = Convert.ToString(context.Session["BookingChange.Csrf"]);
            if (String.IsNullOrEmpty(token) || String.IsNullOrEmpty(supplied)) throw new InvalidOperationException("Your booking page expired. Reload it and try again.");
            using (var sha = SHA256.Create())
            {
                string expected = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(token)));
                if (!String.Equals(expected, supplied, StringComparison.Ordinal)) throw new InvalidOperationException("Your booking page expired. Reload it and try again.");
            }
        }

        private static void WriteQuote(HttpContext context, BookingChangeQuote quote)
        {
            var rooms = new JArray(quote.Rooms.Select(r => new JObject { ["roomId"] = r.RoomId, ["roomNumber"] = r.RoomNumber, ["type"] = r.RoomType, ["rate"] = r.RatePerNight, ["subtotal"] = r.Subtotal }));
            context.Response.Write(new JObject { ["bookingId"] = quote.BookingId, ["oldTotal"] = quote.OldTotal, ["newTotal"] = quote.NewTotal,
                ["difference"] = quote.Difference, ["branch"] = quote.BranchName, ["rooms"] = rooms }.ToString());
        }

        private static void Fail(HttpContext context, int status, string message)
        { context.Response.StatusCode = status; context.Response.TrySkipIisCustomErrors = true; context.Response.Write(new JObject { ["error"] = message }.ToString()); }
    }
}
