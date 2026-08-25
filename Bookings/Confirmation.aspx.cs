using System;
using System.Web.UI;
namespace CodeCrafters_Major_Project_Website.Bookings { public partial class Confirmation : Page { protected void Page_Load(object sender, EventArgs e) { int id; if (!int.TryParse(Request.QueryString["bookingId"], out id) || id < 1) Response.Redirect("~/Bookings/MyBookings.aspx"); BookingReference.Text = id.ToString(); } } }
