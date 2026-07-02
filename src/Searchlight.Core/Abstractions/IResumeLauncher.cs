namespace Searchlight.Abstractions;

/// <summary>
/// Launches a resumed Copilot session. Abstracted so the platform-neutral core can
/// reference it without taking a dependency on the Windows-only terminal launcher.
/// The Windows front-end implements this over <c>wt.exe</c>/<c>cmd.exe</c>; a mock
/// implementation is used for the demo/mock data source.
/// </summary>
public interface IResumeLauncher
{
    /// <summary>
    /// Resumes the session with id <paramref name="sessionId"/>, optionally titling
    /// the terminal tab <paramref name="tabTitle"/>. Returns <c>true</c> when the
    /// launch was dispatched successfully.
    /// </summary>
    bool Resume(string sessionId, string? tabTitle = null);
}
