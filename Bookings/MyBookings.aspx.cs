using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web;
using CodeCrafters_Major_Project_Website.Models;

namespace CodeCrafters_Major_Project_Website.Bookings
{
    public partial class MyBookings : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                Response.Redirect("~/Account/Login.aspx?ReturnUrl=" + Server.UrlEncode(Request.RawUrl));
                return;
            }
            if (!IsPostBack)
            {
                string notice = Session["MyBookings.Notice"] as string;
                if (!String.IsNullOrWhiteSpace(notice))
                {
                    Session.Remove("MyBookings.Notice");
                    ClientScript.RegisterStartupScript(GetType(), "bookingNotice", "RegalDialog.alert(" + HttpUtility.JavaScriptStringEncode(notice, true) + ", { title: 'Booking update' });", true);
                }
            }
            BindBookings();
        }

        private void BindBookings()
        {
            try
            {
                var bookings = new MyBookingsService().GetBookingsForUser(User.Identity.Name);
                string tab = (Request.QueryString["tab"] ?? "upcoming").ToLowerInvariant();
                DateTime today = CurrentHotelDate();
                IEnumerable<BookingSummaryVm> filtered = bookings;
                if (tab == "upcoming") filtered = bookings.Where(b => IsActive(b.Status) && b.CheckIn.Date > today);
                else if (tab == "active") filtered = bookings.Where(b => IsActive(b.Status) && b.CheckIn.Date <= today && b.CheckOut.Date > today);
                else if (tab == "past") filtered = bookings.Where(b => b.Status == "Completed" || (b.Status != "Cancelled" && b.CheckOut.Date <= today));
                else if (tab == "cancelled") filtered = bookings.Where(b => b.Status == "Cancelled");

                rptBookings.DataSource = filtered.OrderByDescending(b => b.BookingDate).ToList();
                rptBookings.DataBind();
                pnlNoBookings.Visible = rptBookings.Items.Count == 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("MyBookings load failed: " + ex);
                lblBookingsMessage.Text = "We could not load your bookings. Please refresh the page or try again later.";
                lblBookingsMessage.CssClass = "form-msg error";
                lblBookingsMessage.Visible = true;
            }
        }

        protected void rptBookings_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            int bookingId;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument), out bookingId) || bookingId < 1) return;
            string redirectTab = null;
            try
            {
                var service = new MyBookingsService();
                if (e.CommandName == "CancelBooking")
                {
                    decimal refund = service.CancelBooking(User.Identity.Name, bookingId);
                    try
                    {
                        BookingSupportStore.RecordDemoRefund(bookingId, refund);
                        string creditNote = refund > 0 ? new InvoiceService().GetOrCreate(User.Identity.Name, bookingId, true) : null;
                        BookingSupportStore.EnqueueEmail(User.Identity.Name, "Your Regal Inn booking was cancelled",
                            "Booking #" + bookingId + " was cancelled. Demo refund credit recorded: R" + refund.ToString("N2") + ". This is recorded for demonstration and no PayPal refund was sent.", creditNote);
                    }
                    catch (Exception queueError) { System.Diagnostics.Trace.TraceError("Cancellation follow-up queue failed: " + queueError); }
                    lblBookingsMessage.Text = refund <= 0
                        ? "Booking #" + bookingId + " was cancelled. No demo credit is due under the cancellation policy."
                        : "Booking #" + bookingId + " was cancelled. Demo refund credit of R" + refund.ToString("N2") + " is recorded. No PayPal refund was sent.";
                    lblBookingsMessage.CssClass = "form-msg success";
                    redirectTab = "cancelled";
                }
                else if (e.CommandName == "ResendConfirmation")
                {
                    service.QueueConfirmationEmail(User.Identity.Name, bookingId);
                    lblBookingsMessage.Text = "A confirmation email has been queued for booking #" + bookingId + ".";
                    lblBookingsMessage.CssClass = "form-msg success";
                }
                else if (e.CommandName == "MobileCheckIn")
                {
                    var document = e.Item.FindControl("txtCheckInDocument") as TextBox;
                    var arrival = e.Item.FindControl("txtArrivalTime") as TextBox;
                    var requests = e.Item.FindControl("txtSpecialRequests") as TextBox;
                    bool receptionNotified = service.MobileCheckIn(User.Identity.Name, bookingId, document == null ? null : document.Text,
                        arrival == null ? null : arrival.Text, requests == null ? null : requests.Text);
                    lblBookingsMessage.Text = receptionNotified
                        ? "Mobile check-in is complete for booking #" + bookingId + ". Your details and special requests are queued for reception."
                        : "Mobile check-in is complete for booking #" + bookingId + ". Your details are saved, but no reception email could be queued.";
                    lblBookingsMessage.CssClass = "form-msg success";
                    redirectTab = "active";
                }
                else if (e.CommandName == "MobileCheckOut")
                {
                    service.MobileCheckOut(User.Identity.Name, bookingId);
                    lblBookingsMessage.Text = "Mobile check-out is complete for booking #" + bookingId + ".";
                    lblBookingsMessage.CssClass = "form-msg success";
                    redirectTab = "past";
                }
            }
            catch (InvalidOperationException ex)
            {
                lblBookingsMessage.Text = Server.HtmlEncode(ex.Message);
                lblBookingsMessage.CssClass = "form-msg error";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("MyBookings action failed: " + ex);
                lblBookingsMessage.Text = "We could not update this booking. Please refresh and try again.";
                lblBookingsMessage.CssClass = "form-msg error";
            }
            if (!String.IsNullOrEmpty(redirectTab) && lblBookingsMessage.CssClass == "form-msg success")
            {
                Session["MyBookings.Notice"] = lblBookingsMessage.Text;
                Response.Redirect("MyBookings.aspx?tab=" + redirectTab, false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            lblBookingsMessage.Visible = true;
            ClientScript.RegisterStartupScript(GetType(), "bookingNotice", "RegalDialog.alert(" + HttpUtility.JavaScriptStringEncode(Server.HtmlDecode(lblBookingsMessage.Text), true) + ", { title: 'Booking update' });", true);
            BindBookings();
        }

        private static bool IsActive(string status)
        { return status == "Pending" || status == "Booked" || status == "Confirmed" || status == "CheckedIn"; }

        private static DateTime CurrentHotelDate()
        {
            try
            {
                string zoneId = ConfigurationManager.AppSettings["Hotel:TimeZoneId"];
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zoneId)).Date;
            }
            catch { return DateTime.Today; }
        }
    }
}
