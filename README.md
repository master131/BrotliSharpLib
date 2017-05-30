# BrotliSharpLib

BrotliSharpLib is a full C# port of the brotli compression format and implementation [by Google](https://github.com/google/brotli). It is intended to be a mostly 1:1 conversion of the original C code. All code is correct as of v0.6.0 of the reference implementation.

The code targets .NET Framework 2.0 and thus uses a minimal set of APIs to ensure compatibility with a wide range of frameworks including .NET Standard and .NET Core.

Currently the code only supports decompression and will implement compression in the future. The underlying APIs do support streams however a proper Stream class won't be created until compression has been completed.

BrotliSharpLib is licensed under [MIT](https://github.com/master131/BrotliSharpLib/blob/master/LICENSE).

## Usage
**Generic/basic usage:**
```c#
byte[] brotliCompressedData = ...; // arbitary data source
byte[] uncompressedData = Brotli.DecompressBuffer(brotliCompressedData, 0, brotliCompressedData.Length);
```

**Real-life example:**
The following allows for acceptance and decompression of brotli encoded web content via a [HttpClient](https://msdn.microsoft.com/en-us/library/system.net.http.httpclient(v=vs.118).aspx) and falls back to gzip or deflate when required.
```c#
static class HttpClientEx
{
    private class BrotliCompressionHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
            var response = await base.SendAsync(request, cancellationToken);
            IEnumerable<string> ce;
            if (response.Content.Headers.TryGetValues("Content-Encoding", out ce) && ce.First() == "br")
            {
                var buffer = await response.Content.ReadAsByteArrayAsync();
                response.Content = new ByteArrayContent(Brotli.DecompressBuffer(buffer, 0, buffer.Length));
            }
            return response;
        }
    }

    public static HttpClient Create()
    {
        var handler = new HttpClientHandler();
        if (handler.SupportsAutomaticDecompression)
            handler.AutomaticDecompression = System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.GZip;
        return HttpClientFactory.Create(handler, new BrotliCompressionHandler());
    }
}
```

