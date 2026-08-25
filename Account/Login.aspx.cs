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
            // Nothing needed here on initial load — form posts back to itself.
        }

        protected void LogIn_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;

            // SignInStatus.Success / LockedOut / RequiresVerification / Failure
            var result = SignInManager.PasswordSignIn(
                Email.Text,
                Password.Text,
                RememberMe.Checked,
                shouldLockout: false);

            switch (result)
            {
                case SignInStatus.Success:
                    RedirectAfterLogin();
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

        private void RedirectAfterLogin()
        {
            string returnUrl = Request.QueryString["ReturnUrl"];

            // Only redirect to a local URL — never trust an external ReturnUrl,
            // this prevents open-redirect attacks. (Web Forms has no built-in
            // IsLocalUrl helper like MVC does, so we check manually.)
            if (IsSafeLocalUrl(returnUrl))
            {
                Response.Redirect(returnUrl);
            }
            else
            {
                Response.Redirect("~/Default.aspx");
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
