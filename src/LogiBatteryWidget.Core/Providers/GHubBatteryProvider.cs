using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LogiBatteryWidget.Core.Models;

namespace LogiBatteryWidget.Core.Providers;

/// <summary>
/// Reads battery status from Logitech G HUB's local, undocumented WebSocket API
/// (ws://localhost:9010, exposed by lghub_agent.exe). This is the same interface used by
/// community tools like LGSTrayBattery_GHUB - there is no official public API, so this client
/// is intentionally defensive about field names and never throws when G HUB isn't running.
/// </summary>
public sealed class GHubBatteryProvider : IBatteryProvider
{
    private static readonly Uri GHubWebSocketUri = new("ws://localhost:9010");
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);

    public string SourceName => "Logitech G HUB";

    public async Task<IReadOnlyList<BatteryDevice>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(ConnectTimeout);

        try
        {
            await socket.ConnectAsync(GHubWebSocketUri, connectCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            // G HUB / lghub_agent.exe isn't running, or isn't listening on 9010. Not an error
            // condition for us - just means this source currently has nothing to report.
            Debug.WriteLine($"[GHubBatteryProvider] connect failed: {ex.Message}");
            return [];
        }

        try
        {
            var devices = await RequestDeviceListAsync(socket, cancellationToken).ConfigureAwait(false);
            var results = new List<BatteryDevice>(devices.Count);

            foreach (var device in devices)
            {
                if (!device.HasBatteryStatus)
                {
                    continue;
                }

                var reading = await RequestBatteryStateAsync(socket, device.Id, cancellationToken).ConfigureAwait(false);
                if (reading is null)
                {
                    continue;
                }

                results.Add(new BatteryDevice(
                    Id: device.Id,
                    Source: SourceName,
                    Name: device.Name,
                    Kind: device.Kind,
                    Percentage: reading.Value.Percentage,
                    IsCharging: reading.Value.IsCharging));
            }

            return results;
        }
        finally
        {
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // best-effort close; nothing to recover from here
                }
            }
        }
    }

    private static async Task<IReadOnlyList<GHubDeviceInfo>> RequestDeviceListAsync(
        ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync(socket, "/devices/list", cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return [];
        }

        if (!response.Value.TryGetProperty("payload", out var payload) ||
            !payload.TryGetProperty("deviceInfos", out var deviceInfos) ||
            deviceInfos.ValueKind != JsonValueKind.Array)
        {
            Debug.WriteLine("[GHubBatteryProvider] unexpected /devices/list payload shape: " + response.Value.GetRawText());
            return [];
        }

        var results = new List<GHubDeviceInfo>();
        foreach (var device in deviceInfos.EnumerateArray())
        {
            var id = FirstString(device, "id", "deviceId");
            if (id is null)
            {
                continue;
            }

            var name = FirstString(device, "extendedDisplayName", "displayName", "deviceName", "name") ?? id;
            var typeHint = FirstString(device, "deviceType", "type") ?? string.Empty;
            var hasBattery = device.TryGetProperty("capabilities", out var capabilities) &&
                              TryGetBool(capabilities, "hasBatteryStatus", "hasBattery") is true;

            results.Add(new GHubDeviceInfo(id, name, MapDeviceKind(typeHint), hasBattery));
        }

        return results;
    }

    private static async Task<GHubBatteryReading?> RequestBatteryStateAsync(
        ClientWebSocket socket, string deviceId, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync(socket, $"/battery/{deviceId}/state", cancellationToken).ConfigureAwait(false);
        if (response is null || !response.Value.TryGetProperty("payload", out var payload))
        {
            return null;
        }

        var percentage = TryGetPercentage(payload);
        var charging = TryGetBool(payload, "charging", "isCharging") ?? false;
        return new GHubBatteryReading(percentage, charging);
    }

    private static async Task<JsonElement?> SendRequestAsync(
        ClientWebSocket socket, string path, CancellationToken cancellationToken)
    {
        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestCts.CancelAfter(RequestTimeout);

        var request = JsonSerializer.Serialize(new { path, verb = "GET" });
        var requestBytes = Encoding.UTF8.GetBytes(request);
        await socket.SendAsync(requestBytes, WebSocketMessageType.Text, endOfMessage: true, requestCts.Token)
            .ConfigureAwait(false);

        // lghub_agent can multiplex unrelated pushes on the same socket, so keep reading until we
        // see a message whose "path" matches what we asked for (or we time out).
        var buffer = new byte[16 * 1024];
        while (true)
        {
            using var messageStream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, requestCts.Token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }
                messageStream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            messageStream.Position = 0;
            using var doc = JsonDocument.Parse(messageStream);
            var root = doc.RootElement.Clone();

            if (FirstString(root, "path") == path)
            {
                return root;
            }
        }
    }

    private static BatteryDeviceKind MapDeviceKind(string typeHint) => typeHint.ToLowerInvariant() switch
    {
        var t when t.Contains("mouse") => BatteryDeviceKind.Mouse,
        var t when t.Contains("keyboard") => BatteryDeviceKind.Keyboard,
        var t when t.Contains("headset") => BatteryDeviceKind.Headset,
        var t when t.Contains("speaker") => BatteryDeviceKind.Speaker,
        _ => BatteryDeviceKind.Other,
    };

    private static string? FirstString(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        return null;
    }

    private static bool? TryGetBool(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value))
            {
                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    return value.GetBoolean();
                }
            }
        }
        return null;
    }

    private static int? TryGetPercentage(JsonElement payload)
    {
        foreach (var name in new[] { "percentage", "batteryPercentage", "batteryLevel", "level" })
        {
            if (payload.ValueKind == JsonValueKind.Object &&
                payload.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetDouble(out var number))
            {
                // Some firmwares report 0-1 fractions instead of 0-100.
                return (int)Math.Round(number <= 1.0 ? number * 100 : number);
            }
        }
        return null;
    }

    private readonly record struct GHubDeviceInfo(string Id, string Name, BatteryDeviceKind Kind, bool HasBatteryStatus);

    private readonly record struct GHubBatteryReading(int? Percentage, bool IsCharging);
}
