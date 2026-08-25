using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace CodeCrafters_Major_Project_Website.Models
{
    public static class RoleBootstrapper
    {
        public const string Guest = "Guest";
        public const string Staff = "Staff";
        public const string Manager = "Manager";
        public const string Admin = "Admin";

        public static void EnsureGuestRole(ApplicationUserManager userManager, string userId)
        {
            using (var context = new ApplicationDbContext())
            {
                var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(context));
                if (!roleManager.RoleExists(Guest)) roleManager.Create(new IdentityRole(Guest));
                if (!userManager.IsInRole(userId, Guest)) userManager.AddToRole(userId, Guest);
            }
        }

        public static bool CanManage(System.Security.Principal.IPrincipal user)
        {
            return user != null && (user.IsInRole(Staff) || user.IsInRole(Manager) || user.IsInRole(Admin));
        }
    }
}
