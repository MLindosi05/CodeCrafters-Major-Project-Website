using System;
using System.Web;
using System.Web.SessionState;
using CodeCrafters_Major_Project_Website.Models;

namespace CodeCrafters_Major_Project_Website.Bookings
{
    public sealed class InvoiceDownload : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable { get { return false; } }
        public void ProcessRequest(HttpContext context)
        {
            if (context.User == null || context.User.Identity == null || !context.User.Identity.IsAuthenticated)
            { context.Response.StatusCode = 401; return; }
            int id;
            if (!Int32.TryParse(context.Request.QueryString["bookingId"], out id) || id < 1)
            { context.Response.StatusCode = 400; return; }
            try
            {
                string type = Convert.ToString(context.Request.QueryString["type"]);
                string path = String.Equals(type, "credit", StringComparison.OrdinalIgnoreCase)
                    ? new InvoiceService().GetOrCreate(context.User.Identity.Name, id, true)
                    : String.Equals(type, "amended", StringComparison.OrdinalIgnoreCase)
                        ? new InvoiceService().GetOrCreateAmended(context.User.Identity.Name, id)
                        : new InvoiceService().GetOrCreate(context.User.Identity.Name, id, false);
                context.Response.ContentType = "application/pdf";
                context.Response.AddHeader("Content-Disposition", "attachment; filename=\"" + System.IO.Path.GetFileName(path) + "\"");
                context.Response.TransmitFile(path);
            }
            catch (InvalidOperationException ex)
            { context.Response.StatusCode = 404; context.Response.ContentType = "text/plain"; context.Response.Write(ex.Message); }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Invoice generation failed: " + ex);
                context.Response.StatusCode = 500; context.Response.ContentType = "text/plain"; context.Response.Write("Invoice generation is temporarily unavailable.");
            }
        }
    }
}
