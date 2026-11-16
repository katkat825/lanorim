using System;
using System.Collections.Generic;
using System.Linq;
using Content.Challengers;
using Content.Dialogue;
using Core.Combat;
using Core.Tables;

namespace Content.Play
{
    public sealed partial class CampaignRun
    {
        // --- the arena (cc_task_cg) -----------------------------------------------------------------------------

        // reputation, the roster, the pick: saved with the run (SaveGame.Arena), read by the story ($reputation and
        // the rest, ArenaVariables)
        public Arena Arena { get; private set; } = new Arena();

        public ArenaRules ArenaRules => Pack.Manifest?.Rules.Arena ?? ArenaRules.Default;

        // <<challengers>>: the roster scene, drawn the first time it's asked for. a campaign with no challengers says so
        // to its author, and the story goes on as if the player walked away
        Scene? RosterAsked()
        {
            if (Pack.Challengers.Count == 0 && !Pack.SpecialChallengers.Any())
            {
                Talk.Complained.Add("<<challengers>> in a campaign with no challengers/ and no fight marked \"challenger\"");
                Arena.Picked = null;
                Reply(new Answer());
                return null;
            }

            if (!Arena.Drawn) DrawRoster();

            return Scene.Roster;
        }

        // the player picked one from the roster: the story reads $challenger and fights them with <<fight {$challenger}>>
        public Scene Pick(string id)
        {
            if (Now != Scene.Roster) return Now;

            Challenger picked = Arena.Roster.FirstOrDefault(c => c.Id == id);

            if (picked == null) return Now;

            Arena.Picked = picked;
            Reply(new Answer { Entry = picked.Id });

            return Settle();
        }

        // the player went back to the story without picking (to the quartermaster, say): $challenger is ""
        public Scene LeaveRoster()
        {
            if (Now != Scene.Roster) return Now;

            Arena.Picked = null;
            Reply(new Answer());

            return Settle();
        }

        void DrawRoster(Challenger keep = null, Challenger beaten = null) =>
            new RosterDraw(Arena, Pack.Challengers, Find, _gm, Statblocks)
                .Draw(Hero.Level, ArenaRules, Pack.SpecialChallengers, keep, beaten);

        // every statblock this campaign can field: its own and the SRD's
        IEnumerable<string> Statblocks => Pack.Monsters.Select(m => m.Id).Concat(Library.Bestiary.All.Select(m => m.Id));

        // <<fight {$challenger}>>: a challenger on the roster (or the one just fought, for a story that fights again),
        // framed like any fight. a special is its own entry, its counts rolled as ever; a generated one is the group the
        // roster fitted, on its type's map
        FightCall ChallengerCall(string id)
        {
            Challenger challenger = Arena.Find(id);

            if (challenger == null) return null;

            if (challenger.Special)
            {
                EncounterEntry entry = Pack.SpecialChallengers.FirstOrDefault(e => e.Id == challenger.Entry);

                if (entry == null) return null;

                var rolled = entry.Monsters.Select(b => new Mustered(b.Monster, Math.Max(1, b.Count.Roll(_gm)))).ToList();

                return Framed(entry, rolled, challenger);
            }

            var bands = challenger.Group.Select(m => new Band(m.Monster, Core.Dice.DiceRoll.Flat(m.Count))).ToList();
            var generated = new EncounterEntry(challenger.Id, EntryKind.Fight, monsters: bands,
                                               map: Pack.KindOfChallenger(challenger.Type)?.Map);

            return Framed(generated, challenger.Group, challenger);
        }

        // A WON BOUT'S XP (cc_task_cg Part 4): a share of the hero's next level by the challenger's difficulty, in place
        // of its monsters' XP
        public int BoutXp => Fight?.Challenger is Challenger fought ? Challengers.BoutXp.For(Hero.Level, fought.Difficulty, ArenaRules) : 0;

        // a bout is over: reputation moves (a yielded or fled bout is a lost one), a beaten special doesn't come back,
        // and the roster is drawn again - the one the hero lost to stays for a rematch, the one they beat leaves
        void Bout(Outcome outcome)
        {
            if (Fight?.Challenger is not Challenger fought) return;

            bool won = outcome == Outcome.HeroesWon;

            Arena.Reputation = ArenaRules.Moved(Arena.Reputation, won);

            if (won && fought.Special) Arena.Beaten.Add(fought.Entry);

            if (won) DrawRoster(beaten: fought);
            else DrawRoster(keep: Arena.Roster.FirstOrDefault(c => c.Id == fought.Id) ?? fought);
        }

        // back from a save: the arena as it was
        void Restore(Arena arena) => Arena = arena?.Copy() ?? new Arena();
    }
}
