using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Inventory;
using Content.Items;
using Content.Species;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Rules;

namespace Content.Sheet
{
    // the whole character: the Actor the rules roll against, plus everything the rules do not
    // need to know about - which class it is, what is in the pack, which spells are on the card
    // rack. the character sheet UI binds to this; nothing here draws anything.
    public sealed partial class Hero
    {
        public Hero(string name, CharacterClass cls, Kind species, Background background,
                    AbilityScores scores, int level = 1, Kind lineage = null,
                    SpellResourceMode resource = SpellResourceMode.Slots)
        {
            Name = name ?? "";
            Class = cls ?? throw new ArgumentNullException(nameof(cls));
            Species = species ?? throw new ArgumentNullException(nameof(species));
            Background = background;
            Lineage = lineage;
            Resource = resource;

            Actor = new Actor(Id(name), level, scores ?? new AbilityScores(), Allegiance.Hero);

            // SRD 5.2.1: every playable species is a Humanoid - what Hold Person and Charm Person
            // read
            Actor.Tag("humanoid");

            Pack = new Pack();
            Equipment = new Equipment();
        }

        // WHICH WAY THIS CHARACTER PAYS FOR LEVELED SPELLS, chosen once at creation and kept for
        // life. Slots is the default because it is the SRD's, and because a player who does not
        // care which they have should end up with the faithful one.
        //
        // It is settable so a save can put back the mode it was written with, and so the creation
        // screen can flip it while the character is still being built. Flipping it after that is
        // not a supported move: it would hand the character a full resource of the other shape,
        // which is a free long rest.
        public SpellResourceMode Resource { get; set; }

        // identity only - nothing reads it but the sheet (character_sheet_decisions.md)
        public Alignment Alignment { get; set; } = Alignment.Neutral;

        // the player types a name, so it is not a localization key - it is the one string in the
        // game that is neither authored nor translated
        public string Name { get; set; }

        public Actor Actor { get; }

        public CharacterClass Class { get; }

        public Kind Species { get; }

        public Kind Lineage { get; }

        public Background Background { get; }

        public Pack Pack { get; }

        public Equipment Equipment { get; }

        public Caster Caster { get; private set; }

        public int Level => Actor.Level;

        public bool Casts => Caster != null;

        static string Id(string name)
        {
            // the actor id has to survive being a key segment; the player's own spelling does not
            var id = new System.Text.StringBuilder();

            foreach (char c in (name ?? "").ToLowerInvariant())
                if (c >= 'a' && c <= 'z' || c >= '0' && c <= '9') id.Append(c);
                else if (c == ' ' || c == '_' || c == '-') id.Append('_');

            return id.Length == 0 ? "hero" : id.ToString();
        }

        public IEnumerable<Feature> Features =>
            Class.By(Level).Concat(Species.Features.Where(f => f.Level <= Level))
                 .Concat(Lineage?.Features.Where(f => f.Level <= Level) ??
                         Enumerable.Empty<Feature>());

        // what the "Special" section of the sheet lists: the things with a button on them
        public IEnumerable<Feature> Activatable => Features.Where(f => f.IsActive);

        // a borrowed shape swings with its own claws, and the sword stays on the sheet for after
        public IEnumerable<Attack> Attacks => Form != null ? Form.Attacks : Equipment.Attacks;

        public IEnumerable<Spell> Spells => Caster?.Known ?? Enumerable.Empty<Spell>();


        // --- building -------------------------------------------------------------------------

        // everything the class, species, background and gear do to the actor, applied in the SRD
        // order: species and background before the class, because the class's hit points read
        // Constitution after the background has raised it.
        public void Build(IReadOnlyDictionary<Ability, int> backgroundSpend = null,
                          IEnumerable<Skill> chosenSkills = null,
                          IEnumerable<Skill> expertise = null,
                          ItemShelf shelf = null,
                          IEnumerable<Spell> spells = null,
                          IEnumerable<AbilityImprovement> improvements = null)
        {
            // what Build was handed, kept so a save can hand it over again. the finished scores
            // are not the character: they are the array plus whatever the rules did to it, and
            // saving the array is how a retuned species or improvement reaches an old save
            Picked = Abilities.All.ToDictionary(a => a, a => Actor.Scores.Base(a));
            BackgroundSpend = new Dictionary<Ability, int>(
                backgroundSpend ?? new Dictionary<Ability, int>());

            Species.Outfit(Actor, Level);
            Lineage?.Outfit(Actor, Level);
            Background?.Outfit(Actor, backgroundSpend);

            Class.Outfit(Actor, Level, chosenSkills);

            foreach (Skill skill in expertise ?? Enumerable.Empty<Skill>())
                Actor.Train(skill, Training.Expert);

            // THE ASI LEVELS ARE THE PLAYER'S (decisions_checklist.md section 1, 2026-09-24): only
            // the improvements handed in - chosen at creation, or read back from a save - are
            // spent. the rest wait on the sheet as PendingImprovements for the level-up screen
            _improvements.Clear();
            RefusedImprovements = Respend(improvements);

            // hit points are read after the bumps, so they have to be set again
            Actor.SetHealth(new Health(
                Class.HitPointsAt(Level, Actor.AbilityModifier(Ability.Constitution)),
                Class.HitDie, Level));

            if (Class.Casts)
            {
                Caster = new Caster(Actor, Class.CastingAbility.Value,
                                    Class.ResourceAt(Level, Resource));

                foreach (Spell spell in spells ?? Enumerable.Empty<Spell>()) Caster.Learn(spell);
            }

            if (shelf != null) Kit(shelf);

            Budget = BuildBudget();
        }

        // the scores as picked, before the species, the background or an improvement touched
        // them; null on a hero nobody has built
        public IReadOnlyDictionary<Ability, int> Picked { get; private set; }

        // the +2/+1 or +1/+1/+1 the player spent. the background only says where it MAY go
        public IReadOnlyDictionary<Ability, int> BackgroundSpend { get; private set; } =
            new Dictionary<Ability, int>();

        // SRD grants one at 4, 8, 12, 16 and 19
        public static int AbilityScoreImprovements(int level) =>
            CharacterClass.UsualImprovementLevels.Count(l => level >= l);

        // the class's own levels: the Fighter's 6 and 14, the Rogue's 10
        public static int AbilityScoreImprovements(int level, CharacterClass cls) =>
            cls?.ImprovementsBy(level) ?? AbilityScoreImprovements(level);

        // improvements Build was handed that no longer fit the rule (a save from before a change,
        // a score that is now over 20): left pending rather than forced
        public IReadOnlyList<AbilityImprovement> RefusedImprovements { get; private set; } =
            Array.Empty<AbilityImprovement>();

        void Kit(ItemShelf shelf)
        {
            foreach (string id in Class.StartingGear.Concat(Background?.Gear ??
                                                            Enumerable.Empty<string>()))
            {
                Item item = shelf.Find(id);

                if (item == null) continue;

                Pack.Take(item);
            }

            Pack.Earn(Background?.Gold ?? 0);

            // put the best of it on: the highest armor class body armor it may wear, a shield if
            // the class trains with one, and the biggest weapon
            foreach (Item armor in Pack.Stacks.Select(s => s.Item)
                                      .Where(i => i.Kind == ItemKind.Armor &&
                                                  i.Armor.HasValue &&
                                                  Class.ArmorTraining.Contains(i.Armor.Value.Weight))
                                      .OrderByDescending(i => i.Armor.Value.BaseArmorClass)
                                      .Take(1))
                Wear(armor);

            Item weapon = Pack.Stacks.Select(s => s.Item)
                              .Where(i => i.Attack != null && i.Slot != Slot.TwoHand)
                              .OrderByDescending(i => i.Attack.Damage.Average)
                              .FirstOrDefault()
                       ?? Pack.Stacks.Select(s => s.Item)
                              .Where(i => i.Attack != null)
                              .OrderByDescending(i => i.Attack.Damage.Average)
                              .FirstOrDefault();

            if (weapon != null) Wear(weapon);

            if (Class.Shields)
                foreach (Item shield in Pack.Stacks.Select(s => s.Item)
                                            .Where(i => i.Kind == ItemKind.Shield).Take(1))
                    Wear(shield);

            foreach (Item trinket in Pack.Stacks.Select(s => s.Item)
                                         .Where(i => i.Slot == Slot.Trinket).Take(1))
                Wear(trinket);
        }

        // wearing something takes it out of the pack, and whatever comes off goes back in
        public bool Wear(Item item)
        {
            if (item == null || Equipment.Refuses(item, Actor, Class.Id) != null) return false;

            IReadOnlyList<Item> off = Equipment.Wear(item, Actor, Class.Id);

            Pack.Drop(item.Id);

            foreach (Item was in off) Pack.Take(was);

            return true;
        }

        public bool TakeOff(Slot slot)
        {
            Item was = Equipment.Remove(slot, Actor);

            if (was == null) return false;

            Pack.Take(was);
            return true;
        }


        // --- the action economy ------------------------------------------------------------

        public ActionBudget Budget { get; private set; } = new ActionBudget();

        ActionBudget BuildBudget()
        {
            var budget = new ActionBudget();

            foreach (Feature feature in Features.Where(f => f.Trait == Trait.ActionGrant))
            {
                int how = Math.Max(1, feature.Count);

                if (feature.Uses > 0)
                {
                    budget.ExtraActionsPerLongRest += feature.Uses;
                    continue;
                }

                switch (feature.Grants)
                {
                    case Grants.BonusAction:
                        budget.ExtraBonusActionsEachRound += how;
                        break;

                    case Grants.Reaction:
                        budget.ExtraReactionsEachRound += how;
                        break;

                    default:
                        budget.ExtraActionsEachRound += how;
                        break;
                }
            }

            budget.LongRest();

            return budget;
        }


        // --- hitting things ------------------------------------------------------------------

        // every rider the hero's features hand to a blow. the fight layer asks once per attack;
        // whether Sneak Attack fires is a question about the attack, not about the Rogue.
        public IReadOnlyList<Rider> RidersFor(bool hadAdvantage, bool spent = false) =>
            Features.Select(f => f.RiderFor(Level, hadAdvantage, spent))
                    .Where(r => r != null)
                    .ToList();

        public Blow Hit(Encounter fight, Turn turn, Actor target, Attack attack,
                        bool spendResource = false)
        {
            if (fight == null || turn == null || attack == null) return null;

            // a bear does not hold a sword
            if (Form != null && !Form.Owns(attack)) return null;

            // Sneak Attack's setup: whether the attack has advantage is worked out before the
            // roll, so the rider can be handed to it rather than patched on afterwards
            bool advantage = Strike.Lean(Actor, target,
                                         fight.Field.Distance(Actor, target) <= 1,
                                         fight.Sees(Actor, target), fight.Sees(target, Actor)) ==
                             Advantage.Advantage;

            return fight.Hit(turn, target, attack, Spend.Action, Advantage.Flat,
                             WithFormRider(RidersFor(advantage, spendResource), attack));
        }


        // everything this hero can do with a reaction, handed to a fight that is about to start:
        // an opportunity attack with the weapon in hand, and every reaction spell on the sheet.
        // the chooser is the sensible default - opportunity attacks always, a Shield only when it
        // turns the hit - and the UI replaces it with its own when the player wants to be asked
        public void ReadyFor(Encounter fight, Incantation incantation = null)
        {
            if (fight == null) return;

            Attack swing = Attacks.FirstOrDefault(a => !a.IsRanged) ?? Attacks.FirstOrDefault();

            if (swing != null) fight.ArmOpportunity(Actor, swing);

            if (Caster != null && incantation != null)
                foreach (Spell spell in Caster.Known.Where(s => s.Answers))
                    fight.Arm(Actor, new SpellReaction(Caster, spell, incantation));

            fight.ChooseReactionsWith(Actor, ReactionChoosers.WhenItHelps);
        }


        // --- what spells hand over, and what gets used up --------------------------------------

        // Goodberry: the items a casting made go in the pack. what does not fit is left behind -
        // returned, so the discard flow can offer a swap
        public IReadOnlyList<(Item item, int count)> Receive(Casting casting, ItemShelf shelf)
        {
            var left = new List<(Item, int)>();

            if (casting == null || shelf == null) return left;

            foreach ((string id, int count) in casting.Conjured)
            {
                Item item = shelf.Find(id);

                if (item == null) continue;

                int taken = Pack.Take(item, count);

                if (taken < count) left.Add((item, count - taken));
            }

            return left;
        }

        // drink a potion, eat a berry: it heals what it heals and one is gone. in a fight it costs
        // what the item says (a bonus action for both); refused when there is none to spend
        public int Use(string itemId, IResolver resolver, Turn turn = null)
        {
            Stack stack = Pack.FirstOf(itemId);

            if (stack == null || resolver == null || stack.Item.Kind != ItemKind.Consumable) return -1;

            if (turn != null && !turn.Take(stack.Item.UseTime)) return -1;

            int healed = stack.Item.Heals.IsNothing
                ? 0
                : Actor.Mend(Math.Max(0, resolver.Roll(stack.Item.Heals)));

            Pack.Drop(itemId, 1);

            return healed;
        }


        // --- stances and recovery -------------------------------------------------------------

        readonly Dictionary<string, int> _spent = new Dictionary<string, int>();

        public int UsesLeft(Feature feature) =>
            feature == null || feature.Uses <= 0
                ? int.MaxValue
                : feature.Uses - (_spent.TryGetValue(feature.Id, out int used) ? used : 0);

        // uses spent since the last rest, by feature id. internal because only a save reads the
        // ledger whole - everybody else asks UsesLeft about one feature
        internal IReadOnlyDictionary<string, int> Spent => _spent;

        // a save putting the ledger back. never more than the feature has, because the uses are
        // the class's and a retuned class may give fewer
        internal void Respend(Feature feature, int used)
        {
            if (feature == null || feature.Uses <= 0) return;

            int clamped = Math.Clamp(used, 0, feature.Uses);

            if (clamped == 0) _spent.Remove(feature.Id);
            else _spent[feature.Id] = clamped;
        }

        public bool Invoke(Feature feature, IResolver resolver = null)
        {
            if (feature == null || UsesLeft(feature) <= 0) return false;

            switch (feature.Trait)
            {
                case Trait.Stance:
                    Boon boon = feature.BoonFor(Level);

                    if (boon == null) return false;

                    Actor.Boons.Add(boon);
                    break;

                case Trait.Recovery:
                    if (resolver == null) return false;

                    Actor.Mend(Math.Max(feature.Flat,
                                        resolver.Roll(feature.AmountAt(Level)) + feature.Flat));
                    break;

                default:
                    return false;
            }

            if (feature.Uses > 0)
                _spent[feature.Id] = (_spent.TryGetValue(feature.Id, out int used) ? used : 0) + 1;

            return true;
        }

        public bool EndStance(Feature feature)
        {
            if (feature == null) return false;

            return Actor.Boons.EndFrom(feature.Id) > 0;
        }

        // the Orc's Relentless Endurance and the Barbarian's Relentless Rage: at 0 hit points,
        // spend a use and stay up on one instead of taking the death save
        public Feature DeathIntercept =>
            Features.FirstOrDefault(f => f.Trait == Trait.DeathIntercept && UsesLeft(f) > 0);

        public bool Intercept()
        {
            Feature intercept = DeathIntercept;

            if (intercept == null || !Actor.IsDown || Actor.IsDead) return false;

            Actor.Health.Revive(Math.Max(1, intercept.Flat));
            Actor.Remove(Condition.Unconscious);

            _spent[intercept.Id] =
                (_spent.TryGetValue(intercept.Id, out int used) ? used : 0) + 1;

            return true;
        }


        // --- resting ---------------------------------------------------------------------------

        // short rest: spend hit dice by choice, and everything per-rest comes back
        public int ShortRest(IResolver resolver, int hitDiceToSpend = 0)
        {
            Revert();

            int healed = 0;

            for (int i = 0; i < hitDiceToSpend; i++)
                healed += Actor.Health.SpendHitDie(resolver,
                                                   Actor.AbilityModifier(Ability.Constitution));

            Actor.ShortRest();

            // gives nothing back in either mode today; the call is here so that if a short rest
            // ever does, it is one line and not a thing somebody has to remember to add
            Caster?.Rested(Rest.Short);

            RestoreAfterShortRest();
            Budget.LongRest();

            Equipment.Apply(Actor);

            return healed;
        }

        // long rest: full hit points, the delta from SRD (updated_decisions.md)
        public void LongRest()
        {
            Revert();

            Actor.LongRest();

            // slots all come back; points refills and forgets which high spells went off today
            Caster?.Rested(Rest.Long);

            // Goodberry's berries last a day
            Pack.Vanish();

            _spent.Clear();
            Budget.LongRest();

            Equipment.Apply(Actor);
        }

        public void FightOver()
        {
            Actor.Boons.FightOver();
            Actor.EndConcentration();
            Equipment.Apply(Actor);
        }

        // milestone levelling: the campaign says when, and the sheet is rebuilt at the new level
        public void LevelTo(int level, IReadOnlyDictionary<Ability, int> backgroundSpend = null)
        {
            if (level <= Level) return;

            // the new level is written onto the druid's own body, never onto the bear's
            Revert();

            int was = Level;

            Actor.SetLevel(level);

            // an ASI level reached here is pending: the story does not wait on it, and the level-up
            // screen spends it when the player says (PendingImprovements, Improve)

            // only what this level brings. Grant is not idempotent - a speed bonus or a flat bonus
            // to a save adds on every call - and granting everything again on every level-up gave
            // a wood elf five more feet of speed each time
            foreach (Feature feature in Features.Where(f => f.Level > was))
                feature.Grant(Actor, level);

            int wasCurrent = Actor.Health.Current;
            int wasMax = Actor.Health.Maximum;

            Actor.SetHealth(new Health(
                Class.HitPointsAt(level, Actor.AbilityModifier(Ability.Constitution)),
                Class.HitDie, level));

            // levelling is not healing: the damage already taken comes with you
            Actor.Health.Take(Math.Max(0, wasMax - wasCurrent));

            // LEVELLING REBUILDS THE RESOURCE AND FILLS IT. A new level is a bigger table or a
            // bigger pool, and there is no sensible way to carry "three quarters spent" across a
            // change of shape - so a level-up is a rest for spells, the way it is for hit points.
            if (Class.Casts && Caster != null)
                Caster.Resource = Class.ResourceAt(level, Resource);

            Budget = BuildBudget();
            Equipment.Apply(Actor);
        }

        public override string ToString() =>
            $"{Name} the level {Level} {Species.Id} {Class.Id}: {Actor.Health}, " +
            $"ac {Actor.ArmorClass}" +
            (Casts ? $", {Caster.Resource?.Describe()}, {Caster.Known.Count} spells" : "") +
            $", {Pack}";
    }
}
