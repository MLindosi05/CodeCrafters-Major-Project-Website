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
            bool creatingStaffAccount = String.Equals(AccountType.SelectedValue, "Staff", StringComparison.OrdinalIgnoreCase);
            if (!creatingStaffAccount && !String.Equals(AccountType.SelectedValue, "Guest", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage.Text = "<div class='form-msg error'>Choose a valid account type.</div>";
                return;
            }

            string staffRole = null;
            if (creatingStaffAccount)
            {
                try
                {
                    // Staff accounts may only be created for an employee already present
                    // and active in the existing Staff table. Never create a Client row here.
                    staffRole = new StaffAccountService().MatchActiveStaffForRegistration(
                        email, FirstName.Text.Trim(), LastName.Text.Trim(), Phone.Text.Trim());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("Staff registration lookup failed: " + ex);
                    ErrorMessage.Text = "<div class='form-msg error'>Staff registration is temporarily unavailable. Please try again.</div>";
                    return;
                }
                if (String.IsNullOrWhiteSpace(staffRole))
                {
                    ErrorMessage.Text = "<div class='form-msg error'>We couldn't match those details to an active staff record. Check your details or contact your administrator.</div>";
                    return;
                }
            }

            var user = new ApplicationUser { UserName = email, Email = email };

            IdentityResult result;
            try
            {
                result = UserManager.Create(user, Password.Text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Account creation failed during registration: " + ex);
                ErrorMessage.Text = "<div class='form-msg error'>Account creation failed unexpectedly. Please try again.</div>";
                return;
            }

            if (!result.Succeeded)
            {
                ErrorMessage.Text = "<div class='form-msg error'>" + HttpUtility.HtmlEncode(string.Join(" ", result.Errors)) + "</div>";
                return;
            }

            try
            {
                if (creatingStaffAccount)
                    RoleBootstrapper.EnsureRole(UserManager, user.Id, StaffAccountService.ToIdentityRole(staffRole));
                else
                {
                    CreateClientProfile(email);
                    RoleBootstrapper.EnsureGuestRole(UserManager, user.Id);
                }
            }
            catch (Exception ex)
            {
                try { UserManager.Delete(user); } catch { /* ignore */ }
                System.Diagnostics.Trace.TraceError("Profile setup failed during registration: " + ex);
                ErrorMessage.Text = "<div class='form-msg error'>We could not finish setting up this account. Please try again.</div>";
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
