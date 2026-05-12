using System;
using System.Diagnostics;
using System.IO;
using Godot;

namespace Sts2SkinManager.Runtime;

public static class RestartHelper
{
    private const uint Sts2SteamAppId = 2868840;

    public static void TriggerRestart(string managerDataDir)
    {
        try
        {
            var processName = Process.GetCurrentProcess().ProcessName;
            var exeName = processName + ".exe";
            var helperPath = Path.Combine(managerDataDir, "restart_helper.bat");

            var bat = $@"@echo off
:wait
tasklist /fi ""imagename eq {exeName}"" 2>nul | find /i ""{exeName}"" >nul
if %errorlevel%==0 (
    timeout /t 1 /nobreak >nul
    goto wait
)
start """" steam://run/{Sts2SteamAppId}
timeout /t 2 /nobreak >nul
del ""%~f0""
";
            File.WriteAllText(helperPath, bat);

            var psi = new ProcessStartInfo
            {
                FileName = helperPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
            };
            Process.Start(psi);

            MainFile.Logger.Info($"auto-restart helper spawned (watching for {exeName}); quitting STS2");

            Callable.From(() =>
            {
                try
                {
                    if (Engine.GetMainLoop() is SceneTree tree)
                    {
                        tree.Quit();
                    }
                }
                catch (Exception ex)
                {
                    MainFile.Logger.Warn($"quit failed: {ex.Message}");
                }
            }).CallDeferred();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"auto-restart failed: {ex}");
        }
    }
}
