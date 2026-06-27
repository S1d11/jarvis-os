using System.Collections.Generic;

namespace Jarvis.Core.Shell;

/// <summary>
/// Platform-specific system access. The Windows project provides the implementation.
/// </summary>
public interface ISystemAccess
{
    bool LaunchProcess(string path, string args);
    IReadOnlyList<RunningApp> GetRunningApps();
    bool LockScreen();
    bool Shutdown();
    bool Restart();
    bool Sleep();
    bool Logoff();
}
