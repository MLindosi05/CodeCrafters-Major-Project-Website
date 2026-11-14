using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;

namespace CodeCrafters_Major_Project_Website.Models
{
    /// <summary>SMS OTP verification through Textbee using the user's own Android phone and SIM.</summary>
    public static class BookingOtpService
    {
        private static readonly HttpClient Client = new HttpClient();

        public static async Task SendAsync(string email, string orderId, HttpContext context)
        {
            string phone = GetClientPhone(email);
            if (String.IsNullOrWhiteSpace(phone)) throw new InvalidOperationException("Add a phone number to your profile before paying. Open My Profile and save a mobile number that can receive SMS.");
            phone = NormalizePhone(phone);
            string apiKey = ConfigurationManager.AppSettings["Sms:TextbeeApiKey"];
            if (String.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Real SMS OTP is not configured. Install Textbee on an Android phone with an active SIM, link the phone, create an API key, and add it as Sms:TextbeeApiKey in Web.config.");
            string code; using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) { var b = new byte[4]; rng.GetBytes(b); code = (BitConverter.ToUInt32(b, 0) % 1000000).ToString("D6"); }
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.textbee.dev/api/v1/gateway/send-sms");
            request.Headers.Add("x-api-key", apiKey);
            request.Content = new StringContent(new JObject {
                ["recipients"] = new JArray(phone),
                ["message"] = "Your Regal Inn payment verification code is " + code + ". It expires in 5 minutes."
            }.ToString(), Encoding.UTF8, "application/json");
            var response = await Client.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException((int)response.StatusCode == 429
                    ? "The free SMS limit has been reached. Try again after the Textbee quota resets."
                    : "Textbee could not send the SMS. Check that the Android gateway is online, the API key is correct, and the number is in international format.");
            context.Session["BookingOtp.Order"] = orderId;
            context.Session["BookingOtp.Code"] = code;
            context.Session["BookingOtp.Expires"] = DateTime.UtcNow.AddMinutes(5);
            context.Session["BookingOtp.Attempts"] = 0;
            context.Session.Remove("BookingOtp.Verified");
        }

        public static bool Verify(string orderId, string code, HttpContext context)
        {
            if (!String.Equals(Convert.ToString(context.Session["BookingOtp.Order"]), orderId, StringComparison.Ordinal) ||
                !(context.Session["BookingOtp.Expires"] is DateTime) || (DateTime)context.Session["BookingOtp.Expires"] < DateTime.UtcNow)
                throw new InvalidOperationException("Your code expired. Request a new code and try again.");
            int attempts = Convert.ToInt32(context.Session["BookingOtp.Attempts"] ?? 0) + 1;
            context.Session["BookingOtp.Attempts"] = attempts;
            if (attempts > 5) { context.Session.Remove("BookingOtp.Code"); throw new InvalidOperationException("Too many incorrect attempts. Restart payment to request a new code."); }
            if (!String.Equals(Convert.ToString(context.Session["BookingOtp.Code"]), (code ?? String.Empty).Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException("That code is incorrect. Check the SMS and try again.");
            context.Session["BookingOtp.Verified"] = orderId;
            return true;
        }

        public static bool IsVerified(string orderId, HttpContext context)
        { return String.Equals(Convert.ToString(context.Session["BookingOtp.Verified"]), orderId, StringComparison.Ordinal); }

        private static string GetClientPhone(string email)
        {
            using (var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString))
            using (var command = new SqlCommand("SELECT Phone_Number FROM Client WHERE Email_Address=@Email AND Client_Status='Active'", connection))
            { command.Parameters.AddWithValue("@Email", email ?? String.Empty); connection.Open(); return Convert.ToString(command.ExecuteScalar()); }
        }

        private static string NormalizePhone(string value)
        {
            string compact = value.Trim().Replace(" ", String.Empty).Replace("-", String.Empty).Replace("(", String.Empty).Replace(")", String.Empty);
            if (compact.StartsWith("0", StringComparison.Ordinal)) compact = "+27" + compact.Substring(1);
            else if (compact.StartsWith("27", StringComparison.Ordinal)) compact = "+" + compact;
            int digits = 0;
            foreach (char c in compact) { if (Char.IsDigit(c)) digits++; else if (c != '+' || compact.IndexOf(c) != 0) throw new InvalidOperationException("Save a valid South African mobile number, for example 082 123 4567, in My Profile."); }
            if (!compact.StartsWith("+", StringComparison.Ordinal) || digits < 10 || digits > 15)
                throw new InvalidOperationException("Save a valid mobile number in My Profile, for example 082 123 4567.");
            return compact;
        }
    }
}
