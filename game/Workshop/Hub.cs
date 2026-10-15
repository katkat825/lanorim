using System;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Content.Workshop;
using Game.Campaigns;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE WORKSHOP'S HUB (cc_task_m Part 1): New campaign first, then the shelves - My campaigns (each a card: its cover,
    // name, chapters and when it was last changed, Open, Duplicate and Delete, and what's wrong with it), Recently
    // deleted when anything is, My maps (L's library; each opens the builder), Subscribed (empty until task R), and in the
    // editor build the game's own campaigns. On a panel over the open book; the look is Kathleen's (#5)
    public static class Hub
    {
        // `problem`: what went wrong with the last thing asked of it, said under the buttons (the disk's own words)
        public static void Show(Launch launch, string problem = null)
        {
            // anything deleted more than thirty days ago goes when the hub opens
            int expired = RecentlyDeleted.Expire(WorkshopPage.Mine, DateTime.Today);
            if (expired > 0) GD.Print($"workshop {expired} campaigns past their thirty days in Recently deleted, gone");

            var shelves = Ui.Column(14);

            Mine(launch, shelves);
            Deleted(launch, shelves);
            Maps(launch, shelves);

            shelves.AddChild(Ui.Title(WorkshopWords.SubscribedKey));
            shelves.AddChild(Forms.Hint(WorkshopWords.SubscribedEmptyKey));

            if (WorkshopPage.EditorBuild) GameCampaigns(launch, shelves);

            Button make = Ui.Button(WorkshopWords.NewCampaignKey, () => WizardScreen.Begin(launch));
            make.Name = "NewCampaign";

            var page = Ui.Column(12,
                Ui.Title(WorkshopWords.TitleKey),
                Ui.Row(12, make, Ui.Button(CampaignBook.ContentsKey, launch.ShowBook)),
                problem == null ? null : Forms.Hint(WorkshopWords.CouldNotKey, problem),
                Ui.Scroll(shelves, 520));
            page.Name = "WorkshopHub";

            launch.Show(Ui.Panel(page), 1040);
        }

        // --- my campaigns ---------------------------------------------------------------------------------------------

        static void Mine(Launch launch, VBoxContainer shelves)
        {
            shelves.AddChild(Ui.Title(WorkshopWords.MyCampaignsKey));

            var cards = WorkshopShelf.Cards(WorkshopPage.Mine, WorkshopShelf.Ids(new[] { CampaignFolders.Local() }));

            if (cards.Count == 0) shelves.AddChild(Forms.Hint(WorkshopWords.NoCampaignsKey));

            foreach (CampaignCard card in cards) shelves.AddChild(Card(launch, card, mine: true));
        }

        // A CARD: the cover, its name, its chapters and when it was last changed, what's wrong, and what to do with it
        static Control Card(Launch launch, CampaignCard card, bool mine)
        {
            Label name = Ui.Plain(Named(card.NameKey, card.Name));
            name.ThemeTypeVariation = "TitleLabel";

            var about = Ui.Column(4, name);
            about.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Said chapters = WorkshopWords.Chapters(card.Chapters);
            about.AddChild(Ui.Plain(Ui.Say(chapters.Key, chapters.Args) + "   " +
                                    Ui.Say(WorkshopWords.EditedKey, card.Edited.ToLocalTime().ToString("yyyy-MM-dd"))));

            if (card.Shadowed) about.AddChild(Warning(Ui.Label(WorkshopWords.SameIdKey)));

            if (card.Errors > 0)
            {
                Said wont = WorkshopWords.WontLoad(card.Errors);
                about.AddChild(Warning(Ui.Label(wont.Key, wont.Args)));
            }

            var buttons = Ui.Row(8);
            buttons.Name = "Buttons";
            about.AddChild(buttons);

            Button open = Ui.Button(WorkshopWords.OpenKey, () => WorkshopPage.OpenCampaign(launch, card.Folder));
            open.Name = "Open_" + card.Id;
            buttons.AddChild(open);

            if (mine)
            {
                buttons.AddChild(Ui.Button(WorkshopWords.DuplicateKey, () => Duplicate(launch, card)));
                buttons.AddChild(Ui.Button(WorkshopWords.DeleteKey, () => AskToDelete(launch, buttons, card)));

                if (card.Shadowed)
                    buttons.AddChild(Ui.Button(WorkshopWords.RenameKey, () =>
                        Done(launch, card.Folder, WorkshopShelf.Rename(card.Folder, WorkshopPage.Taken, out string problem), problem)));
            }

            var row = Ui.Row(14, Pictures.CoverOf(card.Cover, card.GmScreen, new Vector2(Ui.Px(160), Ui.Px(90))), about);
            row.Name = "Card_" + card.Id;
            return Ui.Panel(row);
        }

        // a campaign's name: the words the game registered for it, else its own locale's English, else "(no name yet)"
        internal static string Named(string key, string english)
        {
            string said = Ui.Say(key);

            return said != key && said.Length > 0 ? said : english is { Length: > 0 } ? english : Ui.Say(WorkshopWords.UnnamedKey);
        }

        static Label Warning(Label label)
        {
            label.Modulate = new Color(1f, 0.75f, 0.45f);
            return label;
        }

        static void Duplicate(Launch launch, CampaignCard card)
        {
            string copy = WorkshopShelf.Duplicate(card.Folder, WorkshopPage.Taken,
                                                  Ui.Say(WorkshopWords.CopyOfKey, Named(card.NameKey, card.Name)), out string problem);
            Done(launch, copy, copy, problem);
        }

        // DELETE, asked first: the row becomes the question, with Keep (the safe answer) focused
        static void AskToDelete(Launch launch, HBoxContainer buttons, CampaignCard card)
        {
            Ui.Clear(buttons);

            Label question = Ui.Label(WorkshopWords.DeleteQuestionKey, Named(card.NameKey, card.Name));
            question.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Button keep = Ui.Button(WorkshopWords.KeepKey, () => Show(launch));
            Button delete = Ui.Button(WorkshopWords.DeleteKey, () =>
            {
                string gone = RecentlyDeleted.Delete(card.Folder, DateTime.Today, out string problem);
                Done(launch, card.Folder, gone, problem);
            });
            delete.Name = "ConfirmDelete";

            buttons.AddChild(question);
            buttons.AddChild(delete);
            buttons.AddChild(keep);
            Ui.FocusLater(keep);
        }

        // after anything that moved a campaign's files: found again, and the hub drawn again (or what went wrong said)
        static void Done(Launch launch, string changed, string result, string problem)
        {
            if (result == null) GD.PushWarning($"workshop: {changed} - {problem}");

            GameState.CampaignsChanged(changed);
            Show(launch, result == null ? problem : null);
        }

        // --- recently deleted -----------------------------------------------------------------------------------------

        static void Deleted(Launch launch, VBoxContainer shelves)
        {
            var items = RecentlyDeleted.Items(WorkshopPage.Mine);

            if (items.Count == 0) return;

            shelves.AddChild(Ui.Title(WorkshopWords.RecentlyDeletedKey));

            foreach (RecentlyDeleted.Item item in items)
            {
                RecentlyDeleted.Item one = item;
                string name = Named(CampaignFiles.NameKey(one.Id), CampaignFiles.Sheet(one.Folder, one.Id).Get(CampaignFiles.NameKey(one.Id)));
                Said left = WorkshopWords.DaysLeft(one.DaysLeft(DateTime.Today));

                Label what = Ui.Plain($"{name}   {Ui.Say(left.Key, left.Args)}");
                what.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

                var row = Ui.Row(8, what);
                row.Name = "Deleted_" + one.Id;

                row.AddChild(Ui.Button(WorkshopWords.RestoreKey, () =>
                    Done(launch, one.Folder, RecentlyDeleted.Restore(one, WorkshopPage.Mine, WorkshopPage.Taken, out string problem), problem)));
                row.AddChild(Ui.Button(WorkshopWords.DeleteForeverKey, () => AskForever(launch, row, one, name)));

                shelves.AddChild(row);
            }
        }

        static void AskForever(Launch launch, HBoxContainer row, RecentlyDeleted.Item item, string name)
        {
            Ui.Clear(row);

            Label question = Ui.Label(WorkshopWords.ForeverQuestionKey, name);
            question.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Button keep = Ui.Button(WorkshopWords.KeepKey, () => Show(launch));

            row.AddChild(question);
            row.AddChild(Ui.Button(WorkshopWords.DeleteForeverKey, () =>
            {
                string problem = RecentlyDeleted.Forget(item);
                Done(launch, item.Folder, problem == null ? item.Folder : null, problem);
            }));
            row.AddChild(keep);
            Ui.FocusLater(keep);
        }

        // --- my maps, and the game's campaigns ------------------------------------------------------------------------

        static void Maps(Launch launch, VBoxContainer shelves)
        {
            shelves.AddChild(Ui.Title(MapPlace.LibraryKey));

            MapPlace library = MapBuilderPage.Library;
            var ids = MapFiles.Ids(library);

            if (ids.Count == 0) shelves.AddChild(Forms.Hint(WorkshopWords.NoMyMapsKey));

            foreach (string id in ids)
            {
                string one = id;
                shelves.AddChild(Launch.Leader(Ui.Button(one, () => MapBuilderPage.OpenMap(launch, library, one), true), "MyMap_" + one));
            }

            Button make = Ui.Button(WorkshopWords.NewMapKey, () => launch.PageCalled("builder")?.Open(launch));
            make.Name = "NewMap";
            shelves.AddChild(Ui.Row(8, make));
        }

        static void GameCampaigns(Launch launch, VBoxContainer shelves)
        {
            shelves.AddChild(Ui.Title(WorkshopWords.GameCampaignsKey));
            shelves.AddChild(Forms.Hint(WorkshopWords.GameCampaignsHintKey));

            foreach (CampaignCard card in WorkshopShelf.Cards(CampaignFolders.Local(), null))
                shelves.AddChild(Card(launch, card, mine: false));
        }
    }
}
