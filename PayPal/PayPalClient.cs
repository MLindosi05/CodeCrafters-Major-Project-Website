using System;
using System.Configuration;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CodeCrafters_Major_Project_Website.PayPal
{
    public static class PayPalClient
    {
        private static readonly HttpClient Http = new HttpClient();
        private static string BaseUrl { get { return ConfigurationManager.AppSettings["PayPal:BaseUrl"]; } }

        public static decimal ZarToUsd(decimal zar)
        {
            decimal rate = decimal.Parse(ConfigurationManager.AppSettings["PayPal:ZarPerUsd"], CultureInfo.InvariantCulture);
            return Math.Round(zar / rate, 2);
        }

        private static async Task<string> GetAccessTokenAsync()
        {
            string id = ConfigurationManager.AppSettings["PayPal:ClientId"];
            string secret = ConfigurationManager.AppSettings["PayPal:Secret"];

            var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/v1/oauth2/token");
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(id + ":" + secret)));
            req.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

            var res = await Http.SendAsync(req);
            string body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) throw new InvalidOperationException("PayPal auth failed: " + body);
            return (string)JObject.Parse(body)["access_token"];
        }

        public static async Task<JObject> CreateOrderAsync(decimal usdTotal, string description)
        {
            string token = await GetAccessTokenAsync();
            var payload = new JObject
            {
                ["intent"] = "CAPTURE",
                ["purchase_units"] = new JArray(new JObject
                {
                    ["description"] = description,
                    ["amount"] = new JObject
                    {
                        ["currency_code"] = "USD",
                        ["value"] = usdTotal.ToString("0.00", CultureInfo.InvariantCulture)
                    }
                })
            };
            return await PostAsync("/v2/checkout/orders", token, payload.ToString());
        }

        public static async Task<JObject> CaptureOrderAsync(string orderId)
        {
            string token = await GetAccessTokenAsync();
            return await PostAsync("/v2/checkout/orders/" + Uri.EscapeDataString(orderId) + "/capture", token, "{}");
        }

        private static async Task<JObject> PostAsync(string path, string token, string json)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var res = await Http.SendAsync(req);
            string body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) throw new InvalidOperationException("PayPal error: " + body);
            return JObject.Parse(body);
        }
    }
}