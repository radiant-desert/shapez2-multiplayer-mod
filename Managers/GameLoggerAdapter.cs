using System;
using Core.Logging; // The game's logging interface namespace

namespace Shapez2MultiplayerMod.Managers
{
    public class GameLoggerAdapter : ILogger
    {
        private class BepInExLogChannel : ILogChannel
        {
            private readonly Action<string> _logAction;
            public BepInExLogChannel(Action<string> logAction) { _logAction = logAction; }
            
            public void Log(string message) => _logAction?.Invoke(message);
            public void LogFormat(string format, params object[] args) => _logAction?.Invoke(string.Format(format, args));
            public void LogException(Exception exception) => _logAction?.Invoke($"[Game System Exception] {exception.Message}\n{exception.StackTrace}");
        }

        public ILogChannel Debug { get; }
        public ILogChannel Info { get; }
        public ILogChannel Warning { get; }
        public ILogChannel Error { get; }
        public ILogChannel Exception => Error;

        public GameLoggerAdapter()
        {
            Debug = new BepInExLogChannel(MultiplayerMod.Log.LogDebug);
            Info = new BepInExLogChannel(MultiplayerMod.Log.LogInfo);
            Warning = new BepInExLogChannel(MultiplayerMod.Log.LogWarning);
            Error = new BepInExLogChannel(MultiplayerMod.Log.LogError);
        }
    }
}