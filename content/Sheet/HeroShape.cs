using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Core.Combat;
using Core.Rules;

namespace Content.Sheet
{
    // WILD SHAPE: the Druid picking up a form card and putting it down again. its own file
    // because it is its own subsystem - decisions_checklist.md section 6 calls it one - and
    // because the rest of the sheet is not where it should have to be read.
    //
    // the body swap itself is core's (Actor.Assume and Actor.Revert), and so is ending it at 0
    // hit points: 0 is unconscious, unconscious incapacitates, and SRD 5.2.1 ends the shape on
    // incapacitated. what is here is only what a druid knows and a goblin does not - which card,
    // whether the level allows it, the use it costs, and the temporary hit points it brings.
    public sealed partial class Hero
    {
        Form _form;

        // the card being worn, or null. read through the actor every time, so a shape that
        // ended because the druid went down is gone here too without anybody telling the sheet
        public Form Form => _form != null && Actor.Shape?.Id == _form.Id ? _form : null;

        public bool IsShifted => Form != null;

        public Feature WildShape => Features.FirstOrDefault(f => f.Trait == Trait.Shape);

        // the cards this hero may pick up today, uses permitting
        public IEnumerable<Form> FormsOpen(FormShelf forms) =>
            WildShape == null || forms == null ? Enumerable.Empty<Form>() : forms.OpenAt(Level);

        // why not, in engineer's English; null means it may. the UI greys the card instead
        public string RefusesShape(Form form, Turn turn = null)
        {
            if (form == null) return "no form";

            Feature shape = WildShape;

            if (shape == null) return "no wild shape";

            if (!form.OpenAt(Level)) return $"opens at level {form.MinimumLevel}";

            if (UsesLeft(shape) <= 0) return "no uses left";

            if (!Actor.CanAct) return "cannot act";

            // Moonbeam: a creature it turned back can't shift again until it leaves the beam
            if (Actor.Boons.NoShifting) return "held in its true form";

            if (turn != null && !turn.Can(Spend.Bonus)) return "no bonus action left";

            return null;
        }

        // a bonus action when there is a turn to pay it from, and free outside a fight. taking
        // a second card while wearing one is allowed and costs a second use: SRD 5.2.1 ends the
        // first shape when Wild Shape is used again
        public bool Shift(Form form, Turn turn = null)
        {
            if (RefusesShape(form, turn) != null) return false;

            if (turn != null && !turn.Take(Spend.Bonus)) return false;

            Feature shape = WildShape;

            Actor.Assume(form.ToShape(shape.Id));
            _form = form;

            // SRD 5.2.1: temporary hit points equal to the druid level, on every shift. they do
            // not stack with a bigger pool and are not taken back when the shape comes off
            Actor.Health.GrantTemporary(Level);

            _spent[shape.Id] = (_spent.TryGetValue(shape.Id, out int used) ? used : 0) + 1;

            return true;
        }

        // a save putting the card back on. not Shift: the use was paid when it went on the first
        // time and is already in the ledger, and the temporary hit points are the save's to say
        internal bool Resume(Form form)
        {
            Feature shape = WildShape;

            if (form == null || shape == null || !form.OpenAt(Level)) return false;

            Actor.Assume(form.ToShape(shape.Id));
            _form = form;

            return true;
        }

        // back to the druid: a bonus action with a turn, free without one. a rest and a level-up
        // call this too, so the shape never outlasts either
        public bool Revert(Turn turn = null)
        {
            if (Form == null)
            {
                // the actor may already have dropped it at 0 hit points; forget the card too
                _form = null;
                return false;
            }

            if (turn != null && !turn.Take(Spend.Bonus)) return false;

            Actor.Revert();
            _form = null;

            return true;
        }

        // the spider's venom rides on the spider's bite and nothing else
        IReadOnlyList<Rider> WithFormRider(IReadOnlyList<Rider> riders, Core.Characters.Attack attack)
        {
            Rider venom = Form?.RiderFor(attack);

            return venom == null ? riders : riders.Append(venom).ToList();
        }

        // everything per-rest comes back on a short rest except Wild Shape, which SRD 5.2.1
        // returns one use at a time - the rest wait for a long rest
        // SRD 5.2.1: what a short rest gives back - every use, one use (Rage, Second Wind,
        // Channel Divinity, Wild Shape), or nothing (Lay on Hands, Indomitable)
        void RestoreAfterShortRest()
        {
            foreach (string id in _spent.Keys.ToList())
            {
                Feature feature = Features.FirstOrDefault(f => f.Id == id);

                Recharge recharge = feature?.Recharge ?? Recharge.Short;

                if (recharge == Recharge.Short) _spent.Remove(id);
                else if (recharge == Recharge.ShortOne)
                {
                    if (_spent[id] > 1) _spent[id]--;
                    else _spent.Remove(id);
                }
            }
        }
    }
}
