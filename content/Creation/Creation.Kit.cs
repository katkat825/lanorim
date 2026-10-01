using Content.Sheet;

namespace Content.Creation
{
    // THE STARTING-EQUIPMENT CHOICE (cc_task_e-shop-species-and-ui-notes.md 1.4, SRD 5.2.1 pp.28-77 and 83): the class's
    // gear, or its gold instead; the background's, or 50 GP. Pre-answered with the gear, like the spell resource, so a
    // player who never opens the step starts as before. Gold for either and the hero shops before the campaign
    public sealed partial class Creation
    {
        public KitChoice ClassKit { get; private set; } = KitChoice.Gear;

        public KitChoice BackgroundKit { get; private set; } = KitChoice.Gear;

        public void PickClassKit(KitChoice choice) => ClassKit = choice;

        public void PickBackgroundKit(KitChoice choice) => BackgroundKit = choice;

        // the gold each choice comes with: the gear's little purse, or the gold instead
        public int ClassGold(KitChoice choice) =>
            Class == null ? 0 : choice == KitChoice.Gear ? Class.Gold : Class.GoldInstead;

        public int BackgroundGold(KitChoice choice) =>
            Background == null ? 0 : choice == KitChoice.Gear ? Background.Gold : Background.GoldInstead;

        public int StartingGold => ClassGold(ClassKit) + BackgroundGold(BackgroundKit);
    }
}
