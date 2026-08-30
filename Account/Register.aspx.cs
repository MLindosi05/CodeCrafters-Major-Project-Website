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
using System.Data.Entity;

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

            string email = Email.Text.Trim();
            var user = new ApplicationUser { UserName = email, Email = email };

            IdentityResult result;
            try
            {
                result = UserManager.Create(user, Password.Text);
            }
            catch (Exception ex)
            {
                ErrorMessage.Text = "<div class='form-msg error'>Account creation failed unexpectedly: " + ex.Message + "</div>";
                return;
            }

            if (!result.Succeeded)
            {
                ErrorMessage.Text = "<div class='form-msg error'>" + string.Join("<br/>", result.Errors) + "</div>";
                return;
            }

            try
            {
                CreateClientProfile(email);
                RoleBootstrapper.EnsureGuestRole(UserManager, user.Id);
            }
            catch (Exception ex)
            {
                try { UserManager.Delete(user); } catch { /* ignore */ }
                ErrorMessage.Text = "<div class='form-msg error'>We could not create your guest profile. Please try again. (" + ex.Message + ")</div>";
                return;
            }

            Response.Redirect("~/RegisterConfirmation.aspx?email=" + Server.UrlEncode(email));
        }
        private void CreateClientProfile(string email)
        {
            const string sql = @"IF NOT EXISTS (SELECT 1 FROM Client WHERE Email_Address = @Email)
INSERT INTO Client (First_Name, Last_Name, Password, Email_Address, Phone_Number, Client_Status, Date_Registered)
VALUES (@FirstName, @LastName, '[Identity managed]', @Email, @Phone, 'Active', CAST(GETDATE() AS date));";

            using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@FirstName", FirstName.Text.Trim());
                command.Parameters.AddWithValue("@LastName", LastName.Text.Trim());
                command.Parameters.AddWithValue("@Email", email);
                command.Parameters.AddWithValue("@Phone", Phone.Text.Trim());
                connection.Open();
                int rows = command.ExecuteNonQuery();
                Log("CreateClientProfile for " + email + " — rows affected: " + rows);
            }
        }
        

        private void Log(string message)
        {
            try
            {
                System.IO.File.AppendAllText(Server.MapPath("~/App_Data/register_debug.txt"),
                    DateTime.Now + " | " + message + Environment.NewLine);
            }
            catch { /* never let logging break registration */ }
        }
    }
}