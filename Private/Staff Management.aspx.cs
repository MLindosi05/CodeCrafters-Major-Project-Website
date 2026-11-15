using System;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using CodeCrafters_Major_Project_Website.Models;

namespace CodeCrafters_Major_Project_Website.Private
{
    public partial class WebForm1 : Page
    {
        private string currentEmail;
        private string CurrentEmail
        {
            get
            {
                if (currentEmail != null) return currentEmail;
                var manager = Context.GetOwinContext().GetUserManager<ApplicationUserManager>();
                var user = manager.FindById(User.Identity.GetUserId());
                currentEmail = user == null ? User.Identity.Name : user.Email;
                return currentEmail;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!User.IsInRole(RoleBootstrapper.Manager) && !User.IsInRole(RoleBootstrapper.Admin))
            {
                Response.Redirect("~/Default.aspx");
                return;
            }
            if (!IsPostBack) BindStaff();
        }

        private void BindStaff()
        {
            try
            {
                StaffGrid.DataSource = new StaffAccountService().GetStaffForManager(CurrentEmail);
                StaffGrid.DataBind();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Staff list could not be loaded: " + ex);
                StaffFeedback.Text = "<div class=\"form-msg error\">Staff records could not be loaded. Verify your manager access and try again.</div>";
            }
        }

        protected void StaffGrid_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow) return;
            var staff = e.Row.DataItem as StaffAccountService.StaffRecord;
            if (staff == null) return;

            var status = e.Row.FindControl("StatusInput") as DropDownList;
            if (status != null)
            {
                ListItem item = status.Items.FindByValue(staff.Status ?? String.Empty);
                if (item == null && !String.IsNullOrWhiteSpace(staff.Status))
                {
                    status.Items.Insert(0, new ListItem(staff.Status, staff.Status));
                    item = status.Items[0];
                }
                if (item != null) status.SelectedValue = item.Value;
            }

            bool protectedManager = IsProtectedManager(staff.Role) &&
                !String.Equals(staff.Email, CurrentEmail, StringComparison.OrdinalIgnoreCase);
            e.Row.Attributes["data-is-self"] = String.Equals(staff.Email, CurrentEmail, StringComparison.OrdinalIgnoreCase) ? "true" : "false";
            foreach (string controlId in new[] { "FirstNameInput", "SurnameInput", "PhoneInput" })
            {
                var text = e.Row.FindControl(controlId) as TextBox;
                if (text != null) text.Enabled = !protectedManager;
            }
            if (status != null) status.Enabled = !protectedManager;
            var save = e.Row.FindControl("SaveStaffButton") as Button;
            if (save != null) save.Visible = !protectedManager;
            var protectedLabel = e.Row.FindControl("ProtectedLabel") as Label;
            if (protectedLabel != null) protectedLabel.Visible = protectedManager;
        }

        protected void StaffGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!String.Equals(e.CommandName, "SaveStaff", StringComparison.Ordinal)) return;
            int rowIndex;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument), out rowIndex) || rowIndex < 0 || rowIndex >= StaffGrid.Rows.Count)
            {
                SetFeedback(false, "Select a staff record and try again.");
                return;
            }

            GridViewRow row = StaffGrid.Rows[rowIndex];
            string targetEmail = Convert.ToString(StaffGrid.DataKeys[rowIndex].Value);
            var first = row.FindControl("FirstNameInput") as TextBox;
            var surname = row.FindControl("SurnameInput") as TextBox;
            var phone = row.FindControl("PhoneInput") as TextBox;
            var status = row.FindControl("StatusInput") as DropDownList;
            if (first == null || surname == null || phone == null || status == null)
            {
                SetFeedback(false, "The staff form could not be read. Refresh and try again.");
                return;
            }

            var original = StaffGrid.DataKeys[rowIndex].Values;
            bool unchanged = SameText(first.Text, Convert.ToString(original["FirstName"])) &&
                SameText(surname.Text, Convert.ToString(original["Surname"])) &&
                SameText(phone.Text, Convert.ToString(original["Phone"])) &&
                SameText(status.SelectedValue, Convert.ToString(original["Status"]));
            if (unchanged)
            {
                SetFeedback(true, "No changes to save.");
                return;
            }

            try
            {
                new StaffAccountService().UpdateStaffForManager(CurrentEmail, targetEmail,
                    first.Text, surname.Text, phone.Text, status.SelectedValue);
                if (String.Equals(targetEmail, CurrentEmail, StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(status.SelectedValue, "Inactive", StringComparison.OrdinalIgnoreCase))
                {
                    Context.GetOwinContext().Authentication.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                    Response.Redirect("~/Account/Login.aspx?staff=selfinactive", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
                SetFeedback(true, "Staff details and status were saved.");
                BindStaff();
            }
            catch (InvalidOperationException ex) { SetFeedback(false, ex.Message); }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Staff update failed: " + ex);
                SetFeedback(false, "The staff update could not be saved. Refresh and try again.");
            }
        }

        private void SetFeedback(bool success, string message)
        {
            StaffFeedback.Text = "<div class=\"form-msg " + (success ? "success" : "error") + "\">" + HttpUtility.HtmlEncode(message) + "</div>";
            string title = success ? (message == "No changes to save." ? "No changes" : "Staff update saved") : "Check staff update";
            string script = "(function(){var show=function(){if(window.RegalDialog)window.RegalDialog.alert('" +
                HttpUtility.JavaScriptStringEncode(message) + "',{title:'" + title + "'});};if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',show);else show();})();";
            ClientScript.RegisterStartupScript(GetType(), "staffUpdateFeedback", script, true);
        }

        private static bool SameText(string left, string right)
        { return String.Equals((left ?? String.Empty).Trim(), (right ?? String.Empty).Trim(), StringComparison.OrdinalIgnoreCase); }

        private static bool IsProtectedManager(string role)
        {
            return String.Equals(role, RoleBootstrapper.Manager, StringComparison.OrdinalIgnoreCase) ||
                String.Equals(role, RoleBootstrapper.Admin, StringComparison.OrdinalIgnoreCase);
        }
    }
}
