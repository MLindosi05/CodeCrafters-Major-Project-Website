using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;


namespace CodeCrafters_Major_Project_Website
{
    public partial class Payments : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Gate on the server, not just the client — so someone can't
            // reach this page just by typing the URL, even without clicking
            // "Continue to Booking" while logged out.
            if (!User.Identity.IsAuthenticated)
            {
                Response.Redirect("~/Account/Login.aspx?ReturnUrl=" + Server.UrlEncode(Request.RawUrl));
            }
        }
    }
}
