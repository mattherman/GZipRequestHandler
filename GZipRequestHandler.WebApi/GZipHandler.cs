﻿using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace GZipRequestHandler.WebApi
{
    /// <summary>
    /// Message handler used to decompress gzip'd request content.
    /// http://stackoverflow.com/questions/24180697/how-to-upload-gzip-compressed-data-using-system-net-webclient-in-c-sharp
    /// </summary>
    public class GZipHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            if (IsRequestCompressed(request))
            {
                request.Content = DecompressRequestContent(request);
            }

            return base.SendAsync(request, cancellationToken);
        }

        private bool IsRequestCompressed(HttpRequestMessage request)
        {
            if (request.Content.Headers.ContentEncoding != null &&
                request.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                return true;
            }

            return false;
        }

        private HttpContent DecompressRequestContent(HttpRequestMessage request)
        {
            // Read in the input stream, then decompress in to the outputstream.
            // Doing this asynronously, but not really required at this point
            // since we end up waiting on it right after this.
            MemoryStream outputStream = new MemoryStream();

            Task task = request.Content.ReadAsStreamAsync().ContinueWith(t =>
            {
                Stream inputStream = t.Result;

                using (GZipStream gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
                {
                    gzipStream.CopyTo(outputStream);
                }

                // Setting output streem position to begin is ESSENTIAL!
                outputStream.Seek(0, SeekOrigin.Begin);
            });

            // Wait for inputstream and decompression to complete. Would be nice
            // to not block here and work async when ready instead, but I couldn't 
            // figure out how to do it in context of a DelegatingHandler.
            task.Wait();

            // Save the original content
            HttpContent origContent = request.Content;

            // Replace request content with the newly decompressed stream
            HttpContent newContent = new StreamContent(outputStream);

            // Copy all headers from original content in to new one
            // Change content-encoding and content-length
            foreach (var header in origContent.Headers)
            {
                // Change content-encoding header to default value
                if (header.Key.ToLowerInvariant() == "content-encoding")
                {
                    newContent.Headers.Add(header.Key, "identity");
                    continue;
                }

                // Change content-length header value to decompressed length
                if (header.Key.ToLowerInvariant() == "content-length")
                {
                    newContent.Headers.Add(header.Key, outputStream.Length.ToString());
                    continue;
                }

                // Copy other headers
                newContent.Headers.Add(header.Key, header.Value);
            }

            return newContent;
        }
    }
}
