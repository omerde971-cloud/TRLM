using TRLM.Save;

namespace TRLM.Story
{
    /// <summary>
    /// Captures/restores StoryFlags — the one-time story event ids (cinematics already played,
    /// cave discovered, etc.). Static Capture()/Restore() adapter shape matching
    /// NotebookStatePersistence/WorldStatePersistence; SaveOrchestrator is the only caller, in the
    /// world/progression phase right after the notebook section. Restore is silent (Clear + Seed,
    /// never Set) so OnFlagSet side effects don't re-fire on load.
    /// </summary>
    public static class StoryFlagsPersistence
    {
        public static StoryFlagsData Capture()
        {
            var d = new StoryFlagsData();

            var flags = StoryFlags.Instance;
            if (flags != null)
            {
                foreach (var id in flags.All)
                    d.flagIds.Add(id);
            }

            return d;
        }

        public static void Restore(StoryFlagsData d)
        {
            if (d == null) return;

            var flags = StoryFlags.Instance;
            if (flags == null) return;

            flags.Clear();
            flags.Seed(d.flagIds);
        }
    }
}
