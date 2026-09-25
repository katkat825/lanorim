using System;
using System.Collections.Generic;
using System.Linq;
using Content.Items;
using Core.Characters;
using Core.Dice;
using Core.Magic;

namespace Content.Saves
{
    // WHAT A SAVED GAME IS. Ids and numbers, never objects: the statblock says what a goblin IS,
    // and this says what happened to THIS one. So retuning a monster retunes every save made
    // against it, and a save is small enough to read in a text editor when something goes wrong.
    //
    // ADAPTED from the old build's SaveGame, which is where the discipline comes from. What went is
    // the homebrew it was written for - Vigor, Nerve, Strain, notches, the trait-die growth ladder -
    // and the World layer's places and facts. SRD needs different things and fewer of them: hit
    // points, temporary hit points, hit dice, the spell resource, conditions, and where the
    // piece is standing.
    // why a save was written: the player asked, or something happened worth coming back to
    public enum SaveKind
    {
        Manual,
        ChapterStart,
        FightStart,
        FightWon,
        Rest,
        LevelUp,
        Quit,
    }

    public sealed class SaveGame
    {
        public string Campaign { get; set; } = "";

        public int CampaignFormat { get; set; }

        public string Chapter { get; set; } = "";

        // which map the party is standing on, by the campaign's own local id
        public string Map { get; set; } = "";

        public int Format { get; set; } = SaveFormat.Current;

        public Version Engine { get; set; } = Core.EngineVersion.Current;

        // the fight, when there is one. Round 0 means there is not.
        public int Round { get; set; }

        public int Turn { get; set; } = -1;

        public int ActionsLeft { get; set; }

        public SavedHero Hero { get; set; }

        // THE STORY: which Yarn node it is on, and every story variable, by type (Yarn's three).
        // what makes a loaded game pick up the conversation where it left it
        public string Node { get; set; } = "";

        public IDictionary<string, float> Numbers { get; } = new SortedDictionary<string, float>(StringComparer.Ordinal);

        public IDictionary<string, string> Words { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public IDictionary<string, bool> Flags { get; } = new SortedDictionary<string, bool>(StringComparer.Ordinal);

        // WHERE IN THE NODE: every answer and choice since it began, in order, so a load replays
        // the node to the exact spot rather than from its top (which would run a won fight again).
        // with steps, the variables above are the ones the node began with
        public IList<string> Steps { get; } = new List<string>();

        // what made it: an autosave on an event, or the player's own (docs/decisions_checklist.md
        // section 3: autosave on events plus manual saves)
        public SaveKind Kind { get; set; } = SaveKind.Manual;

        // which of the campaign's five character slots
        public int Slot { get; set; }

        // the player's own words for a manual save; empty for an autosave
        public string Label { get; set; } = "";

        public IList<SavedActor> Foes { get; } = new List<SavedActor>();

        public IList<SavedDie> Felt { get; } = new List<SavedDie>();

        public bool MidFight => Round > 0;

        public bool HasAHero => Hero != null && !string.IsNullOrEmpty(Hero.Name);

        public override string ToString() =>
            $"{Campaign}/{Map} format {Format}" +
            (HasAHero ? $", {Hero}" : ", nobody") +
            (MidFight ? $", round {Round}, {Foes.Count} foes, {Felt.Count} dice on the felt"
                      : ", not mid-fight");
    }

    // THE CHARACTER, not what the fight did to them - though both are here, because a resumed game
    // needs the sheet to rebuild the hero and the hit points to put them back where they were.
    public sealed class SavedHero
    {
        // the one string in the game that is neither authored nor translated
        public string Name { get; set; } = "";

        public string Class { get; set; } = "";

        public string Species { get; set; } = "";

        public string Lineage { get; set; } = "";

        public string Background { get; set; } = "";

        public string Alignment { get; set; } = "";

        public int Level { get; set; } = 1;

        // BASE scores, before anything shifted them. A shift lasts until a rest, so it is a thing
        // that happened rather than a thing the character is, and rebuilding from base and
        // re-applying is how a changed rule reaches an old save.
        //
        // Base here means the array AS PICKED - before the species, the background's spend and the
        // improvements - because all three are rules, and HeroSaves re-applies them on the way in.
        public IDictionary<Ability, int> Scores { get; } = new Dictionary<Ability, int>();

        // the +2/+1 or +1/+1/+1 the player spent. It is the player's choice rather than the
        // background's, so it cannot be read back off the background and has to be written down.
        public IDictionary<Ability, int> BackgroundSpend { get; } = new Dictionary<Ability, int>();

        // every skill the character is trained in, and the ones doubled. Expertise is a subset.
        public IList<Skill> Skills { get; } = new List<Skill>();

        public IList<Skill> Expertise { get; } = new List<Skill>();

        // THE ABILITY SCORE IMPROVEMENTS THE PLAYER SPENT, in order: "str" is +2 Strength,
        // "dex+con" is +1 to each. The player's choices, so they are written down - Build does not
        // spend them on its own any more (decisions_checklist.md section 1, 2026-09-24).
        public IList<string> Improvements { get; } = new List<string>();

        // false for a save written before improvements were saved at all: its improvements were
        // the automatic ones, and it loads with them pending (and a caution) rather than re-spent
        public bool ImprovementsRecorded { get; set; } = true;

        // how many were waiting to be spent when it was saved - a check on the list above
        public int PendingImprovements { get; set; }

        // the player said not to warn this character about discards again
        public bool DiscardWarningDismissed { get; set; }

        public int HitPoints { get; set; } = -1;

        public int TemporaryHitPoints { get; set; }

        public int HitDice { get; set; } = -1;

        // WHICH WAY THIS CHARACTER PAYS FOR LEVELED SPELLS, and what is left of it today. The mode
        // is part of the character - chosen at creation and kept for life - so it is saved beside
        // the class and the species rather than with the day's damage.
        public SpellResourceMode Resource { get; set; } = SpellResourceMode.Slots;

        // MODE A: slots remaining, by slot level. Index 0 is 1st-level slots. Only what is LEFT is
        // written; the maxima come back off the class table, so a retuned table reaches old saves.
        public IList<int> Slots { get; } = new List<int>();

        // MODE B: points left, and which 6+ levels have had their one cast today. -1 is "the pool
        // this character's class and level gives", which is how an untouched caster saves.
        public int Points { get; set; } = -1;

        public IList<int> SpentHighLevels { get; } = new List<int>();

        // spell ids, in the order they were learned
        public IList<string> Known { get; } = new List<string>();

        public IList<Condition> Conditions { get; } = new List<Condition>();

        // USES SPENT SINCE THE LAST REST, by feature id. Spent rather than left, for the same
        // reason slots save what is left rather than the maximum: the class says how many a
        // feature has, and a retuned feature should reach a save without the save arguing.
        public IDictionary<string, int> Spent { get; } = new Dictionary<string, int>();

        // the per-rest extra actions still banked - Action Surge. -1 is "all of them", which is
        // how a hero who has not surged since the rest saves.
        public int ExtraActions { get; set; } = -1;

        // the Wild Shape card being worn, or empty. Its statblock is the card's, so only the id.
        public string Form { get; set; } = "";

        // item ids by the slot they are worn in
        public IDictionary<Slot, string> Worn { get; } = new Dictionary<Slot, string>();

        // what is in the pack: item id and how many
        public IList<SavedStack> Pack { get; } = new List<SavedStack>();

        public int Gold { get; set; }

        public int? X { get; set; }

        public int? Y { get; set; }

        public bool OnTheBoard => X.HasValue && Y.HasValue;

        public override string ToString() =>
            $"{Name} the level {Level} {Species} {Class}".TrimEnd() +
            (HitPoints >= 0 ? $", {HitPoints} hp" : "");
    }

    // ids, not values: the statblock says what a goblin is, this says what happened to this one
    public sealed class SavedActor
    {
        public string Id { get; set; } = "";

        // how a save names one of four identical foes
        public int Ordinal { get; set; }

        // STORED, NOT DERIVED. Initiative order is settled by a roll and a tie-break, and a changed
        // tie-break rule would otherwise silently re-order every save that had one. -1 before the
        // fight began.
        public int Seat { get; set; } = -1;

        public int Initiative { get; set; }

        // -1 means the statblock's own, which is how an untouched foe saves
        public int HitPoints { get; set; } = -1;

        public int TemporaryHitPoints { get; set; }

        public IList<Condition> Conditions { get; } = new List<Condition>();

        // null for an actor not on the board - fallen, or waiting off it
        public int? X { get; set; }

        public int? Y { get; set; }

        public bool OnTheBoard => X.HasValue && Y.HasValue;

        public override string ToString() =>
            $"{Id}" + (Ordinal > 0 ? $"#{Ordinal}" : "") + $" hp {HitPoints}" +
            (OnTheBoard ? $" on ({X}, {Y})" : " off the board");
    }

    public sealed class SavedStack
    {
        public SavedStack() { }

        public SavedStack(string item, int count)
        {
            Item = item ?? "";
            Count = count;
        }

        public string Item { get; set; } = "";

        public int Count { get; set; } = 1;

        public override string ToString() => Count > 1 ? $"{Item} x{Count}" : Item;
    }

    // the dice as they were left lying on the felt. Not referenced from the Godot project's own
    // die type, because this assembly must never reference Godot.
    public sealed class SavedDie
    {
        public SavedDie() { }

        public SavedDie(Die die, int value)
        {
            Die = die;
            Value = value;
        }

        public Die Die { get; set; } = Die.None;

        public int Value { get; set; }

        public override string ToString() => $"{Die.Label()}->{Value}";
    }
}
