using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI.WebControls;
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
            ProfileValidationSummary.Enabled = true;
            ProfileValidationSummary.Visible = true;
            rfvProfileUsername.Enabled = true;
            rfvProfileFirstName.Enabled = true;
            rfvProfileLastName.Enabled = true;
            rfvProfileEmail.Enabled = true;
            revProfileEmail.Enabled = true;
            rfvProfilePhone.Enabled = true;
            revProfilePhone.Enabled = true;

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
                    RegisterProfileDialog("Your profile changes were saved.", "Profile updated");
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

            bool staffLogin = RoleBootstrapper.CanManage(User);
            StaffAccountService.StaffRecord staff = staffLogin ? new StaffAccountService().FindStaffProfile(email) : null;
            if (staffLogin)
            {
                if (staff == null) throw new InvalidOperationException("The staff profile linked to this login could not be found.");
                txtProfileFirstName.Text = staff.FirstName;
                txtProfileLastName.Text = staff.Surname;
                txtProfilePhone.Text = staff.Phone;
                txtProfileEmail.Text = staff.Email;
                txtProfileUsername.Text = staff.Email;
                txtProfileEmail.ReadOnly = true;
                txtProfileUsername.ReadOnly = true;
                lblStaffAccountHint.Visible = true;
                clientAddressSection.Visible = false;
                staffStatusSection.Visible = User.IsInRole(RoleBootstrapper.Manager) || User.IsInRole(RoleBootstrapper.Admin);
                ListItem statusItem = ddlProfileStatus.Items.FindByValue(staff.Status ?? String.Empty);
                if (statusItem == null && !String.IsNullOrWhiteSpace(staff.Status))
                {
                    ddlProfileStatus.Items.Insert(0, new ListItem(staff.Status, staff.Status));
                    statusItem = ddlProfileStatus.Items[0];
                }
                if (statusItem != null) ddlProfileStatus.SelectedValue = statusItem.Value;
                return;
            }

            clientAddressSection.Visible = true;
            staffStatusSection.Visible = false;
            lblStaffAccountHint.Visible = false;

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
            string newEmail = txtProfileEmail.Text.Trim();
            string newUsername = txtProfileUsername.Text.Trim();
            string authenticatedEmail = manager.FindById(userId)?.Email ?? User.Identity.Name;

            if (!Page.IsValid)
            {
                ShowProfileError("Check the highlighted fields and correct them before saving.", "Check profile details");
                return;
            }

            if (RoleBootstrapper.CanManage(User))
            {
                if (!User.IsInRole(RoleBootstrapper.Manager) && !User.IsInRole(RoleBootstrapper.Admin))
                {
                    try
                    {
                        new StaffAccountService().UpdateOwnStaffContact(authenticatedEmail,
                            txtProfileFirstName.Text, txtProfileLastName.Text, txtProfilePhone.Text);
                        Response.Redirect("~/Account/Manage.aspx?pm=ProfileUpdateSuccess", false);
                        Context.ApplicationInstance.CompleteRequest();
                        return;
                    }
                    catch (InvalidOperationException ex)
                    {
                        ShowProfileError(ex.Message, "Check profile details");
                        return;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError("Staff profile update failed: " + ex);
                        ShowProfileError("Your staff profile could not be saved. Please try again.", "Profile not saved");
                        return;
                    }
                }
                try
                {
                    new StaffAccountService().UpdateOwnManagerProfile(authenticatedEmail,
                        txtProfileFirstName.Text, txtProfileLastName.Text, txtProfilePhone.Text, ddlProfileStatus.SelectedValue);
                    if (String.Equals(ddlProfileStatus.SelectedValue, "Inactive", StringComparison.OrdinalIgnoreCase))
                    {
                        HttpContext.Current.GetOwinContext().Authentication.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                        Response.Redirect("~/Account/Login.aspx?staff=selfinactive", false);
                        Context.ApplicationInstance.CompleteRequest();
                        return;
                    }
                    Response.Redirect("~/Account/Manage.aspx?pm=ProfileUpdateSuccess", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
                catch (InvalidOperationException ex)
                {
                    ShowProfileError(ex.Message, "Check profile details");
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("Staff profile update failed: " + ex);
                    ShowProfileError("Your staff profile could not be saved. Please try again.", "Profile not saved");
                    return;
                }
            }

            var user = manager.FindById(userId);
            if (user == null)
            {
                ShowProfileError("Your sign-in account could not be found. Please sign in again.", "Profile not saved");
                return;
            }
            if (!String.Equals(newUsername, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                ShowProfileError("Use your email address as your username so you can continue signing in with email.", "Check login details");
                return;
            }
            var matchingUser = manager.FindByEmail(newEmail);
            if (matchingUser != null && matchingUser.Id != userId)
            {
                ShowProfileError("That email address is already linked to another account.", "Check email address");
                return;
            }

            string oldUsername = user.UserName, oldEmail = user.Email;
            bool identityChanged = !String.Equals(oldUsername, newUsername, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(oldEmail, newEmail, StringComparison.OrdinalIgnoreCase);
            if (identityChanged)
            {
                user.UserName = newUsername;
                user.Email = newEmail;
                IdentityResult identityResult = manager.Update(user);
                if (!identityResult.Succeeded)
                {
                    ShowProfileError(String.Join(" ", identityResult.Errors), "Profile not saved");
                    return;
                }
            }

            try
            {
                const string sql = @"UPDATE Client
                          SET First_Name = @FirstName, Last_Name = @LastName,
                              Email_Address = @NewEmail, Phone_Number = @Phone, Client_Address = @Address
                          WHERE Email_Address = @OriginalEmail AND Client_Status='Active'";
                using (var conn = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    using (var cmd = new SqlCommand(sql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@FirstName", txtProfileFirstName.Text.Trim());
                        cmd.Parameters.AddWithValue("@LastName", txtProfileLastName.Text.Trim());
                        cmd.Parameters.AddWithValue("@NewEmail", newEmail);
                        cmd.Parameters.AddWithValue("@Phone", txtProfilePhone.Text.Trim());
                        cmd.Parameters.AddWithValue("@Address", txtProfileAddress.Text.Trim());
                        cmd.Parameters.AddWithValue("@OriginalEmail", authenticatedEmail);
                        if (cmd.ExecuteNonQuery() != 1) throw new InvalidOperationException("Your active customer profile could not be uniquely updated. Refresh and try again.");
                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                if (identityChanged)
                {
                    user.UserName = oldUsername;
                    user.Email = oldEmail;
                    IdentityResult rollback = manager.Update(user);
                    if (!rollback.Succeeded) System.Diagnostics.Trace.TraceError("Customer profile identity rollback failed for user " + userId);
                }
                System.Diagnostics.Trace.TraceError("Customer profile update failed for user " + userId + ": " + ex);
                ShowProfileError(ex is InvalidOperationException ? ex.Message : "Your profile could not be saved. Please try again.", "Profile not saved");
                return;
            }

            var signInManager = Context.GetOwinContext().Get<ApplicationSignInManager>();
            signInManager.SignIn(user, isPersistent: false, rememberBrowser: false);
            Response.Redirect("~/Account/Manage.aspx?pm=ProfileUpdateSuccess");
        }

        private void ShowProfileError(string message, string title)
        {
            ProfileErrorMessage.Text = "<div class=\"form-msg error\">" + HttpUtility.HtmlEncode(message) + "</div>";
            RegisterProfileDialog(message, title);
        }

        private void RegisterProfileDialog(string message, string title)
        {
            string script = "(function(){var show=function(){if(window.RegalDialog)window.RegalDialog.alert('" +
                HttpUtility.JavaScriptStringEncode(message) + "',{title:'" + HttpUtility.JavaScriptStringEncode(title) + "'});};if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',show);else show();})();";
            ClientScript.RegisterStartupScript(GetType(), "profileUpdateDialog", script, true);
        }
    }
}
