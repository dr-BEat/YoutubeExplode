﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode.Internal;

namespace YoutubeExplode.Services
{
    /// <summary>
    /// Uses <see cref="WebRequest"/> for handling requests
    /// </summary>
    public partial class DefaultRequestService : IRequestService
    {
        /// <summary>
        /// Whether to throw an exception when a request fails
        /// </summary>
        public bool ShouldThrowOnErrors { get; set; }

        /// <summary>
        /// Cookie container for sharing cookies among requests
        /// </summary>
        public CookieContainer CookieContainer { get; set; }

        /// <summary>
        /// Creates <see cref="WebRequest"/> for use by the other methods
        /// </summary>
        protected virtual WebRequest CreateWebRequest(string url, string method)
        {
            var request = WebRequest.CreateHttp(url);
            request.Method = method;

            if (CookieContainer != null)
                request.CookieContainer = CookieContainer;

            return request;
        }

        /// <inheritdoc />
        public virtual string GetString(string url)
        {
            if (url.IsBlank())
                throw new ArgumentNullException(nameof(url));

            try
            {
                var request = CreateWebRequest(url, "GET");

                using (var response = request.GetResponse())
                {
                    var data = GetArray(response.GetResponseStream());
                    return Encoding.UTF8.GetString(data);
                }
            }
            catch
            {
                if (ShouldThrowOnErrors) throw;
                return null;
            }
        }

        /// <inheritdoc />
        public virtual async Task<string> GetStringAsync(string url)
        {
            if (url.IsBlank())
                throw new ArgumentNullException(nameof(url));

            try
            {
                var request = CreateWebRequest(url, "GET");

                using (var response = await request.GetResponseAsync())
                {
                    var data = await GetArrayAsync(response.GetResponseStream());
                    return Encoding.UTF8.GetString(data);
                }
            }
            catch
            {
                if (ShouldThrowOnErrors) throw;
                return null;
            }
        }

        /// <inheritdoc />
        public virtual IDictionary<string, string> GetHeaders(string url)
        {
            if (url.IsBlank())
                throw new ArgumentNullException(nameof(url));

            try
            {
                var request = CreateWebRequest(url, "HEAD");

                using (var response = request.GetResponse())
                {
                    return WebHeadersToDictionary(response.Headers);
                }
            }
            catch
            {
                if (ShouldThrowOnErrors) throw;
                return null;
            }
        }

        /// <inheritdoc />
        public virtual async Task<IDictionary<string, string>> GetHeadersAsync(string url)
        {
            if (url.IsBlank())
                throw new ArgumentNullException(nameof(url));

            try
            {
                var request = CreateWebRequest(url, "HEAD");

                using (var response = await request.GetResponseAsync())
                {
                    return WebHeadersToDictionary(response.Headers);
                }
            }
            catch
            {
                if (ShouldThrowOnErrors) throw;
                return null;
            }
        }

        /// <inheritdoc />
        public virtual Stream DownloadFile(string url)
        {
            if (url.IsBlank())
                throw new ArgumentNullException(nameof(url));

            try
            {
                var request = CreateWebRequest(url, "GET");

                return request.GetResponse().GetResponseStream();
            }
            catch
            {
                if (ShouldThrowOnErrors) throw;
                return null;
            }
        }

        /// <inheritdoc />
        public virtual async Task<Stream> DownloadFileAsync(string url)
        {
            if (url.IsBlank())
                throw new ArgumentNullException(nameof(url));

            try
            {
                var request = CreateWebRequest(url, "GET");

                return (await request.GetResponseAsync()).GetResponseStream();
            }
            catch
            {
                if (ShouldThrowOnErrors) throw;
                return null;
            }
        }
    }

    public partial class DefaultRequestService
    {
        /// <summary>
        /// Default instance
        /// </summary>
        public static DefaultRequestService Instance { get; } = new DefaultRequestService();

        /// <summary>
        /// Reads stream into an array
        /// </summary>
        protected static byte[] GetArray(Stream input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            using (input)
            using (var ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Reads stream into an array
        /// </summary>
        protected static async Task<byte[]> GetArrayAsync(Stream input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            using (input)
            using (var ms = new MemoryStream())
            {
                await input.CopyToAsync(ms);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Converts <see cref="WebHeaderCollection"/> to <see cref="IDictionary{TKey,TValue}" />
        /// </summary>
        protected static IDictionary<string, string> WebHeadersToDictionary(WebHeaderCollection headers)
        {
            if (headers == null)
                throw new ArgumentNullException(nameof(headers));

            var result = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
            {
                string headerName = headers.GetKey(i);
                string headerValue = headers.Get(i);
                result.Add(headerName, headerValue);
            }
            return result;
        }
    }
}
