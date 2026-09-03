using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Owin;
using CodeCrafters_Major_Project_Website.Models;
using System.Configuration;
using System.Data.SqlClient;

namespace CodeCrafters_Major_Project_Website.Account
{
    public partial class Manage : System.Web.UI.Page
    {
        protected string SuccessMessage
        {
            get;
            private set;
        }

        private bool HasPassword(ApplicationUserManager manager)
        {
            return manager.HasPassword(User.Identity.GetUserId());
        }

        public bool HasPhoneNumber { get; private set; }

        public bool TwoFactorEnabled { get; private set; }

        public bool TwoFactorBrowserRemembered { get; private set; }

        public int LoginsCount { get; set; }

        protected void Page_Load()
        {
            var manager = Context.GetOwinContext().GetUserManager<ApplicationUserManager>();

            HasPhoneNumber = String.IsNullOrEmpty(manager.GetPhoneNumber(User.Identity.GetUserId()));

            // Enable this after setting up two-factor authentientication
            //PhoneNumber.Text = manager.GetPhoneNumber(User.Identity.GetUserId()) ?? String.Empty;

            TwoFactorEnabled = manager.GetTwoFactorEnabled(User.Identity.GetUserId());

            LoginsCount = manager.GetLogins(User.Identity.GetUserId()).Count;

            var authenticationManager = HttpContext.Current.GetOwinContext().Authentication;

            if (!IsPostBack)
            {
                LoadProfileFields(manager);

                var profileMsg = Request.QueryString["pm"];
                if (profileMsg == "ProfileUpdateSuccess")
                {
                    ProfileSuccessMessage = "Your profile has been updated.";
                    profileSuccessMessage.Visible = true;
                }
                // Determine the sections to render
                if (HasPassword(manager))
                {
                    ChangePassword.Visible = true;
                }
                else
                {
                    CreatePassword.Visible = true;
                    ChangePassword.Visible = false;
                }

                // Render success message
                var message = Request.QueryString["m"];
                if (message != null)
                {
                    // Strip the query string from action
                    Form.Action = ResolveUrl("~/Account/Manage");

                    SuccessMessage =
                        message == "ChangePwdSuccess" ? "Your password has been changed."
                        : message == "SetPwdSuccess" ? "Your password has been set."
                        : message == "RemoveLoginSuccess" ? "The account was removed."
                        : message == "AddPhoneNumberSuccess" ? "Phone number has been added"
                        : message == "RemovePhoneNumberSuccess" ? "Phone number was removed"
                        : String.Empty;
                    successMessage.Visible = !String.IsNullOrEmpty(SuccessMessage);
                }
            }
        }


        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        // Remove phonenumber from user
        protected void RemovePhone_Click(object sender, EventArgs e)
        {
            var manager = Context.GetOwinContext().GetUserManager<ApplicationUserManager>();
            var signInManager = Context.GetOwinContext().Get<ApplicationSignInManager>();
            var result = manager.SetPhoneNumber(User.Identity.GetUserId(), null);
            if (!result.Succeeded)
            {
                return;
            }
            var user = manager.FindById(User.Identity.GetUserId());
            if (user != null)
            {
                signInManager.SignIn(user, isPersistent: false, rememberBrowser: false);
                Response.Redirect("/Account/Manage?m=RemovePhoneNumberSuccess");
            }
        }

        // DisableTwoFactorAuthentication
        protected void TwoFactorDisable_Click(object sender, EventArgs e)
        {
            var manager = Context.GetOwinContext().GetUserManager<ApplicationUserManager>();
            manager.SetTwoFactorEnabled(User.Identity.GetUserId(), false);

            Response.Redirect("/Account/Manage");
        }

        //EnableTwoFactorAuthentication 
        protected void TwoFactorEnable_Click(object sender, EventArgs e)
        {
            var manager = Context.GetOwinContext().GetUserManager<ApplicationUserManager>();
            manager.SetTwoFactorEnabled(User.Identity.GetUserId(), true);

            Response.Redirect("/Account/Manage");
        }

        protected string ProfileSuccessMessage
        {
            get;
            private set;
        }
        private void LoadProfileFields(ApplicationUserManager manager)
        {
            string email = manager.FindById(User.Identity.GetUserId())?.Email ?? User.Identity.Name;
            hdnOriginalEmail.Value = email;
            txtProfileUsername.Text = User.Identity.Name;
            txtProfileEmail.Text = email;

            const string sql = @"SELECT First_Name, Last_Name, Phone_Number, Client_Address
                          FROM Client WHERE Email_Address = @Email";

            using (var conn = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Email", email);
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        txtProfileFirstName.Text = reader["First_Name"].ToString();
                        txtProfileLastName.Text = reader["Last_Name"].ToString();
                        txtProfilePhone.Text = reader["Phone_Number"].ToString();
                        txtProfileAddress.Text = reader["Client_Address"].ToString();
                    }
                }
            }
        }

        protected void SaveProfile_Click(object sender, EventArgs e)
        {
            var manager = Context.GetOwinContext().GetUserManager<ApplicationUserManager>();
            string userId = User.Identity.GetUserId();
            string originalEmail = hdnOriginalEmail.Value;
            string newEmail = txtProfileEmail.Text.Trim();
            string newUsername = txtProfileUsername.Text.Trim();

            // Update AspNetUsers (username / email) via Identity
            var user = manager.FindById(userId);
            if (user != null)
            {
                bool changed = false;

                if (!string.Equals(user.UserName, newUsername, StringComparison.OrdinalIgnoreCase))
                {
                    user.UserName = newUsername;
                    changed = true;
                }

                if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
                {
                    user.Email = newEmail;
                    changed = true;
                }

                if (changed)
                {
                    manager.Update(user);
                }

                var signInManager = Context.GetOwinContext().Get<ApplicationSignInManager>();
                signInManager.SignIn(user, isPersistent: false, rememberBrowser: false);
            }

            // Update Client table, matched by the email as it was before this save
            const string sql = @"UPDATE Client
                          SET First_Name = @FirstName, Last_Name = @LastName,
                              Email_Address = @NewEmail, Phone_Number = @Phone, Client_Address = @Address
                          WHERE Email_Address = @OriginalEmail";

            using (var conn = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@FirstName", txtProfileFirstName.Text.Trim());
                cmd.Parameters.AddWithValue("@LastName", txtProfileLastName.Text.Trim());
                cmd.Parameters.AddWithValue("@NewEmail", newEmail);
                cmd.Parameters.AddWithValue("@Phone", txtProfilePhone.Text.Trim());
                cmd.Parameters.AddWithValue("@Address", txtProfileAddress.Text.Trim());
                cmd.Parameters.AddWithValue("@OriginalEmail", originalEmail);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            Response.Redirect("~/Account/Manage.aspx?pm=ProfileUpdateSuccess");
        }
    }
}