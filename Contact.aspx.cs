using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Net.Mail;
using System.Configuration;

namespace CodeCrafters_Major_Project_Website
{
    public partial class Contact : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
        }

        protected void btnSend_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;

            try
            {
                var mail = new MailMessage();
                mail.To.Add(ConfigurationManager.AppSettings["ReservationsInboxEmail"] ?? "reservations@theregalinnhotel.co.za");
                mail.From = new MailAddress(ConfigurationManager.AppSettings["SmtpFromAddress"] ?? "noreply@theregalinnhotel.co.za", "Regal Inn Website");
                mail.ReplyToList.Add(new MailAddress(txtEmail.Text, txtName.Text));
                mail.Subject = "[" + ddlSubject.SelectedItem.Text + "] Website enquiry from " + txtName.Text;
                mail.Body = "From: " + txtName.Text + " (" + txtEmail.Text + ")\n\n" + txtMessage.Text;

                using (var smtp = new SmtpClient())
                {
                    // SMTP host/credentials are read from <system.net><mailSettings> in web.config
                    smtp.Send(mail);
                }

                litFormMsg.Text = "<div class='form-msg success'>Thanks, " + Server.HtmlEncode(txtName.Text) + "! We'll be in touch shortly.</div>";
                txtName.Text = string.Empty;
                txtEmail.Text = string.Empty;
                txtMessage.Text = string.Empty;
            }
            catch (Exception)
            {
                // Log the real exception via your logging framework of choice.
                litFormMsg.Text = "<div class='form-msg error'>Something went wrong sending your message — please try again or call us directly.</div>";
            }
        }
    }
}
