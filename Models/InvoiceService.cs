using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CodeCrafters_Major_Project_Website.Models
{
    /// <summary>File-backed PDF invoices; uses existing shared database fields only.</summary>
    public sealed class InvoiceService
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        private sealed class InvoiceFileMetadata
        {
            public int Version { get; set; }
            public int BookingId { get; set; }
            public string InvoiceNumber { get; set; }
            public string Type { get; set; }
            public DateTime CreatedUtc { get; set; }
            public string FileName { get; set; }
        }

        private sealed class InvoiceData
        {
            public int Id;
            public DateTime Booked, CheckIn, CheckOut;
            public string Status, Branch, BranchAddress, BranchPhone, BranchEmail;
            public string GuestName, GuestAddress, GuestPhone, Email, PayPalOrderId, CaptureId;
            public decimal Total, Refund, Penalty;
            public decimal? AmountPaidUsd;
            public DateTime? PaidUtc;
            public int GuestCount;
            public decimal? Subtotal, TaxAmount;
            public readonly List<InvoiceRoomLine> Rooms = new List<InvoiceRoomLine>();
        }
        private sealed class InvoiceRoomLine { public string Label; public decimal? Rate; public decimal? Subtotal; }

        public string GetOrCreate(string email, int bookingId, bool creditNote)
        { return GetOrCreate(email, bookingId, creditNote ? "CreditNote" : "Invoice"); }

        public string GetOrCreateAmended(string email, int bookingId)
        { return GetOrCreate(email, bookingId, "Amended"); }

        public bool HasAmendedInvoice(int bookingId)
        {
            if (bookingId < 1) return false;
            string folder = Path.Combine(System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Invoices"), bookingId.ToString(CultureInfo.InvariantCulture));
            return Directory.Exists(folder) && Directory.GetFiles(folder, "AMD-*.pdf").Length > 0;
        }

        public string GetOrCreate(string email, int bookingId, string type)
        {
            bool creditNote = type == "CreditNote";
            InvoiceData data = LoadOwnedBooking(email, bookingId);
            var storedPayment = BookingSupportStore.ReadPayment(bookingId);
            if (creditNote && data.Status != "Cancelled") throw new InvalidOperationException("A credit note is available only for a cancelled booking.");
            if (!creditNote && data.Status != "Confirmed" && data.Status != "Booked" && data.Status != "CheckedIn" && data.Status != "Completed" &&
                !(data.Status == "Cancelled" && storedPayment != null && storedPayment.Status == "Paid"))
                throw new InvalidOperationException("An invoice is available only after payment confirmation.");

            string root = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Invoices");
            string folder = Path.Combine(root, bookingId.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            string prefix = creditNote ? "CN-" : type == "Amended" ? "AMD-" : "INV-";
            string number = prefix + data.Booked.ToString("yyyy", CultureInfo.InvariantCulture) + "-" + bookingId.ToString("D6", CultureInfo.InvariantCulture) +
                (type == "Amended" ? "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) : String.Empty);
            string fileName = number + ".pdf";
            string output = Path.Combine(folder, fileName);
            string metadataPath = Path.Combine(folder, number + ".json");
            if (!IsCurrentInvoice(output, metadataPath))
            {
                string temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    try { RenderPdf(data, number, type, temporary); }
                    catch (Exception renderError)
                    {
                        System.Diagnostics.Trace.TraceError("QuestPDF invoice rendering failed; using the built-in PDF fallback: " + renderError);
                    }
                    if (!IsValidPdf(temporary)) RenderFallbackPdf(data, number, type, temporary);
                    if (!IsValidPdf(temporary)) throw new InvalidDataException("The invoice renderer did not produce a valid PDF.");
                    File.Copy(temporary, output, true);
                    if (!IsValidPdf(output))
                    {
                        if (File.Exists(output)) File.Delete(output);
                        throw new InvalidDataException("The generated invoice could not be saved as a valid PDF.");
                    }
                    File.WriteAllText(metadataPath, JsonConvert.SerializeObject(new InvoiceFileMetadata
                    { Version = 3, BookingId = bookingId, InvoiceNumber = number, Type = type, CreatedUtc = DateTime.UtcNow, FileName = fileName }));
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            return output;
        }

        private InvoiceData LoadOwnedBooking(string email, int id)
        {
            const string sql = @"SELECT b.Booking_ID,b.Booking_Date,b.Checkin_Date,b.Checkout_Date,b.Booking_Status,
 b.Booking_Total_Amount,b.Credit_Amount,b.Penalty_Amount,b.Number_Adults,b.Number_Children,br.Branch_Name,br.Branch_Address,br.Branch_Phone,br.Branch_Email,
 c.Email_Address,c.First_Name,c.Last_Name,c.Client_Address,c.Phone_Number
FROM Booking b INNER JOIN Client c ON c.Client_ID=b.Client_ID
LEFT JOIN Branch br ON br.Branch_ID=b.Branch_ID
WHERE b.Booking_ID=@Id AND c.Email_Address=@Email AND c.Client_Status='Active';
SELECT hr.hotel_room_type,hr.hotel_room_number FROM Room_Assignment ra
INNER JOIN Booking b ON b.Booking_ID=ra.Booking_ID
INNER JOIN Client c ON c.Client_ID=b.Client_ID
LEFT JOIN Hotel_Room hr ON hr.Hotel_Room_ID=ra.Hotel_Room_ID
WHERE b.Booking_ID=@Id AND c.Email_Address=@Email AND c.Client_Status='Active'";
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@Email", email ?? String.Empty);
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("That booking was not found on your account.");
                    var data = new InvoiceData
                    {
                        Id = id,
                        Email = Convert.ToString(reader["Email_Address"]),
                        GuestName = (Convert.ToString(reader["First_Name"]) + " " + Convert.ToString(reader["Last_Name"])).Trim(),
                        GuestAddress = Convert.ToString(reader["Client_Address"]),
                        GuestPhone = Convert.ToString(reader["Phone_Number"]),
                        Status = Convert.ToString(reader["Booking_Status"]),
                        Booked = Convert.ToDateTime(reader["Booking_Date"]),
                        CheckIn = Convert.ToDateTime(reader["Checkin_Date"]),
                        CheckOut = Convert.ToDateTime(reader["Checkout_Date"]),
                        Branch = reader["Branch_Name"] == DBNull.Value ? "Branch details unavailable" : Convert.ToString(reader["Branch_Name"]),
                        BranchAddress = Convert.ToString(reader["Branch_Address"]),
                        BranchPhone = Convert.ToString(reader["Branch_Phone"]),
                        BranchEmail = Convert.ToString(reader["Branch_Email"]),
                        Total = reader["Booking_Total_Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Booking_Total_Amount"]),
                        GuestCount = (reader["Number_Adults"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Number_Adults"])) +
                            (reader["Number_Children"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Number_Children"])),
                        Refund = reader["Credit_Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Credit_Amount"]),
                        Penalty = reader["Penalty_Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Penalty_Amount"])
                    };
                    if (reader.NextResult())
                        while (reader.Read()) data.Rooms.Add(new InvoiceRoomLine { Label = Convert.ToString(reader["hotel_room_type"]) + " · Room " + Convert.ToString(reader["hotel_room_number"]) });
                    var storedPayment = BookingSupportStore.ReadPayment(id);
                    if (storedPayment != null)
                    {
                        data.Branch = String.IsNullOrWhiteSpace(storedPayment.BranchName) ? data.Branch : storedPayment.BranchName;
                        data.GuestCount = storedPayment.GuestCount;
                        data.PayPalOrderId = storedPayment.PayPalOrderId;
                        data.CaptureId = storedPayment.CaptureId;
                        data.AmountPaidUsd = storedPayment.UsdAmount;
                        data.PaidUtc = storedPayment.CapturedUtc == DateTime.MinValue ? (DateTime?)null : storedPayment.CapturedUtc;
                        if (storedPayment.Status == "Paid" || storedPayment.Status == "PendingPayment")
                        {
                            data.Subtotal = storedPayment.Subtotal;
                            data.TaxAmount = storedPayment.TaxAmount;
                        }
                        if (storedPayment.Rooms != null && storedPayment.Rooms.Count > 0)
                        {
                            data.Rooms.Clear();
                            foreach (var room in storedPayment.Rooms)
                                data.Rooms.Add(new InvoiceRoomLine { Label = room.RoomType + " · Room " + room.RoomNumber, Rate = room.RatePerNight, Subtotal = room.Subtotal });
                        }
                    }
                    return data;
                }
            }
        }

        private static void RenderPdf(InvoiceData data, string invoiceNumber, string type, string path)
        {
            bool creditNote = type == "CreditNote";
            byte[] qrImage;
            using (var generator = new QRCodeGenerator())
            using (var qrData = generator.CreateQrCode("REGAL-" + data.Id.ToString(CultureInfo.InvariantCulture), QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new PngByteQRCode(qrData))
                qrImage = qrCode.GetGraphic(8);
            var payment = BookingSupportStore.ReadPayment(data.Id);
            Document.Create(document => document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(42);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style.FontSize(10).FontColor(Colors.Grey.Darken3));
                page.Header().Column(column =>
                {
                    column.Item().Text("THE REGAL INN").FontSize(22).Bold().FontColor(Colors.Blue.Darken3);
                    column.Item().PaddingTop(5).Text(creditNote ? "CREDIT NOTE" : type == "Amended" ? "AMENDED INVOICE" : "PAID INVOICE / RECEIPT").FontSize(15).Bold();
                    column.Item().Text("Invoice number: " + invoiceNumber).FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                page.Content().PaddingVertical(20).Column(column =>
                {
                    column.Spacing(9);
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("GUEST").Bold();
                            c.Item().Text(String.IsNullOrWhiteSpace(data.GuestName) ? data.Email : data.GuestName);
                            c.Item().Text(data.Email);
                            if (!String.IsNullOrWhiteSpace(data.GuestPhone)) c.Item().Text(data.GuestPhone);
                            if (!String.IsNullOrWhiteSpace(data.GuestAddress)) c.Item().Text(data.GuestAddress);
                        });
                        row.RelativeItem().Column(c => { c.Item().Text("BOOKING").Bold(); c.Item().Text("#" + data.Id); c.Item().Text("Booked " + data.Booked.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)); });
                    });
                    column.Item().Text("Invoice / receipt date: " + DateTime.UtcNow.ToString("dd MMM yyyy", CultureInfo.InvariantCulture));
                    column.Item().PaddingTop(6).Text("Branch: " + data.Branch).Bold();
                    if (!String.IsNullOrWhiteSpace(data.BranchAddress)) column.Item().Text(data.BranchAddress);
                    if (!String.IsNullOrWhiteSpace(data.BranchPhone) || !String.IsNullOrWhiteSpace(data.BranchEmail))
                        column.Item().Text(String.Join(" · ", new[] { data.BranchPhone, data.BranchEmail }.Where(value => !String.IsNullOrWhiteSpace(value))));
                    column.Item().Text("Check-in: " + data.CheckIn.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) +
                        "     Check-out: " + data.CheckOut.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) +
                        "     Nights: " + Math.Max(0, (data.CheckOut.Date - data.CheckIn.Date).Days) +
                        (data.GuestCount > 0 ? "     Guests: " + data.GuestCount : String.Empty));
                    column.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns => { columns.RelativeColumn(4); columns.RelativeColumn(2); columns.RelativeColumn(2); });
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Blue.Darken3).Padding(7).Text("Booked room").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken3).Padding(7).Text("Rate / night").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken3).Padding(7).Text("Room subtotal").FontColor(Colors.White).Bold();
                        });
                        if (data.Rooms.Count == 0)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(7).Text("Room details unavailable");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(7).Text("Not stored");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(7).Text("Not stored");
                        }
                        foreach (var room in data.Rooms)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(7).Text(room.Label);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(7).Text(room.Rate.HasValue ? "R" + room.Rate.Value.ToString("N2", CultureInfo.InvariantCulture) : "Not stored");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(7).Text(room.Subtotal.HasValue ? "R" + room.Subtotal.Value.ToString("N2", CultureInfo.InvariantCulture) : "Not stored");
                        }
                    });
                    if (data.Subtotal.HasValue)
                    {
                        column.Item().AlignRight().Text("Room subtotal: R" + data.Subtotal.Value.ToString("N2", CultureInfo.InvariantCulture));
                        column.Item().AlignRight().Text("Taxes / fees: R" + (data.TaxAmount ?? 0m).ToString("N2", CultureInfo.InvariantCulture));
                    }
                    else column.Item().PaddingTop(7).Text("Historical per-room rates and tax breakdown were not retained for this booking in the existing database.").FontSize(8).FontColor(Colors.Grey.Darken1);
                    if (creditNote)
                    {
                        column.Item().AlignRight().PaddingTop(8).Text("Original booking total: R" + data.Total.ToString("N2", CultureInfo.InvariantCulture));
                        column.Item().AlignRight().Text("Non-refundable amount: R" + data.Penalty.ToString("N2", CultureInfo.InvariantCulture));
                        column.Item().AlignRight().Text("Recorded refund credit: R" + data.Refund.ToString("N2", CultureInfo.InvariantCulture)).FontSize(14).Bold();
                    }
                    else
                    {
                        column.Item().AlignRight().PaddingTop(8).Text("Total paid: R" + data.Total.ToString("N2", CultureInfo.InvariantCulture)).FontSize(14).Bold();
                        column.Item().AlignRight().Text("Balance due: R0.00");
                    }
                    column.Item().PaddingTop(3).Text("Booking status: " + data.Status + " · Payment method: PayPal · Payment status: " + (data.Status == "Cancelled" ? "Cancelled" : "Paid / confirmed"));
                    if (data.PaidUtc.HasValue) column.Item().Text("Paid on: " + data.PaidUtc.Value.ToString("dd MMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture));
                    if (!String.IsNullOrWhiteSpace(data.PayPalOrderId)) column.Item().Text("PayPal order: " + data.PayPalOrderId);
                    if (!String.IsNullOrWhiteSpace(data.CaptureId)) column.Item().Text("PayPal transaction: " + data.CaptureId);
                    if (data.AmountPaidUsd.HasValue && data.AmountPaidUsd.Value > 0)
                        column.Item().Text("PayPal amount: USD " + data.AmountPaidUsd.Value.ToString("N2", CultureInfo.InvariantCulture));
                    if (creditNote) column.Item().Text("Refund processing: " + (payment == null ? "Manual review" : payment.RefundStatus));
                    column.Item().PaddingTop(8).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(policy =>
                    {
                        policy.Item().Text("CANCELLATION REFUND POLICY").Bold();
                        policy.Item().Text("7 or more days before check-in: 100% credit.");
                        policy.Item().Text("48 hours to less than 7 days before check-in: 50% credit.");
                        policy.Item().Text("Less than 48 hours before check-in or no-show: 0% credit.");
                        policy.Item().Text("Demo credits are recorded on the booking; no PayPal refund is sent.").FontSize(8);
                    });
                    column.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().Column(qr =>
                        {
                            qr.Item().Text("CHECK-IN QR CODE").Bold();
                            qr.Item().Text("REGAL-" + data.Id).FontSize(13).Bold().FontColor(Colors.Blue.Darken3);
                            qr.Item().Text("Present this booking reference at reception.").FontSize(8);
                        });
                        row.ConstantItem(105).Height(105).Image(qrImage);
                    });
                });
                page.Footer().AlignCenter().Text(text => { text.Span("Regal Inn · Booking #" + data.Id + " · Page "); text.CurrentPageNumber(); });
            })).GeneratePdf(path);
        }

        private sealed class PdfTextLine
        {
            public string Text;
            public int FontSize;
            public bool Bold;
        }

        // Keep invoice downloads available if a native QuestPDF/font runtime fails under IIS Express.
        // This fallback uses only .NET Framework APIs and writes a small, valid PDF directly.
        private static void RenderFallbackPdf(InvoiceData data, string invoiceNumber, string type, string path)
        {
            bool creditNote = type == "CreditNote";
            var lines = new List<PdfTextLine>();
            Action<string, int, bool> add = (text, size, bold) => AddPdfLines(lines, text, size, bold);
            var payment = BookingSupportStore.ReadPayment(data.Id);
            int nights = Math.Max(0, (data.CheckOut.Date - data.CheckIn.Date).Days);

            add("THE REGAL INN HOTEL", 20, true);
            add(creditNote ? "CREDIT NOTE" : type == "Amended" ? "AMENDED INVOICE" : "PAID INVOICE / RECEIPT", 14, true);
            add("Invoice number: " + invoiceNumber, 10, false);
            add("Invoice date: " + DateTime.UtcNow.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + " UTC", 10, false);
            add("", 10, false);
            add("GUEST DETAILS", 12, true);
            add("Guest: " + (String.IsNullOrWhiteSpace(data.GuestName) ? data.Email : data.GuestName), 10, false);
            add("Email: " + data.Email, 10, false);
            if (!String.IsNullOrWhiteSpace(data.GuestPhone)) add("Phone: " + data.GuestPhone, 10, false);
            if (!String.IsNullOrWhiteSpace(data.GuestAddress)) add("Address: " + data.GuestAddress, 10, false);
            add("", 10, false);
            add("BOOKING DETAILS", 12, true);
            add("Booking reference: #" + data.Id + "  |  Status: " + data.Status, 10, false);
            add("Booked: " + data.Booked.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), 10, false);
            add("Branch: " + data.Branch, 10, false);
            if (!String.IsNullOrWhiteSpace(data.BranchAddress)) add("Branch address: " + data.BranchAddress, 10, false);
            if (!String.IsNullOrWhiteSpace(data.BranchPhone)) add("Branch phone: " + data.BranchPhone, 10, false);
            if (!String.IsNullOrWhiteSpace(data.BranchEmail)) add("Branch email: " + data.BranchEmail, 10, false);
            add("Check-in: " + data.CheckIn.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) +
                "  |  Check-out: " + data.CheckOut.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) +
                "  |  Nights: " + nights + "  |  Guests: " + data.GuestCount, 10, false);
            add("", 10, false);
            add("BOOKED ROOMS", 12, true);
            if (data.Rooms.Count == 0) add("Room details are unavailable for this booking.", 10, false);
            foreach (var room in data.Rooms)
            {
                string amount = room.Subtotal.HasValue ? "R" + room.Subtotal.Value.ToString("N2", CultureInfo.InvariantCulture) : "Rate not retained";
                string rate = room.Rate.HasValue ? "R" + room.Rate.Value.ToString("N2", CultureInfo.InvariantCulture) + " per night" : "nightly rate not retained";
                add(room.Label + "  |  " + rate + "  |  Room total: " + amount, 10, false);
            }
            add("", 10, false);
            add("PAYMENT SUMMARY", 12, true);
            if (data.Subtotal.HasValue) add("Room subtotal: R" + data.Subtotal.Value.ToString("N2", CultureInfo.InvariantCulture), 10, false);
            if (data.TaxAmount.HasValue) add("Taxes / fees: R" + data.TaxAmount.Value.ToString("N2", CultureInfo.InvariantCulture), 10, false);
            if (!data.Subtotal.HasValue && !data.TaxAmount.HasValue) add("Tax and room-rate breakdown was not retained in the booking record.", 9, false);
            add("Booking total: R" + data.Total.ToString("N2", CultureInfo.InvariantCulture), 12, true);
            add("Payment method: " + (payment == null ? "PayPal Checkout" : "PayPal Checkout"), 10, false);
            add("Payment status: " + (payment != null && payment.Status == "Paid" ? "Paid" : data.Status == "Cancelled" ? "Cancelled after payment" : "Confirmed"), 10, false);
            if (data.PaidUtc.HasValue) add("Paid on: " + data.PaidUtc.Value.ToString("dd MMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture), 10, false);
            if (!String.IsNullOrWhiteSpace(data.PayPalOrderId)) add("PayPal order reference: " + data.PayPalOrderId, 9, false);
            if (!String.IsNullOrWhiteSpace(data.CaptureId)) add("Payment transaction reference: " + data.CaptureId, 9, false);
            if (creditNote)
            {
                add("Recorded refund credit: R" + data.Refund.ToString("N2", CultureInfo.InvariantCulture), 12, true);
                add("Non-refundable amount: R" + data.Penalty.ToString("N2", CultureInfo.InvariantCulture), 10, false);
                add("Refund processing: " + (payment == null ? "Manual review" : payment.RefundStatus), 10, false);
            }
            add("", 10, false);
            add("CANCELLATION REFUND POLICY", 12, true);
            add("7 or more days before check-in: 100% credit.", 9, false);
            add("48 hours to less than 7 days before check-in: 50% credit.", 9, false);
            add("Less than 48 hours before check-in or no-show: 0% credit.", 9, false);
            add("Demo credits are recorded on the booking; no PayPal refund is sent.", 8, false);
            add("", 10, false);
            add("Check-in reference: REGAL-" + data.Id + "  |  Please present this reference at reception.", 10, true);

            var pages = new List<List<PdfTextLine>>();
            for (int i = 0; i < lines.Count; i += 48) pages.Add(lines.Skip(i).Take(48).ToList());
            if (pages.Count == 0) pages.Add(new List<PdfTextLine>());

            var objects = new List<byte[]>();
            objects.Add(PdfAscii("<< /Type /Catalog /Pages 2 0 R >>"));
            string kids = String.Join(" ", Enumerable.Range(0, pages.Count).Select(i => (5 + i * 2) + " 0 R"));
            objects.Add(PdfAscii("<< /Type /Pages /Kids [" + kids + "] /Count " + pages.Count + " >>"));
            objects.Add(PdfAscii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
            objects.Add(PdfAscii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));
            for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                var content = new System.Text.StringBuilder();
                int y = 790;
                foreach (var line in pages[pageIndex])
                {
                    content.Append("BT /").Append(line.Bold ? "F2" : "F1").Append(' ').Append(line.FontSize)
                        .Append(" Tf 48 ").Append(y).Append(" Td (").Append(EscapePdfText(line.Text)).Append(") Tj ET\n");
                    y -= line.FontSize + 6;
                }
                content.Append("BT /F1 8 Tf 48 28 Td (Regal Inn | Booking #").Append(data.Id)
                    .Append(" | Page ").Append(pageIndex + 1).Append(" of ").Append(pages.Count).Append(") Tj ET\n");
                byte[] stream = PdfAscii(content.ToString());
                objects.Add(PdfAscii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents " + (6 + pageIndex * 2) + " 0 R >>"));
                var streamObject = new byte[PdfAscii("<< /Length " + stream.Length + " >>\nstream\n").Length + stream.Length + PdfAscii("\nendstream").Length];
                byte[] start = PdfAscii("<< /Length " + stream.Length + " >>\nstream\n"), end = PdfAscii("\nendstream");
                Buffer.BlockCopy(start, 0, streamObject, 0, start.Length);
                Buffer.BlockCopy(stream, 0, streamObject, start.Length, stream.Length);
                Buffer.BlockCopy(end, 0, streamObject, start.Length + stream.Length, end.Length);
                objects.Add(streamObject);
            }

            using (var output = new MemoryStream())
            {
                WritePdf(output, "%PDF-1.4\n% Regal Inn invoice\n");
                var offsets = new List<long> { 0 };
                for (int i = 0; i < objects.Count; i++)
                {
                    offsets.Add(output.Position);
                    WritePdf(output, (i + 1) + " 0 obj\n");
                    output.Write(objects[i], 0, objects[i].Length);
                    WritePdf(output, "\nendobj\n");
                }
                long xref = output.Position;
                WritePdf(output, "xref\n0 " + (objects.Count + 1) + "\n0000000000 65535 f \n");
                foreach (long offset in offsets.Skip(1)) WritePdf(output, offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
                WritePdf(output, "trailer\n<< /Size " + (objects.Count + 1) + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF");
                File.WriteAllBytes(path, output.ToArray());
            }
        }

        private static void AddPdfLines(List<PdfTextLine> lines, string text, int fontSize, bool bold)
        {
            string value = (text ?? String.Empty).Replace('\r', ' ').Replace('\n', ' ');
            if (String.IsNullOrWhiteSpace(value)) { lines.Add(new PdfTextLine { Text = String.Empty, FontSize = 5, Bold = false }); return; }
            const int maxChars = 92;
            while (value.Length > maxChars)
            {
                int split = value.LastIndexOf(' ', maxChars);
                if (split < 1) split = maxChars;
                lines.Add(new PdfTextLine { Text = value.Substring(0, split), FontSize = fontSize, Bold = bold });
                value = value.Substring(split).TrimStart();
            }
            lines.Add(new PdfTextLine { Text = value, FontSize = fontSize, Bold = bold });
        }

        private static string EscapePdfText(string value)
        {
            var plain = new System.Text.StringBuilder();
            foreach (char ch in (value ?? String.Empty).Normalize(System.Text.NormalizationForm.FormD))
            {
                if (Char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (ch == '\u2013' || ch == '\u2014') plain.Append('-');
                else if (ch == '\u00b7') plain.Append('|');
                else if (ch == '\u2019' || ch == '\u2018') plain.Append('\'');
                else if (ch == '\u00a0') plain.Append(' ');
                else if (ch >= 32 && ch <= 126) plain.Append(ch);
                else plain.Append('?');
            }
            return plain.ToString().Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }

        private static byte[] PdfAscii(string value) { return System.Text.Encoding.ASCII.GetBytes(value); }
        private static void WritePdf(Stream stream, string value) { byte[] bytes = PdfAscii(value); stream.Write(bytes, 0, bytes.Length); }

        private static bool IsValidPdf(string path)
        {
            if (!File.Exists(path)) return false;
            long length = new FileInfo(path).Length;
            if (length < 256) return false;
            using (var stream = File.OpenRead(path))
            {
                byte[] header = new byte[5];
                if (stream.Read(header, 0, header.Length) != header.Length || System.Text.Encoding.ASCII.GetString(header) != "%PDF-") return false;
                int tailLength = (int)Math.Min(1024, length);
                stream.Seek(-tailLength, SeekOrigin.End);
                byte[] tail = new byte[tailLength];
                int read = stream.Read(tail, 0, tail.Length);
                return System.Text.Encoding.ASCII.GetString(tail, 0, read).Contains("%%EOF");
            }
        }

        private static bool IsCurrentInvoice(string pdfPath, string metadataPath)
        {
            if (!IsValidPdf(pdfPath) || !File.Exists(metadataPath)) return false;
            try
            {
                var metadata = JsonConvert.DeserializeObject<InvoiceFileMetadata>(File.ReadAllText(metadataPath));
                return metadata != null && metadata.Version == 3 &&
                    String.Equals(metadata.FileName, Path.GetFileName(pdfPath), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }
}
