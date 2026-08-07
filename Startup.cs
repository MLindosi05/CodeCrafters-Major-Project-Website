using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(CodeCrafters_Major_Project_Website.Startup))]
namespace CodeCrafters_Major_Project_Website
{
    public partial class Startup {
        public void Configuration(IAppBuilder app) {
            ConfigureAuth(app);
        }
    }
}
