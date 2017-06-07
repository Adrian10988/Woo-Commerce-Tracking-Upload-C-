using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace WooCommerceTrackingTester
{
    public class WCTrackingUploader
    {
        public struct Response
        {
            public HttpStatusCode Code { get; set; }
            public string Message { get; set; }
            public string WooCommerceCode { get; set; }
            public bool Succeeded { get; set; }
            public Exception CaughtException { get; set; }
        }
        private string _secret;
        private string _storeUrl;
        private string _username;

        private const string _createShipmentTrackingEndpoint = "/wp-json/wc/v1/orders/{0}/shipment-trackings";
        private const string _getShipmentTrackingEndpoint = "/wp-json/wc/v1/orders/{0}/shipment-trackings/{1}";
        private const string _deleteShipmentTrackingEndpoint = "/wp-json/wc/v1/orders/{0}/shipment-trackings/{1}";

        private HttpClient _httpClient;

        public WCTrackingUploader(string secret, string username, string storeUrl)
        {
            ChangeClient(secret, username, storeUrl);
            _httpClient = new HttpClient();
        }

        public void ChangeClient(string secret, string username, string storeUrl)
        {
            //normalize urls. They should not end with forward slash
            if (storeUrl.EndsWith("/"))
                storeUrl = storeUrl.Remove(storeUrl.Length - 1);

            _secret = secret;
            _storeUrl = storeUrl;
            _username = username;

            ResetHttpClient();
        }

        private void ResetHttpClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public Response SubmitTrackingInfo(string trackingId, string trackingUrl, string carrier, DateTime lastUpdated, int orderId, string storeReferenceId)
        {
            var container = new
            {
                tracking_number = trackingId,
                custom_tracking_provider = carrier,
                custom_tracking_link = trackingUrl,
            };


            try
            {
                var securityFragments = "";

                var formattedBaseUrl = string.Format(_storeUrl + _createShipmentTrackingEndpoint, storeReferenceId);
                if (formattedBaseUrl.Contains("https://"))
                {
                    
                    securityFragments = GenerateSecurityFragmentsSSL(HttpMethod.Post, formattedBaseUrl);
                }
                    
                
                else
                    securityFragments = GenerateSecurityUriFragments(formattedBaseUrl, HttpMethod.Post);

                var finalUrl = formattedBaseUrl + securityFragments;

                var result = _httpClient.PostAsJsonAsync(finalUrl, container).Result;
                var json = JsonConvert.DeserializeObject<dynamic>(result.Content.ReadAsStringAsync().Result);

                var response = BuildResponse(json);

                return response;
            }
            catch(Exception e)
            {
                return BuildResponse(e);
            }
        }

        public Response DeleteTrackingInfo(string trackingId, int orderId, string storeReferenceId)
        {
            try
            {
                var securityFragments = "";

                var formattedBaseUrl = string.Format(_storeUrl + _createShipmentTrackingEndpoint, storeReferenceId, trackingId);
                if (formattedBaseUrl.Contains("https://"))
                    securityFragments = GenerateSecurityFragmentsSSL(HttpMethod.Post, formattedBaseUrl);
                else
                    securityFragments = GenerateSecurityUriFragments(formattedBaseUrl, HttpMethod.Post);
                var finalUrl = formattedBaseUrl + securityFragments;
                var response = _httpClient.DeleteAsync(string.Format(finalUrl)).Result;
                return BuildResponse(response);
            }
            catch(Exception e)
            {
                return BuildResponse(e);
            }
        }



        #region helpers
        private Response BuildResponse(Exception e)
        {
            var r = new Response();
            r.CaughtException = e;
            r.Succeeded = false;

            return r;
        }
        private Response BuildResponse(dynamic response)
        {
            var r = new Response();

            HttpStatusCode code = HttpStatusCode.BadRequest;

            if(response.data != null && response.data.status != null)
            {
                var c = (string)response.data.status;

                switch (c)
                {
                    case "400":
                        code = HttpStatusCode.BadRequest;
                        r.Succeeded = false;
                        break;
                    case "401":
                        code = HttpStatusCode.Unauthorized;
                        r.Succeeded = false;
                        break;
                    case "500":
                        code = HttpStatusCode.InternalServerError;
                        r.Succeeded = false;
                        break;
                    case "200":
                        code = HttpStatusCode.OK;
                        r.Succeeded = true;
                        break;
                    case "404":
                        code = HttpStatusCode.NotFound;
                        r.Succeeded = false;
                        break;
                    case "403":
                        code = HttpStatusCode.Forbidden;
                        r.Succeeded = false;
                        break;
                    
                }
            }

            r.Code = code;

            if (response.message != null)
                r.Message = response.message;

            if (response.code != null)
                r.WooCommerceCode = response.code;

            return r;
        }
        private string GenerateSecurityFragmentsSSL(HttpMethod httpMethod, string apiEndpoint)
        {
            var parameters = new Dictionary<string, string>();
            parameters = parameters ?? new Dictionary<string, string>();
            parameters["consumer_key"] = _username;
            parameters["consumer_secret"] = _secret;
            StringBuilder stringBuilder = new StringBuilder();
            foreach (KeyValuePair<string, string> parameter in parameters)
                stringBuilder.AppendFormat("&{0}={1}", SafeUpperCaseUrlEncode(parameter.Key), SafeUpperCaseUrlEncode(parameter.Value));
            string str = stringBuilder.ToString().Substring(1);
            return "?" + str;
        }


        private string GenerateSecurityUriFragments(string completeUri, HttpMethod httpMethod)
        {
            var parameters = new Dictionary<string, string>();
            parameters["oauth_consumer_key"] = _username;
            parameters["oauth_timestamp"] = Math.Round(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds).ToString((IFormatProvider)CultureInfo.InvariantCulture);
            parameters["oauth_nonce"] = GenerateNonce();
            parameters["oauth_signature_method"] = "HMAC-SHA1";
            parameters["oauth_version"] = "1.0";
            parameters["oauth_signature"] = UpperCaseUrlEncode(GenerateSignature(httpMethod, parameters, completeUri));

            StringBuilder stringBuilder = new StringBuilder();
            foreach (KeyValuePair<string, string> parameter in parameters)
                stringBuilder.AppendFormat("&{0}={1}", SafeUpperCaseUrlEncode(parameter.Key), SafeUpperCaseUrlEncode(parameter.Value));
            string str = stringBuilder.ToString().Substring(1);

            return "?" + str;
        }

        private string GenerateNonce()
        {
            Random random = new Random();
            StringBuilder stringBuilder = new StringBuilder();
            for (int index = 0; index < 32; ++index)
                stringBuilder.Append("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[random.Next(0, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".Length - 1)]);
            return stringBuilder.ToString();
        }

        private Dictionary<string, string> NormalizeParameters(Dictionary<string, string> parameters)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> parameter in parameters)
            {
                string key = SafeUpperCaseUrlEncode(parameter.Key).Replace("%", "%25");
                string str = SafeUpperCaseUrlEncode(parameter.Value).Replace("%", "%25");
                dictionary.Add(key, str);
            }
            return dictionary;
        }

        private byte[] Sha1(byte[] key, byte[] message)
        {
            return new HMACSHA1(key).ComputeHash(message);
        }

        private string GenerateSignature(HttpMethod httpMethod,  Dictionary<string, string> parameters, string completeUri)
        {
            string str1 = SafeUpperCaseUrlEncode(completeUri);
            string str2 = string.Join("%26", NormalizeParameters(parameters).OrderBy((x => x.Key)).ToList().ConvertAll((x => x.Key + "%3D" + x.Value)));
            return Convert.ToBase64String(Sha1(Encoding.UTF8.GetBytes(_secret + "&"), Encoding.UTF8.GetBytes(string.Format("{0}&{1}&{2}", httpMethod.ToString().ToUpper(), str1, str2))));
        }

        private string UpperCaseUrlEncode(string stringToEncode)
        {
            string input = HttpUtility.UrlEncode(stringToEncode);
            if (string.IsNullOrEmpty(input))
                return string.Empty;
            return Regex.Replace(input, "(%[0-9a-f][0-9a-f])", c => c.Value.ToUpper());
        }

        private string SafeUpperCaseUrlEncode(string stringToEncode)
        {
            return UpperCaseUrlEncode(HttpUtility.UrlDecode(stringToEncode));
        }
        #endregion


    }





   
}
