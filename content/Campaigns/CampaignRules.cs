using Core.Combat;

namespace Content.Campaigns
{
    // HOW A CAMPAIGN PLAYS, where it differs from the game as it always was (cc_task_i Parts 3 and 4): pack.json's
    // "defeat", "leveling", "characters", "starting_shop" and "consequences". Each one's default is the game's
    public sealed class CampaignRules
    {
        public static readonly CampaignRules Default = new CampaignRules();

        // the most characters a campaign can hold; the book's page is laid out for the default
        public const int MostCharacters = 20;

        // what losing a fight means; a fight entry can say otherwise for itself
        public Defeat Defeat { get; init; } = Defeat.Death;

        public Leveling Leveling { get; init; } = Leveling.Milestone;

        // character slots for this campaign (the game's five, SaveLibrary.CharactersPerCampaign)
        public int Characters { get; init; } = Saves.SaveLibrary.CharactersPerCampaign;

        // whether a new hero opens on the starting shop; the beginner tutorial teaches shopping in its story
        public bool StartingShop { get; init; } = true;

        // the campaign's consequences/: added to the game-wide pool, or in place of it
        public Layering Consequences { get; init; } = Layering.Add;

        // what losing this fight means: the fight's own word, else the campaign's
        public Defeat For(Core.Tables.EncounterEntry fight) =>
            fight != null && fight.Defeat != Defeat.Unsaid ? fight.Defeat : Defeat;

        public override string ToString() =>
            $"defeat {Defeat}, {Leveling}, {Characters} characters" + (StartingShop ? "" : ", no starting shop") +
            $", consequences {Consequences}";
    }
}
