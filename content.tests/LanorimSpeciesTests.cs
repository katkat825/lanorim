using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Combat;
using Content.Creation;
using Content.Inventory;
using Content.Saves;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Space;

namespace Content.Tests
{
    // KATHLEEN'S SPECIES AND BACKGROUNDS (cc_task_m2 Part 4, her vault's Playable Species.md and Backgrounds.md): the
    // Fairy (Fey, Small, Fey Resilience, Fairy Flutter, Fey Magic) and the Beastfolk (four kinds as lineages, Darkvision,
    // Keen Senses, Natural Agility, Instinctive Awareness); Homesteader, Trader, Gravedigger, Explorer and Gentry, their
    // skill picks in the Traits step, and their kits
    public class LanorimSpeciesTests
    {
        static readonly Library Srd = Library.Srd();

        static Creation.Creation Making(string cls, string species, string lineage, string background)
        {
            var making = new Creation.Creation(Srd, Srd.Backgrounds);

            making.Pick(Srd.Class(cls));
            making.Pick(Srd.Kind(species));
            if (lineage != null) Assert.True(making.PickLineage(Srd.Kind(lineage)));
            Assert.True(making.Pick(Srd.Background(background)));

            return making;
        }

        static Hero Finish(Creation.Creation making)
        {
            foreach (Skill skill in making.SkillChoices.Take(making.SkillPicksLeft).ToList()) making.Train(skill);
            foreach (Skill skill in making.Skills.Take(making.ExpertisePicksLeft).ToList()) making.Master(skill);
            foreach (Spell spell in making.SpellChoices.ToList())
                if (making.CantripPicksLeft > 0 || making.SpellPicksLeft > 0) making.Learn(spell);
            making.Call("Pip");

            Assert.True(making.Ready, string.Join("; ", making.Problems));
            return making.Finish();
        }

        static Hero RoundTrip(Hero hero) => SpeciesTraitsTests.RoundTrip(hero);

        // --- the species, end to end through the creator -------------------------------------------------------

        [Fact]
        public void AFairyTraderIsSmallFeyAndPicksATradersSkill()
        {
            Creation.Creation making = Making("rogue", "fairy", null, "trader");

            // the Trader's one of seven, in the Traits step as Skillful is; the Fairy has no picks of its own
            Assert.Equal(Step.Traits, making.Next);
            Assert.Equal(1, making.TraitSkillPicks);
            Assert.Equal(new[] { Skill.AnimalHandling, Skill.Insight, Skill.History, Skill.Investigation, Skill.Perception,
                                 Skill.Deception, Skill.Performance }.OrderBy(s => (int)s),
                         making.TraitSkillChoices);
            Assert.False(making.PickTraitSkill(Skill.Arcana));
            Assert.True(making.PickTraitSkill(Skill.Insight));
            Assert.True(making.PickSpellAbility(Ability.Charisma));

            Hero hero = RoundTrip(Finish(making));

            Assert.True(Trained(hero, Skill.Persuasion));
            Assert.True(Trained(hero, Skill.Insight));
            Assert.Equal(Size.Small, hero.Actor.Size);
            Assert.Equal(30, hero.Actor.Speed);

            // Fey, not Humanoid: what Hold Person and Charm Person read
            Assert.True(hero.Actor.Is("fey"));
            Assert.False(hero.Actor.Is("humanoid"));

            // Fey Resilience, Fey Magic's Druidcraft at 1 (Faerie Fire waits for 3)
            Assert.True(hero.Actor.Boons.Has("fey_resilience"));
            Assert.Contains(hero.Spells, s => s.Id == "druidcraft");
            Assert.DoesNotContain(hero.Spells, s => s.Id == "faerie_fire");
        }

        [Fact]
        public void AtThreeAFairyCastsFaerieFireOnceWithoutASlot()
        {
            Creation.Creation making = Making("fighter", "fairy", null, "explorer");
            making.StartAt(3);
            making.SuggestTraits();
            while (making.ImprovementPicksLeft > 0 && making.Improve(making.SuggestedImprovement())) { }

            Hero hero = Finish(making);

            Assert.Contains(hero.Spells, s => s.Id == "faerie_fire");
            Assert.Equal(1, hero.Caster.FreeLeft("faerie_fire"));
        }

        [Fact]
        public void ABeastfolkPicksAKindAndAnAgilityAndHasInitiativeAdvantage()
        {
            Creation.Creation making = Making("fighter", "beastfolk", "beastfolk_selachian", "gentry");

            // the Beastfolk's kinds are its lineages, a look and nothing more
            Assert.True(making.NeedsLineage);
            Assert.Empty(Srd.Kind("beastfolk_selachian").Features);

            // Natural Agility's one of two, and the Gentry's any: two picks, kept to their own lists
            Assert.Equal(2, making.TraitSkillPicks);
            Assert.True(making.PickTraitSkill(Skill.Arcana));

            // the Gentry's pick is spent, so only Natural Agility's two are left
            Assert.Equal(new[] { Skill.Athletics, Skill.Acrobatics }.OrderBy(s => (int)s), making.TraitSkillChoices);
            Assert.True(making.PickTraitSkill(Skill.Acrobatics));
            Assert.Equal(0, making.TraitSkillPicksLeft);

            Hero hero = RoundTrip(Finish(making));

            Assert.True(Trained(hero, Skill.Perception));
            Assert.True(Trained(hero, Skill.Acrobatics));
            Assert.True(Trained(hero, Skill.Arcana));
            Assert.True(Trained(hero, Skill.Persuasion));
            Assert.True(hero.Actor.Is("humanoid"));
            Assert.Equal("beastfolk_selachian", hero.Lineage.Id);
            Assert.Contains(hero.Features, f => f.Id == "darkvision_60");
        }

        // cc_task_m3 Part 9: Feline, Canine, Rodent and Ursine are gone; a hero saved as one still loads, as a Vulpine, and
        // the save says so once rather than being refused
        [Theory]
        [InlineData("beastfolk_feline")]
        [InlineData("beastfolk_canine")]
        [InlineData("beastfolk_rodent")]
        [InlineData("beastfolk_ursine")]
        public void AHeroSavedAsARetiredKindLoadsAsAVulpine(string retired)
        {
            Assert.Null(Srd.Kind(retired));

            Hero hero = Finish(Suggested(Making("rogue", "beastfolk", "beastfolk_vulpine", "soldier")));
            SavedHero saved = HeroSaves.Capture(hero);
            saved.Lineage = retired;

            Read<SaveGame> read = SaveReader.Parse(SaveWriter.Write(new SaveGame { Campaign = "ash_yard", Hero = saved }));
            Assert.True(read.Ok, string.Join("; ", read.Problems));

            Hero back = HeroSaves.Restore(read.Value.Hero, Srd, out var problems);

            Assert.NotNull(back);
            Assert.Equal("beastfolk_vulpine", back.Lineage?.Id);
            Assert.Contains(problems, p => p.ToString().Contains(retired));
        }

        [Fact]
        public void InstinctiveAwarenessIsAdvantageOnInitiativeAndOnlyTheBeastfolkHaveIt()
        {
            Hero beast = Finish(Suggested(Making("rogue", "beastfolk", "beastfolk_vulpine", "soldier")));
            Hero human = Finish(Suggested(Making("rogue", "human", null, "soldier")));

            Assert.True(beast.Actor.InitiativeAdvantage);
            Assert.False(human.Actor.InitiativeAdvantage);

            // and the roll takes it: the higher of two d20s
            IReadOnlyList<InitiativeRoll> rolled = Initiative.Roll(new StandardResolver(new ScriptedRng(3, 17)), new[] { beast.Actor });
            Assert.Equal(17, rolled.Single().Roll.Natural);
        }

        static Creation.Creation Suggested(Creation.Creation making)
        {
            making.SuggestTraits();
            return making;
        }

        // an Elf's Keen Senses and a Gentry's any are two lists: a pick only the Gentry's offers uses the Gentry's,
        // and the Elf's is still one of its three
        [Fact]
        public void APickLeavesEverySourceAbleToHaveItsOwn()
        {
            Creation.Creation making = Making("fighter", "elf", "elf_wood", "gentry");

            Assert.True(making.PickTraitSkill(Skill.History));
            Assert.Equal(new[] { Skill.Insight, Skill.Perception, Skill.Survival }.OrderBy(s => (int)s),
                         making.TraitSkillChoices);

            // a new background puts back only the picks that still fit
            Assert.True(making.Pick(Srd.Background("trader")));
            Assert.Equal(new[] { Skill.History }, making.TraitSkills);

            Assert.True(making.Pick(Srd.Background("soldier")));
            Assert.Empty(making.TraitSkills);
        }

        // --- the backgrounds and their kits ------------------------------------------------------------------------

        [Fact]
        public void KathleensBackgroundsAreHersAndEachIsTwoSkillsAndThreeAbilities()
        {
            foreach (string id in new[] { "homesteader", "trader", "gravedigger", "explorer", "gentry" })
            {
                Background b = Srd.Background(id);

                Assert.True(b.NotInSrd, id);
                Assert.Equal(2, b.Skills.Count + b.SkillPicks);
                Assert.Equal(3, b.Abilities.Count);
                Assert.Equal(50, b.GoldInstead);
            }

            Assert.Equal(new[] { Skill.Persuasion }, Srd.Background("gentry").Skills);
            Assert.Empty(Srd.Background("gentry").SkillChoices);
            Assert.Equal(7, Srd.Background("trader").SkillChoices.Count);
        }

        [Fact]
        public void AGravediggersKitIsHerListAndTheFlavourTakesNoSlot()
        {
            Creation.Creation making = Suggested(Making("fighter", "human", null, "gravedigger"));
            making.PickClassKit(KitChoice.Gold);

            Hero hero = Finish(making);

            Assert.Equal(3, hero.Pack.CountOf("oil"));
            Assert.Equal(1, hero.Pack.CountOf("shovel"));
            Assert.Equal(1, hero.Pack.CountOf("hooded_lantern"));
            Assert.Equal(1, hero.Pack.CountOf("common_clothes"));

            // the clothes are a quest item: flavour, no slot (inventory_decisions.md)
            Assert.Contains(hero.Pack.QuestItems, s => s.Item.Id == "common_clothes");
            Assert.DoesNotContain(hero.Pack.Stacks, s => s.Item.Id == "common_clothes");

            // her 18 gold, beside the class's gold instead of its gear
            Assert.Equal(Coins.FromGold(Srd.Class("fighter").GoldInstead + 18), hero.Pack.Copper);
        }

        [Fact]
        public void EveryItemHerKitsNameIsAnItemAndTheTwoTheSrdLacksAreMarked()
        {
            foreach (Background b in Srd.Backgrounds.Where(b => b.NotInSrd))
                foreach (string gear in b.Gear)
                    Assert.True(Srd.Items.Has(gear), $"{b.Id}: {gear}");

            Assert.True(Srd.Items.Find("signet_ring").NotInSrd);
            Assert.True(Srd.Items.Find("common_clothes").NotInSrd);
            Assert.False(Srd.Items.Find("shovel").NotInSrd);
        }

        // --- the Fairy's Flutter in a fight ---------------------------------------------------------------------

        // the hero at the '@' (0,0) in a field of difficult ground; a goblin far off
        const string Brambles = @"
+-+-+-+-+-+-+-+-+-+-+
|@ ~ ~ ~ ~ ~ ~ ~ ~ ~|
+ + + + + + + + + + +
|~ ~ ~ ~ ~ ~ ~ ~ ~ ~|
+ + + + + + + + + + +
|~ ~ ~ ~ ~ ~ ~ ~ ~ ~|
+-+-+-+-+-+-+-+-+-+-+";

        static CombatSession Fight(Hero hero, int goblinX = 9, int goblinY = 2)
        {
            Assert.True(MapReader.TryRead(Brambles, out MapLayout map, out string problem), problem);

            var resolver = new TableResolver(new ScriptedRng(1), new Always(20), a => ReferenceEquals(a, hero.Actor));
            Battle battle = Battle.Set(Srd, map, hero,
                                       new[] { new Battle.Foe(Srd.Bestiary.Find("goblin"), new Cell(goblinX, goblinY)) },
                                       resolver, new PolicyChooser(new ReactionSettings(), new AnswerAlways(false)));

            var session = new CombatSession(battle, Srd.Items, Srd.Forms);
            session.Start();
            return session;
        }

        static Hero Fairy() => Finish(Suggested(Making("fighter", "fairy", null, "soldier")));

        static ActionOption Flutter(CombatSession session) =>
            session.Options().Single(o => o.Feature?.Id == "fairy_flutter");

        [Fact]
        public void AFlutterFliesTwentyFeetOverDifficultGroundAndSpendsAUse()
        {
            Hero fairy = Fairy();
            CombatSession session = Fight(fairy);
            Feature flutter = fairy.FeatureCalled("fairy_flutter");
            int uses = fairy.UsesLeft(flutter);

            // walking, the brambles cost double: three squares
            Assert.True(session.Reachable().ContainsKey(new Cell(3, 0)));
            Assert.False(session.Reachable().ContainsKey(new Cell(4, 0)));

            ActionOption option = Flutter(session);
            Assert.True(option.Enabled);
            Assert.Equal(Targeting.Square, option.Targeting);
            Assert.True(session.Select(option));

            // flying, they cost nothing extra, and it goes 20 feet: four squares, not five
            Assert.Contains(new Cell(4, 0), session.LegalSquares());
            Assert.DoesNotContain(new Cell(5, 0), session.LegalSquares());
            Assert.True(session.Preview(System.Array.Empty<Actor>(), new Cell(4, 0)).Template.Count > 1);

            ActionResult flown = session.Confirm(new Cell(4, 0));

            Assert.True(flown.Done);
            Assert.Equal(new Cell(4, 0), session.Fight.Field.Where(fairy.Actor));
            Assert.Equal(uses - 1, fairy.UsesLeft(flutter));

            // it landed: the Flying status is off, and the rest of the move is the walk's
            Assert.False(fairy.Actor.IsFlying);
            Assert.Equal(10, session.Turn.Movement);
        }

        [Fact]
        public void AFlutterUsesTheMovementLeft()
        {
            Hero fairy = Fairy();
            CombatSession session = Fight(fairy);

            // two squares of brambles walked, four squares' worth of the move: two left, so the flight goes two, not four
            Assert.True(session.Move(new Cell(0, 2)).Done);
            Assert.Equal(10, session.Turn.Movement);

            Assert.True(session.Select(Flutter(session)));
            Assert.Contains(new Cell(2, 2), session.LegalSquares());
            Assert.DoesNotContain(new Cell(3, 2), session.LegalSquares());
        }

        [Fact]
        public void AFlutterProvokesAsWalkingDoes()
        {
            Hero fairy = Fairy();
            CombatSession session = Fight(fairy, goblinX: 1, goblinY: 0);
            Actor goblin = session.Battle.Monsters.Single();

            Assert.Equal(1, session.Fight.ReactionsLeft(goblin));

            // out of the goblin's reach in one flight: it takes its opportunity attack, as it would of a walk
            Assert.True(session.Select(Flutter(session)));
            Assert.True(session.Confirm(new Cell(0, 2)).Done);

            Assert.Equal(0, session.Fight.ReactionsLeft(goblin));
        }

        sealed class Always : IDiceSource
        {
            readonly int _face;

            public Always(int face) => _face = face;

            public IReadOnlyList<int> Throw(IReadOnlyList<Die> dice) =>
                dice.Select(d => System.Math.Min(_face, d.Sides())).ToList();
        }

        static bool Trained(Hero hero, Skill skill) => hero.Actor.TrainingIn(skill) != Training.Untrained;
    }
}
