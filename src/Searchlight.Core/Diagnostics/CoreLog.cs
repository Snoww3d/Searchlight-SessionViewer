namespace Searchlight.Diagnostics;

/// <summary>
/// Minimal logging seam for the platform-neutral core. The core writes diagnostic
/// breadcrumbs through <see cref="Write"/>; the host front-end assigns <see cref="Sink"/>
/// to route them wherever it logs (e.g. the tray app's temp-file logger). When no sink
/// is set the breadcrumbs are silently dropped, so the core never depends on any host
/// logging type.
/// </summary>
public static class CoreLog
{
    /// <summary>Host-supplied log target. Null (default) drops all breadcrumbs.</summary>
    public static Action<string>? Sink { get; set; }

    /// <summary>Writes a breadcrumb to <see cref="Sink"/> if one is set.</summary>
    public static void Write(string message) => Sink?.Invoke(message);
}
