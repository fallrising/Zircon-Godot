// Host lifecycle substitutes only: no game, network, assets or MirDB.
using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Library
{
    public static class ConfigReader
    {
        public static void Load(Assembly _) { }
        public static void Save(Assembly _) => Console.WriteLine("CONFIG_SAVED");
    }
    public static class Encryption { public static void SetKey(byte[] _) { } }
}

namespace Server.Envir
{
    public static class Config
    {
        public static bool SinglePlayerDev, EncryptionEnabled;
        public static int MaxLevel;
        public static string EncryptionKey = "";
        public static void LoadVersion() { }
    }
    public static class DevSinglePlayer { public const int DevLevel = 1; }

    public static class SEnvir
    {
        public static byte[] CryptoKey;
        public static bool UseLogConsole;
        public static volatile bool Started;
        public static Thread EnvirThread;

        public static void StartServer()
        {
            Started = true;
            EnvirThread = new Thread(() =>
            {
                // Main installs CancelKeyPress immediately after StartServer returns.
                Thread.Sleep(100);
                Console.WriteLine("READY");
                if (Environment.GetEnvironmentVariable("HOST_SELF_STOP") == "1")
                    Started = false;
                while (Started) Thread.Sleep(5);
                Console.WriteLine("WORLD_STOPPING");
                // The parent controls completion, so it can check the host stays alive
                // throughout the save instead of relying on a fast simulated save.
                string gate = Environment.GetEnvironmentVariable("HOST_SAVE_GATE");
                if (!SpinWait.SpinUntil(() => File.Exists(gate), TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Parent did not release the save gate.");
                Console.WriteLine("WORLD_SAVED");
                EnvirThread = null;
            }) { IsBackground = true };
            EnvirThread.Start();
        }
    }
}
