using System.IO;
using System.Linq;
using Content.Workshop;
using Core.Localization;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE CHARACTERS TAB (cc_task_m 4.3): the named people who speak or appear - a name (the locale's actor.<id>.name), a
    // note to self (workshop.json, the Workshop's only) and a portrait if wanted. The story editor (O) lists them as
    // speakers, with the GM and the companion always there
    public partial class CampaignScreen
    {
        void People(VBoxContainer page)
        {
            page.AddChild(Forms.Hint(WorkshopWords.PeopleHintKey));

            Label always = Ui.Label(WorkshopWords.AlwaysSpeakKey);
            always.Modulate = new Color(1f, 1f, 1f, 0.7f);
            page.AddChild(always);

            foreach (Person person in Draft.People.ToList()) page.AddChild(PersonRow(person));

            LineEdit name = Forms.Line("NewPerson", "", _ => { }, WorkshopWords.PersonPlaceholderKey);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            void Add()
            {
                if (string.IsNullOrWhiteSpace(name.Text)) return;
                Editor.AddPerson(name.Text.Trim());
                Redraw();
            }

            name.TextSubmitted += _ => Add();
            Button add = Ui.Button(WorkshopWords.AddPersonKey, Add);
            add.Name = "AddPerson";
            page.AddChild(Ui.Row(8, name, add));

            Json(page, WorkshopNotes.Of(Draft).Text() + "\n" +
                       string.Join("\n", Draft.People.Select(p => $"{KeyConventions.ActorName(p.Id)},{p.Name}")));
        }

        Control PersonRow(Person person)
        {
            string id = person.Id;

            var fields = Ui.Column(6,
                Forms.Field(WorkshopWords.PersonNameKey,
                    Forms.Line("PersonName_" + id, person.Name, t => Edit($"character.{id}.name", d => { if (d.Character(id) is { } p) p.Name = t; }, true)), null),
                Forms.Field(WorkshopWords.PersonNoteKey,
                    Forms.Line("PersonNote_" + id, person.Note, t => Edit($"character.{id}.note", d => { if (d.Character(id) is { } p) p.Note = t; }, true)),
                    WorkshopWords.PersonNoteHintKey));
            fields.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var buttons = Ui.Row(8,
                Ui.Button(WorkshopWords.PickPictureKey, () => Pictures.Picker(this, picked => Portrait(id, picked))),
                Ui.Button(WorkshopWords.NoPortraitKey, () => Change(() => Editor.PortraitOf(id, ""))).Greyed(person.Portrait.Length == 0, null));
            buttons.AddChild(Ui.Button(WorkshopWords.DeleteKey, () => AskToDelete(buttons, person)));
            fields.AddChild(buttons);

            Control picture = person.Portrait.Length > 0 && Pictures.Load(Path.Combine(Editor.Folder, person.Portrait)) is { } shown
                ? Pictures.Shown(shown, new Vector2(Ui.Px(72), Ui.Px(72)), "Portrait")
                : new ColorRect { Name = "Portrait", Color = new Color(0.3f, 0.26f, 0.22f), CustomMinimumSize = new Vector2(Ui.Px(72), Ui.Px(72)) };

            var row = Ui.Row(12, picture, fields);
            row.Name = "Person_" + id;
            return Ui.Panel(row);
        }

        // a portrait picked: copied into the campaign at the portrait's size
        public void Portrait(string id, string picked)
        {
            string to = CampaignEditor.PortraitPath(id);
            string problem = Pictures.Import(picked, Path.Combine(Editor.Folder, to), Pictures.Portrait);

            if (problem != null)
            {
                GD.PushWarning($"workshop: the portrait {picked} - {problem}");
                return;
            }

            Editor.PortraitOf(id, to);
            Redraw();
        }

        void AskToDelete(HBoxContainer buttons, Person person)
        {
            Ui.Clear(buttons);

            Label question = Ui.Label(WorkshopWords.DeletePersonKey, person.Name.Length > 0 ? person.Name : person.Id);
            question.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            Button keep = Ui.Button(WorkshopWords.KeepKey, Redraw);

            buttons.AddChild(question);
            buttons.AddChild(Ui.Button(WorkshopWords.DeleteKey, () => Change(() => Editor.RemovePerson(person.Id))));
            buttons.AddChild(keep);
            Ui.FocusLater(keep);
        }
    }
}
