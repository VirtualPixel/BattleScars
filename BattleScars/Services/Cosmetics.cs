using System;
using System.Collections.Generic;
using System.Linq;
using BattleScars.Configuration;
using UnityEngine;

namespace BattleScars.Services
{
    public enum CosmeticPool
    {
        Rusty = 0,
        Bandages = 1,
        Damaged = 2,
    }

    // Three pools plus a wrecked-face set, discovered once against
    // MetaManager.cosmeticAssets via the substring tokens in PluginConfig.
    // Picks are seeded by steamID so identities stay stable across a run:
    // healing peels scars off the end, taking damage adds the next one.
    //
    // SetupCosmeticsRPC only fires from the cosmetic's owner, so applying
    // scars on the local player is enough for every peer in the lobby to see
    // them through vanilla networking. The RPC is buffered, late joiners
    // pick the current state up automatically.
    public static class Cosmetics
    {
        private static List<int>? _rustyPool;
        private static List<int>? _bandagesPool;
        private static List<int>? _damagedPool;
        private static List<int>? _wreckedFacePool;
        private static bool _discoveryRan;

        public static void DiscoverIfNeeded()
        {
            if (_discoveryRan) return;
            if (MetaManager.instance == null || MetaManager.instance.cosmeticAssets == null) return;

            var assets = MetaManager.instance.cosmeticAssets;
            _rustyPool       = BuildPool(assets, PluginConfig.RustyAllowList);
            _bandagesPool    = BuildPool(assets, PluginConfig.BandagesAllowList);
            _damagedPool     = BuildPool(assets, PluginConfig.DamagedAllowList);
            _wreckedFacePool = BuildPool(assets, PluginConfig.WreckedFaceCosmetic);
            _discoveryRan = true;

            int total = _rustyPool.Count + _bandagesPool.Count + _damagedPool.Count + _wreckedFacePool.Count;
            BattleScars.Log.LogInfo(
                $"[Cosmetics] pools rusty={_rustyPool.Count} bandages={_bandagesPool.Count} damaged={_damagedPool.Count} wreckedFace={_wreckedFacePool.Count}");
            if (total == 0)
                BattleScars.Log.LogWarning("[Cosmetics] no pool matches, cosmetic effects disabled");
        }

        private static List<int> BuildPool(IList<CosmeticAsset> assets, string allowList)
        {
            var tokens = ParseList(allowList);
            var matched = new List<int>();
            if (tokens.Count == 0) return matched;
            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                if (asset == null) continue;
                string a = (asset.name ?? string.Empty).ToLowerInvariant();
                string b = (asset.assetName ?? string.Empty).ToLowerInvariant();
                if (tokens.Any(t => a.Contains(t) || b.Contains(t)))
                    matched.Add(i);
            }
            return matched;
        }

        private static List<string> ParseList(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return result;
            foreach (var token in raw.Split(','))
            {
                var t = token.Trim().ToLowerInvariant();
                if (t.Length > 0) result.Add(t);
            }
            return result;
        }

        // Stable pick: seed comes from steamID so the same player always gets
        // the same scar identities at the same count. Picks span distinct
        // CosmeticTypes first to spread scars across body slots, then fall
        // back to type-conflicting picks if the pool is shorter than count.
        public static List<int> PickForCount(string steamID, int count, CosmeticPool pool)
        {
            if (count <= 0) return new List<int>();
            var source = pool switch
            {
                CosmeticPool.Rusty => _rustyPool,
                CosmeticPool.Bandages => _bandagesPool,
                CosmeticPool.Damaged => _damagedPool,
                _ => _bandagesPool,
            };
            if (source == null || source.Count == 0) return new List<int>();
            var assets = MetaManager.instance?.cosmeticAssets;
            if (assets == null) return new List<int>();

            int seed = string.IsNullOrEmpty(steamID) ? 1 : steamID.GetHashCode();
            var rng = new System.Random(seed);
            var shuffled = source.OrderBy(_ => rng.Next()).ToList();

            var picks = new List<int>(count);
            var usedTypes = new HashSet<SemiFunc.CosmeticType>();
            foreach (var idx in shuffled)
            {
                if (picks.Count >= count) break;
                if (idx < 0 || idx >= assets.Count) continue;
                var asset = assets[idx];
                if (asset == null) continue;
                if (usedTypes.Contains(asset.type)) continue;
                picks.Add(idx);
                usedTypes.Add(asset.type);
            }
            if (picks.Count < count)
            {
                foreach (var idx in shuffled)
                {
                    if (picks.Count >= count) break;
                    if (!picks.Contains(idx)) picks.Add(idx);
                }
            }
            return picks;
        }

        public static IReadOnlyList<int> WreckedFaceIndices() =>
            _wreckedFacePool ?? (IReadOnlyList<int>)Array.Empty<int>();

        // Merge forced picks with the player's own loadout for SetupCosmetics.
        // Player cosmetics that share a CosmeticType with a forced pick are
        // dropped, so the forced cosmetic wins the slot instead of being
        // visually covered by the player's existing one.
        public static List<int> Merge(IList<CosmeticAsset>? assets, IList<int> ownList, IList<int> forced)
        {
            var combined = new List<int>(ownList.Count + forced.Count);
            var occupied = new HashSet<SemiFunc.CosmeticType>();
            if (assets != null)
            {
                foreach (var idx in forced)
                {
                    if (idx < 0 || idx >= assets.Count) continue;
                    var a = assets[idx];
                    if (a != null) occupied.Add(a.type);
                }
            }
            foreach (var idx in forced)
                if (!combined.Contains(idx)) combined.Add(idx);
            foreach (var idx in ownList)
            {
                if (combined.Contains(idx)) continue;
                if (assets != null && idx >= 0 && idx < assets.Count)
                {
                    var a = assets[idx];
                    if (a != null && occupied.Contains(a.type)) continue;
                }
                combined.Add(idx);
            }
            return combined;
        }

        public static void ApplyForState(PlayerAvatar avatar, int count, CosmeticPool pool, bool wreckedFace)
        {
            if (avatar == null || avatar.playerCosmetics == null) return;
            if (!avatar.photonView.IsMine && SemiFunc.IsMultiplayer()) return;

            var assets = MetaManager.instance?.cosmeticAssets;
            var ownList = avatar.playerCosmetics.cosmeticEquippedRaw ?? new List<int>();
            var picks = PickForCount(avatar.steamID, count, pool);
            var face = wreckedFace ? WreckedFaceIndices() : (IReadOnlyList<int>)Array.Empty<int>();

            var forced = new List<int>(picks.Count + face.Count);
            foreach (var idx in face) forced.Add(idx);
            foreach (var idx in picks)
                if (!forced.Contains(idx)) forced.Add(idx);
            var combined = Merge(assets, ownList, forced);

            using (CosmeticReassertGuard.Enter())
            {
                avatar.playerCosmetics.SetupCosmetics(_synced: SemiFunc.IsMultiplayer(), _forced: true, _cosmetics: combined);
                avatar.playerCosmetics.SetupColors(_synced: SemiFunc.IsMultiplayer());
            }
        }

        public static void RestoreToLocal(PlayerAvatar avatar)
        {
            if (avatar == null || avatar.playerCosmetics == null) return;
            if (!avatar.photonView.IsMine && SemiFunc.IsMultiplayer()) return;
            using (CosmeticReassertGuard.Enter())
            {
                avatar.playerCosmetics.SetupCosmetics(_synced: SemiFunc.IsMultiplayer(), _forced: true, _cosmetics: null);
                avatar.playerCosmetics.SetupColors(_synced: SemiFunc.IsMultiplayer());
            }
        }
    }

    // Thread-static refcount used by SetupCosmeticsReassertPatch to tell its
    // own calls apart from vanilla refreshes. Reentrant: nested Enter/Dispose
    // pairs only release on the outermost Dispose.
    internal static class CosmeticReassertGuard
    {
        [System.ThreadStatic] private static int _depth;
        public static bool IsInside => _depth > 0;

        public static Releaser Enter()
        {
            _depth++;
            return default;
        }

        public struct Releaser : System.IDisposable
        {
            public void Dispose()
            {
                if (_depth > 0) _depth--;
            }
        }
    }
}
