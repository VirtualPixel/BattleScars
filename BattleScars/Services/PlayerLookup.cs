namespace BattleScars.Services
{
    internal static class PlayerLookup
    {
        public static PlayerAvatar? LocalAvatar()
        {
            foreach (var p in SemiFunc.PlayerGetAll())
                if (p != null && p.isLocal) return p;
            return null;
        }
    }
}
