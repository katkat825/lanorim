using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // SAVE AS AND COPY TO A CAMPAIGN (cc_task_l Part 1.4): a name and a place (My maps or a campaign; a copy goes only to
    // a campaign), then written; and when that name is taken there, the question first: give it another name, replace
    // that one, or cancel. Nothing writes over a map without asking
    public partial class MapBuilderUi
    {
        void AskWhere(bool copy)
        {
            int gap = BuilderLayout.Current.Gap;

            var places = new List<MapPlace>();
            if (!copy) places.Add(MapBuilderPage.Library);
            places.AddRange(MapBuilderPage.Campaigns.Select(c => c.Place));

            if (places.Count == 0)
            {
                Notice(Ui.Say(MapBuilderView.NoCampaignsKey));
                return;
            }

            var name = new LineEdit { Name = "WhereId", Text = Builder.View.Id, CustomMinimumSize = new Vector2(Ui.Px(220), 0) };
            var where = new OptionButton { Name = "WherePlace" };
            foreach (MapPlace place in places) where.AddItem(Ui.Say(place.NameKey ?? place.Id));

            Ask(Ui.Panel(Ui.Column(gap,
                Ui.Title(copy ? MapBuilderView.CopyToKey : MapBuilderView.SaveAsKey),
                Ui.Row(gap, Ui.Label(MapBuilderView.IdKey), name),
                Ui.Row(gap, Ui.Label(MapBuilderView.LivesKey), where),
                Ui.Row(gap,
                    Ui.Button(copy ? MapBuilderView.CopyToKey : MapBuilderView.SaveAsKey,
                        () => Write(copy, places[System.Math.Max(0, where.Selected)], name.Text.Trim(), replace: false)),
                    Ui.Button(MapBuilderView.CancelKey, Dismiss)))), copy ? "CopyTo" : "SaveAs");
        }

        // written there, or the question when the name is taken
        public void Write(bool copy, MapPlace place, string id, bool replace)
        {
            if (!replace && Builder.View.Taken(place, id))
            {
                AskTaken(copy, place, id);
                return;
            }

            Said refused = copy ? Builder.View.CopyTo(place, id, replace) : Builder.View.SaveAs(place, id, replace);

            Dismiss();

            Notice(refused != null ? Game.Access.AccessDesk.Words(refused)
                 : copy ? Ui.Say(MapBuilderView.CopiedKey, Ui.Say(place.NameKey ?? place.Id), id)
                 : Ui.Say(Builder.View.SavedKey));

            Refresh();
        }

        // "there's already a map called mill_yard there": rename, replace or cancel
        void AskTaken(bool copy, MapPlace place, string id)
        {
            int gap = BuilderLayout.Current.Gap;

            Ask(Ui.Panel(Ui.Column(gap,
                Ui.Label(MapBuilderView.TakenKey, id),
                Ui.Row(gap,
                    Ui.Button(MapBuilderView.RenameKey, () => Write(copy, place, MapFiles.FreeId(place, id), replace: false)),
                    Ui.Button(MapBuilderView.ReplaceKey, () => Write(copy, place, id, replace: true)),
                    Ui.Button(MapBuilderView.CancelKey, Dismiss)))), "Taken");
        }
    }
}
