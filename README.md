# BrotliSharpLib

BrotliSharpLib is a full C# port of the brotli library/compression code [by Google](https://github.com/google/brotli). It is intended to be a mostly 1:1 conversion of the original C code. All code is correct as of v0.6.0 of the reference implementation.

The projects uses a minimal set of APIs to ensure compatibility with a wide range of frameworks including .NET Standard and .NET Core. It also supports little-endian and big-endian architectures and is optimised for x86, x64 and ARM processors.

BrotliSharpLib is licensed under [MIT](https://github.com/master131/BrotliSharpLib/blob/master/LICENSE).

## Usage
**Generic/basic usage:**
```c#
/** Decompression **/
byte[] brotliCompressedData = ...; // arbritary data source
byte[] uncompressedData = Brotli.DecompressBuffer(brotliCompressedData, 0, brotliCompressedData.Length /**, customDictionary **/);

/** Compression **/
byte[] uncompressedData = ...; // arbritary data source

// By default, brotli uses a quality value of 11 and window size of 22 if the parameters are omitted.
byte[] compressedData = Brotli.CompressBuffer(uncompressedData, 0, uncompressedData.Length /**, quality, windowSize, customDictionary **/);
```

**Stream usage:**
```c#
/** Decompression **/
using (var ms = new MemoryStream())
using (var bs = new BrotliStream(compressedStream, CompressionMode.Decompress))
{
    bs.CopyTo(ms);
}

/** Compression **/
using (var fs = File.OpenRead(filePath))
using (var ms = new MemoryStream())
{
    using (var bs = new BrotliStream(ms, CompressionMode.Compress))
    {
        // By default, brotli uses a quality value of 11 and window size of 22 if the methods are not called.
        /** bs.SetQuality(quality); **/
        /** bs.SetWindow(windowSize); **/
        /** bs.SetCustomDictionary(customDict); **/
        fs.CopyTo(bs);
        
        /** Do not call ms.ToArray() here, refer to note below **/
    }
    
    /* IMPORTANT: Only use the destination stream after closing/disposing the BrotliStream
       as the BrotliStream must be closed in order for the final block to be flushed out
       as required by the original implementation
     */
    byte[] compressed = ms.ToArray();
}
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

## Performance
### Considerations for Build
For optimal performance, ensure to build BrotliSharpLib in **Release** mode to enable all possible JIT optimisations.

Performance can also be further improved by building BrotliSharpLib using .NET Framework 4.5 or above (or any framework that supports AggressiveInlining). Selecting a specific target platform (instead of AnyCPU) where possible can also further improve performance. All of this however, is completely optional as BrotliSharpLib is designed to run in a wide range of contexts and configurations regardless.

### Benchmark
On average, BrotliSharpLib runs about 11-12% faster than the standard [C# decoding implementation provided by Google](https://github.com/google/brotli/tree/master/csharp/org/brotli/dec) which is a straight auto-conversion of the Java port using [sharpen](https://github.com/mono/sharpen).

The following are benchmark results using [DotNetBenchmark](https://github.com/dotnet/BenchmarkDotNet) with BrotliSharpLib and Google's C# implementation built against .NET Framework 4.6.1.

``` ini

BenchmarkDotNet=v0.10.6, OS=Windows 10 Redstone 2 (10.0.15063)
Processor=Intel Core i5-6600K CPU 3.50GHz (Skylake), ProcessorCount=4
Frequency=3421875 Hz, Resolution=292.2374 ns, Timer=TSC
  [Host]       : Clr 4.0.30319.42000, 64bit RyuJIT-v4.7.2046.0
  LegacyJitX64 : Clr 4.0.30319.42000, 64bit LegacyJIT/clrjit-v4.7.2046.0;compatjit-v4.7.2046.0
  LegacyJitX86 : Clr 4.0.30319.42000, 32bit LegacyJIT-v4.7.2046.0
  RyuJitX64    : Clr 4.0.30319.42000, 64bit RyuJIT-v4.7.2046.0

Runtime=Clr  
```
 |         Method |          Job |       Jit | Platform |     Mean |     Error |    StdDev |
 |--------------- |------------- |---------- |--------- |---------:|----------:|----------:|
 |     GoogleImpl | LegacyJitX64 | LegacyJit |      X64 | 12.49 ms | 0.1086 ms | 0.0907 ms |
 | BrotliSharpLib | LegacyJitX64 | LegacyJit |      X64 | 11.20 ms | 0.1380 ms | 0.1290 ms |
 |     GoogleImpl | LegacyJitX86 | LegacyJit |      X86 | 16.16 ms | 0.1305 ms | 0.1156 ms |
 | BrotliSharpLib | LegacyJitX86 | LegacyJit |      X86 | 14.02 ms | 0.1395 ms | 0.1305 ms |
 |     GoogleImpl |    RyuJitX64 |    RyuJit |      X64 | 12.75 ms | 0.1556 ms | 0.1456 ms |
 | BrotliSharpLib |    RyuJitX64 |    RyuJit |      X64 | 11.63 ms | 0.0785 ms | 0.0735 ms |
