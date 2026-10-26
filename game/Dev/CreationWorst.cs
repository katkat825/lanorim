using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Creation;
using Content.Schema;
using Content.Sheet;
using Content.Spells;
using Content.Species;
using Core.Magic;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Dev
{
    // THE CREATION PAGES AT THEIR LONGEST (cc_task_m3 Part 1), for check-layout: the pages that grow with what was picked,
    // each opened with the picks that make it longest, so Next and Back are proved on screen at the worst, not the usual.
    // - Equipment: the class and background whose two kits give the most lines (both kits taken as gear);
    // - Traits: the species (or lineage) and background with the most trait buttons and the longest trait descriptions;
    // - Cantrips and Spells: every casting class's choices, the one with the longest spell card selected to read.
    public static class CreationWorst
    {
        public static readonly Step[] Pages = { Step.Equipment, Step.Traits, Step.Cantrips, Step.Spells };

        public static void Show(Launch launch, Step step)
        {
            launch.ShowCreation(GameState.Manifests.FirstOrDefault()?.Id ?? "");

            CreationScreen creation = Nodes.Under<CreationScreen>(launch).FirstOrDefault();
            if (creation == null) return;

            Library library = GameState.Content;
            Creation making = creation.Making;

            switch (step)
            {
                case Step.Equipment:
                    (CharacterClass cls, Background background) = LongestKit(library);
                    making.Pick(cls);
                    QuickHero.Defaults(making, library, learn: true);
                    making.Pick(background);
                    break;

                case Step.Traits:
                    making.Pick(library.Class("fighter"));
                    QuickHero.Defaults(making, library, learn: true);
                    (Kind species, Kind lineage, Background most) = MostTraits(library);
                    making.Pick(most);
                    making.Pick(species);
                    if (lineage != null) making.PickLineage(lineage);
                    break;

                default:
                    (CharacterClass caster, Spell longest) = LongestCard(library, step == Step.Cantrips);
                    making.Pick(caster);
                    QuickHero.Defaults(making, library, learn: false);
                    creation.Open(step);
                    Read(creation, longest);
                    GD.Print($"layout  worst {step}: {caster.Id}, reading {longest.Id}");
                    return;
            }

            GD.Print($"layout  worst {step}: {making.Class?.Id} {making.Species?.Id} {making.Lineage?.Id} {making.Background?.Id}");
            creation.Open(step);
        }

        // the class and background whose kits make the longest one list
        public static (CharacterClass, Background) LongestKit(Library library)
        {
            var making = new Creation(library, library.Backgrounds);

            return library.Classes.SelectMany(c => library.Backgrounds.Select(b => (c, b)))
                          .OrderByDescending(pair =>
                          {
                              making.Pick(pair.c);
                              making.Pick(pair.b);
                              return making.StartingGear.Count;
                          })
                          .First();
        }

        // the species, lineage and background that put the most on the Traits page
        static (Kind, Kind, Background) MostTraits(Library library)
        {
            var making = new Creation(library, library.Backgrounds);
            making.Pick(library.Class("fighter"));

            var all = new List<(Kind, Kind, Background, int)>();

            foreach (Background background in library.Backgrounds)
                foreach (Kind species in making.SpeciesChoices.ToList())
                {
                    making.Pick(background);
                    making.Pick(species);

                    foreach (Kind lineage in making.NeedsLineage ? making.LineageChoices.ToList() : new List<Kind> { null })
                    {
                        if (lineage != null) making.PickLineage(lineage);
                        if (!making.HasTraits) continue;

                        int size = (making.TraitSkills.Count + making.TraitSkillChoices.Count() + making.SpellAbilityChoices.Count +
                                    (making.ChoosesSize ? making.SizeChoices.Count : 0)) * 60 +
                                   making.TraitFeatures.Sum(f => Ui.Say(f.NameKey).Length + Ui.Say(f.DescriptionKey).Length);

                        all.Add((species, lineage, background, size));
                    }
                }

            (Kind s, Kind l, Background b, int _) = all.OrderByDescending(t => t.Item4).First();
            return (s, l, b);
        }

        // the casting class and the spell (or cantrip) on its page with the longest card
        static (CharacterClass, Spell) LongestCard(Library library, bool cantrips)
        {
            var making = new Creation(library, library.Backgrounds);

            return library.Classes.Where(c => c.Casts)
                          .SelectMany(c =>
                          {
                              making.Pick(c);
                              return making.SpellChoices.Where(s => s.IsCantrip == cantrips).Select(s => (c, s)).ToList();
                          })
                          .OrderByDescending(pair => SpellCardText.Of(SpellCard.Of(pair.s)).Length)
                          .First();
        }

        // the spell's name focused, as a player reading down the list selects one (after the page's own first focus)
        static void Read(CreationScreen creation, Spell spell)
        {
            string name = Ui.Say(spell.NameKey);

            Button title = Nodes.Under<Button>(creation).FirstOrDefault(b => b.ToggleMode && b.Text.EndsWith(name));
            if (title != null) Ui.FocusLater(title);
        }
    }
}
