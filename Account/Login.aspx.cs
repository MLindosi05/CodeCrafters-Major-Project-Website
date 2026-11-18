using System;
using System.Web;
using System.Web.UI;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Owin;
using CodeCrafters_Major_Project_Website.Models;

namespace CodeCrafters_Major_Project_Website.Account
{
    public partial class Login : System.Web.UI.Page
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public ApplicationSignInManager SignInManager
        {
            get { return _signInManager ?? (_signInManager = Context.GetOwinContext().Get<ApplicationSignInManager>()); }
            private set { _signInManager = value; }
        }

        public ApplicationUserManager UserManager
        {
            get { return _userManager ?? (_userManager = Context.GetOwinContext().Get<ApplicationUserManager>()); }
            private set { _userManager = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                string staffMessage = Request.QueryString["staff"];
                if (staffMessage == "selfinactive")
                {
                    FailureText.Text = "<div class=\"form-msg success\">Your manager status was changed to Inactive. You have been signed out.</div>";
                    RegisterLoginDialog("Your manager status was changed to Inactive. You have been signed out.", "Status updated");
                }
                else if (staffMessage == "inactive")
                    FailureText.Text = "<div class=\"form-msg error\">This staff account is inactive or its role changed. Contact an administrator if you need access restored.</div>";
                else if (staffMessage == "verify")
                    FailureText.Text = "<div class=\"form-msg error\">Staff access could not be verified. Please try again when the system is available.</div>";
            }
        }

        protected void LogIn_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;

            bool signingInAsStaff = String.Equals(AccountType.SelectedValue, "Staff", StringComparison.OrdinalIgnoreCase);
            string staffRole = null;
            if (signingInAsStaff)
            {
                try { staffRole = new StaffAccountService().FindActiveStaffRole(Email.Text.Trim()); }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("Staff login lookup failed: " + ex);
                    FailureText.Text = "<div class=\"form-msg error\">Staff sign-in is temporarily unavailable. Please try again.</div>";
                    return;
                }
                if (String.IsNullOrWhiteSpace(staffRole))
                {
                    FailureText.Text = "<div class=\"form-msg error\">No active staff account matches this email. Choose Guest / User or contact your administrator.</div>";
                    return;
                }
            }

            var user = UserManager.FindByEmail(Email.Text.Trim());
            if (user == null)
            {
                FailureText.Text = "<div class=\"form-msg error\">Invalid email or password.</div>";
                return;
            }

            bool passwordValid = UserManager.CheckPassword(user, Password.Text);
            if (!passwordValid)
            {
                FailureText.Text = "<div class=\"form-msg error\">Invalid email or password.</div>";
                return;
            }

            if (!signingInAsStaff)
            {
                bool hasStaffRole = UserManager.IsInRole(user.Id, RoleBootstrapper.Staff) ||
                    UserManager.IsInRole(user.Id, RoleBootstrapper.Manager) || UserManager.IsInRole(user.Id, RoleBootstrapper.Admin);
                if (hasStaffRole || !UserManager.IsInRole(user.Id, RoleBootstrapper.Guest))
                {
                    FailureText.Text = "<div class=\"form-msg error\">This account is not registered for guest sign-in. Select Manager / Staff.</div>";
                    return;
                }
            }
            else
            {
                if (!CompleteSelectedAccountSignIn(signingInAsStaff, staffRole, user)) return;
            }

            // Sign user in immediately without OTP
            SignInManager.SignIn(user, RememberMe.Checked, false);
            RedirectAfterLogin(signingInAsStaff);
        }

        private bool CompleteSelectedAccountSignIn(bool signingInAsStaff, string staffRole, ApplicationUser user)
        {
            string expectedRole = StaffAccountService.ToIdentityRole(staffRole);
            try
            {
                foreach (string role in UserManager.GetRoles(user.Id))
                {
                    if ((role == RoleBootstrapper.Staff || role == RoleBootstrapper.Manager || role == RoleBootstrapper.Admin || role == RoleBootstrapper.Guest)
                        && role != expectedRole)
                    {
                        var removed = UserManager.RemoveFromRole(user.Id, role);
                        if (!removed.Succeeded) throw new InvalidOperationException("Could not update staff access.");
                    }
                }
                RoleBootstrapper.EnsureRole(UserManager, user.Id, expectedRole);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Staff role synchronization failed: " + ex);
                SignOutCurrentUser();
                FailureText.Text = "<div class=\"form-msg error\">Staff access could not be verified. Please contact your administrator.</div>";
                return false;
            }
        }

        private void SignOutCurrentUser()
        {
            Context.GetOwinContext().Authentication.SignOut(Microsoft.AspNet.Identity.DefaultAuthenticationTypes.ApplicationCookie);
        }

        private void RegisterLoginDialog(string message, string title)
        {
            string script = "(function(){var show=function(){if(window.RegalDialog)window.RegalDialog.alert('" +
                HttpUtility.JavaScriptStringEncode(message) + "',{title:'" + HttpUtility.JavaScriptStringEncode(title) + "'});};if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',show);else show();})();";
            ClientScript.RegisterStartupScript(GetType(), "loginStatusDialog", script, true);
        }

        private void RedirectAfterLogin(bool signingInAsStaff)
        {
            string returnUrl = Request.QueryString["ReturnUrl"];
            if (signingInAsStaff)
            {
                Response.Redirect(IsSafeLocalUrl(returnUrl) ? returnUrl : "~/Private/Business%20Intelligence.aspx", false);
            }
            else if (IsSafeLocalUrl(returnUrl)) Response.Redirect(returnUrl, false);
            else
            {
                Response.Redirect("~/Bookings/MyBookings.aspx", false);
            }
            Context.ApplicationInstance.CompleteRequest();
        }

        private bool IsSafeLocalUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (!url.StartsWith("/")) return false;
            if (url.StartsWith("//")) return false;
            if (url.Contains("://")) return false;
            return true;
        }
    }
}