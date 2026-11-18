using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using CodeCrafters_Major_Project_Website.Models;

namespace CodeCrafters_Major_Project_Website.Account
{
    public partial class TwoFactorAuthenticationSignIn : System.Web.UI.Page
    {
        private ApplicationSignInManager signinManager;
        private ApplicationUserManager manager;

        public TwoFactorAuthenticationSignIn()
        {
            manager = Context.GetOwinContext().GetUserManager<ApplicationUserManager>();
            signinManager = Context.GetOwinContext().GetUserManager<ApplicationSignInManager>();
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Retrieve user ID either from standard Identity signin manager or our session fallback
                var userId = signinManager.GetVerifiedUserId<ApplicationUser, string>();
                if (String.IsNullOrEmpty(userId))
                {
                    userId = Session["SelectedUserId"] as string;
                }

                if (String.IsNullOrEmpty(userId))
                {
                    Response.Redirect("~/Account/Login.aspx", true);
                    return;
                }

                // Automatically populate and select "Email Code" provider if available
                var userFactors = manager.GetValidTwoFactorProviders(userId);
                if (userFactors != null && userFactors.Count > 0)
                {
                    Providers.DataSource = userFactors.Select(x => x).ToList();
                    Providers.DataBind();
                }
                else
                {
                    // Fallback default provider if none registered explicitly
                    Providers.DataSource = new List<string> { "Email Code" };
                    Providers.DataBind();
                }

                // Auto-send token on page load so the user instantly receives their email OTP
                string selectedProvider = Providers.SelectedValue;
                if (String.IsNullOrEmpty(selectedProvider)) selectedProvider = "Email Code";

                try
                {
                    string token = manager.GenerateTwoFactorToken(userId, selectedProvider);
                    var user = manager.FindById(userId);
                    if (user != null)
                    {
                        BookingSupportStore.EnqueueEmail(
                            user.Email,
                            "Your Regal Inn Security Code",
                            "Your verification code is: " + token,
                            null
                        );
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("Auto-sending 2FA token failed: " + ex);
                }

                SelectedProvider.Value = selectedProvider;
                sendcode.Visible = false;
                verifycode.Visible = true;
            }
        }

        protected void CodeSubmit_Click(object sender, EventArgs e)
        {
            bool rememberMe = false;
            bool.TryParse(Request.QueryString["RememberMe"], out rememberMe);

            string userId = Session["SelectedUserId"] as string;
            string provider = string.IsNullOrEmpty(SelectedProvider.Value) ? "Email Code" : SelectedProvider.Value;
            string enteredCode = Code.Text.Trim();

            // If Identity's signinManager lacks the active verified user ID in cookie, sign them in manually via token verification
            bool isAuthenticated = false;
            if (!String.IsNullOrEmpty(userId))
            {
                var user = manager.FindById(userId);
                if (user != null && manager.VerifyTwoFactorToken(userId, provider, enteredCode))
                {
                    signinManager.SignIn(user, isPersistent: rememberMe, rememberBrowser: RememberBrowser.Checked);
                    Session.Remove("SelectedUserId");
                    isAuthenticated = true;
                }
            }
            else
            {
                var result = signinManager.TwoFactorSignIn<ApplicationUser, string>(provider, enteredCode, isPersistent: rememberMe, rememberBrowser: RememberBrowser.Checked);
                if (result == SignInStatus.Success)
                {
                    isAuthenticated = true;
                }
                else if (result == SignInStatus.LockedOut)
                {
                    Response.Redirect("/Account/Lockout");
                    return;
                }
            }

            if (isAuthenticated)
            {
                string returnUrl = Request.QueryString["ReturnUrl"];
                if (!String.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith("/"))
                {
                    Response.Redirect(returnUrl, false);
                }
                else
                {
                    Response.Redirect("~/Bookings/MyBookings.aspx", false);
                }
                Context.ApplicationInstance.CompleteRequest();
            }
            else
            {
                FailureText.Text = "Invalid or expired security code. Please try again.";
                ErrorMessage.Visible = true;
            }
        }

        protected void ProviderSubmit_Click(object sender, EventArgs e)
        {
            string userId = signinManager.GetVerifiedUserId<ApplicationUser, string>();
            if (String.IsNullOrEmpty(userId))
            {
                userId = Session["SelectedUserId"] as string;
            }

            if (!String.IsNullOrEmpty(userId))
            {
                var user = manager.FindById(userId);
                if (user != null)
                {
                    var code = manager.GenerateTwoFactorToken(user.Id, Providers.SelectedValue);
                    BookingSupportStore.EnqueueEmail(
                        user.Email,
                        "Your Regal Inn Security Code",
                        "Your verification code is: " + code,
                        null
                    );
                }
            }

            SelectedProvider.Value = Providers.SelectedValue;
            sendcode.Visible = false;
            verifycode.Visible = true;
        }
    }
}