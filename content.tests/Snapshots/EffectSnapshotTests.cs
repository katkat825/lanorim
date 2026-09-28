using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Content.Classes;
using Content.Inventory;
using Content.Items;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Space;
using Core.Words;

namespace Content.Tests
{
    // THE SAFETY NET for cc_task_dedupe-effects.md (Phase 0). every spell in the SRD data is cast
    // with a fixed seed, at its own level and one up, on the same standard targets, and three
    // rounds are played out; every stance and every item that makes a boon is put on a fresh
    // actor. what happened is written down as behaviour - damage, conditions, positions, zones,
    // and what the actor now answers when asked (its armor class, its speed, its advantages, what
    // it may not do) - never as property names, so renaming a setting cannot change the text.
    //
    // a refactor that changes play changes a golden file. to re-bless on purpose, set
    // LANORIM_BLESS_SNAPSHOTS=1 and say why in the run log.
    public class EffectSnapshotTests
    {
        [Fact]
        public void CastingEverythingPlaysTheSameAsTheGoldenFile() =>
            Matches("cast_everything", CastEverything());

        [Fact]
        public void EveryStanceAndItemBoonReadsTheSameAsTheGoldenFile() =>
            Matches("boons_everything", BoonsEverything());

        [Fact]
        public void WhatLoadedIsTheSameAsTheGoldenFile() => Matches("loaded", Loaded());


        // --- the three records ---------------------------------------------------------------

        static string Loaded()
        {
            Library library = Library.Srd();
            var text = new StringBuilder();

            text.AppendLine($"spells {library.Spells.All.Count()}");
            text.AppendLine($"classes {library.Classes.Count}");
            text.AppendLine($"class features {library.Classes.Sum(c => c.Features.Count)}");
            text.AppendLine($"species {library.Species.Count}");
            text.AppendLine($"species features {library.Species.Sum(s => s.Features.Count)}");
            text.AppendLine($"items {library.Items.Count}");
            text.AppendLine($"items with boons {library.Items.All.Count(i => i.Boons.Count > 0)}");
            text.AppendLine($"monsters {library.Bestiary.Count}");
            text.AppendLine($"monster actions {library.Bestiary.All.Sum(m => m.Actions.Count)}");
            text.AppendLine($"problems {library.Problems.Count}");

            foreach (Spell spell in library.Spells.All.OrderBy(s => s.Id, StringComparer.Ordinal))
                text.AppendLine($"spell {spell.Id} {spell.Level} effects {spell.Effects.Count}");

            return text.ToString();
        }

        static string CastEverything()
        {
            Library library = Library.Srd();
            var text = new StringBuilder();

            var spells = library.Spells.All.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();

            // the species' own spells and the statblocks' special actions are effects too
            spells.AddRange(library.Species.SelectMany(s => s.Features)
                                   .Where(f => f.InnateSpell != null).Select(f => f.InnateSpell)
                                   .OrderBy(s => s.Id, StringComparer.Ordinal));
            spells.AddRange(library.Bestiary.All.SelectMany(m => m.Actions).Select(a => a.Spell)
                                   .Where(s => s != null).OrderBy(s => s.Id, StringComparer.Ordinal));

            int seed = 0;

            foreach (Spell spell in spells)
            {
                var levels = new List<int> { spell.Level };

                if (!spell.IsCantrip && spell.Level < 9) levels.Add(spell.Level + 1);

                IReadOnlyList<string> modes = spell.Modes.Count > 0 ? spell.Modes : new[] { "" };

                foreach (int castAt in levels)
                    foreach (string mode in modes)
                    {
                        seed++;
                        OnBoard(text, spell, castAt, mode, seed);
                        OffBoard(text, spell, castAt, mode, seed);
                    }
            }

            return text.ToString();
        }

        static string BoonsEverything()
        {
            Library library = Library.Srd();
            var text = new StringBuilder();

            var owners = library.Classes.Select(c => (c.Id, c.Features))
                                .Concat(library.Species.Select(s => (s.Id, s.Features)));

            foreach ((string owner, IReadOnlyList<Feature> features) in owners)
                foreach (Feature feature in features)
                {
                    Actor actor = Plain("bearer", Allegiance.Hero);
                    SortedDictionary<string, string> before = Probe(actor, null, null);

                    feature.Grant(actor, 20);

                    if (feature.Trait == Trait.Stance)
                        actor.Boons.Add(feature.BoonFor(20, actor));

                    string changed = Diff(before, Probe(actor, null, null));

                    if (changed.Length > 0) text.AppendLine($"== feature {owner}/{feature.Id}{changed}");
                }

            foreach (Item item in library.Items.All.Where(i => i.Boons.Count > 0))
            {
                Actor actor = Plain("bearer", Allegiance.Hero);
                SortedDictionary<string, string> before = Probe(actor, null, null);

                var gear = new Equipment();
                gear.Wear(item, actor, item.Classes.FirstOrDefault());

                // an item that isn't worn (a potion) puts its boons on when it is used
                if (!item.IsEquippable)
                    foreach (Boon boon in item.Boons) actor.Boons.Add(boon);

                text.AppendLine($"== item {item.Id}{Diff(before, Probe(actor, null, null))}");
            }

            return text.ToString();
        }


        // --- casting one spell ---------------------------------------------------------------

        // eleven by nine, all floor
        const string Hall = @"
+-+-+-+-+-+-+-+-+-+-+-+
|@ . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+-+-+-+-+-+-+-+-+-+-+-+";

        static readonly Cell Aimed = new Cell(4, 5);

        static readonly Cell Beside = new Cell(3, 5);

        static readonly Cell[] Centres = { new Cell(4, 5), new Cell(6, 5), new Cell(5, 2), new Cell(7, 3) };

        static readonly Attack Quarterstaff =
            new Attack("quarterstaff", DiceRoll.Parse("1d6"), DamageType.Bludgeoning);

        // the standard cast: the wizard, an ally, a goblin beside the wizard, a fiend, an undead,
        // a creature at low hit points and one over 150
        sealed class Table
        {
            public Encounter Fight;
            public Caster Wizard;
            public List<Actor> Everyone = new();
            public Actor Ally, Goblin, Fiend, Undead, Weak, Giant;
            public Incantation Magic;
        }

        static Table Set(int seed, bool board)
        {
            var table = new Table();
            var resolver = new StandardResolver(new SeededRng(seed));

            table.Wizard = Wizard(out Actor me);
            table.Ally = Plain("ally", Allegiance.Hero, 40, "humanoid");
            table.Goblin = Plain("goblin", Allegiance.Enemy, 30, "humanoid");
            table.Fiend = Plain("fiend", Allegiance.Enemy, 60, "fiend");
            table.Undead = Plain("undead", Allegiance.Enemy, 60, "undead");
            table.Weak = Plain("weak", Allegiance.Enemy, 4, "humanoid");
            table.Giant = Plain("giant", Allegiance.Enemy, 180, "giant");

            table.Everyone.AddRange(new[] { me, table.Ally, table.Goblin, table.Fiend, table.Undead,
                                            table.Weak, table.Giant });
            table.Magic = new Incantation(resolver);

            if (!board) return table;

            Assert.True(MapReader.TryRead(Hall, out MapLayout map, out string problem), problem);

            table.Fight = new Encounter(resolver, new Battlefield(map), new CombatLog());

            table.Fight.Enlist(me, new Cell(2, 4));
            table.Fight.Enlist(table.Ally, new Cell(1, 4));
            table.Fight.Enlist(table.Goblin, new Cell(3, 4));
            table.Fight.Enlist(table.Fiend, new Cell(5, 3));
            table.Fight.Enlist(table.Undead, new Cell(5, 5));
            table.Fight.Enlist(table.Weak, new Cell(4, 3));
            table.Fight.Enlist(table.Giant, new Cell(7, 4));
            table.Fight.Begin();

            return table;
        }

        static Caster Wizard(out Actor actor)
        {
            actor = new Actor("wizard", 17, new AbilityScores(10, 14, 14, 18, 12, 10), Allegiance.Hero);
            actor.SetHealth(new Health(80, Die.D6, 17));

            var caster = new Caster(actor, Ability.Intelligence,
                                    SpellSlots.For(CasterProgression.Full, 17));

            return caster;
        }

        static Actor Plain(string id, Allegiance side, int hp = 50, params string[] tags)
        {
            var actor = new Actor(id, 5, new AbilityScores(12, 12, 12, 10, 10, 10), side);
            actor.SetHealth(new Health(hp, Die.D8, 5));
            actor.Armor = new ArmorProfile(ArmorCategory.Medium, 13);

            foreach (string tag in tags) actor.Tag(tag);

            return actor;
        }

        // a spell that helps is cast on a friend, one that harms on the foes
        static bool Helps(Spell spell)
        {
            SpellEffect first = spell.Effects[0];

            return first.Kind == Primitive.Heal || first.Kind == Primitive.Ward ||
                   first.Kind == Primitive.Relieve || first.Kind == Primitive.Stabilize ||
                   first.Kind == Primitive.Sway && !first.Save.HasValue;
        }

        static Aim AimFor(Table table, Spell spell, string mode)
        {
            IEnumerable<Actor> creatures = Helps(spell)
                ? new[] { table.Ally, table.Wizard.Actor, table.Goblin }
                : new[] { table.Goblin, table.Fiend, table.Undead, table.Weak, table.Giant };

            DamageType type = spell.Effects.SelectMany(Choices).FirstOrDefault();

            IReadOnlyList<Ability> abilities = spell.Effects.SelectMany(AbilityChoices).ToList();
            Ability ability = abilities.Count == 0 || abilities.Contains(Ability.Strength)
                ? Ability.Strength
                : abilities[0];

            // as many creatures as it takes, so a touch spell is not refused for the ones it can't
            int picks = Math.Max(1, spell.Effects.Where(e => e.AimKind == AimKind.Creatures)
                                         .Select(e => e.TargetsAt(spell.Level, spell.Level, 17))
                                         .DefaultIfEmpty(1).Max());

            // a spell of touch or self range is aimed at the square beside the wizard
            Cell square = spell.RangeAt(17) < 2 ? Beside : Aimed;
            IReadOnlyList<Cell> squares = spell.RangeAt(17) < 2 ? new[] { Beside } : Centres;

            return new Aim(creatures.Take(picks), square, Facing.East, Skill.Stealth, ability, squares, mode, type)
            {
                Weapon = spell.Strikes ? Quarterstaff : null,
                Side = Side.Left,
            };
        }

        // the damage types a caster may pick from, when the spell leaves it to them
        static IEnumerable<DamageType> Choices(SpellEffect effect) => effect.DamageChoices;

        static IEnumerable<Ability> AbilityChoices(SpellEffect effect) => effect.Boon.AbilityChoices;

        static void OnBoard(StringBuilder text, Spell spell, int castAt, string mode, int seed)
        {
            Table table = Set(seed, board: true);
            Encounter fight = table.Fight;
            Actor me = table.Wizard.Actor;

            table.Wizard.Learn(spell);

            // everybody before the wizard passes
            Turn turn;

            while ((turn = fight.Next()) != null && !ReferenceEquals(turn.Actor, me)) fight.EndTurn();

            text.AppendLine($"== {spell.Id} @{castAt}" + (mode.Length > 0 ? $" [{mode}]" : "") + " on the board");

            if (turn == null)
            {
                text.AppendLine("  the fight ended before the wizard's turn");
                return;
            }

            Dictionary<Actor, SortedDictionary<string, string>> before = ProbeAll(table);
            Aim aim = AimFor(table, spell, mode);

            Casting casting = spell.Answers
                ? table.Magic.Answer(table.Wizard, spell, MomentFor(spell, table), fight)
                : table.Magic.Cast(table.Wizard, spell, aim, castAt, fight, turn);

            Report(text, casting, fight);
            before = Changes(text, "cast", table, before);

            int round = fight.Round;

            fight.EndTurn();

            while ((turn = fight.Next()) != null && fight.Round <= round + 3)
            {
                // the wizard keeps swinging whatever repeats (Spiritual Weapon, a moved Moonbeam)
                if (ReferenceEquals(turn.Actor, me))
                {
                    before = Changes(text, $"round {fight.Round}", table, before);

                    if (spell.Repeat.HasValue && table.Magic.CanRepeat(table.Wizard, spell))
                    {
                        Casting again = table.Magic.Again(table.Wizard, spell, aim, turn, fight);
                        text.Append("  again:");
                        Report(text, again, fight);
                    }

                    // the second round: a point of damage to everybody else, from the wizard's
                    // side, for whatever taking damage ends or saves against
                    if (fight.Round == round + 2)
                    {
                        foreach (Actor other in table.Everyone.Where(o => !ReferenceEquals(o, me) && !o.IsDown))
                            fight.Hurt(me, other, other.Suffer(1, DamageType.Bludgeoning));

                        before = Changes(text, "a point of damage each", table, before);
                    }
                }

                fight.EndTurn();
            }

            before = Changes(text, "after three rounds", table, before);
            text.AppendLine($"  zones left {fight.Zones.Count}; over {fight.Outcome}");

            // what ends when it is let go of (Haste's lethargy), and what a long rest eases
            table.Magic.Release(me);
            before = Changes(text, "let go", table, before);

            foreach (Actor actor in table.Everyone) actor.LongRest();
            Changes(text, "a long rest", table, before);
        }

        static void OffBoard(StringBuilder text, Spell spell, int castAt, string mode, int seed)
        {
            Table table = Set(seed, board: false);

            table.Wizard.Learn(spell);

            text.AppendLine($"== {spell.Id} @{castAt}" + (mode.Length > 0 ? $" [{mode}]" : "") + " off the board");

            Dictionary<Actor, SortedDictionary<string, string>> before = ProbeAll(table);
            Aim aim = AimFor(table, spell, mode);

            Casting casting = spell.Answers
                ? table.Magic.Answer(table.Wizard, spell, MomentFor(spell, table))
                : table.Magic.Cast(table.Wizard, spell, aim, castAt);

            Report(text, casting, null);
            Changes(text, "cast", table, before);
        }

        static Moment MomentFor(Spell spell, Table table)
        {
            Actor me = table.Wizard.Actor;

            return spell.Trigger switch
            {
                Trigger.Hit => Moment.Hit(table.Goblin, me,
                                          new Attempt(RollKind.Attack, D20Roll.Fixed(15, 0), 14)),
                Trigger.Cast => Moment.Cast(table.Goblin, "fire_bolt", 1),
                Trigger.Damaged => Moment.Damaged(table.Goblin, me, 5),
                Trigger.Struck => Moment.Struck(me, table.Goblin, null),
                Trigger.Targeted => Moment.Targeted(table.Goblin, me, "magic_missile"),
                _ => Moment.Damaged(table.Goblin, me, 5),
            };
        }

        static void Report(StringBuilder text, Casting casting, Encounter fight)
        {
            if (!casting.Cast)
            {
                text.AppendLine($"  refused: {casting.Refusal}");
                return;
            }

            text.AppendLine($"  cast at {casting.CastAt}: {casting.Landings.Count} landings, " +
                            $"{casting.Squares.Count} squares, {casting.Covered.Count} covered" +
                            (casting.Conjured.Any()
                                ? ", conjured " + string.Join(" ", casting.Conjured.Select(c => $"{c.item}x{c.count}"))
                                : ""));

            foreach (Landing landing in casting.Landings)
                text.AppendLine($"    {landing.Effect.Kind.Id()} on {landing.Target?.Id ?? "-"}: " +
                                (landing.Landed ? "landed" : "not") +
                                (landing.Amount != 0 ? $" {landing.Amount}" : "") +
                                (landing.Condition != Condition.None ? $" {landing.Condition.Id()}" : "") +
                                (landing.Attempt != null
                                    ? $" ({landing.Attempt.Kind} {landing.Attempt.Total} vs {landing.Attempt.Against}" +
                                      (landing.Attempt.Succeeded ? " made" : " failed") + ")"
                                    : ""));

            if (fight != null)
                foreach (IZone zone in fight.Zones)
                    text.AppendLine($"    zone {zone.Source} of {zone.Owner?.Id}: " +
                                    $"{fight.Field.Map.Cells.Count(c => zone.Covers(fight.Field, c))} squares");
        }


        // --- what an actor answers when asked ------------------------------------------------

        static Dictionary<Actor, SortedDictionary<string, string>> ProbeAll(Table table) =>
            table.Everyone.ToDictionary(a => a, a => Probe(a, table.Fight, table.Wizard.Actor));

        static Dictionary<Actor, SortedDictionary<string, string>> Changes(
            StringBuilder text, string when, Table table,
            Dictionary<Actor, SortedDictionary<string, string>> before)
        {
            Dictionary<Actor, SortedDictionary<string, string>> now = ProbeAll(table);
            var lines = new List<string>();

            foreach (Actor actor in table.Everyone)
            {
                string changed = Diff(before[actor], now[actor]);

                if (changed.Length > 0) lines.Add($"    {actor.Id}:{changed}");
            }

            if (lines.Count > 0)
            {
                text.AppendLine($"  {when}:");
                foreach (string line in lines) text.AppendLine(line);
            }

            return now;
        }

        static string Diff(SortedDictionary<string, string> before, SortedDictionary<string, string> now)
        {
            var changed = new StringBuilder();

            foreach (string key in before.Keys.Union(now.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                before.TryGetValue(key, out string was);
                now.TryGetValue(key, out string is_);

                if (was != is_) changed.Append($" {key}={is_ ?? "-"}");
            }

            return changed.ToString();
        }

        static readonly Skill[] ProbedSkills = { Skill.Athletics, Skill.Stealth, Skill.Perception };

        static SortedDictionary<string, string> Probe(Actor a, Encounter fight, Actor wizard)
        {
            var p = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["hp"] = $"{a.Health.Current}/{a.Health.Maximum}+{a.Health.Temporary}",
                ["down"] = $"{a.IsDown}/{a.IsDead}/{a.Dust}/{a.Stable}",
                ["conditions"] = string.Join(",", a.Conditions.Select(c => c.Id()).OrderBy(c => c, StringComparer.Ordinal)),
                ["concentrating"] = a.Concentrating ?? "",
                ["ac"] = a.ArmorClass.ToString(),
                ["moves"] = a.Moves.ToString(),
                ["flying"] = a.IsFlying.ToString(),
                ["size"] = a.CurrentSize.Id(),
                ["can act"] = a.CanAct.ToString(),
                ["shifted"] = a.IsShifted.ToString(),
                ["disarmed"] = a.Disarmed.ToString(),
                ["attack leans"] = a.AttackLeans.ToString(),
                ["attack leans str"] = a.AttackLeansWith(Quarterstaff).ToString(),
                ["check leans"] = a.CheckAdvantage.ToString(),
                ["leans against close"] = a.LeansAgainstMe(true).ToString(),
                ["leans against far"] = a.LeansAgainstMe(false).ToString(),
                ["leans against seen"] = a.LeansAgainstMe(true, wizard, true).ToString(),
                ["leans against unseen"] = a.LeansAgainstMe(true, wizard, false).ToString(),
                ["initiative advantage"] = a.InitiativeAdvantage.ToString(),
                ["flat on attacks"] = a.Boons.FlatOnAttacks.ToString(),
                ["flat on damage"] = a.Boons.FlatOnDamage.ToString(),
                ["flat on str damage"] = a.Boons.FlatOnDamageFor(Ability.Strength).ToString(),
                ["dice on attacks"] = string.Join(",", a.Boons.DiceOnAttacks),
                ["boon ac"] = a.Boons.ArmorClass.ToString(),
                ["unarmored base"] = a.Boons.UnarmoredBase.ToString(),
                ["size step"] = a.Boons.SizeStep.ToString(),
                ["raised maximum"] = a.Boons.MaxHitPoints.ToString(),
                ["truesight"] = a.Boons.Truesight.ToString(),
                ["exposed"] = a.Boons.Exposed.ToString(),
                ["no shifting"] = a.Boons.Forbids(Forbid.Shifting).ToString(),
                ["forbids"] = Forbidden(a),
                ["speed questions"] = SpeedQuestions(a),
                ["weapon dice"] = WeaponDice(a),
                ["death ward"] = (a.Boons.DeathWard != null).ToString(),
                ["decoys"] = (a.Boons.Decoy?.Decoys ?? 0).ToString(),
                ["wards magic missile"] = a.Boons.Wards("magic_missile").ToString(),
                ["marks from wizard"] = string.Join(",", a.Boons.MarksFrom(wizard).Select(Mark)),
                ["staff"] = $"{Quarterstaff.AbilityFor(a).Id()} {Quarterstaff.DamageFor(a)} " +
                            $"{Quarterstaff.DamageTypeFor(a, null).Id()} {Quarterstaff.Modifier(a)}",
                // a permanent boon is the creature's nature - a feature's, a statblock's - which was
                // never a boon before cc_task_dedupe-leftovers.md; what it does shows on every other line
                ["boons"] = string.Join(",", a.Boons.All.Where(b => b.Duration != Duration.Permanent)
                                                .Select(b => $"{b.Id}<{b.Source}>{b.Duration}")
                                                .OrderBy(b => b, StringComparer.Ordinal)),
            };

            foreach (Ability ability in Abilities.All)
            {
                p[$"save leans {ability.Id()}"] = a.SaveAdvantage(ability).ToString();
                p[$"check leans {ability.Id()}"] = a.CheckAdvantageFor(ability, Skill.None).ToString();
                p[$"flat on {ability.Id()} saves"] = a.Boons.FlatOnSave(ability).ToString();
                p[$"dice on {ability.Id()} saves"] = string.Join(",", a.Boons.DiceOnSave(ability));
                p[$"save modifier {ability.Id()}"] = a.SaveModifier(ability).ToString();
            }

            foreach (Skill skill in ProbedSkills)
            {
                p[$"check leans {skill.Id()}"] = a.CheckAdvantageFor(skill.Governs(), skill).ToString();
                p[$"flat on {skill.Id()}"] = a.Boons.FlatOnCheck(skill).ToString();
                p[$"dice on {skill.Id()}"] = string.Join(",", a.Boons.DiceOnCheck(skill));
            }

            foreach (Condition condition in Conditions.All)
            {
                if (a.IsImmuneTo(condition)) p[$"immune {condition.Id()}"] = "yes";

                // Fey Ancestry, Brave, Dwarven Resilience: advantage on a save against it
                if (a.AdvantageOnSaveAgainst(condition)) p[$"save against {condition.Id()}"] = "advantage";
            }

            foreach (DamageType type in DamageTypes.All)
            {
                Defense defense = a.DefenseAgainst(type);

                if (defense != Defense.Normal) p[$"defense {type.Id()}"] = defense.ToString();
            }

            if (fight != null)
            {
                p["at"] = fight.Field.Where(a)?.ToString() ?? "off the board";
            }

            return p;
        }

        // the aggregate questions the boons answer about what the bearer may not do
        static string Forbidden(Actor a) =>
            string.Join(",", new[]
            {
                a.Boons.Forbids(Forbid.Actions) ? "actions" : null,
                a.Boons.Forbids(Forbid.Reactions) ? "reactions" : null,
                a.Boons.Forbids(Forbid.Attacks) ? "attacks" : null,
                a.Boons.Forbids(Forbid.Casting) ? "casting" : null,
                a.Boons.Forbids(Forbid.OpportunityAttacks) ? "opportunity_attacks" : null,
                a.Boons.ActionOrBonus ? "action_or_bonus" : null,
                a.Boons.LimitedAction ? "limited_action" : null,
            }.Where(s => s != null));

        static string SpeedQuestions(Actor a) =>
            $"{a.Boons.ExtraSpeed} {a.Boons.FlySpeed} " +
            $"{(a.Boons.SpeedDoubled ? "doubled " : "")}{(a.Boons.SpeedHalved ? "halved " : "")}" +
            $"{(a.Boons.SpeedZero ? "zero" : "")}".TrimEnd();

        // Enlarge's +1d4 and Reduce's -1d4 on a weapon hit
        static string WeaponDice(Actor a) =>
            string.Join(",", a.Boons.WeaponDice.Select(d => (d.Less ? "-" : "+") + d.Dice));

        static string Mark(Boon mark) => $"{mark.Spec.Mark.Dice} {mark.Spec.Mark.Type.Id()}";


        // --- the golden files -----------------------------------------------------------------

        static void Matches(string name, string actual, [CallerFilePath] string here = "")
        {
            string folder = Path.GetDirectoryName(here);
            string golden = Path.Combine(folder, name + ".golden.txt");
            actual = actual.Replace("\r\n", "\n");

            if (Environment.GetEnvironmentVariable("LANORIM_BLESS_SNAPSHOTS") == "1" || !File.Exists(golden))
            {
                File.WriteAllText(golden, actual);
                return;
            }

            string expected = File.ReadAllText(golden).Replace("\r\n", "\n");

            if (expected == actual) return;

            File.WriteAllText(Path.Combine(folder, name + ".actual.txt"), actual);

            string[] want = expected.Split('\n');
            string[] got = actual.Split('\n');
            int line = 0;

            while (line < want.Length && line < got.Length && want[line] == got[line]) line++;

            Assert.Fail($"{name} changed at line {line + 1}:\n  golden: " +
                        (line < want.Length ? want[line] : "(end)") + "\n  now:    " +
                        (line < got.Length ? got[line] : "(end)") +
                        $"\nthe whole run is in {name}.actual.txt");
        }
    }
}
