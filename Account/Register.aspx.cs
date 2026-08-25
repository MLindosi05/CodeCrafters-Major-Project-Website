using System;
using System.Linq;
using System.Web;
using System.Web.UI;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Owin;
using CodeCrafters_Major_Project_Website.Models;

namespace CodeCrafters_Major_Project_Website.Account
{
    public partial class Register : System.Web.UI.Page
    {
        private ApplicationUserManager _userManager;
        private ApplicationSignInManager _signInManager;

        public ApplicationUserManager UserManager
        {
            get { return _userManager ?? (_userManager = Context.GetOwinContext().GetUserManager<ApplicationUserManager>()); }
            private set { _userManager = value; }
        }

        public ApplicationSignInManager SignInManager
        {
            get { return _signInManager ?? (_signInManager = Context.GetOwinContext().Get<ApplicationSignInManager>()); }
            private set { _signInManager = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
        }

        protected void CreateUser_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;

            // ApplicationUser is the model class already generated in your
            // Models/IdentityModels.cs when Identity was first scaffolded.
            var user = new ApplicationUser { UserName = Email.Text, Email = Email.Text };

            IdentityResult result = UserManager.Create(user, Password.Text);

            if (result.Succeeded)
            {
                SendConfirmationEmail(user);

                // Don't sign the user in yet — email confirmation should
                // happen first for a secure flow. Send them to a friendly
                // "check your inbox" page instead.
                Response.Redirect("~/Account/RegisterConfirmation.aspx?email=" + Server.UrlEncode(Email.Text));
            }
            else
            {
                // Identity's built-in rules (min length, needs digit, etc.)
                // surface here automatically — e.g. "Passwords must have at
                // least one non letter or digit character."
                ErrorMessage.Text = "<div class='form-msg error'>" +
                    string.Join("<br/>", result.Errors) + "</div>";
            }
        }

        private void SendConfirmationEmail(ApplicationUser user)
        {
            string code = UserManager.GenerateEmailConfirmationToken(user.Id);

            string confirmUrl = string.Format(
                "{0}://{1}/Account/ConfirmEmail.aspx?userId={2}&code={3}",
                Request.Url.Scheme,
                Request.Url.Authority,
                Server.UrlEncode(user.Id),
                Server.UrlEncode(code));

            string body = "Welcome to The Regal Inn Hotel! Please confirm your account by " +
                "<a href=\"" + confirmUrl + "\">clicking here</a>.";

            // This calls EmailService.cs (see earlier file), which is wired
            // up as UserManager.EmailService in IdentityConfig.cs.
            UserManager.SendEmail(user.Id, "Confirm your Regal Inn Hotel account", body);
        }
    }
}
