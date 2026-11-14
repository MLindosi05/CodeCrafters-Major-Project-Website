using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace CodeCrafters_Major_Project_Website.Models
{
    // File-backed records deliberately avoid adding columns or tables to the shared school database.
    public static class BookingSupportStore
    {
        private static readonly object Sync = new object();
        private static string Root { get { return System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/BookingSupport"); } }
        private static string Payments { get { return Path.Combine(Root, "Payments"); } }
        private static string Queue { get { return Path.Combine(Root, "Queue"); } }
        private static string Changes { get { return Path.Combine(Root, "Changes"); } }

        public sealed class PaymentRecord
        {
            public int BookingId { get; set; }
            public string OwnerEmail { get; set; }
            public string PayPalOrderId { get; set; }
            public string CaptureId { get; set; }
            public decimal ZarAmount { get; set; }
            public decimal UsdAmount { get; set; }
            public string BranchId { get; set; }
            public string BranchName { get; set; }
            public DateTime CheckIn { get; set; }
            public DateTime CheckOut { get; set; }
            public int GuestCount { get; set; }
            public decimal Subtotal { get; set; }
            public decimal TaxAmount { get; set; }
            public List<BookingRoomRate> Rooms { get; set; } = new List<BookingRoomRate>();
            public string Currency { get; set; }
            public string Status { get; set; }
            public decimal RefundZar { get; set; }
            public decimal RefundUsd { get; set; }
            public string RefundStatus { get; set; }
            public string RefundRequestId { get; set; }
            public List<PaymentCapture> Captures { get; set; } = new List<PaymentCapture>();
            public DateTime CapturedUtc { get; set; }
            public DateTime? RefundedUtc { get; set; }
        }

        public sealed class PaymentCapture
        {
            public string CaptureId { get; set; }
            public decimal ZarAmount { get; set; }
            public decimal UsdAmount { get; set; }
            public decimal RefundedUsd { get; set; }
            public decimal PendingRefundUsd { get; set; }
        }

        public sealed class BookingChangeAttempt
        {
            public string OrderId { get; set; }
            public int BookingId { get; set; }
            public string OwnerEmail { get; set; }
            public BookingChangeRequest Request { get; set; }
            public BookingChangeQuote Quote { get; set; }
            public decimal OldTotal { get; set; }
            public decimal NewTotal { get; set; }
            public decimal Difference { get; set; }
            public decimal ExpectedUsd { get; set; }
            public string Status { get; set; }
            public string CaptureId { get; set; }
            public DateTime CreatedUtc { get; set; }
        }

        private sealed class QueueItem
        {
            public string Kind { get; set; }
            public string To { get; set; }
            public string Subject { get; set; }
            public string Body { get; set; }
            public string AttachmentPath { get; set; }
            public int Attempts { get; set; }
            public DateTime NextAttemptUtc { get; set; }
            public int BookingId { get; set; }
            public string RequestId { get; set; }
            public string CaptureId { get; set; }
            public decimal RefundUsd { get; set; }
        }

        private static string PaymentPath(int id) { return Path.Combine(Payments, id.ToString() + ".json"); }
        public static PaymentRecord ReadPayment(int id)
        {
            lock (Sync)
            {
                string path = PaymentPath(id);
                return File.Exists(path) ? JsonConvert.DeserializeObject<PaymentRecord>(File.ReadAllText(path)) : null;
            }
        }

        public static bool HasRefundableCapture(int bookingId)
        {
            var payment = ReadPayment(bookingId);
            return payment != null && ((payment.Captures != null && payment.Captures.Any(c => !String.IsNullOrWhiteSpace(c.CaptureId) && c.UsdAmount > c.RefundedUsd + c.PendingRefundUsd)) || !String.IsNullOrWhiteSpace(payment.CaptureId));
        }

        public static void RecordDemoRefund(int bookingId, decimal refundZar)
        {
            var payment = ReadPayment(bookingId);
            if (payment == null) return;
            payment.RefundZar = refundZar;
            payment.RefundStatus = refundZar > 0 ? "DemoCreditRecorded" : "NoCreditDue";
            payment.RefundedUtc = DateTime.UtcNow;
            SavePayment(payment);
        }

        public static void SavePayment(PaymentRecord record)
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Payments);
                string path = PaymentPath(record.BookingId), temp = path + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(record));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
        }

        public static void SaveChangeAttempt(BookingChangeAttempt attempt)
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Changes);
                string path = Path.Combine(Changes, SafeFileKey(attempt.OrderId) + ".json"), temp = path + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(attempt));
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
        }

        public static BookingChangeAttempt ReadChangeAttempt(string orderId)
        {
            lock (Sync)
            {
                string path = Path.Combine(Changes, SafeFileKey(orderId) + ".json");
                return File.Exists(path) ? JsonConvert.DeserializeObject<BookingChangeAttempt>(File.ReadAllText(path)) : null;
            }
        }

        private static string SafeFileKey(string value)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Length > 120 || value.Any(c => !(Char.IsLetterOrDigit(c) || c == '-' || c == '_')))
                throw new InvalidOperationException("The payment reference is invalid.");
            return value;
        }

        public static void EnqueueEmail(string to, string subject, string body, string attachmentPath = null)
        {
            if (String.IsNullOrWhiteSpace(to)) return;
            if (!String.IsNullOrWhiteSpace(attachmentPath))
            {
                string invoices = Path.GetFullPath(Path.Combine(Root, "..", "Invoices"));
                string candidate = Path.GetFullPath(attachmentPath);
                if (!candidate.StartsWith(invoices + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Email attachments must be generated invoices.");
            }
            Enqueue(new QueueItem { Kind = "email", To = to, Subject = subject, Body = body, AttachmentPath = attachmentPath, NextAttemptUtc = DateTime.UtcNow });
        }

        public static void SaveMobileCheckIn(int bookingId, string documentNumber, string arrivalTime, string requests)
        {
            Directory.CreateDirectory(Path.Combine(Root, "Mobile"));
            var payload = new { bookingId, documentNumber, arrivalTime, requests, savedUtc = DateTime.UtcNow };
            byte[] clear = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
            byte[] protectedData = System.Web.Security.MachineKey.Protect(clear, "RegalInn.MobileCheckIn.v1");
            File.WriteAllBytes(Path.Combine(Root, "Mobile", bookingId + ".bin"), protectedData);
        }

        public static bool QueueRefund(int bookingId, decimal refundZar)
        {
            var payment = ReadPayment(bookingId);
            if (payment == null || refundZar <= 0) return false;
            if (payment.Captures == null) payment.Captures = new List<PaymentCapture>();
            if (payment.Captures.Count == 0 && !String.IsNullOrWhiteSpace(payment.CaptureId))
                payment.Captures.Add(new PaymentCapture { CaptureId = payment.CaptureId, ZarAmount = payment.ZarAmount, UsdAmount = payment.UsdAmount });
            decimal totalZar = payment.Captures.Sum(c => c.ZarAmount), totalUsd = payment.Captures.Sum(c => c.UsdAmount);
            if (totalZar <= 0 || totalUsd <= 0 || payment.Captures.All(c => String.IsNullOrWhiteSpace(c.CaptureId)))
            { payment.RefundZar = refundZar; payment.RefundStatus = "NeedsManualReview"; SavePayment(payment); return false; }
            lock (Sync)
            {
                payment.RefundZar = refundZar;
                payment.RefundUsd = Decimal.Round(totalUsd * payment.RefundZar / totalZar, 2, MidpointRounding.AwayFromZero);
                decimal remaining = payment.RefundUsd;
                int part = 0;
                var allocations = new List<Tuple<PaymentCapture, decimal>>();
                foreach (var capture in payment.Captures.Where(c => !String.IsNullOrWhiteSpace(c.CaptureId)))
                {
                    decimal available = Math.Max(0, capture.UsdAmount - capture.RefundedUsd - capture.PendingRefundUsd);
                    decimal amount = Math.Min(remaining, available);
                    if (amount <= 0) continue;
                    allocations.Add(Tuple.Create(capture, amount));
                    remaining -= amount;
                    if (remaining <= 0) break;
                }
                if (remaining > 0.01m)
                {
                    payment.RefundStatus = "NeedsManualReview";
                    SavePayment(payment);
                    return false;
                }
                foreach (var allocation in allocations)
                {
                    allocation.Item1.PendingRefundUsd += allocation.Item2;
                    string requestId = "booking-" + bookingId + "-cancel-" + (++part).ToString(CultureInfo.InvariantCulture);
                    Enqueue(new QueueItem { Kind = "refund", BookingId = bookingId, CaptureId = allocation.Item1.CaptureId, RefundUsd = allocation.Item2, RequestId = requestId, NextAttemptUtc = DateTime.UtcNow });
                }
                payment.RefundStatus = "Pending";
                payment.RefundRequestId = "booking-" + bookingId + "-cancel";
                SavePayment(payment);
                return true;
            }
        }

        public static bool QueuePartialRefund(int bookingId, string captureId, decimal amountUsd, string requestId)
        {
            if (amountUsd <= 0 || String.IsNullOrWhiteSpace(captureId) || String.IsNullOrWhiteSpace(requestId)) return false;
            lock (Sync)
            {
                var payment = ReadPayment(bookingId);
                if (payment == null) return false;
                if (payment.Captures == null) payment.Captures = new List<PaymentCapture>();
                var capture = payment.Captures.FirstOrDefault(c => c.CaptureId == captureId);
                if (capture == null && payment.CaptureId == captureId)
                {
                    capture = new PaymentCapture { CaptureId = captureId, ZarAmount = payment.ZarAmount, UsdAmount = payment.UsdAmount };
                    payment.Captures.Add(capture);
                }
                if (capture == null || amountUsd > capture.UsdAmount - capture.RefundedUsd - capture.PendingRefundUsd + 0.005m) return false;
                capture.PendingRefundUsd += amountUsd;
                payment.RefundStatus = "Pending";
                Enqueue(new QueueItem { Kind = "refund", BookingId = bookingId, CaptureId = captureId, RefundUsd = amountUsd, RequestId = requestId, NextAttemptUtc = DateTime.UtcNow });
                SavePayment(payment);
                return true;
            }
        }

        public static bool QueueAdjustmentRefund(int bookingId, decimal refundZar, string requestId)
        {
            if (refundZar <= 0) return true;
            lock (Sync)
            {
                var payment = ReadPayment(bookingId);
                if (payment == null) return false;
                if (payment.Captures == null) payment.Captures = new List<PaymentCapture>();
                if (payment.Captures.Count == 0 && !String.IsNullOrWhiteSpace(payment.CaptureId))
                    payment.Captures.Add(new PaymentCapture { CaptureId = payment.CaptureId, ZarAmount = payment.ZarAmount, UsdAmount = payment.UsdAmount });
                decimal totalZar = payment.Captures.Sum(c => c.ZarAmount), totalUsd = payment.Captures.Sum(c => c.UsdAmount);
                if (totalZar <= 0 || totalUsd <= 0) return false;
                decimal remaining = Decimal.Round(refundZar * totalUsd / totalZar, 2, MidpointRounding.AwayFromZero);
                var allocations = new List<Tuple<PaymentCapture, decimal>>();
                foreach (var capture in payment.Captures.Where(c => !String.IsNullOrWhiteSpace(c.CaptureId)))
                {
                    decimal available = Math.Max(0, capture.UsdAmount - capture.RefundedUsd - capture.PendingRefundUsd);
                    decimal amount = Math.Min(remaining, available);
                    if (amount > 0) { allocations.Add(Tuple.Create(capture, amount)); remaining -= amount; }
                    if (remaining <= 0) break;
                }
                if (remaining > 0.01m) return false;
                int index = 0;
                foreach (var allocation in allocations)
                {
                    allocation.Item1.PendingRefundUsd += allocation.Item2;
                    Enqueue(new QueueItem { Kind = "refund", BookingId = bookingId, CaptureId = allocation.Item1.CaptureId,
                        RefundUsd = allocation.Item2, RequestId = requestId + "-" + (++index).ToString(CultureInfo.InvariantCulture), NextAttemptUtc = DateTime.UtcNow });
                }
                payment.RefundStatus = "Pending";
                SavePayment(payment);
                return true;
            }
        }

        private static void Enqueue(QueueItem item)
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Queue);
                string path = Path.Combine(Queue, Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(path, JsonConvert.SerializeObject(item));
            }
        }

        private static bool HasPendingRefunds(int bookingId, string exceptPath)
        {
            if (!Directory.Exists(Queue)) return false;
            foreach (string path in Directory.GetFiles(Queue, "*.json"))
            {
                if (String.Equals(path, exceptPath, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var item = JsonConvert.DeserializeObject<QueueItem>(File.ReadAllText(path));
                    if (item != null && item.Kind == "refund" && item.BookingId == bookingId) return true;
                }
                catch { }
            }
            return false;
        }

        public static async Task ProcessQueueAsync()
        {
            if (!Directory.Exists(Queue)) return;
            foreach (string path in Directory.GetFiles(Queue, "*.json").Take(20))
            {
                QueueItem item;
                try { item = JsonConvert.DeserializeObject<QueueItem>(File.ReadAllText(path)); }
                catch { continue; }
                if (item.NextAttemptUtc > DateTime.UtcNow) continue;
                try
                {
                    if (item.Kind == "email")
                    {
                        using (var client = CreateSmtpClient())
                        using (var message = new MailMessage(new MailAddress(ConfigurationManager.AppSettings["Mail:From"] ?? String.Empty), new MailAddress(item.To))
                            { Subject = item.Subject, Body = item.Body, IsBodyHtml = false })
                        {
                            if (!String.IsNullOrWhiteSpace(item.AttachmentPath))
                            {
                                if (!File.Exists(item.AttachmentPath)) throw new FileNotFoundException("The queued invoice attachment is missing.");
                                message.Attachments.Add(new Attachment(item.AttachmentPath));
                            }
                            await client.SendMailAsync(message).ConfigureAwait(false);
                        }
                    }
                    else if (item.Kind == "refund")
                    {
                        var payment = ReadPayment(item.BookingId);
                        if (payment != null)
                        {
                            payment.RefundStatus = "DemoCreditRecorded";
                            payment.RefundedUtc = DateTime.UtcNow; SavePayment(payment);
                        }
                    }
                    else throw new InvalidOperationException("Unknown queued booking support action.");
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    item.Attempts++;
                    if (item.Attempts >= 8)
                    {
                        string failed = Path.Combine(Root, "Failed"); Directory.CreateDirectory(failed);
                        string failedPath = Path.Combine(failed, Path.GetFileName(path));
                        File.WriteAllText(failedPath, JsonConvert.SerializeObject(item));
                        File.Delete(path);
                        System.Diagnostics.Trace.TraceError("Booking support item moved to the failed queue after repeated failures (" + item.Kind + "): " + ex.Message);
                        continue;
                    }
                    item.NextAttemptUtc = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(item.Attempts, 6))));
                    File.WriteAllText(path, JsonConvert.SerializeObject(item));
                    if (item.Kind == "refund") { var payment = ReadPayment(item.BookingId); if (payment != null) { payment.RefundStatus = "Retrying"; SavePayment(payment); } }
                    System.Diagnostics.Trace.TraceError("Booking support queue item failed: " + ex.Message);
                }
            }
        }

        private static SmtpClient CreateSmtpClient()
        {
            string host = ConfigurationManager.AppSettings["Mail:SmtpHost"];
            int port; if (String.IsNullOrWhiteSpace(host) || !Int32.TryParse(ConfigurationManager.AppSettings["Mail:SmtpPort"], out port) || port < 1 || port > 65535)
                throw new InvalidOperationException("SMTP is not configured. Set Mail:SmtpHost, Mail:SmtpPort, and Mail:From in Web.config.");
            var client = new SmtpClient(host, port) { EnableSsl = !String.Equals(ConfigurationManager.AppSettings["Mail:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase) };
            string user = ConfigurationManager.AppSettings["Mail:UserName"], password = ConfigurationManager.AppSettings["Mail:Password"];
            if (!String.IsNullOrWhiteSpace(user)) client.Credentials = new System.Net.NetworkCredential(user, password ?? String.Empty);
            return client;
        }
    }
}
