using System.Linq;
using Content.Maps;
using Game.Screens;
using Godot;

namespace Game.Dev
{
    // THE PROBE AT THE MAP BUILDER'S PAGE (cc_task_m2 Part 7): a new map named on the page, the name's rule said in red
    // under its box, and a map opened from My maps by its name
    public partial class MapBuilderProbe
    {
        void OpenPage() => Launch.PageCalled("builder")?.Open(Launch);

        // the page's New map: a name, the template, My maps, and the big button
        void NewMap(string template, string name)
        {
            Type(Find<LineEdit>("MapName"), name);
            Choose(Find<OptionButton>("Template"), MapTemplates.Ids.ToList().IndexOf(template));
            Choose(Find<OptionButton>("Lives"), 0);
            Find<Button>("MakeMap")?.EmitSignal(BaseButton.SignalName.Pressed);
        }

        // the name's rule, said in red under its box while it's broken, and gone when it isn't (cc_task_m2 Part 7)
        void NameRule()
        {
            LineEdit name = Find<LineEdit>("MapName");
            Label rule = Find<Label>("NameRule");

            Type(name, "Kathleen's crypt");
            Expect(rule?.Visible == true, "an apostrophe in a map's name: the rule, under the box");

            Type(name, MapName);
            Expect(rule?.Visible == false, "and gone for letters and a space");
        }

        void OpenFromMyMaps()
        {
            Button mine = Nodes.Under<HBoxContainer>(Launch).FirstOrDefault(r => r.Name == "MyMap_" + MapId)?
                               .GetChildren().OfType<Button>().FirstOrDefault(b => b.Text == MapName);

            Expect(mine != null, $"My maps lists the map by its name, {MapName}");
            mine?.EmitSignal(BaseButton.SignalName.Pressed);
        }

        T Find<T>(string name) where T : Node => Nodes.Under<T>(Launch).FirstOrDefault(n => n.Name == name);

        static void Type(LineEdit box, string text)
        {
            if (box == null) return;
            box.Text = text;
            box.EmitSignal(LineEdit.SignalName.TextChanged, text);
        }

        static void Choose(OptionButton list, int index)
        {
            if (list == null) return;
            list.Selected = index;
            list.EmitSignal(OptionButton.SignalName.ItemSelected, index);
        }
    }
}
