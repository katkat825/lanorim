using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Combat;
using Content.Items;
using Content.Schema;
using Content.Screens;
using Content.Sheet;
using Content.Spells;
using Core.Characters;
using Core.Combat;
using Core.Localization;
using Core.Magic;

namespace Content.Tests
{
    // THE FULL CHARACTER SHEET AND THE SPELL CARDS (cc_task_cs): what SheetView grew, the sheet's two buttons going down
    // the HUD's own paths, every spell's card filled in, and the spells page
    public partial class CombatSessionTests
    {
        // --- the sheet's Swap and End concentration are the HUD's options (cc_task_cs 1.2) -----------------

        [Fact]
        public void TheSheetsSwapIsTheHudsOptionAndCostsAnAction()
        {
            Hero hero = Made("fighter");
            hero.Pack.Take(ItemCalled("handaxe"));

            CombatSession session = Session(hero, new[] { Goblin(8, 1) });
            var acts = new SheetActs(hero, session);

            ActionOption fromTheSheet = acts.SwapOption("handaxe");
            ActionOption onTheHud = new Content.Screens.CombatHud(session).Manoeuvres.Single(o => o.Id == "swap:handaxe");

            Assert.NotNull(fromTheSheet);
            Assert.Equal(onTheHud.Id, fromTheSheet.Id);
            Assert.Equal(OptionKind.Swap, fromTheSheet.Kind);
            Assert.True(acts.CanSwap("handaxe"));

            int actions = session.Turn.Actions;

            // what the combat director's Pick does with an option that aims at nothing
            Assert.True(session.Take(fromTheSheet).Done);

            Assert.Equal("handaxe", hero.Equipment.In(Slot.MainHand)?.Id);
            Assert.Equal(actions - 1, session.Turn.Actions);
            Assert.Equal(1, SheetView.Of(hero, session.Fight, session.Turn).SwapCostActions);
        }

        [Fact]
        public void WithNoActionLeftTheSheetsSwapIsGreyAndSaysWhy()
        {
            Hero hero = Made("fighter");
            hero.Pack.Take(ItemCalled("handaxe"));

            CombatSession session = Session(hero, new[] { Goblin(8, 1) });

            while (session.Turn.Can(Spend.Action)) session.Turn.Take(Spend.Action);

            var acts = new SheetActs(hero, session);

            Assert.False(acts.CanSwap("handaxe"));
            Assert.Equal(CombatSession.Why("no_action"), acts.SwapWhyNotKey("handaxe"));
        }

        [Fact]
        public void TheSheetsEndConcentrationIsTheHudsOptionAndLetsTheSpellGo()
        {
            Hero hero = Made("cleric", 3, "bless");
            CombatSession session = Session(hero, new[] { Goblin(8, 1) });

            Assert.Null(new SheetActs(hero, session).EndConcentrationOption);

            Spell bless = Srd.Spells.Find("bless");
            Assert.True(session.Battle.Magic.Cast(hero.Caster, bless, Aim.At(hero.Actor), 1).Cast);
            Assert.Equal("bless", hero.Actor.Concentrating);
            Assert.NotEmpty(hero.Actor.Boons.All.Where(b => b.Source == "bless"));

            var acts = new SheetActs(hero, session);
            ActionOption fromTheSheet = acts.EndConcentrationOption;
            ActionOption onTheHud = new Content.Screens.CombatHud(session).Manoeuvres
                                                                         .Single(o => o.Kind == OptionKind.EndConcentration);

            Assert.Equal(onTheHud.Id, fromTheSheet.Id);
            Assert.Equal(Spend.Free, fromTheSheet.Cost);
            Assert.True(acts.CanEndConcentration);

            int actions = session.Turn.Actions;
            Assert.True(session.Take(fromTheSheet).Done);

            Assert.False(hero.Actor.IsConcentrating);
            Assert.Empty(hero.Actor.Boons.All.Where(b => b.Source == "bless"));
            Assert.Equal(actions, session.Turn.Actions);
            Assert.Null(SheetView.Of(hero, session.Fight, session.Turn).ConcentratingKey);
        }

        // ANY EQUIP FROM ANY SOURCE COSTS AN ACTION IN A FIGHT (Kathleen, 2026-11-03): the pack's Equip, Off hand and
        // Unequip go through the same ChangeGear as the sheet's Swap
        [Fact]
        public void InAFightThePacksEquipOffHandAndUnequipEachCostAnAction()
        {
            Hero hero = Made("fighter");
            hero.Pack.Take(ItemCalled("handaxe"));
            hero.Pack.Take(ItemCalled("dagger"));

            CombatSession session = Session(hero, new[] { Goblin(8, 1) });
            var pack = new PackView(hero, Srd.Items, fight: session);

            int actions = session.Turn.Actions;

            Assert.True(pack.Equip("handaxe"));
            Assert.Equal(actions - 1, session.Turn.Actions);

            Assert.True(pack.TakeOff(Slot.MainHand));
            Assert.Equal(actions - 2, session.Turn.Actions);

            // both actions spent: the pack says why, and nothing changes
            Assert.Equal(CombatSession.Why("no_action"), pack.GearWhyNotKey);
            Assert.False(pack.CanEquip("handaxe"));
            Assert.True(pack.Fits("handaxe"));
            Assert.False(pack.Equip("handaxe"));
            Assert.False(pack.HoldInOffHand("dagger"));
            Assert.Null(hero.Equipment.In(Slot.MainHand));
        }

        [Fact]
        public void OutsideAFightThePacksEquipIsStillFree()
        {
            Hero hero = Made("fighter");
            hero.Pack.Take(ItemCalled("handaxe"));

            var pack = new PackView(hero, Srd.Items);

            Assert.Null(pack.GearWhyNotKey);
            Assert.True(pack.Equip("handaxe"));
            Assert.True(pack.TakeOff(Slot.MainHand));
        }

        [Fact]
        public void OutsideAFightTheSheetsSwapIsThePacksEquipAndFree()
        {
            Hero hero = Made("fighter");
            hero.Pack.Take(ItemCalled("handaxe"));

            var acts = new SheetActs(hero);

            Assert.Null(acts.SwapOption("handaxe"));
            Assert.True(acts.CanSwap("handaxe"));
            Assert.Null(acts.SwapWhyNotKey("handaxe"));
            Assert.True(acts.SwapNow("handaxe"));
            Assert.Equal("handaxe", hero.Equipment.In(Slot.MainHand)?.Id);

            // what came off went back in the pack, and can be swapped back
            Assert.Contains(SheetView.Of(hero).SpareWeapons, i => i.Kind == Content.Items.ItemKind.Weapon);
            Assert.Equal(0, SheetView.Of(hero).SwapCostActions);
        }
    }

    // the rest of the sheet and the cards, kept in the same class for its heroes (Made) and its fights
    public partial class CombatSessionTests
    {
        static readonly IReadOnlyDictionary<string, string> English =
            Locale.Read(File.Exists("game.csv") ? File.ReadAllText("game.csv") : "");

        // --- what SheetView grew -------------------------------------------------------------------------

        [Fact]
        public void TheSheetListsTheWeaponsInThePackToSwapTo()
        {
            Hero hero = Made("fighter", 1);
            hero.Pack.Take(Srd.Items.Find("handaxe"));
            hero.Pack.Take(Srd.Items.Find("handaxe"));

            SheetView sheet = SheetView.Of(hero);

            Assert.Single(sheet.SpareWeapons, i => i.Id == "handaxe");
            Assert.All(sheet.SpareWeapons, i => Assert.Equal(Content.Items.ItemKind.Weapon, i.Kind));
        }

        [Fact]
        public void AFeaturesUsesAreTheHerosLevelsAndSayWhatBringsThemBack()
        {
            SpecialRow wind = SheetView.Of(Made("fighter", 4)).Special.Single(s => s.Feature.Id == "second_wind");

            // SRD 5.2.1: two Second Winds, three from level 4
            Assert.Equal(3, wind.UsesMost);
            Assert.Equal(3, wind.UsesLeft);
            Assert.Equal("ui.recharge.one_per_short_rest.name", wind.RechargeKey);
            Assert.Equal(SpellCard.SpendKeyOf(Spend.Bonus), wind.UseTimeKey);
        }

        [Fact]
        public void ALevelTwentyHerosFeaturesAllHaveACard()
        {
            foreach (string cls in new[] { "fighter", "rogue", "cleric", "mage", "paladin", "druid", "barbarian", "monk" })
            {
                if (Srd.Class(cls) == null) continue;

                foreach (SpecialRow row in SheetView.Of(Made(cls, 20)).Special)
                {
                    Assert.True(English.ContainsKey(row.NameKey), $"{cls}: {row.NameKey} has no English");
                    Assert.True(English.ContainsKey(row.DescriptionKey), $"{cls}: {row.DescriptionKey} has no English");
                    Assert.True(English.ContainsKey(row.UseTimeKey), $"{cls}: {row.UseTimeKey} has no English");
                    Assert.True(row.RechargeKey == null || English.ContainsKey(row.RechargeKey), $"{cls}: {row.RechargeKey}");
                    Assert.True(row.UsesLeft <= row.UsesMost);
                }
            }
        }

        // --- every spell's card (cc_task_cs 2.1, 2.4) -------------------------------------------------------

        [Fact]
        public void EveryV1SpellHasACardWithEveryFieldFilled()
        {
            var missing = new List<string>();

            foreach (Spell spell in SpellBook.Srd().All)
            {
                SpellCard card = SpellCard.Of(spell);

                IEnumerable<string> keys = new[]
                {
                    card.NameKey, card.DescriptionKey, card.LevelKey, card.SchoolKey, card.CastingTimeKey, card.RangeKey,
                    card.DurationKey, card.ComponentsKey, card.EffectKey,
                }.Concat(card.SoloKeys);

                foreach (string key in keys)
                    if (key == null || !English.ContainsKey(key) || string.IsNullOrWhiteSpace(English[key]))
                        missing.Add($"{spell.Id}: {key ?? "(none)"}");
            }

            Assert.True(missing.Count == 0, "spells missing card words:\n" + string.Join("\n", missing));
        }

        [Fact]
        public void ACardsEffectIsTheSpellsOwnDamageHealingOrCondition()
        {
            SpellCard fireball = SpellCard.Of(Srd.Spells.Find("fireball"));
            Assert.Equal(SpellCardEffect.DamageKey, fireball.EffectKey);
            Assert.Equal("8d6", fireball.EffectDice.ToString());
            Assert.Equal(DamageType.Fire.NameKey(), fireball.EffectWordKey);

            SpellCard cure = SpellCard.Of(Srd.Spells.Find("cure_wounds"));
            Assert.Equal(SpellCardEffect.HealKey, cure.EffectKey);
            Assert.False(cure.EffectDice.IsNothing);

            SpellCard hold = SpellCard.Of(Srd.Spells.Find("hold_person"));
            Assert.Equal(SpellCardEffect.ConditionKey, hold.EffectKey);
            Assert.Equal(Condition.Paralyzed.NameKey(), hold.EffectWordKey);
        }

        [Fact]
        public void ACantripsEffectGrowsWithTheCastersLevel()
        {
            Hero five = Made("mage", 5, "fire_bolt");

            Assert.Equal("1d10", SpellCard.Of(Srd.Spells.Find("fire_bolt")).EffectDice.ToString());
            Assert.Equal("2d10", SpellCard.Of(Srd.Spells.Find("fire_bolt"), five.Caster).EffectDice.ToString());
        }

        [Fact]
        public void ACardSaysTheSoloDeltasTheRulesHave()
        {
            Assert.Contains(SpellCard.SoloUnavailableKey, SpellCard.Of(Srd.Spells.Find("spare_the_dying")).SoloKeys);
            Assert.Empty(SpellCard.Of(Srd.Spells.Find("fireball")).SoloKeys);

            Spell longer = SpellBook.Srd().All.FirstOrDefault(s => s.OutOfCombat);

            if (longer != null)
            {
                SpellCard card = SpellCard.Of(longer);
                Assert.Contains(SpellCard.OutOfCombatKey, card.SoloKeys);
                Assert.Equal("ui.casting_time.out_of_combat.name", card.CastingTimeKey);
            }
        }

        // --- the sheet as sentences: what the page shows and the reader says (cc_task_cs 1.4) -----------------

        // a Said in English the way the game says it: a key argument in its words, a Said argument said whole
        static string Words(Said said) =>
            string.Format(English[said.Key], said.Args.Select(a => a switch
            {
                Said inner => Words(inner),
                string key when KeyConventions.IsWellFormed(key) => English[key],
                _ => a,
            }).ToArray());

        [Fact]
        public void EverySectionOfTheSheetReadsInWordsWithItsLabels()
        {
            Hero mage = Made("mage", 5, "fire_bolt", "magic_missile", "shield", "fireball", "hold_person");

            foreach (Hero hero in new[] { Made("fighter", 1), mage, Made("rogue", 20) })
            {
                SheetView sheet = SheetView.Of(hero);

                foreach (SheetReading.Section section in SheetReading.Front.Concat(SheetReading.Back(sheet)))
                {
                    Assert.True(English.ContainsKey(SheetReading.HeadingKey(section, sheet)));

                    IReadOnlyList<Said> lines = SheetReading.Lines(section, sheet);
                    Assert.NotEmpty(lines);

                    foreach (Said line in lines) Assert.False(string.IsNullOrWhiteSpace(Words(line)));
                }
            }

            SheetView fighter = SheetView.Of(Made("fighter", 1));
            List<string> basics = SheetReading.Lines(SheetReading.Section.Basics, fighter).Select(Words).ToList();

            Assert.Contains($"Armor Class {fighter.ArmorClass}", basics);
            Assert.Contains(basics, l => l.StartsWith("Initiative "));
            Assert.Contains(SheetReading.Lines(SheetReading.Section.Skills, fighter).Select(Words),
                            l => l.StartsWith("Passive Perception "));
            Assert.DoesNotContain(SheetReading.Section.Spells, SheetReading.Back(fighter));
        }

        [Fact]
        public void ASpellsRowSaysItsRollAndItsEffect()
        {
            Hero mage = Made("mage", 5, "fire_bolt", "fireball");
            SpellCard fireball = SheetView.Of(mage).Spells.Single(c => c.Spell.Id == "fireball");

            List<string> cells = SheetReading.SpellCells(fireball).Select(Words).ToList();

            Assert.Equal($"DC {mage.Caster.SaveDc} Dexterity", cells[2]);
            Assert.Equal("8d6 Fire", cells[3]);
            Assert.Equal("No concentration", cells[4]);
        }

        // --- the spells page (cc_task_cs 2.3) ----------------------------------------------------------------

        static string Say(string key) => English.TryGetValue(key, out string words) ? words : key;

        [Fact]
        public void TheSpellsPageListsEveryV1SpellByLevel()
        {
            var page = new SpellReference(SpellBook.Srd(), Say);

            Assert.Equal(SpellBook.Srd().All.Count(), page.Count);
            Assert.Equal(page.Count, page.ShownCount);
            Assert.Equal(page.Shown.Select(g => g.Level).OrderBy(l => l), page.Shown.Select(g => g.Level));
            Assert.Contains("mage", page.Classes);
        }

        [Fact]
        public void TheSpellsPageNarrowsByClassLevelAndWhatIsTyped()
        {
            var page = new SpellReference(SpellBook.Srd(), Say) { Class = "cleric" };

            Assert.All(page.Shown.SelectMany(g => g.Spells), s => Assert.Contains("cleric", s.Classes));

            page.Class = null;
            page.Level = 3;
            Assert.All(page.Shown, g => Assert.Equal(3, g.Level));

            page.Level = null;
            page.Search = "FIRE";
            List<Spell> fire = page.Shown.SelectMany(g => g.Spells).ToList();
            Assert.Contains(fire, s => s.Id == "fireball");
            Assert.Contains(fire, s => s.Id == "fire_bolt");
            Assert.DoesNotContain(fire, s => s.Id == "shield");

            page.Search = "no spell is called this";
            Assert.Empty(page.Shown);
        }
    }
}
