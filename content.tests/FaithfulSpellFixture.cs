using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Space;
using static Content.Tests.Fights;

namespace Content.Tests
{
    // what every Faithful*Tests file shares besides the book, the hall, scripted dice, a wizard and a
    // goblin (Fights): the duel they fight and the chooser that always says yes. the tests of the
    // spells the unattended run of 2026-09-24 made faithful, held to what SRD 5.2.1 says they do,
    // split one file per primitive handler (cc_task_dedupe-effects.md, Phase 5). moves only: every
    // test reads as it did
    public abstract class FaithfulSpellFixture
    {
        // the wizard at (2,2) going first, the goblin beside it at (3,2) going second
        protected static Encounter Duel(IRng rng, out Caster wizard, out Actor me, out Actor goblin,
                              int x = 3, params string[] tags)
        {
            Encounter fight = Field(rng);
            wizard = Wizard(out me);
            goblin = Goblin(tags: tags);

            fight.Enlist(me, new Cell(2, 2));
            fight.Enlist(goblin, new Cell(x, 2));
            fight.Begin();

            return fight;
        }

        protected static bool Faithful(string id) => !Book.Find(id).Approximated;

        protected sealed class Yes : IReactionChooser
        {
            public IReaction Choose(Encounter fight, Actor reactor, Moment moment,
                                    IReadOnlyList<IReaction> options) => options.FirstOrDefault();
        }
    }
}
