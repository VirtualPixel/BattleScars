using System;
using System.Collections.Generic;
using System.Linq;
using BattleScars.Configuration;

namespace BattleScars.Services
{
    // Worsening ladder, mildest first. A scarred slot climbs one rung at a time
    // as HP drops; ConfigService.SeverityForSlot decides which rung.
    public enum ScarSeverity
    {
        Bandages = 0,
        Cracks = 1,
        Damaged = 2,
        Broken = 3,
    }

    // Four overlay/mesh sets discovered once against MetaManager.cosmeticAssets
    // via the substring tokens in PluginConfig. The "broken" set is split into a
    // head group and a body group by CosmeticType so the broken-mesh head can be
    // held back to its own HP threshold.
    //
    // Picks are seeded by steamID so a player's scars stay put across a run:
    // each set is shuffled once, and taking the first N of that shuffle means
    // healing peels scars off the end and damage adds the next one.
    //
    // SetupCosmeticsRPC only fires from the cosmetic's owner, so applying scars
    // on the local player is enough for every peer in the lobby to see them
    // through vanilla networking. The RPC is buffered, so late joiners pick the
    // current state up automatically.
    public static class Cosmetics
    {
        private static List<int>? _bandagesPool;
        private static List<int>? _cracksPool;
        private static List<int>? _damagedPool;
        private static List<int>? _brokenBodyPool;
        private static List<int>? _brokenHeadPool;
        private static bool _discoveryRan;

        public static void DiscoverIfNeeded()
        {
            if (_discoveryRan) return;
            if (MetaManager.instance == null || MetaManager.instance.cosmeticAssets == null) return;

            var assets = MetaManager.instance.cosmeticAssets;
            _bandagesPool = BuildPool(assets, PluginConfig.BandagesAllowList);
            _cracksPool   = BuildPool(assets, PluginConfig.CracksAllowList);
            _damagedPool  = BuildPool(assets, PluginConfig.DamagedAllowList);

            _brokenHeadPool = new List<int>();
            _brokenBodyPool = new List<int>();
            foreach (var idx in BuildPool(assets, PluginConfig.BrokenAllowList))
            {
                var asset = assets[idx];
                if (asset == null) continue;
                if (IsHeadMesh(asset.type)) _brokenHeadPool.Add(idx);
                else _brokenBodyPool.Add(idx);
            }

            _discoveryRan = true;

            int total = _bandagesPool.Count + _cracksPool.Count + _damagedPool.Count
                        + _brokenBodyPool.Count + _brokenHeadPool.Count;
            BattleScars.Log.LogInfo(
                $"[Cosmetics] sets bandages={_bandagesPool.Count} cracks={_cracksPool.Count} " +
                $"damaged={_damagedPool.Count} brokenBody={_brokenBodyPool.Count} brokenHead={_brokenHeadPool.Count}");
            if (total == 0)
                BattleScars.Log.LogWarning("[Cosmetics] no set matches, cosmetic effects disabled");
        }

        private static bool IsHeadMesh(SemiFunc.CosmeticType type) =>
            type == SemiFunc.CosmeticType.HeadTopMesh
            || type == SemiFunc.CosmeticType.HeadBottomMesh
            || type == SemiFunc.CosmeticType.EyeLidRightMesh
            || type == SemiFunc.CosmeticType.EyeLidLeftMesh;

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

        // The full set of cosmetic indices to force onto a player at this HP.
        // One pick per scarred slot, drawn from the slot's worsening stage, plus
        // the broken-mesh head once HP is low enough. Picks span distinct
        // CosmeticTypes so two scars never fight over one body part.
        public static List<int> ForcedSetForHealth(string steamID, int currentHP)
        {
            DiscoverIfNeeded();

            var result = new List<int>();
            var assets = MetaManager.instance?.cosmeticAssets;
            if (assets == null) return result;

            int slots = ConfigService.ScarSlotCount(currentHP);
            var need = new int[4];
            for (int i = 0; i < slots; i++)
                need[(int)ConfigService.SeverityForSlot(currentHP, i)]++;

            // Worst stage first so broken meshes claim their body parts before
            // the overlay sets fill the remaining slots.
            var used = new HashSet<SemiFunc.CosmeticType>();
            Take(_brokenBodyPool, need[(int)ScarSeverity.Broken],   steamID, ScarSeverity.Broken,   assets, used, result);
            Take(_damagedPool,    need[(int)ScarSeverity.Damaged],  steamID, ScarSeverity.Damaged,  assets, used, result);
            Take(_cracksPool,     need[(int)ScarSeverity.Cracks],   steamID, ScarSeverity.Cracks,   assets, used, result);
            Take(_bandagesPool,   need[(int)ScarSeverity.Bandages], steamID, ScarSeverity.Bandages, assets, used, result);

            if (ConfigService.BrokenHeadActive(currentHP) && _brokenHeadPool != null)
            {
                foreach (var idx in _brokenHeadPool)
                {
                    if (idx < 0 || idx >= assets.Count) continue;
                    var asset = assets[idx];
                    if (asset == null || used.Contains(asset.type)) continue;
                    result.Add(idx);
                    used.Add(asset.type);
                }
            }
            return result;
        }

        // Take the first `count` of a set's steamID-seeded shuffle, skipping any
        // CosmeticType already claimed by a worse stage. The seed folds in the
        // stage so the four sets shuffle independently; taking a prefix keeps
        // picks stable as `count` grows and shrinks with HP.
        private static void Take(List<int>? pool, int count, string steamID, ScarSeverity stage,
            IList<CosmeticAsset> assets, HashSet<SemiFunc.CosmeticType> used, List<int> result)
        {
            if (pool == null || pool.Count == 0 || count <= 0) return;

            int baseHash = string.IsNullOrEmpty(steamID) ? 1 : steamID.GetHashCode();
            int seed = unchecked(baseHash * 31 + (int)stage);
            var rng = new Random(seed);
            var shuffled = pool.OrderBy(_ => rng.Next()).ToList();

            int taken = 0;
            foreach (var idx in shuffled)
            {
                if (taken >= count) break;
                if (idx < 0 || idx >= assets.Count) continue;
                var asset = assets[idx];
                if (asset == null || used.Contains(asset.type)) continue;
                result.Add(idx);
                used.Add(asset.type);
                taken++;
            }
            // Set ran out of distinct CosmeticTypes before the count was met.
            // Fall back to type-conflicting picks rather than under-filling.
            if (taken < count)
            {
                foreach (var idx in shuffled)
                {
                    if (taken >= count) break;
                    if (idx < 0 || idx >= assets.Count || result.Contains(idx)) continue;
                    result.Add(idx);
                    taken++;
                }
            }
        }

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

        public static void Apply(PlayerAvatar avatar, IList<int> forced)
        {
            if (avatar == null || avatar.playerCosmetics == null) return;
            if (!avatar.photonView.IsMine && SemiFunc.IsMultiplayer()) return;

            var assets = MetaManager.instance?.cosmeticAssets;
            var ownList = avatar.playerCosmetics.cosmeticEquippedRaw ?? new List<int>();
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
