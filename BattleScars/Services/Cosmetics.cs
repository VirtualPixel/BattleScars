using System;
using System.Collections.Generic;
using System.Linq;
using BattleScars.Configuration;
using Photon.Pun;
using Photon.Realtime;

namespace BattleScars.Services
{
    // The accumulation ladder: a scarred limb climbs one rung per step as HP
    // drops, adding a layer each rung. ConfigService.SeverityForSlot picks it.
    // Cracks first so the first scar is a clear surface fracture rather than a
    // bandage (which can read as an ordinary cosmetic); the bandage joins next.
    public enum ScarSeverity
    {
        Cracks = 0,
        Bandages = 1,
        Damaged = 2,
        Broken = 3,
    }

    // Scar cosmetics grouped into limb regions. REPO ships three stacking
    // layers per limb: a bandage accessory, a crack/damaged overlay (one slot,
    // the two mutually exclusive), and a broken mesh. Every scar asset folds
    // onto its limb region by CosmeticType.
    //
    // A scarred limb is pinned for the damage episode and accumulates layers as
    // HP drops: a crack overlay, then a bandage, the overlay worsening to the
    // damaged texture, then a broken mesh, shedding them again on heal. Which
    // limbs scar, and in what order, is seeded once per episode
    // (RerollRoundSeed), so the same HP wears differently the next time you get
    // hurt.
    //
    // Scars broadcast through vanilla SetupCosmeticsRPC so every peer renders
    // them, even on unmodded clients. The buffered RPC also picks late joiners
    // up. The master client doesn't catch the AllBuffered fan-out cleanly, so
    // SyncToMaster duplicates the payload via a direct RPC.
    public static class Cosmetics
    {
        // One scarable limb. Each layer is an asset index, or -1 when REPO ships
        // none for it. The crack and damaged overlays share the limb's single
        // overlay slot, so only one of the two is ever worn at once.
        private sealed class Region
        {
            public string Key = "";
            public int Bandage = -1;
            public int CrackOverlay = -1;
            public int DamagedOverlay = -1;
            public int BrokenMesh = -1;
            // Optional alternate broken meshes for this region (e.g. the HeadTop
            // "Broken Cords" variant). Stable per-level pick rolls each one in
            // turn at RareBrokenMeshChance before falling back to BrokenMesh.
            public readonly List<int> RareBrokenMeshes = new();
            public bool IsHead; // broken mesh waits for the BrokenHeadHP threshold

            // The HeadTop bandage is a Hat-slot cosmetic; forcing it evicts the
            // player's hat, so it yields to a hat the player is wearing.
            public bool IsHeadTop => Key == "HeadTop";
        }

        // Bandages read as a deliberate accent, so only this many limbs wear one
        // even on a fully scarred Semibot. Overlays and broken meshes are uncapped;
        // they carry the damage signal everywhere.
        private const int MaxBandagedLimbs = 3;

        // Substrings (case-insensitive) that mark a broken mesh as a rare
        // variant rather than the region's primary one. REPO ships
        // "Head Top Mesh - Broken Cords" alongside "Head Top Mesh - Broken";
        // tagging "cords" routes Cords into the rare pool so it shows up
        // occasionally instead of always.
        private static readonly string[] RareBrokenTokens = { "cords" };
        private const double RareBrokenMeshChance = 0.25;

        private static List<Region>? _regions;
        private static bool _discoveryRan;

        // Reseeded on the edge back to full health, so one damage episode wears
        // a stable layout and the next one differs. Driver drives this.
        private static int _roundSeed;

        public static void RerollRoundSeed() => _roundSeed = Guid.NewGuid().GetHashCode();

        public static void DiscoverIfNeeded()
        {
            if (_discoveryRan) return;
            if (MetaManager.instance == null || MetaManager.instance.cosmeticAssets == null) return;

            var assets = MetaManager.instance.cosmeticAssets;
            var byKey = new Dictionary<string, Region>();

            // Each set is one severity layer. An asset's CosmeticType folds to
            // the limb it scars; the Overlay and Mesh variants share a limb with
            // the plain accessory.
            foreach (var idx in BuildPool(assets, PluginConfig.BandagesAllowList)) AssignLayer(assets, byKey, idx, ScarSeverity.Bandages);
            foreach (var idx in BuildPool(assets, PluginConfig.CracksAllowList))   AssignLayer(assets, byKey, idx, ScarSeverity.Cracks);
            foreach (var idx in BuildPool(assets, PluginConfig.DamagedAllowList))  AssignLayer(assets, byKey, idx, ScarSeverity.Damaged);
            foreach (var idx in BuildPool(assets, PluginConfig.BrokenAllowList))   AssignLayer(assets, byKey, idx, ScarSeverity.Broken);

            _regions = byKey.Values.ToList();
            // Stable base order so the per-level shuffle is reproducible from its
            // seed alone, not from Dictionary iteration order.
            _regions.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            _discoveryRan = true;

            BattleScars.Log.LogInfo($"[Cosmetics] scar regions={_regions.Count}");
            foreach (var r in _regions)
                ConfigService.LogDiag($"[Cosmetics]   {r.Key}: bandage={r.Bandage >= 0} " +
                    $"crack={r.CrackOverlay >= 0} damaged={r.DamagedOverlay >= 0} " +
                    $"broken={r.BrokenMesh >= 0} rareBroken={r.RareBrokenMeshes.Count}");
            if (_regions.Count == 0)
                BattleScars.Log.LogWarning("[Cosmetics] no scar cosmetics matched, cosmetic effects disabled");
        }

        // Slot one asset into its limb region's layer. The first asset of a
        // region+layer wins; REPO ships a couple of duplicates (e.g. "Damaged2")
        // which are left out.
        private static void AssignLayer(IList<CosmeticAsset> assets,
            Dictionary<string, Region> byKey, int idx, ScarSeverity layer)
        {
            if (idx < 0 || idx >= assets.Count) return;
            var asset = assets[idx];
            if (asset == null) return;
            string? key = RegionKey(asset.type);
            if (key == null) return;

            if (!byKey.TryGetValue(key, out var region))
            {
                region = new Region { Key = key };
                byKey[key] = region;
            }
            switch (layer)
            {
                case ScarSeverity.Bandages:
                    if (region.Bandage < 0) region.Bandage = idx;
                    break;
                case ScarSeverity.Cracks:
                    if (region.CrackOverlay < 0) region.CrackOverlay = idx;
                    break;
                case ScarSeverity.Damaged:
                    if (region.DamagedOverlay < 0) region.DamagedOverlay = idx;
                    break;
                case ScarSeverity.Broken:
                    if (IsRareBroken(asset))
                    {
                        region.RareBrokenMeshes.Add(idx);
                        if (IsHeadMesh(asset.type)) region.IsHead = true;
                    }
                    else if (region.BrokenMesh < 0)
                    {
                        region.BrokenMesh = idx;
                        region.IsHead = IsHeadMesh(asset.type);
                    }
                    break;
            }
        }

        // CosmeticType -> limb region key. The plain accessory, the Overlay
        // variant and the Mesh variant of a limb all fold to one region so scars
        // stack on the same body part. Null for types BattleScars never scars.
        private static string? RegionKey(SemiFunc.CosmeticType type) => type switch
        {
            SemiFunc.CosmeticType.ArmLeft or SemiFunc.CosmeticType.ArmLeftOverlay
                or SemiFunc.CosmeticType.ArmLeftMesh => "ArmLeft",
            SemiFunc.CosmeticType.ArmRight or SemiFunc.CosmeticType.ArmRightOverlay
                or SemiFunc.CosmeticType.ArmRightMesh => "ArmRight",
            SemiFunc.CosmeticType.LegLeft or SemiFunc.CosmeticType.LegLeftOverlay
                or SemiFunc.CosmeticType.LegLeftMesh => "LegLeft",
            SemiFunc.CosmeticType.LegRight or SemiFunc.CosmeticType.LegRightOverlay
                or SemiFunc.CosmeticType.LegRightMesh => "LegRight",
            SemiFunc.CosmeticType.BodyTop or SemiFunc.CosmeticType.BodyTopOverlay
                or SemiFunc.CosmeticType.BodyTopMesh => "BodyTop",
            SemiFunc.CosmeticType.BodyBottom or SemiFunc.CosmeticType.BodyBottomOverlay
                or SemiFunc.CosmeticType.BodyBottomMesh => "BodyBottom",
            SemiFunc.CosmeticType.Hat or SemiFunc.CosmeticType.HeadTopOverlay
                or SemiFunc.CosmeticType.HeadTopMesh => "HeadTop",
            SemiFunc.CosmeticType.HeadBottom or SemiFunc.CosmeticType.HeadBottomOverlay
                or SemiFunc.CosmeticType.HeadBottomMesh => "HeadBottom",
            _ => null,
        };

        private static bool IsHeadMesh(SemiFunc.CosmeticType type) =>
            type == SemiFunc.CosmeticType.HeadTopMesh
            || type == SemiFunc.CosmeticType.HeadBottomMesh
            || type == SemiFunc.CosmeticType.EyeLidRightMesh
            || type == SemiFunc.CosmeticType.EyeLidLeftMesh;

        private static bool IsRareBroken(CosmeticAsset asset)
        {
            string a = (asset.name ?? string.Empty).ToLowerInvariant();
            string b = (asset.assetName ?? string.Empty).ToLowerInvariant();
            foreach (var t in RareBrokenTokens)
                if (a.Contains(t) || b.Contains(t)) return true;
            return false;
        }

        // Stable pick for the region's broken mesh. The rare pool is rolled in
        // order; first hit wins, otherwise the primary mesh stays. The seed is
        // folded with the region key and steamID so the choice holds for one
        // damage episode (until the full-health reroll) and varies across runs
        // and across peers.
        private static int PickBrokenMesh(Region region, string steamID)
        {
            if (region.RareBrokenMeshes.Count == 0) return region.BrokenMesh;
            int baseHash = string.IsNullOrEmpty(steamID) ? 1 : steamID.GetHashCode();
            int seed = unchecked(baseHash * 1009 + _roundSeed * 31 + region.Key.GetHashCode());
            var rng = new Random(seed);
            foreach (var rare in region.RareBrokenMeshes)
                if (rng.NextDouble() < RareBrokenMeshChance) return rare;
            return region.BrokenMesh;
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

        // The cosmetic indices to force at this HP. Each scarred slot is pinned
        // to a fixed limb; the limb accumulates layers as it worsens (crack
        // overlay, then a bandage, then a broken mesh) and sheds them on heal.
        // playerHasHat lets the head keep a worn hat instead of its bandage.
        public static List<int> ForcedSetForHealth(string steamID, int currentHP, bool playerHasHat)
        {
            DiscoverIfNeeded();

            var result = new List<int>();
            if (_regions == null || _regions.Count == 0) return result;

            int slots = ConfigService.ScarSlotCount(currentHP);
            if (slots <= 0) return result;

            bool brokenHead = ConfigService.BrokenHeadActive(currentHP);
            var order = OrderedRegions(steamID);
            int scarred = Math.Min(slots, order.Count);
            int bandaged = 0;

            for (int i = 0; i < scarred; i++)
            {
                var region = order[i];
                var severity = ConfigService.SeverityForSlot(currentHP, i);

                // Overlay layer: a crack from the very first scar, worsening to
                // the damaged texture (the same slot) deeper down. The always-on
                // damage cue, worn by every scarred limb.
                int overlay = severity >= ScarSeverity.Damaged && region.DamagedOverlay >= 0
                    ? region.DamagedOverlay
                    : region.CrackOverlay;
                if (overlay >= 0)
                    result.Add(overlay);

                // Bandage layer: an accent that joins one rung in. Capped so a
                // badly hurt Semibot isn't mummified, and skipped on the head when a
                // worn hat would otherwise be evicted (they share a slot).
                if (severity >= ScarSeverity.Bandages && region.Bandage >= 0
                    && bandaged < MaxBandagedLimbs
                    && !(region.IsHeadTop && playerHasHat))
                {
                    result.Add(region.Bandage);
                    bandaged++;
                }

                // Mesh layer: a fully broken limb. Head meshes wait for the
                // separate broken-head HP threshold. PickBrokenMesh rolls the
                // region's rare variants (e.g. HeadTop Cords) and falls back to
                // the primary mesh.
                int brokenMesh = PickBrokenMesh(region, steamID);
                if (severity >= ScarSeverity.Broken && brokenMesh >= 0
                    && (!region.IsHead || brokenHead))
                    result.Add(brokenMesh);
            }
            return result;
        }

        // The limb regions to scar, worst first, shuffled by the episode seed so
        // the layout holds until you're patched back up to full. SteamID is
        // folded in so peers in one lobby don't scar identically.
        private static List<Region> OrderedRegions(string steamID)
        {
            int baseHash = string.IsNullOrEmpty(steamID) ? 1 : steamID.GetHashCode();
            var rng = new Random(unchecked(baseHash * 31 + _roundSeed));
            return _regions!.OrderBy(_ => rng.Next()).ToList();
        }

        // True when the player's own loadout includes a Hat-type cosmetic, so
        // the head can keep the hat and skip its (Hat-slot) bandage.
        public static bool PlayerWearsHat(PlayerAvatar avatar)
        {
            var assets = MetaManager.instance?.cosmeticAssets;
            var raw = avatar != null && avatar.playerCosmetics != null
                ? avatar.playerCosmetics.cosmeticEquippedRaw
                : null;
            if (assets == null || raw == null) return false;
            foreach (var idx in raw)
            {
                if (idx < 0 || idx >= assets.Count) continue;
                var a = assets[idx];
                if (a != null && a.type == SemiFunc.CosmeticType.Hat) return true;
            }
            return false;
        }

        // Per-CosmeticType view of an index list: body part -> "SetName#idx".
        // Last write wins if a type somehow appears twice.
        private static Dictionary<SemiFunc.CosmeticType, string> ByType(
            IList<CosmeticAsset> assets, IEnumerable<int> indices)
        {
            var map = new Dictionary<SemiFunc.CosmeticType, string>();
            foreach (var idx in indices)
            {
                if (idx < 0 || idx >= assets.Count) continue;
                var asset = assets[idx];
                if (asset == null) continue;
                string set = !string.IsNullOrEmpty(asset.assetName) ? asset.assetName : asset.name;
                map[asset.type] = $"{set}#{idx}";
            }
            return map;
        }

        // Diagnostic: a cosmetic set broken down by body part, sorted so two
        // log lines can be read against each other.
        public static string Describe(IEnumerable<int> indices)
        {
            var assets = MetaManager.instance?.cosmeticAssets;
            if (assets == null) return "?";
            var byType = ByType(assets, indices);
            if (byType.Count == 0) return "(none)";
            var parts = byType.Select(kv => $"{kv.Key}={kv.Value}").ToList();
            parts.Sort(StringComparer.Ordinal);
            return string.Join(" ", parts);
        }

        // Diagnostic: what changed per body part between two cosmetic sets.
        // A part worsening in place (Cracks->Damaged) reads differently from a
        // part being dropped while a different one picks the scar up, which is
        // exactly the distinction worth watching.
        public static string DescribeDiff(IEnumerable<int> before, IEnumerable<int> after)
        {
            var assets = MetaManager.instance?.cosmeticAssets;
            if (assets == null) return "?";
            var b = ByType(assets, before);
            var a = ByType(assets, after);
            var lines = new List<string>();
            foreach (var type in a.Keys.Union(b.Keys))
            {
                b.TryGetValue(type, out var bv);
                a.TryGetValue(type, out var av);
                if (bv == av) continue;
                if (bv == null) lines.Add($"{type} +{av}");
                else if (av == null) lines.Add($"{type} -{bv}");
                else lines.Add($"{type} {bv}->{av}");
            }
            if (lines.Count == 0) return "(no change)";
            lines.Sort(StringComparer.Ordinal);
            return string.Join("  ", lines);
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

            if (ConfigService.DiagOn)
                ConfigService.LogDiag(
                    $"apply steam={avatar.steamID} forced={{ {Describe(forced)} }} (combined {combined.Count} total)");

            avatar.playerCosmetics.SetupCosmetics(_synced: SemiFunc.IsMultiplayer(), _forced: true, _cosmetics: combined);
            avatar.playerCosmetics.SetupColors(_synced: SemiFunc.IsMultiplayer());
            ApplyToDeathHead(avatar, combined);

            SyncToMaster(avatar.playerCosmetics, combined);
            var head = avatar.playerDeathHead;
            if (head != null) SyncToMaster(head.playerCosmetics, combined);
        }

        public static void RestoreToLocal(PlayerAvatar avatar)
        {
            if (avatar == null || avatar.playerCosmetics == null) return;
            if (!avatar.photonView.IsMine && SemiFunc.IsMultiplayer()) return;

            ConfigService.LogDiag($"restore steam={avatar.steamID} (back to the saved loadout)");

            avatar.playerCosmetics.SetupCosmetics(_synced: SemiFunc.IsMultiplayer(), _forced: true, _cosmetics: null);
            avatar.playerCosmetics.SetupColors(_synced: SemiFunc.IsMultiplayer());
            ApplyToDeathHead(avatar, null);

            var saved = MetaManager.instance?.cosmeticEquipped;
            SyncToMaster(avatar.playerCosmetics, saved);
            var head = avatar.playerDeathHead;
            if (head != null) SyncToMaster(head.playerCosmetics, saved);
        }

        // The little expression-preview Semibot under the screen has its own
        // PlayerCosmetics that vanilla only sets up once, via FirstSetup, so it
        // would stay frozen at the saved loadout once that ran. Kicking
        // SetupCosmetics with _forced=false re-runs SetupCosmeticsLogic, which
        // the menu-avatar prefix catches to inject the current scars. The
        // colors need a kick too: each newly-instantiated scar cosmetic has a
        // fresh material at its prefab default (white-ish), and only a
        // SetupColors pass walks playerMaterials and tints them.
        public static void RefreshExpressionPreview()
        {
            var ui = PlayerExpressionsUI.instance;
            if (ui == null) return;
            var visuals = ui.playerAvatarVisuals;
            if (visuals == null || visuals.playerCosmetics == null) return;
            visuals.playerCosmetics.SetupCosmetics(_synced: false, _forced: false);
            visuals.playerCosmetics.SetupColors(_synced: false);
        }

        // The dead head spawns with its own PlayerCosmetics, pre-instantiated
        // by LevelGenerator and parked at the disabled position until
        // PlayerDeathHead.Trigger teleports it in. Vanilla never refreshes it
        // after that initial load, so without this mirror the dead head wears
        // the saved loadout no matter what scars are on the body.
        private static void ApplyToDeathHead(PlayerAvatar avatar, List<int>? combined)
        {
            var head = avatar.playerDeathHead;
            var headCosmetics = head != null ? head.playerCosmetics : null;
            if (head == null || headCosmetics == null) return;
            headCosmetics.SetupCosmetics(_synced: SemiFunc.IsMultiplayer(), _forced: true, _cosmetics: combined);
            headCosmetics.SetupColors(_synced: SemiFunc.IsMultiplayer());
        }

        // Photon Cloud's buffered fan-out delivers cosmetic RPCs from a
        // non-master sender to the master a step late. Sending the same
        // payload directly via Receivers=MasterClient hits a different
        // routing path and lands the live state. No-op when we are the
        // master ourselves; the local apply already covered it.
        private static void SyncToMaster(PlayerCosmetics? cosmetics, IList<int>? equipped)
        {
            if (!SemiFunc.IsMultiplayer() || cosmetics == null || cosmetics.photonView == null) return;
            var master = PhotonNetwork.MasterClient;
            if (master == null || master.IsLocal) return;
            int[] arr = equipped != null ? equipped.ToArray() : Array.Empty<int>();
            cosmetics.photonView.RPC("SetupCosmeticsRPC", master, arr, true);
            var colors = cosmetics.colorsEquipped;
            if (colors != null)
                cosmetics.photonView.RPC("SetupColorsRPC", master, colors);
        }
    }
}
