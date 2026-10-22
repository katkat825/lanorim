using System.IO;
using Content.Campaigns;
using Content.Workshop;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE ABOUT TAB (cc_task_m 4.1): the wizard's first two pages again, editable, and the cover - a PNG or JPG copied in
    // as preview.png at a fixed size (pack.json's "preview"), or the GM screen's picture when there's none
    public partial class CampaignScreen
    {
        void About(VBoxContainer page)
        {
            // a copy of one of the game's own campaigns says so (cc_task_m2 Part 6): "Based on Millbrook (Lanorim)"
            if (Draft.BasedOn.Length > 0)
            {
                Label based = Ui.Label(WorkshopWords.BasedOnKey, Hub.Named(CampaignFiles.NameKey(Draft.BasedOn), Draft.BasedOn));
                based.Name = "BasedOn";
                page.AddChild(based);
            }

            page.AddChild(Ui.Column(4, Ui.Label(WorkshopWords.CoverKey),
                Pictures.CoverOf(Draft.Preview.Length > 0 ? Path.Combine(Editor.Folder, Draft.Preview) : null, Draft.GmScreen,
                                 new Vector2(Ui.Px(240), Ui.Px(135))),
                Ui.Row(8,
                    Named(Ui.Button(WorkshopWords.PickPictureKey, () => Pictures.Picker(this, Cover)), "PickCover"),
                    Ui.Button(WorkshopWords.CoverDefaultKey, () => Change(() => Editor.Edit("about.cover", d => d.Preview = "")))),
                Forms.Hint(WorkshopWords.CoverHintKey)));

            Forms.About(page, Draft, Edit, _advanced, Editor.Id, null);
            Forms.Rules(page, Draft, Edit);

            Json(page, Draft.PackRead ? ManifestWriter.Write(Draft.ToManifest()) : "");
        }

        // a picture picked: copied in at the cover's size, and the pack says it's the cover
        public void Cover(string picked)
        {
            string problem = Pictures.Import(picked, Path.Combine(Editor.Folder, WorkshopShelf.CoverFile), Pictures.Cover);

            if (problem != null)
            {
                GD.PushWarning($"workshop: the cover {picked} - {problem}");
                _status.Visible = true;
                _status.Text = Ui.Say(WorkshopWords.CouldNotKey, problem);
                return;
            }

            Edit("about.cover", d => d.Preview = WorkshopShelf.CoverFile);
            Redraw();
        }

        static Button Named(Button button, string name)
        {
            button.Name = name;
            return button;
        }
    }
}
