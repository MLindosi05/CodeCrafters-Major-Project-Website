using System;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Configuration;
using System.Data.SqlClient;
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
            string email = Email.Text.Trim();
            var user = new ApplicationUser { UserName = email, Email = email };

            IdentityResult result = UserManager.Create(user, Password.Text);

            if (result.Succeeded)
            {
                try
                {
                    CreateClientProfile(email);
                    RoleBootstrapper.EnsureGuestRole(UserManager, user.Id);
                    // EmailService is intentionally a no-op until SMTP is configured.
                    // The account remains usable; do not send users to a dead confirmation page.
                    Response.Redirect("~/RegisterConfirmation.aspx?email=" + Server.UrlEncode(email));
                }
                catch (Exception)
                {
                    UserManager.Delete(user);
                    ErrorMessage.Text = "<div class='form-msg error'>We could not create your guest profile. Please try again.</div>";
                }
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

        private void CreateClientProfile(string email)
        {
            const string sql = @"IF NOT EXISTS (SELECT 1 FROM Client WHERE Email_Address = @Email)
INSERT INTO Client (First_Name, Last_Name, Password, Email_Address, Client_Status, Date_Registered)
VALUES (@FirstName, @LastName, '[Identity managed]', @Email, 'Active', CAST(GETDATE() AS date));";
            using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@FirstName", FirstName.Text.Trim());
                command.Parameters.AddWithValue("@LastName", LastName.Text.Trim());
                command.Parameters.AddWithValue("@Email", email);
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
}
