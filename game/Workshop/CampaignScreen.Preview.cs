using System;
using System.Collections.Generic;
using System.Linq;
using Content.Workshop;
using Content.Workshop.Story;
using Core.Localization;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // PREVIEW (cc_task_o Part 5.1): a card read back in the real dialogue popup, speaker names and all - its lines one by
    // one, a note's options as the buttons they'd be (the one picked walks into its blocks), and each command said as
    // what the GM would do there, "(the GM would check Athletics here)". A feel for the words, not a play-test (Q)
    public partial class CampaignScreen
    {
        public DialoguePopup PreviewPopup { get; private set; }

        void Preview(string title)
        {
            CardDraft card = Draft.Story.Card(title);
            if (card == null || card.IsText) return;

            var picks = new Dictionary<Choice, int>();
            int at = 0;

            var layer = new CanvasLayer { Name = "Preview", Layer = 50 };
            var shade = new ColorRect { Color = new Color(0, 0, 0, 0.35f), MouseFilter = Control.MouseFilterEnum.Stop };
            shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var popup = new DialoguePopup { Name = "PreviewPopup" };

            Button close = Ui.Button(WorkshopWords.CancelKey, () => { layer.QueueFree(); PreviewPopup = null; });
            close.Name = "ClosePreview";
            close.Position = new Vector2(Ui.Px(24), Ui.Px(24));

            layer.AddChild(shade);
            layer.AddChild(popup);
            layer.AddChild(close);
            AddChild(layer);
            PreviewPopup = popup;

            void Show()
            {
                List<PreviewStep> steps = PreviewStep.Of(card.Blocks, card.Speaker, c => picks.TryGetValue(c, out int p) ? p : 0).ToList();

                if (at >= steps.Count)
                {
                    layer.QueueFree();
                    PreviewPopup = null;
                    return;
                }

                PreviewStep step = steps[at];
                string speaker = step.Speaker == StoryWords.Gm ? null : SpeakerName(step.Speaker);

                if (step.IsNote)
                {
                    Choice note = Content.Workshop.Story.Blocks.All(card.Blocks).OfType<Choice>().First(c => c.Options.Select(o => o.Text).SequenceEqual(step.Options));
                    popup.Fill(speaker, "", false,
                               step.Options.Select((o, i) => (o, true, (Action)(() => { picks[note] = i; at++; Show(); }))), null);
                }
                else if (step.Skipped != null)
                    popup.Fill(null, Ui.Say(BlockWords.WouldKey, Would(step.Skipped)), true, null, () => { at++; Show(); });
                else
                    popup.Fill(speaker, step.Text.Replace(StoryWords.HeroName, Ui.Say(StoryWords.HeroNameKey)), true, null, () => { at++; Show(); });
            }

            // the popup is in the tree (and ready) since the layer was added: its first line now
            Show();
        }

        // a speaker's name plate: the companion's word, a character's name
        string SpeakerName(string speaker) =>
            speaker == StoryWords.Companion ? Ui.Say(StoryWords.CompanionKey)
            : Draft.Character(speaker) is { Name.Length: > 0 } person ? person.Name
            : Ui.Say(KeyConventions.ActorName(speaker)) is var said && said != KeyConventions.ActorName(speaker) ? said
            : Content.Workshop.Tables.TablesDraft.Plain(speaker);

        // what a command does, in words: "check Athletics, Medium", "start the fight Goblin camp"
        string Would(Do act)
        {
            string[] args = act.Args;
            string first = args.FirstOrDefault() ?? "";

            object named = act.Verb switch
            {
                "check" => Ui.Say(KeyConventions.Key("skill", first, "name")) is var s && s.StartsWith("skill.") ? Ui.Say(KeyConventions.Key("ability", first, "name")) : s,
                "save" => Ui.Say(KeyConventions.Key("ability", first, "name")),
                "fight" => Draft.Tables.NameOf("fight", first),
                "encounter" => Draft.Tables.NameOf("encounters", first),
                "loot" => Draft.Tables.NameOf("loot", first),
                "give" => Pickers.ItemName(first),
                "bark" => Ui.Say(BlockWords.Pool(Enum.TryParse(first, true, out Content.Dialogue.Bark b) ? b : Content.Dialogue.Bark.Danger)),
                "rest" => Ui.Say(BlockWords.Rest(first == "long" ? "long" : "short")),
                _ => first,
            };

            return Ui.Say(BlockWords.Does(act.Verb), named, args.Length > 1 ? args[1] : "");
        }
    }
}
