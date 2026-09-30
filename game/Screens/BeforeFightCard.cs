using System;
using System.Collections.Generic;
using Content.Spells;
using Core.Magic;
using Godot;

namespace Game.Screens
{
    // BEFORE THE FIGHT (cc_task_ui-issues-9-30.md 5): the moment a fight is about to begin, a caster
    // with a spell that outlasts it (Mage Armor) may cast it on themselves first, paid for as usual.
    // Each spell is its card's text and a button; "To the fight" begins it. Only shown when there is
    // something to cast (BeforeTheFight.Offered), so a fighter never sees it.
    public partial class BeforeFightCard : Overlay
    {
        readonly Func<IReadOnlyList<Spell>> _offered;
        readonly Action<Spell> _cast;

        // what the last cast changed ("Mage Armor: your Armor Class is now 14")
        public string Said { get; set; } = "";

        public BeforeFightCard(Func<IReadOnlyList<Spell>> offered, Action<Spell> cast)
        {
            _offered = offered;
            _cast = cast;
            Width = 620;
        }

        protected override bool CanCancel => true;

        protected override void Draw()
        {
            Body.AddChild(Ui.Title(ScreenWords.BeforeFightTitle));
            Body.AddChild(Ui.Label(ScreenWords.BeforeFightLine));

            if (Said != "") Body.AddChild(Ui.Plain(Said));

            foreach (Spell spell in _offered())
            {
                Spell one = spell;

                Label card = Ui.Plain(SpellCardText.Of(SpellCard.Of(one)));
                card.ThemeTypeVariation = "CardLabel";
                Body.AddChild(card);
                Body.AddChild(Ui.Button(ScreenWords.BeforeFightCast, () => _cast(one), Ui.Say(one.NameKey)));
            }

            Body.AddChild(Ui.Button(ScreenWords.ToTheFight, Close));
        }

        public void Again() => Redraw();
    }
}
