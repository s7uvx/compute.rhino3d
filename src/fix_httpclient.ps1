# PowerShell script to fix the obsolete WebRequest.Create issue
$filePath = "C:\gits\compute.rhino3d\src\compute.geometry\RhinoComputeAsync.cs"
$content = Get-Content $filePath -Raw

# First, replace the problematic method definition and the method content
$oldDoPostMethod = @'
        // run all synchronous requests through here
        private static System.Net.WebResponse DoPost(string function, string json)
        {
            if (!function.StartsWith("/")) // add leading /
                function = "/" + function; // if not present

            string uri = $"{WebAddress}{function}".ToLower();
            var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
            request.ContentType = "application/json";
            request.UserAgent = $"compute.rhino3d.cs/{Version}";
            request.Method = "POST";

            // try auth token (compute.rhino3d.com only)
            if (!string.IsNullOrWhiteSpace(AuthToken))
                request.Headers.Add("Authorization", "Bearer " + AuthToken);

            // try api key (self-hosted compute)
            if (!string.IsNullOrWhiteSpace(ApiKey))
                request.Headers.Add("RhinoComputeKey", ApiKey);
            
            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(json);
                streamWriter.Flush();
            }

            return request.GetResponse();
        }
'@

$newDoPostMethod = @'
        // run all synchronous requests through here
        private static System.Net.WebResponse DoPost(string function, string json)
        {
            if (!function.StartsWith("/")) // add leading /
                function = "/" + function; // if not present

            string uri = $"{WebAddress}{function}".ToLower();
            
            using (var client = new System.Net.Http.HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", $"compute.rhino3d.cs/{Version}");
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                // try auth token (compute.rhino3d.com only)
                if (!string.IsNullOrWhiteSpace(AuthToken))
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + AuthToken);

                // try api key (self-hosted compute)
                if (!string.IsNullOrWhiteSpace(ApiKey))
                    client.DefaultRequestHeaders.Add("RhinoComputeKey", ApiKey);

                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var httpResponse = client.PostAsync(uri, content).GetAwaiter().GetResult();
                
                // Return a compatible wrapper
                return new HttpResponseWrapper(httpResponse);
            }
        }
'@

# Replace the method
$content = $content.Replace($oldDoPostMethod, $newDoPostMethod)

# Add the wrapper class at the end, before the last closing brace
$wrapperClass = @'

    // Wrapper class to maintain compatibility with existing code that expects WebResponse
    internal class HttpResponseWrapper : System.Net.WebResponse
    {
        private readonly System.Net.Http.HttpResponseMessage _httpResponse;
        private readonly string _content;

        public HttpResponseWrapper(System.Net.Http.HttpResponseMessage httpResponse)
        {
            _httpResponse = httpResponse;
            _content = httpResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }

        public override Stream GetResponseStream()
        {
            return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_content));
        }

        public override void Close()
        {
            _httpResponse?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpResponse?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
'@

# Find the last closing brace and add the wrapper class before it
$lastBraceIndex = $content.LastIndexOf('}')
if ($lastBraceIndex -gt 0) {
    $content = $content.Substring(0, $lastBraceIndex) + $wrapperClass + "`n" + $content.Substring($lastBraceIndex)
}

# Write the updated content back to the file
Set-Content $filePath $content -Encoding UTF8
Write-Host "Fixed HttpClient usage in RhinoComputeAsync.cs"
