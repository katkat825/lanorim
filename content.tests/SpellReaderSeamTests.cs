using System.Collections.Generic;
using Content.Spells;

namespace Content.Tests
{
    // THE READER ASKS THE HANDLER (cc_task_d-seams-and-duplication.md §5): the rules that used to name primitives
    // ("only a zone is put down as a wall", "add_modifier is for damage or healing") now ask each handler what it
    // allows. each rule here is tripped on a primitive that may not and passed on one that may, so the reader
    // refuses what it refused before and nothing else
    public class SpellReaderSeamTests
    {
        static IReadOnlyList<string> Problems(string effects)
        {
            SpellReader.TryRead(@"{""spells"":[{""id"":""probe"",""level"":1,""effects"":[" + effects + "]}]}",
                                out _, out IReadOnlyList<string> problems);

            return problems;
        }

        const string Zone = @"{""primitive"":""zone"",""aim"":""place"",""radius"":2}";

        [Theory]
        // a radius: what covers ground is sized by one
        [InlineData(@"{""primitive"":""illuminate"",""aim"":""place""}",
                    @"{""primitive"":""narrate"",""aim"":""place""}", "with no 'radius'")]
        // a wall is a zone's
        [InlineData(@"{""primitive"":""damage"",""aim"":""wall"",""length"":4,""amount"":""1d6"",""damage_type"":""fire""}",
                    @"{""primitive"":""zone"",""aim"":""wall"",""length"":4}", "only a zone is put down as a wall")]
        // the caster's modifier adds to damage or healing
        [InlineData(@"{""primitive"":""ward"",""aim"":""caster"",""amount"":""1d6"",""add_modifier"":true}",
                    @"{""primitive"":""heal"",""aim"":""caster"",""amount"":""1d6"",""add_modifier"":true}", "it adds to")]
        // reaching the zone needs a pulse, unless it moves the zone or lasts while a creature is in it
        [InlineData(Zone + @",{""primitive"":""damage"",""aim"":""zone"",""amount"":""1d6"",""damage_type"":""fire""}",
                    Zone + @",{""primitive"":""shift"",""aim"":""zone""}", "has to say when")]
        [InlineData(Zone + @",{""primitive"":""damage"",""aim"":""zone"",""amount"":""1d6"",""damage_type"":""fire""}",
                    Zone + @",{""primitive"":""sway"",""aim"":""zone"",""while_in_zone"":true,""flat"":1}", "has to say when")]
        // 'affects' sorts a zone's creatures, or an area's
        [InlineData(@"{""primitive"":""damage"",""aim"":""creature"",""affects"":""foes"",""amount"":""1d6"",""damage_type"":""fire""}",
                    @"{""primitive"":""zone"",""aim"":""place"",""radius"":2,""affects"":""foes""}", "is for a zone or an area")]
        [InlineData(@"{""primitive"":""damage"",""aim"":""burst"",""radius"":2,""affects"":""allies"",""amount"":""1d6"",""damage_type"":""fire""}",
                    @"{""primitive"":""zone"",""aim"":""place"",""radius"":2,""affects"":""allies""}", "belongs on the zone")]
        // only damage lands each turn
        [InlineData(@"{""primitive"":""heal"",""aim"":""creature"",""amount"":""1d6"",""lands"":""each_turn""}",
                    @"{""primitive"":""damage"",""aim"":""creature"",""amount"":""1d6"",""damage_type"":""fire"",""lands"":""each_turn""}",
                    "nothing else does")]
        // an upcast radius grows a zone
        [InlineData(@"{""primitive"":""damage"",""aim"":""burst"",""radius"":2,""amount"":""1d6"",""damage_type"":""fire"",""upcast"":{""radius"":1}}",
                    @"{""primitive"":""zone"",""aim"":""place"",""radius"":2,""upcast"":{""radius"":1}}", "grows a zone")]
        // a shift that moves the zone needs none to have been made in the same breath; anything else reaching it does
        [InlineData(@"{""primitive"":""damage"",""aim"":""zone"",""pulses"":""enter"",""amount"":""1d6"",""damage_type"":""fire""}",
                    @"{""primitive"":""shift"",""aim"":""zone""}", "makes no zone")]
        // one zone to a spell
        [InlineData(Zone + "," + Zone, Zone, "one zone to a spell")]
        // a boon is what a sway carries
        [InlineData(@"{""primitive"":""ward"",""aim"":""caster"",""amount"":""1d6"",""flat"":1}",
                    @"{""primitive"":""sway"",""aim"":""caster"",""flat"":1}", "belongs on a sway")]
        public void ARuleAboutPrimitivesIsAskedOfTheHandler(string refused, string allowed, string said)
        {
            Assert.Contains(Problems(refused), p => p.Contains(said));
            Assert.DoesNotContain(Problems(allowed), p => p.Contains(said));
        }
    }
}
