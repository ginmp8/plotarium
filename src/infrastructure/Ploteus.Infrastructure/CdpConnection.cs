using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace Ploteus.Infrastructure;

// Deliberately limited to one owned Chromium render job. Not a general browser SDK.
internal sealed class CdpConnection : IAsyncDisposable
{
    private const int MaximumMessageBytes = 64 * 1024 * 1024;
    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Channel<JsonElement> _events = Channel.CreateBounded<JsonElement>(new BoundedChannelOptions(512)
    { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    private Task _reader = Task.CompletedTask;
    private int _nextId;
    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    public ChannelReader<JsonElement> Events => _events.Reader;

    public static async Task<CdpConnection> OpenAsync(Uri uri, CancellationToken token)
    {
        if (uri.Scheme != "ws" || uri.Host != "127.0.0.1") throw new InvalidOperationException("Only the owned loopback CDP endpoint is accepted.");
        var connection = new CdpConnection();
        try
        {
            connection._socket.Options.Proxy = null;
            await connection._socket.ConnectAsync(uri, token);
            connection._reader = connection.ReadLoopAsync();
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task<JsonElement> SendAsync(string method, object? parameters, string? sessionId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_stop.IsCancellationRequested) throw new IOException("CDP connection is closed.");
        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending.TryAdd(id, completion);
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters, sessionId }, Json);
            if (bytes.Length > MaximumMessageBytes) throw new IOException("CDP command exceeds the limit.");
            await _sendGate.WaitAsync(token);
            try { await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, token); }
            finally { _sendGate.Release(); }
            return await completion.Task.WaitAsync(token);
        }
        finally { _pending.TryRemove(id, out _); }
    }

    private async Task ReadLoopAsync()
    {
        Exception? failure = null;
        var buffer = new byte[32 * 1024];
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                ValueWebSocketReceiveResult part;
                do
                {
                    part = await _socket.ReceiveAsync(buffer.AsMemory(), _stop.Token);
                    if (part.MessageType == WebSocketMessageType.Close) throw new IOException("Chromium closed CDP.");
                    if (part.MessageType != WebSocketMessageType.Text || message.Length + part.Count > MaximumMessageBytes)
                        throw new IOException("Invalid or oversized CDP message.");
                    message.Write(buffer, 0, part.Count);
                } while (!part.EndOfMessage);
                using var doc = JsonDocument.Parse(message.GetBuffer().AsMemory(0, checked((int)message.Length)));
                var root = doc.RootElement;
                if (root.TryGetProperty("id", out var id))
                {
                    if (!_pending.TryRemove(id.GetInt32(), out var completion)) continue;
                    if (root.TryGetProperty("error", out _)) completion.TrySetException(new IOException("Chromium rejected a CDP command."));
                    else completion.TrySetResult(root.GetProperty("result").Clone());
                }
                else if (!_events.Writer.TryWrite(root.Clone())) throw new IOException("CDP event limit exceeded.");
            }
        }
        catch (Exception e) when (e is IOException or WebSocketException or OperationCanceledException or JsonException)
        { failure = e; }
        finally
        {
            _events.Writer.TryComplete(failure);
            foreach (var entry in _pending.Values) entry.TrySetException(failure ?? new IOException("CDP stopped."));
            _stop.Cancel();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _socket.Abort();
        try { await _reader; } catch (OperationCanceledException) { }
        _socket.Dispose();
        _sendGate.Dispose();
        _stop.Dispose();
    }
}
