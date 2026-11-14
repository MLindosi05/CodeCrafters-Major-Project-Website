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
        }
    }
}
