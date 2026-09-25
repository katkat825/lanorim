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

            // and what kind: "elf" is what a Ghoul's claw reads
            Actor.Tag(Species.Id);

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

        // a borrowed shape swings with its own claws, and the sword stays on the sheet for after.
        // everyone has an Unarmed Strike (SRD 5.2.1 p.190)
        public IEnumerable<Attack> Attacks =>
            Form != null ? Form.Attacks : Equipment.Attacks.Select(Wielded).Append(UnarmedStrike);

        // a weapon as this hero swings it: the proficiency bonus only with a weapon the class
        // trains with, and a Versatile weapon's bigger die with both hands free for it (SRD 5.2.1
        // p.89)
        Attack Wielded(Attack attack)
        {
            bool trained = Class.TrainedWith(attack);
            bool twoHanded = !attack.Versatile.IsNothing && !Actor.HasShield &&
                             Equipment.In(Slot.OffHand) == null;

            if (trained && !twoHanded) return attack;

            return attack.With(proficient: trained && attack.Proficient,
                               damage: twoHanded ? attack.Versatile : attack.Damage);
        }

        // SRD 5.2.1 Unarmed Strike: an attack roll with Strength and Proficiency, 1 + Strength
        // modifier Bludgeoning. no hand to drop it from
        public static readonly Attack UnarmedStrike =
            new Attack("unarmed_strike", DiceRoll.Flat(1), DamageType.Bludgeoning, Ability.Strength,
                       proficient: true, reach: 1, hand: Hand.None);

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
            Actor.SetHealth(new Health(MaxHitPointsAt(Level), Class.HitDie, Level));

            if (Class.Casts)
            {
                Caster = new Caster(Actor, Class.CastingAbility.Value,
                                    Class.ResourceAt(Level, Resource));

                foreach (Spell spell in spells ?? Enumerable.Empty<Spell>()) Caster.Learn(spell);
            }

            // a species' own spells, for a hero whose class casts nothing: cantrips, the free
            // casts a day and nothing else (SRD 5.2.1: the High Elf's, the Tiefling's legacy)
            else if (Features.Any(f => f.Spells.Count > 0 || f.InnateSpell != null))
            {
                Caster = new Caster(Actor, InnateAbility());
            }

            Prepare();

            if (shelf != null) Kit(shelf);

            Budget = BuildBudget();
            Rerolls();
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

            Pack.Earn((Background?.Gold ?? 0) + Class.Gold);

            // put the best of it on: the highest armor class body armor it may wear, a shield if
            // the class trains with one, and the biggest weapon
            foreach (Item armor in Pack.Stacks.Select(s => s.Item)
                                      .Where(i => i.Kind == ItemKind.Armor &&
                                                  i.Armor.HasValue &&
                                                  Class.ArmorTraining.Contains(i.Armor.Value.Weight))
                                      .OrderByDescending(i => i.Armor.Value.BaseArmorClass)
                                      .Take(1))
                Wear(armor);

            // the biggest weapon it trains with, melee before ranged; a one-handed one only when
            // there is a shield to carry with it (the Barbarian's greataxe, not a handaxe)
            List<Item> arms = Pack.Stacks.Select(s => s.Item)
                                  .Where(i => i.Attack != null)
                                  .OrderByDescending(i => Class.TrainedWith(i.Attack))
                                  .ThenByDescending(i => i.Attack.Damage.Average)
                                  .ThenBy(i => i.Attack.IsRanged)
                                  .ToList();

            bool shielded = Class.Shields && Pack.Stacks.Any(s => s.Item.Kind == ItemKind.Shield);

            Item weapon = (shielded ? arms.FirstOrDefault(i => i.Slot != Slot.TwoHand) : null)
                       ?? arms.FirstOrDefault();

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
        public IReadOnlyList<Rider> RidersFor(bool hadAdvantage, bool spent = false,
                                              Attack attack = null, Turn turn = null) =>
            Features.Where(f => Eligible(f, attack, turn))
                    .Select(f => f.RiderFor(Level, hadAdvantage, spent))
                    .Where(r => r != null)
                    .ToList();

        // SRD 5.2.1: Sneak Attack "once per turn" with "a Finesse or a Ranged weapon" (p.61);
        // Frenzy and Brutal Strike only while their stances are up (p.29-30); Radiant Strikes on a
        // Melee weapon (p.55)
        bool Eligible(Feature feature, Attack attack, Turn turn)
        {
            if (feature.Trait != Trait.Rider) return true;

            if (feature.WhileStances.Any(s => !Actor.Boons.Has(s))) return false;

            switch (feature.Weapon)
            {
                case "finesse_or_ranged":
                    if (attack == null || !(attack.Finesse || attack.IsRanged)) return false;
                    break;

                case "melee":
                    if (attack == null || attack.IsRanged) return false;
                    break;
            }

            return !(feature.OncePerTurn && turn != null && ReferenceEquals(_turn, turn) &&
                     _firedThisTurn.Contains(feature.Id));
        }

        // the once-a-turn riders already spent, and on which turn
        Turn _turn;
        readonly HashSet<string> _firedThisTurn = new(StringComparer.Ordinal);

        void Fired(Turn turn, IEnumerable<Rider> riders)
        {
            if (turn == null) return;

            if (!ReferenceEquals(_turn, turn))
            {
                _turn = turn;
                _firedThisTurn.Clear();
            }

            foreach (Rider rider in riders ?? Enumerable.Empty<Rider>())
                if (Features.Any(f => f.Id == rider.Id && f.OncePerTurn)) _firedThisTurn.Add(rider.Id);
        }

        public Blow Hit(Encounter fight, Turn turn, Actor target, Attack attack,
                        bool spendResource = false, Spend spend = Spend.Action)
        {
            if (fight == null || turn == null || attack == null) return null;

            // a bear does not hold a sword
            if (Form != null && !Form.Owns(attack)) return null;

            bool close = fight.Field.Distance(Actor, target) <= 1;

            // Brutal Strike: the first Strength attack of a turn, while reckless, gives up Reckless
            // Attack's advantage for the extra die - unless the roll would then be at disadvantage
            Feature brutal = Features.FirstOrDefault(f => f.Forgoes.Length > 0 && Eligible(f, attack, turn) &&
                                                          attack.AbilityFor(Actor) == Ability.Strength);

            if (brutal != null)
            {
                Actor.ForgoingAdvantageFrom = brutal.Forgoes;

                if (Strike.Lean(Actor, target, close, fight.Sees(Actor, target), fight.Sees(target, Actor),
                                fearInSight: fight.FearInSight(Actor), attack: attack) == Advantage.Disadvantage)
                {
                    Actor.ForgoingAdvantageFrom = null;
                    brutal = null;
                }
            }

            // Sneak Attack's setup: whether the attack has advantage is worked out before the
            // roll, so the rider can be handed to it rather than patched on afterwards
            bool advantage = Strike.Lean(Actor, target, close,
                                         fight.Sees(Actor, target), fight.Sees(target, Actor),
                                         fearInSight: fight.FearInSight(Actor), attack: attack) ==
                             Advantage.Advantage;

            IReadOnlyList<Rider> riders = RidersFor(advantage, spendResource, attack, turn)
                .Where(r => brutal != null || !Features.Any(f => f.Id == r.Id && f.Forgoes.Length > 0))
                .ToList();

            Blow blow;

            try
            {
                blow = fight.Hit(turn, target, attack, spend, Advantage.Flat,
                                 WithFormRider(riders, attack));
            }
            finally
            {
                Actor.ForgoingAdvantageFrom = null;
            }

            if (blow != null && blow.Hit) Fired(turn, blow.Riders);

            // the Light property's bonus attack follows an attack with a Light weapon (p.89)
            if (blow != null && attack.Light && spend == Spend.Action)
            {
                LightTurn = turn;
                LightWeapon = attack.Id;
            }

            return blow;
        }

        // the turn a Light weapon was attacked with, and which: the other Light weapon may then
        // attack with the bonus action, without the ability modifier on its damage
        public Turn LightTurn { get; private set; }

        public string LightWeapon { get; private set; } = "";

        public Attack LightBonusAttack(Attack attack) =>
            attack.With(addsAbility: false,
                        damageBonus: attack.DamageBonus + Math.Min(0, Actor.AbilityModifier(attack.AbilityFor(Actor))));


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

            // Uncanny Dodge: halve a hit you see coming
            foreach (Feature halve in Features.Where(f => f.Reaction == "halve"))
                fight.Arm(Actor, new HalveReaction(halve.Id));

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

        public int UsesLeft(Feature feature)
        {
            if (feature == null) return int.MaxValue;

            Feature pool = PoolOf(feature);
            int uses = pool.UsesAt(Level);

            return uses <= 0
                ? int.MaxValue
                : uses - (_spent.TryGetValue(pool.Id, out int used) ? used : 0);
        }

        // Sacred Weapon and Preserve Life spend Channel Divinity's uses, not their own
        public Feature PoolOf(Feature feature) =>
            feature == null || feature.Spends.Length == 0
                ? feature
                : Features.FirstOrDefault(f => f.Id == feature.Spends) ?? feature;

        void SpendUse(Feature feature)
        {
            Feature pool = PoolOf(feature);

            if (pool.UsesAt(Level) > 0)
                _spent[pool.Id] = (_spent.TryGetValue(pool.Id, out int used) ? used : 0) + 1;
        }

        // uses spent since the last rest, by feature id. internal because only a save reads the
        // ledger whole - everybody else asks UsesLeft about one feature
        internal IReadOnlyDictionary<string, int> Spent => _spent;

        // a save putting the ledger back. never more than the feature has, because the uses are
        // the class's and a retuned class may give fewer
        internal void Respend(Feature feature, int used)
        {
            if (feature == null || feature.UsesAt(Level) <= 0) return;

            int clamped = Math.Clamp(used, 0, feature.UsesAt(Level));

            if (clamped == 0) _spent.Remove(feature.Id);
            else _spent[feature.Id] = clamped;
        }

        public bool Invoke(Feature feature, IResolver resolver = null)
        {
            if (feature == null || UsesLeft(feature) <= 0) return false;

            switch (feature.Trait)
            {
                case Trait.Stance:
                    Boon boon = feature.BoonFor(Level, Actor);

                    if (boon == null) return false;

                    Actor.Boons.Add(boon);
                    break;

                case Trait.Recovery:
                {
                    if (resolver == null) return false;

                    // Preserve Life: only a Bloodied creature, and never past half its maximum
                    if (feature.OnlyBloodied && !Actor.Health.IsBloodied) return false;

                    int amount = Math.Max(feature.Flat,
                                          resolver.Roll(feature.AmountAt(Level)) + feature.Flat);

                    if (feature.CapHalf)
                        amount = Math.Min(amount, Math.Max(0, Actor.Health.Maximum / 2 - Actor.Health.Current));

                    Actor.Mend(amount);
                    break;
                }

                // the Orc's Adrenaline Rush: temporary hit points equal to the proficiency bonus;
                // the Dash is the turn's (CombatSession)
                case Trait.Boost:
                    Actor.Health.GrantTemporary(Actor.ProficiencyBonus);
                    break;

                default:
                    return false;
            }

            SpendUse(feature);

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
            Features.FirstOrDefault(f => f.Trait == Trait.DeathIntercept && UsesLeft(f) > 0 &&
                                         f.WhileStances.All(s => Actor.Boons.Has(s)));

        // SRD 5.2.1 Relentless Rage (p.30): while raging, a DC 10 Constitution save - 5 more each
        // time until a rest - and on a success the hit points are twice the Barbarian's level
        public bool Intercept(IResolver resolver = null)
        {
            Feature intercept = DeathIntercept;

            if (intercept == null || !Actor.IsDown || Actor.IsDead) return false;

            int used = _spent.TryGetValue(intercept.Id, out int u) ? u : 0;

            if (intercept.SaveDc > 0)
            {
                if (resolver == null) return false;

                int dc = intercept.SaveDc + intercept.DcStep * used;

                _spent[intercept.Id] = used + 1;

                if (Checks.Save(resolver, Actor, intercept.Ability ?? Ability.Constitution, dc).Failed)
                    return false;
            }
            else
            {
                _spent[intercept.Id] = used + 1;
            }

            int back = intercept.HitPointsPerLevel > 0 ? intercept.HitPointsPerLevel * Level
                                                       : Math.Max(1, intercept.Flat);

            Actor.Health.Revive(Math.Max(1, back));
            Actor.Remove(Condition.Unconscious);

            return true;
        }


        // --- resting ---------------------------------------------------------------------------

        // short rest: spend hit dice by choice, and everything per-rest comes back
        public int ShortRest(IResolver resolver, int hitDiceToSpend = 0)
        {
            // SRD 5.2.1: a rest needs at least 1 hit point to start
            if (Actor.IsDown) return 0;

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
            // SRD 5.2.1: a rest needs at least 1 hit point to start - nothing comes back, slots
            // and features included, not only the hit points
            if (Actor.IsDown || Actor.IsDead) return;

            Revert();

            Actor.LongRest();

            // slots all come back; points refills and forgets which high spells went off today
            Caster?.Rested(Rest.Long);

            // Goodberry's berries last a day
            Pack.Vanish();

            _spent.Clear();
            Budget.LongRest();

            Rerolls();

            Equipment.Apply(Actor);
        }

        // Indomitable (SRD 5.2.1 p.48): so many rerolls a long rest, each adding the class level
        void Rerolls()
        {
            Feature reroll = Features.LastOrDefault(f => f.Trait == Trait.Reroll);

            Actor.SaveRerolls = reroll?.UsesAt(Level) ?? 0;
            Actor.SaveRerollBonus = reroll == null ? 0 : Level;
        }

        // the spells a feature always has prepared, and its free casts (SRD 5.2.1: a Life Domain's
        // spells, Paladin's Smite). only the ones v1 builds - the rest are reference cards
        static readonly Lazy<Content.Spells.SpellBook> SrdSpells = new Lazy<Content.Spells.SpellBook>(Content.Spells.SpellBook.Srd);

        void Prepare()
        {
            // a species' spells arriving at 3rd level on a hero whose class casts nothing
            if (Caster == null && Features.Any(f => f.Spells.Count > 0 || f.InnateSpell != null))
                Caster = new Caster(Actor, InnateAbility());

            if (Caster == null) return;

            foreach (Feature feature in Features)
            {
                foreach (string id in feature.SpellsAt(Level))
                    if (SrdSpells.Value.Find(id) is Spell spell) Caster.Prepare(spell);

                // a species' free cast comes with its spell's level (SRD 5.2.1 p.84), not before
                foreach (KeyValuePair<string, int> free in feature.FreeCasts)
                    if (!Caster.HasFree(free.Key) &&
                        (feature.Spells.Count == 0 || feature.SpellsAt(Level).Contains(free.Key)))
                        Caster.GrantFree(free.Key, free.Value);

                // the Dragonborn's Breath Weapon: its own spell, so many a long rest
                if (feature.InnateSpell != null)
                {
                    int uses = Math.Max(1, feature.UsesAt(Level));

                    Caster.Prepare(feature.InnateSpell);

                    if (Caster.UseOf(feature.InnateSpell.Id)?.PerDay != uses)
                        Caster.Limit(feature.InnateSpell.Id, new SpellUse(0, uses));
                }
            }
        }

        // the best of the abilities a species lets its spells use (SRD 5.2.1: "Intelligence,
        // Wisdom, or Charisma"); Charisma when it names none
        Ability InnateAbility()
        {
            List<Ability> allowed = Features.SelectMany(f => f.SpellAbilities).Distinct().ToList();

            return allowed.Count == 0
                ? Ability.Charisma
                : allowed.OrderByDescending(a => Actor.AbilityModifier(a)).First();
        }

        // the class's hit points, and Dwarven Toughness's one more a level (SRD 5.2.1 p.84)
        int MaxHitPointsAt(int level) =>
            Class.HitPointsAt(level, Actor.AbilityModifier(Ability.Constitution)) +
            Features.Sum(f => f.MaxHitPointsPerLevel) * level;

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

            Actor.SetHealth(new Health(MaxHitPointsAt(level), Class.HitDie, level));

            // levelling is not healing: the damage already taken comes with you
            Actor.Health.Take(Math.Max(0, wasMax - wasCurrent));

            // LEVELLING REBUILDS THE RESOURCE AND FILLS IT. A new level is a bigger table or a
            // bigger pool, and there is no sensible way to carry "three quarters spent" across a
            // change of shape - so a level-up is a rest for spells, the way it is for hit points.
            if (Class.Casts && Caster != null)
                Caster.Resource = Class.ResourceAt(level, Resource);

            Prepare();

            Budget = BuildBudget();
            Rerolls();
            Equipment.Apply(Actor);
        }

        public override string ToString() =>
            $"{Name} the level {Level} {Species.Id} {Class.Id}: {Actor.Health}, " +
            $"ac {Actor.ArmorClass}" +
            (Casts ? $", {Caster.Resource?.Describe()}, {Caster.Known.Count} spells" : "") +
            $", {Pack}";
    }
}
