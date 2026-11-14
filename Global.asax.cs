using System;
using System.Web;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
using CodeCrafters_Major_Project_Website.Models;

namespace CodeCrafters_Major_Project_Website
{
    public class Global : HttpApplication
    {
        private static System.Threading.Timer bookingMaintenanceTimer;
        private static int bookingMaintenanceRunning;

        void Application_Start(object sender, EventArgs e)
        {
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            bookingMaintenanceTimer = new System.Threading.Timer(RunBookingMaintenance, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        private static void RunBookingMaintenance(object state)
        {
            if (System.Threading.Interlocked.Exchange(ref bookingMaintenanceRunning, 1) != 0) return;
            try { new AvailabilityService().RunMaintenance(); }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("Booking maintenance failed: " + ex); }
            try { BookingSupportStore.ProcessQueueAsync().GetAwaiter().GetResult(); }
            catch (Exception ex) { System.Diagnostics.Trace.TraceError("Booking support queue failed: " + ex); }
            finally { System.Threading.Interlocked.Exchange(ref bookingMaintenanceRunning, 0); }
        }

        void Application_End(object sender, EventArgs e)
        {
            if (bookingMaintenanceTimer != null) bookingMaintenanceTimer.Dispose();
        }
    }
}
