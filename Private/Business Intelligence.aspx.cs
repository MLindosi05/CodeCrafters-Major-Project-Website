using System;
using System.Configuration;
using System.Web;
using System.Web.UI;
using CodeCrafters_Major_Project_Website.Models;

namespace CodeCrafters_Major_Project_Website.Private
{
    public partial class Business_Intelligence : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RoleBootstrapper.CanManage(User))
            {
                Response.Redirect("~/Account/Login.aspx?ReturnUrl=" + Server.UrlEncode(Request.RawUrl));
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            if (IsPostBack) return;
            Uri reportUri;
            string configuredUrl = ConfigurationManager.AppSettings["PowerBI:SecureEmbedUrl"];
            bool validReportUrl = Uri.TryCreate(configuredUrl, UriKind.Absolute, out reportUri) &&
                String.Equals(reportUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                String.Equals(reportUri.Host, "app.powerbi.com", StringComparison.OrdinalIgnoreCase) &&
                reportUri.AbsolutePath.StartsWith("/reportEmbed", StringComparison.OrdinalIgnoreCase);
            PowerBiLink.Visible = validReportUrl && (User.IsInRole(RoleBootstrapper.Manager) || User.IsInRole(RoleBootstrapper.Admin));
            if (PowerBiLink.Visible) PowerBiLink.NavigateUrl = reportUri.AbsoluteUri;
            PowerBiSetupNote.Visible = !PowerBiLink.Visible && (User.IsInRole(RoleBootstrapper.Manager) || User.IsInRole(RoleBootstrapper.Admin));
            if (!IsPostBack)
            {
                // Fetch the Power BI embed URL from Web.config appSettings
                string powerBiUrl = ConfigurationManager.AppSettings["PowerBI:SecureEmbedUrl"];

                if (!string.IsNullOrEmpty(powerBiUrl))
                {
                    PowerBiLink.NavigateUrl = powerBiUrl;
                    PowerBiLink.Visible = true;
                    PowerBiSetupNote.Visible = false;
                }
                else
                {
                    PowerBiLink.Visible = false;
                    PowerBiSetupNote.Visible = true;
                }

            }
        }
    }
}
