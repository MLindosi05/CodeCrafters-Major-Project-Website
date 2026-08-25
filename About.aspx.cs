using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;


namespace CodeCrafters_Major_Project_Website
{
    public partial class About : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack) BindBranches();
        }

        private void BindBranches()
        {
            using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString))
            using (var command = new SqlCommand("SELECT Branch_Name AS Name, Branch_Address AS Address, Branch_Phone AS Phone, Branch_Email AS Email FROM Branch WHERE Branch_Status='Active' ORDER BY Branch_Name", connection))
            {
                var branches = new DataTable();
                using (var adapter = new SqlDataAdapter(command)) adapter.Fill(branches);

                // Map branch name -> image filename
                var imageMap = new Dictionary<string, string>
        {
            { "Durban Ballito", "balito.jpg" },
            { "Durban North Beach", "Northbeach.jpg" },
            { "Durban Umhlanga", "Umhlanga.jpg" },
            { "JHB Midrand", "midrand.jpg" },
            { "Pietermaritzburg", "regall inn home page.jpg" }
        };

                branches.Columns.Add("Image", typeof(string));
                foreach (DataRow row in branches.Rows)
                {
                    string name = row["Name"].ToString();
                    row["Image"] = imageMap.ContainsKey(name) ? imageMap[name] : "default-branch.jpg";
                }

                BranchesRepeater.DataSource = branches;
                BranchesRepeater.DataBind();
                ddlMapBranch.DataSource = branches;
                ddlMapBranch.DataTextField = "Name";
                ddlMapBranch.DataValueField = "Address";
                ddlMapBranch.DataBind();
            }
        }
    }
}
