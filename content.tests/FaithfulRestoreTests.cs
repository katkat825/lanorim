using Core.Characters;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using static Content.Tests.Fights;

namespace Content.Tests
{
    // stabilize, relieve and dispel: what undoes harm and ends spells (StabilizeHandler, RelieveHandler, DispelHandler)
    public class FaithfulRestoreTests : FaithfulSpellFixture
    {
        [Fact]
        public void SpareTheDyingStabilizesAndDamageUndoesIt()
        {
            Assert.True(Faithful("spare_the_dying"));
            Assert.Equal(3, Book.Find("spare_the_dying").RangeAt(1));
            Assert.Equal(24, Book.Find("spare_the_dying").RangeAt(17));

            Caster wizard = Wizard(out _);
            var friend = new Actor("friend", 1, new AbilityScores(), Allegiance.Hero);
            friend.SetHealth(new Health(10));
            friend.Suffer(10, DamageType.Slashing);

            new Incantation(new StandardResolver(new ScriptedRng(1)))
                .Cast(wizard, Book.Find("spare_the_dying"), Aim.At(friend));

            Assert.True(friend.Stable);

            friend.Suffer(1, DamageType.Slashing);
            Assert.False(friend.Stable);
        }

        [Fact]
        public void RemoveCurseEndsAHexWhateverItsLevel()
        {
            Assert.True(Faithful("remove_curse"));
            Assert.True(Book.Find("hex").Curse);

            Caster wizard = Wizard(out Actor me);
            Caster witch = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            cast.Cast(witch, Book.Find("hex"), Aim.At(me).Choosing(Ability.Strength), castAt: 5);
            Assert.True(me.Boons.Has("hex"));

            cast.Cast(wizard, Book.Find("remove_curse"), Aim.At(me));
            Assert.False(me.Boons.Has("hex"));
        }

        [Fact]
        public void GreaterRestorationEndsTheChosenOneAndRestoresScores()
        {
            Assert.True(Faithful("greater_restoration"));

            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            var friend = new Actor("friend", 1, new AbilityScores(), Allegiance.Hero);
            friend.SetHealth(new Health(10));
            friend.Apply(Condition.Petrified);
            friend.Scores.ShiftUntilRest(Ability.Wisdom, -1);

            cast.Cast(wizard, Book.Find("greater_restoration"),
                      Aim.At(friend).Choosing("petrified"));
            Assert.False(friend.Has(Condition.Petrified));
            Assert.True(friend.Scores.AnyShifted);

            cast.Cast(wizard, Book.Find("greater_restoration"),
                      Aim.At(friend).Choosing("abilities"));
            Assert.False(friend.Scores.AnyShifted);
        }
    }
}
