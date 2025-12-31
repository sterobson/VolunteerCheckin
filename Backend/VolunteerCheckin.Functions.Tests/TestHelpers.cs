using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace VolunteerCheckin.Functions.Tests
{
    public static class TestHelpers
    {
        /// <summary>
        /// Creates an HttpRequest with a JSON body from an object
        /// </summary>
        public static HttpRequest CreateHttpRequest(object body)
        {
            DefaultHttpContext context = new();
            HttpRequest request = context.Request;

            string json = JsonSerializer.Serialize(body);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            request.Body = new MemoryStream(bytes);

            return request;
        }

        /// <summary>
        /// Creates an HttpRequest with a string body (for testing invalid JSON)
        /// </summary>
        public static HttpRequest CreateHttpRequest(string body)
        {
            DefaultHttpContext context = new();
            HttpRequest request = context.Request;

            byte[] bytes = Encoding.UTF8.GetBytes(body);
            request.Body = new MemoryStream(bytes);

            return request;
        }

        /// <summary>
        /// Creates an HttpRequest with a JSON body and custom headers
        /// </summary>
        public static HttpRequest CreateHttpRequestWithHeaders(object body, Dictionary<string, string> headers)
        {
            DefaultHttpContext context = new();
            HttpRequest request = context.Request;

            string json = JsonSerializer.Serialize(body);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            request.Body = new MemoryStream(bytes);

            foreach (KeyValuePair<string, string> header in headers)
            {
                request.Headers[header.Key] = header.Value;
            }

            return request;
        }

        /// <summary>
        /// Creates an HttpRequest with admin email header
        /// </summary>
        public static HttpRequest CreateHttpRequestWithAdminHeader(object body, string adminEmail)
        {
            return CreateHttpRequestWithHeaders(body, new Dictionary<string, string>
            {
                { "X-Admin-Email", adminEmail }
            });
        }

        /// <summary>
        /// Creates an empty HttpRequest (for GET/DELETE operations)
        /// </summary>
        public static HttpRequest CreateEmptyHttpRequest()
        {
            DefaultHttpContext context = new();
            return context.Request;
        }

        /// <summary>
        /// Creates an HttpRequest with admin email header (for GET/DELETE operations)
        /// </summary>
        public static HttpRequest CreateEmptyHttpRequestWithAdminHeader(string adminEmail)
        {
            DefaultHttpContext context = new();
            HttpRequest request = context.Request;
            request.Headers["X-Admin-Email"] = adminEmail;
            return request;
        }

        /// <summary>
        /// Creates an HttpRequest with a session token in the Authorization header
        /// </summary>
        public static HttpRequest CreateHttpRequestWithAuth(object body, string sessionToken)
        {
            DefaultHttpContext context = new();
            HttpRequest request = context.Request;

            string json = JsonSerializer.Serialize(body);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            request.Body = new MemoryStream(bytes);
            request.Headers["Authorization"] = $"Bearer {sessionToken}";

            return request;
        }

        /// <summary>
        /// Creates an empty HttpRequest with a session token (for GET/DELETE operations)
        /// </summary>
        public static HttpRequest CreateEmptyHttpRequestWithAuth(string sessionToken)
        {
            DefaultHttpContext context = new();
            HttpRequest request = context.Request;
            request.Headers["Authorization"] = $"Bearer {sessionToken}";
            return request;
        }
    }
}
