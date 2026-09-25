using System;
using System.Collections.Generic;
using System.Linq;
using Content.Monsters;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Space;
using Core.Tables;

namespace Content.Combat
{
    // ONE FIGHT, SET UP: the board, the hero on its start square, the monsters on their spawns,
    // their brains and their reactions armed, and one Incantation for every spell cast in it. The
    // combat session drives it; the table shows it. Nothing here draws or waits.
    public sealed class Battle
    {
        Battle(Encounter fight, Incantation magic, Hero hero)
        {
            Fight = fight;
            Magic = magic;
            Hero = hero;
        }

        public Encounter Fight { get; }

        public Incantation Magic { get; }

        public Hero Hero { get; }

        readonly Dictionary<Actor, ITactics> _brains = new Dictionary<Actor, ITactics>();
        readonly Dictionary<Actor, Monster> _statblocks = new Dictionary<Actor, Monster>();

        // the statblock behind a monster on the board - its name, its mini
        public Monster StatblockOf(Actor actor) =>
            actor != null && _statblocks.TryGetValue(actor, out Monster m) ? m : null;

        public ITactics BrainOf(Actor actor) =>
            actor != null && _brains.TryGetValue(actor, out ITactics brain) ? brain : null;

        public IEnumerable<Actor> Monsters => _statblocks.Keys.Where(a => !_allies.Contains(a));

        // statblocks fighting on the hero's side: Finger of Death's risen Zombie
        readonly HashSet<Actor> _allies = new HashSet<Actor>();

        public IEnumerable<Actor> Allies => _allies;

        Library _library;

        // SRD 5.2.1 Finger of Death: "A Humanoid killed by this spell rises at the start of your next
        // turn as a Zombie that follows your verbal orders." it takes the corpse's square, joins the
        // order after its master, and fights the master's enemies
        void Rise(Actor corpse, string statblock, Actor master)
        {
            Monster monster = _library?.Bestiary.Find(statblock);

            if (monster == null || !(Fight.Field.Where(corpse) is Cell at)) return;

            Fight.Field.Remove(corpse);

            Actor risen = monster.Spawn($"{statblock}_risen_{_allies.Count + 1}", master.Side);

            if (!Fight.Join(risen, at, monster.Budget(), master)) return;

            _statblocks[risen] = monster;
            _allies.Add(risen);
            _brains[risen] = monster.Brain(null, Magic);

            if (monster.Opportunity != null) Fight.ArmOpportunity(risen, monster.Opportunity);

            Fight.ChooseReactionsWith(risen, ReactionChoosers.WhenItHelps);
        }

        // what Day needs to know a boss when it sees one fall: its challenge, and whether its
        // statblock is tagged "boss"
        public (double challenge, bool boss) What(Actor actor)
        {
            Monster m = StatblockOf(actor);

            return m == null
                ? (0, false)
                : (m.Challenge, m.Tags.Contains("boss", StringComparer.OrdinalIgnoreCase));
        }

        // one band of monsters to put down: which statblock, and where
        public readonly struct Foe
        {
            public Foe(Monster monster, Cell at)
            {
                Monster = monster;
                At = at;
            }

            public Monster Monster { get; }

            public Cell At { get; }
        }

        // SETS IT UP AND BEGINS IT. The chooser is the player's reaction settings; the resolver is
        // the table's (the tray for the hero, the GM for everyone else)
        public static Battle Set(Library library, MapLayout map, Hero hero, IEnumerable<Foe> foes,
                                 IResolver resolver, IReactionChooser heroChooser = null,
                                 ICombatObserver observer = null,
                                 IEnumerable<string> setting = null)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (hero == null) throw new ArgumentNullException(nameof(hero));

            var fight = new Encounter(resolver ?? new StandardResolver(new SeededRng(1)),
                                      new Battlefield(map), observer);

            foreach (string tag in setting ?? Enumerable.Empty<string>()) fight.Setting.Add(tag);

            var magic = new Incantation(fight.Resolver);
            var battle = new Battle(fight, magic, hero) { _library = library };

            fight.Rising += battle.Rise;

            fight.Enlist(hero.Actor, map.Start, hero.Budget);
            hero.ReadyFor(fight, magic);

            // the Orc's Relentless Endurance, the Barbarian's Relentless Rage: dropped to 0, stay up
            fight.Damaged += (attacker, target, amount) =>
            {
                if (ReferenceEquals(target, hero.Actor) && target.IsDown && !target.IsDead)
                    hero.Intercept(fight.Resolver);
            };

            if (heroChooser != null) fight.ChooseReactionsWith(hero.Actor, heroChooser);

            var used = new HashSet<Cell> { map.Start };
            int n = 0;

            foreach (Foe foe in foes ?? Enumerable.Empty<Foe>())
            {
                if (foe.Monster == null) continue;

                Actor actor = foe.Monster.Spawn($"{foe.Monster.Id}_{++n}");
                Cell at = Free(fight.Field, foe.At, used);

                Caster caster = foe.Monster.CasterFor(actor, library?.Spells);

                // the statblock's turn, not the hero's: one action, its Multiattack, a bonus
                // action only if it has one
                fight.Enlist(actor, at, foe.Monster.Budget(caster));
                used.Add(at);

                battle._statblocks[actor] = foe.Monster;

                if (foe.Monster.Opportunity != null) fight.ArmOpportunity(actor, foe.Monster.Opportunity);

                // a statblock's Shield and Counterspell answer like a hero's do
                if (caster != null)
                    foreach (Spell spell in caster.Known.Where(s => s.Answers))
                        fight.Arm(actor, new SpellReaction(caster, spell, magic));

                fight.ChooseReactionsWith(actor, ReactionChoosers.WhenItHelps);

                battle._brains[actor] = foe.Monster.Brain(caster, magic);
            }

            fight.Begin();

            return battle;
        }

        // FROM A ROLLED ENCOUNTER ENTRY: each band's count rolled behind the screen, and the
        // monsters put on the map's numbered spawns in order (then the nearest free squares)
        public static Battle From(Library library, MapLayout map, Hero hero, EncounterEntry entry,
                                  IResolver resolver, Func<string, Monster> find = null,
                                  IReactionChooser heroChooser = null,
                                  ICombatObserver observer = null, IEnumerable<string> setting = null)
        {
            find ??= id => library?.Bestiary.Find(id);

            var foes = new List<Foe>();
            int slot = 1;
            IResolver gm = resolver ?? new StandardResolver(new SeededRng(1));

            foreach (Band band in entry?.Monsters ?? Array.Empty<Band>())
            {
                Monster monster = find(band.Monster);

                if (monster == null) continue;

                int count = Math.Max(1, gm.Roll(band.Count));

                for (int i = 0; i < count; i++, slot++)
                {
                    Cell at = map.SpawnAt(slot) ?? map.SpawnAt(1) ?? FarCorner(map);
                    foes.Add(new Foe(monster, at));
                }
            }

            return Set(library, map, hero, foes, resolver, heroChooser, observer, setting);
        }

        static Cell FarCorner(MapLayout map) => new Cell(map.Columns - 2, 1);

        // the square asked for, or the nearest free one to it
        static Cell Free(Battlefield field, Cell wanted, HashSet<Cell> used)
        {
            if (field.Map.IsPassable(wanted) && !used.Contains(wanted) && !field.IsOccupied(wanted))
                return wanted;

            for (int r = 1; r < Math.Max(field.Columns, field.Rows); r++)
                foreach (Cell cell in field.Burst(wanted, r).OrderBy(c => c.Y).ThenBy(c => c.X))
                    if (field.Map.IsPassable(cell) && !used.Contains(cell) && !field.IsOccupied(cell))
                        return cell;

            return wanted;
        }

        public override string ToString() =>
            $"{Hero.Name} against {_statblocks.Count} on {Fight.Field}: {Fight}";
    }
}
