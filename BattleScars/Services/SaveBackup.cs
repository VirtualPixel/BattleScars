using System;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace BattleScars.Services
{
    // Copies MetaSave.es3 once per session to BepInEx/config/BattleScars/backups/<playerName>/.
    // BattleScars never writes to MetaManager.cosmeticEquipped or cosmeticUnlocks,
    // but ES3 corruption is expensive to recover from and the copy is cheap.
    // Keeps the 5 newest backups per player.
    public static class SaveBackup
    {
        private const int BackupsToKeep = 5;
        private static bool _ranThisSession;

        // Invoked from Driver.SlowTick. The wait is so avatar.playerName is
        // populated before the directory is named.
        public static void TryBackupOnce(PlayerAvatar? avatar)
        {
            if (_ranThisSession) return;
            if (avatar == null || string.IsNullOrWhiteSpace(avatar.playerName)) return;

            try
            {
                string source = Path.Combine(Application.persistentDataPath, "MetaSave.es3");
                if (!File.Exists(source))
                {
                    _ranThisSession = true;
                    return;
                }

                string dir = Path.Combine(Paths.ConfigPath, "BattleScars", "backups", Sanitize(avatar.playerName));
                Directory.CreateDirectory(dir);

                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                string dest = UniqueDestination(dir, stamp);
                File.Copy(source, dest, overwrite: false);

                BattleScars.Log.LogInfo($"[Backup] saved {Path.GetFileName(dest)}");
                Prune(dir);
                _ranThisSession = true;
            }
            catch (Exception e)
            {
                BattleScars.Log.LogWarning($"[Backup] failed: {e.Message}");
                _ranThisSession = true;
            }
        }

        private static string Sanitize(string raw)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = raw.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            string cleaned = new string(chars).Trim();
            return cleaned.Length == 0 ? "unknown_player" : cleaned;
        }

        private static string UniqueDestination(string dir, string stamp)
        {
            string dest = Path.Combine(dir, $"{stamp}_MetaSave.es3");
            if (!File.Exists(dest)) return dest;
            for (int i = 1; i < 1000; i++)
            {
                string candidate = Path.Combine(dir, $"{stamp}_{i:D2}_MetaSave.es3");
                if (!File.Exists(candidate)) return candidate;
            }
            return Path.Combine(dir, $"{stamp}_{Guid.NewGuid():N}_MetaSave.es3");
        }

        private static void Prune(string dir)
        {
            try
            {
                var files = Directory.GetFiles(dir, "*_MetaSave.es3")
                    .OrderByDescending(File.GetCreationTimeUtc)
                    .ToList();
                for (int i = BackupsToKeep; i < files.Count; i++)
                    File.Delete(files[i]);
            }
            catch { /* prune is best-effort */ }
        }
    }
}
