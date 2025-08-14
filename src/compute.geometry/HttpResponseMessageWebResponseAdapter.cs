using System;
using System.IO;
using System.Net;
using System.Net.Http;

namespace compute.geometry
{
    // Adapter to allow HttpResponseMessage to be used as WebResponse
    public class HttpResponseMessageWebResponseAdapter : WebResponse
    {
        private readonly HttpResponseMessage _response;
        private readonly Stream _stream;

        public HttpResponseMessageWebResponseAdapter(HttpResponseMessage response, Stream stream)
        {
            _response = response;
            _stream = stream;
        }

        public override Stream GetResponseStream()
        {
            return _stream;
        }

        public override void Close()
        {
            _stream?.Close();
            _response?.Dispose();
            base.Close();
        }
    }
}
