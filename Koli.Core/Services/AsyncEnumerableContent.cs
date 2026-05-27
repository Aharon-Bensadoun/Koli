using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Koli.Services;

internal sealed class AsyncEnumerableContent : HttpContent
{
    private readonly IAsyncEnumerable<byte[]> _stream;
    private readonly CancellationToken _cancellationToken;
    private const string Boundary = "----WebKitFormBoundary7MA4YWxkTrZu0gW";

    public AsyncEnumerableContent(IAsyncEnumerable<byte[]> stream, CancellationToken cancellationToken)
    {
        _stream = stream;
        _cancellationToken = cancellationToken;
        Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data")
        {
            Parameters = { new NameValueHeaderValue("boundary", Boundary) }
        };
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        await SerializeToStreamAsync(stream, context, _cancellationToken).ConfigureAwait(false);
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        var boundaryBytes = Encoding.UTF8.GetBytes($"--{Boundary}\r\n");
        var fileHeaderBytes = Encoding.UTF8.GetBytes(
            "Content-Disposition: form-data; name=\"file\"; filename=\"audio.wav\"\r\n" +
            "Content-Type: audio/wav\r\n\r\n");
        var endBoundaryBytes = Encoding.UTF8.GetBytes($"\r\n--{Boundary}--\r\n");

        // Write boundary and file header
        await stream.WriteAsync(boundaryBytes, 0, boundaryBytes.Length, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(fileHeaderBytes, 0, fileHeaderBytes.Length, cancellationToken).ConfigureAwait(false);

        // Write audio data
        await foreach (var chunk in _stream.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            await stream.WriteAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false);
        }

        // Write end boundary
        await stream.WriteAsync(endBoundaryBytes, 0, endBoundaryBytes.Length, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = -1;
        return false;
    }
}
