namespace FanStack.Core.Logging;

/// <summary>Regola di rotazione del log: oltre la dimensione massima il file viene archiviato.</summary>
public static class LogRotation
{
    public const long DefaultMaxBytes = 512 * 1024;

    public static bool ShouldRotate(long currentLength, long incomingLength, long maxBytes = DefaultMaxBytes) =>
        currentLength > 0 && currentLength + incomingLength > maxBytes;
}
