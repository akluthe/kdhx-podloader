using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace podloader.Services.KdhxHostedService.kdhxer
{
    public static class HttpClientHelper
    {
        private static readonly HttpClient httpClient;

        static HttpClientHelper()
        {
            var httpClientHandler = new HttpClientHandler()
            {
                UseProxy = false,
                MaxConnectionsPerServer = 10,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };

            httpClient = new HttpClient(httpClientHandler);
            httpClient.DefaultRequestHeaders.ConnectionClose = false;
        }

        public static HttpClient GetHttpClient()
        {
            return httpClient;
        }
    }
}
