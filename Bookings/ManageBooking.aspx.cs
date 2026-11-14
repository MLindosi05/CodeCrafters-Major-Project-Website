using System;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using CodeCrafters_Major_Project_Website.Models;

namespace CodeCrafters_Major_Project_Website.Bookings
{
    public partial class ManageBooking : Page
    {
        protected int BookingId { get; private set; }
        protected string InitialCheckIn { get; private set; }
        protected string InitialCheckOut { get; private set; }
        protected string ChangeCsrfToken { get; private set; }
        protected string PayPalClientId { get { return ConfigurationManager.AppSettings["PayPal:ClientId"] ?? String.Empty; } }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            { Response.Redirect("~/Account/Login.aspx?ReturnUrl=" + Server.UrlEncode(Request.RawUrl)); return; }
            if (!Int32.TryParse(Request.QueryString["bookingId"], out int id) || id < 1)
            { ShowUnavailable("Choose a valid booking."); return; }
            try
            {
                var booking = new MyBookingsService().GetBookingsForUser(User.Identity.Name).FirstOrDefault(b => b.BookingId == id);
                if (booking == null || !booking.CanModifyDates) { ShowUnavailable("Only your upcoming paid bookings can be changed online."); return; }
                BookingId = id;
                InitialCheckIn = booking.CheckIn.ToString("yyyy-MM-dd"); InitialCheckOut = booking.CheckOut.ToString("yyyy-MM-dd");
                litBookingId.Text = id.ToString(); litStay.Text = booking.CheckIn.ToString("dd MMM yyyy") + " – " + booking.CheckOut.ToString("dd MMM yyyy"); litBranch.Text = booking.BranchName;
                ddlSourceRoom.DataSource = booking.Rooms; ddlSourceRoom.DataTextField = "RoomType"; ddlSourceRoom.DataValueField = "RoomId"; ddlSourceRoom.DataBind();
                foreach (var branch in new BookingModificationService().GetBranches(User.Identity.Name, id)) ddlBranch.Items.Add(new ListItem(branch.Value, branch.Key));
                string token = Convert.ToString(Session["BookingChange.Csrf"]);
                if (String.IsNullOrEmpty(token)) { byte[] bytes = new byte[32]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes); token = Convert.ToBase64String(bytes); Session["BookingChange.Csrf"] = token; }
                using (var sha = SHA256.Create()) ChangeCsrfToken = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(token)));
            }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("Manage booking load failed: " + ex); ShowUnavailable("We could not load this booking. Please return to My Bookings and try again."); }
        }

        private void ShowUnavailable(string message) { pnlChange.Visible = false; lblError.Visible = true; lblError.Text = Server.HtmlEncode(message); }
    }
}
