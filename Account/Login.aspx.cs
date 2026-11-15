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
        // These managers are already set up in your IdentityConfig.cs
        // (they were generated automatically when your login system was
        // first scaffolded). We just retrieve them here rather than
        // creating our own — reusing your existing Identity setup.
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public ApplicationSignInManager SignInManager
        {
            get { return _signInManager ?? (_signInManager = Context.GetOwinContext().Get<ApplicationSignInManager>()); }
            private set { _signInManager = value; }
        }

        public ApplicationUserManager UserManager
        {
            get { return _userManager ?? (_userManager = Context.GetOwinContext().GetUserManager<ApplicationUserManager>()); }
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

            // SignInStatus.Success / LockedOut / RequiresVerification / Failure
            var result = SignInManager.PasswordSignIn(
                Email.Text.Trim(),
                Password.Text,
                RememberMe.Checked,
                shouldLockout: false);

            switch (result)
            {
                case SignInStatus.Success:
                    if (!CompleteSelectedAccountSignIn(signingInAsStaff, staffRole)) return;
                    RedirectAfterLogin(signingInAsStaff);
                    break;

                case SignInStatus.LockedOut:
                    FailureText.Text = "<div class=\"form-msg error\">This account has been temporarily locked due to multiple failed attempts. Please try again later.</div>";
                    break;

                case SignInStatus.RequiresVerification:
                    // Two-factor auth is set up on this account — redirect to that flow if you have one.
                    Response.Redirect("~/Account/TwoFactorAuthenticationSignIn.aspx");
                    break;

                case SignInStatus.Failure:
                default:
                    FailureText.Text = "<div class=\"form-msg error\">Invalid email or password.</div>";
                    break;
            }
        }

        private bool CompleteSelectedAccountSignIn(bool signingInAsStaff, string staffRole)
        {
            var user = UserManager.FindByEmail(Email.Text.Trim());
            if (user == null)
            {
                SignOutCurrentUser();
                FailureText.Text = "<div class=\"form-msg error\">Invalid email or password.</div>";
                return false;
            }

            if (!signingInAsStaff)
            {
                bool hasStaffRole = UserManager.IsInRole(user.Id, RoleBootstrapper.Staff) ||
                    UserManager.IsInRole(user.Id, RoleBootstrapper.Manager) || UserManager.IsInRole(user.Id, RoleBootstrapper.Admin);
                if (hasStaffRole || !UserManager.IsInRole(user.Id, RoleBootstrapper.Guest))
                {
                    SignOutCurrentUser();
                    FailureText.Text = "<div class=\"form-msg error\">This account is not registered for guest sign-in. Select Manager / Staff.</div>";
                    return false;
                }
                return true;
            }

            string expectedRole = StaffAccountService.ToIdentityRole(staffRole);
            try
            {
                // The existing active Staff row is authoritative. Synchronize its role into
                // the Identity role claim so a changed staff role takes effect at next login.
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
                // Reissue the cookie so it contains the current staff role, not stale claims.
                SignInManager.SignIn(user, RememberMe.Checked, false);
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

            // Only redirect to a local URL — never trust an external ReturnUrl,
            // this prevents open-redirect attacks. (Web Forms has no built-in
            // IsLocalUrl helper like MVC does, so we check manually.)
            if (signingInAsStaff)
            {
                Response.Redirect(IsSafeLocalUrl(returnUrl) ? returnUrl : "~/Private/Business%20Intelligence.aspx");
            }
            else if (IsSafeLocalUrl(returnUrl)) Response.Redirect(returnUrl);
            else
            {
                Response.Redirect("~/Bookings/MyBookings.aspx");
            }
        }

        private bool IsSafeLocalUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (!url.StartsWith("/")) return false;          // must be relative
            if (url.StartsWith("//")) return false;           // protocol-relative = external
            if (url.Contains("://")) return false;             // absolute URL = external
            return true;
        }
    }
}
