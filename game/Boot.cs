using System.Linq;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Godot;

namespace Game
{
    // A smoke test with a scene around it: does the Godot build actually load core and content,
    // can it read the SRD data, and does the locale answer in English?
    //
    // It prints to the Output panel and draws nothing. Everything visual is yours - this is here
    // so that when the table scene doesn't work, you can tell in one run whether the problem is
    // the scene or the engine underneath it.
    public partial class Boot : Node
    {
        public override void _Ready()
        {
            GD.Print("Lanorim boot");
            GD.Print("------------");

            Library srd = Library.Srd();

            GD.Print($"spells   {srd.Spells}");
            GD.Print($"items    {srd.Items}");
            GD.Print($"monsters {srd.Bestiary}");
            GD.Print($"classes  {srd.Classes.Count}: " +
                     string.Join(", ", srd.Classes.Select(c => c.Id)));
            GD.Print($"species  {srd.Playable.Count()}: " +
                     string.Join(", ", srd.Playable.Select(s => s.Id)));

            if (!srd.Sound)
            {
                GD.PrintErr($"{srd.Problems.Count} problems in the SRD data:");

                foreach (string problem in srd.Problems.Take(20)) GD.PrintErr("  " + problem);
            }

            GD.Print("");

            // the locale check, from inside Godot. check-locale.ps1 proves the CSV has English in
            // it; this proves Godot re-imported it. A raw key on screen means the .translation
            // beside the CSV is stale - run: godot --headless --path game --import
            string name = Tr("ability.str.name");

            GD.Print(name == "ability.str.name"
                         ? "LOCALE STALE: re-import with  godot --headless --path game --import"
                         : $"locale   reads '{name}' for ability.str.name");

            GD.Print($"fireball is '{Tr("spell.fireball.name")}'");

            GD.Print("");

            // and one whole character, built by the same code the creator screen will drive
            var making = new Content.Creation.Creation(srd, srd.Backgrounds);

            making.Pick(srd.Class("fighter"));
            making.Pick(srd.Kind("human"));
            making.Pick(srd.Background("soldier"));

            foreach (Skill skill in making.SkillChoices.Take(making.SkillPicksLeft))
                making.Train(skill);

            making.Call("Brenna");

            Hero hero = making.Finish();

            GD.Print(hero == null
                         ? "the creator could not finish a character: " +
                           string.Join("; ", making.Problems)
                         : "hero     " + hero);

            GD.Print("");
            GD.Print("engine up. the table scene is next, and it is yours to lay out.");
        }
    }
}
