using System;
using System.Net.Http;

namespace compute.geometry
{
    public static class HttpClientFactory
    {
        private static readonly Lazy<HttpClient> _httpClient = new Lazy<HttpClient>(() =>
        {
            var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            client.DefaultRequestHeaders.Add("User-Agent", "compute.rhino3d/1.0.0");
            return client;
        });

        public static HttpClient Instance => _httpClient.Value;
    }
}
