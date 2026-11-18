using System;
using System.Web.UI;
namespace CodeCrafters_Major_Project_Website.Bookings
{
    public partial class Confirmation : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            int id;
            var paidBookings = Session["Payment.ConfirmedBookingIds"] as System.Collections.Generic.List<int>;
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated ||
                !String.Equals(Convert.ToString(Session["Payment.Owner"]), User.Identity.Name, StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(Request.QueryString["bookingId"], out id) || id < 1 || paidBookings == null || !paidBookings.Contains(id))
            {
                Response.Redirect("~/Bookings/MyBookings.aspx");
                return;
            }
            BookingReference.Text = id.ToString();
            lnkDownloadInvoice.NavigateUrl = "Invoice.ashx?bookingId=" + id;
        }
    }
}