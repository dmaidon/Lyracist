using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Lyracist.Trivia.Core.Services;
using Xunit;

namespace Lyracist.Trivia.Tests;

/// Integration tests that talk to a real TriviaWebServer over a real TCP socket, since the
/// keep-alive behavior being tested (whether the server closes the socket after a response or
/// leaves it open for another request) can't be observed through the in-process API alone.
public class TriviaWebServerTests
{
    private const string RequestPath = "GET /api/trivia/state HTTP/1.1\r\nHost: localhost\r\n\r\n";

    [Fact]
    public async Task HandleClientAsync_ReusesConnectionAcrossMultipleRequests()
    {
        using var engine = new TriviaGameEngine();
        using var server = new TriviaWebServer(engine, port: 58085);
        server.Start();
        await Task.Delay(200); // let the listener finish binding before connecting

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 58085);

            // Two requests sent over the SAME TcpClient - only possible if the server left the
            // socket open after the first response instead of closing it.
            var first = await SendAndReadResponseAsync(client, RequestPath);
            Assert.Equal(200, first.StatusCode);
            Assert.Contains("Connection: keep-alive", first.Headers, StringComparison.OrdinalIgnoreCase);

            var second = await SendAndReadResponseAsync(client, RequestPath);
            Assert.Equal(200, second.StatusCode);
            Assert.Contains("Connection: keep-alive", second.Headers, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task HandleClientAsync_ClosesConnectionWhenClientRequestsClose()
    {
        using var engine = new TriviaGameEngine();
        using var server = new TriviaWebServer(engine, port: 58086);
        server.Start();
        await Task.Delay(200);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 58086);

            string request = "GET /api/trivia/state HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n";
            var response = await SendAndReadResponseAsync(client, request);
            Assert.Equal(200, response.StatusCode);
            Assert.Contains("Connection: close", response.Headers, StringComparison.OrdinalIgnoreCase);

            // The server should have closed its end of the socket - a further read must see EOF
            // (0 bytes), not hang waiting for a response that will never come.
            var stream = client.GetStream();
            client.ReceiveTimeout = 3000;
            byte[] probe = new byte[1];
            int read = await stream.ReadAsync(probe.AsMemory(0, 1));
            Assert.Equal(0, read);
        }
        finally
        {
            server.Stop();
        }
    }

    private static async Task<(int StatusCode, string Headers)> SendAndReadResponseAsync(TcpClient client, string request)
    {
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.UTF8.GetBytes(request));

        // Read the response headers byte-by-byte up to the blank-line terminator, mirroring how
        // TriviaWebServer itself reads request headers.
        using var headerBytes = new MemoryStream();
        byte[] oneByte = new byte[1];
        int pattern = 0;
        while (true)
        {
            int n = await stream.ReadAsync(oneByte.AsMemory(0, 1));
            if (n == 0) break;
            headerBytes.WriteByte(oneByte[0]);

            if (oneByte[0] == '\r' && (pattern == 0 || pattern == 2)) pattern++;
            else if (oneByte[0] == '\n' && (pattern == 1 || pattern == 3)) pattern++;
            else if (oneByte[0] == '\r') pattern = 1;
            else pattern = 0;

            if (pattern == 4) break;
        }

        string headers = Encoding.UTF8.GetString(headerBytes.ToArray());
        string[] lines = headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        string statusLine = lines.Length > 0 ? lines[0] : "";
        string[] statusParts = statusLine.Split(' ');
        int statusCode = statusParts.Length > 1 && int.TryParse(statusParts[1], out int sc) ? sc : -1;

        int contentLength = 0;
        foreach (var line in lines)
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(line["Content-Length:".Length..].Trim(), out contentLength);
            }
        }

        // Drain exactly the body the server said it was sending, so the stream is correctly
        // positioned for a follow-up request on a kept-alive connection.
        if (contentLength > 0)
        {
            byte[] bodyBuffer = new byte[contentLength];
            int readTotal = 0;
            while (readTotal < contentLength)
            {
                int read = await stream.ReadAsync(bodyBuffer.AsMemory(readTotal, contentLength - readTotal));
                if (read == 0) break;
                readTotal += read;
            }
        }

        return (statusCode, headers);
    }
}
