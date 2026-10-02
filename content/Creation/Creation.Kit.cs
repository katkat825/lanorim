using System.Collections.Generic;
using System.Linq;
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

        // THE ONE LIST (Kathleen, 2026-10-05; cc_task_f 1.4): every item the hero starts with, the class's and the
        // background's together, each item once with its count (two daggers, not a dagger twice), in the order the
        // kits give them. it follows both choices, which stay apart, as the SRD gives them
        public IReadOnlyList<(string Item, int Count)> StartingGear =>
            Hero.KitGear(Class, ClassKit, Background, BackgroundKit)
                .GroupBy(id => id)
                .Select(g => (g.Key, g.Count()))
                .ToList();
    }
}
