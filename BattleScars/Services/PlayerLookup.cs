namespace BattleScars.Services
{
    internal static class PlayerLookup
    {
        // SemiFunc.PlayerGetAll dereferences GameDirector.instance with no
        // guard of its own, and GameDirector is scene-scoped: it doesn't exist
        // at all until the first gameplay scene loads. Anything that reaches
        // this before then (a config change from the main menu, say) would
        // take an NRE inside a Harmony postfix or an event handler.
        public static PlayerAvatar? LocalAvatar()
        {
            if (GameDirector.instance == null) return null;
            foreach (var p in SemiFunc.PlayerGetAll())
                if (p != null && p.isLocal) return p;
            return null;
        }
    }
}
