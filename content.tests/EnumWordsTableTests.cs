using System;
using System.Linq;
using System.Reflection;
using Content.Classes;
using Content.Sheet;
using Core.Characters;
using Core.Words;

namespace Content.Tests
{
    // every enum in core and content reads its words from its declaration (EnumWords); two values an
    // enum lets a file name must never share a word, or one of them could not be read back
    public class EnumWordsTableTests
    {
        [Fact]
        public void NoEnumGivesTwoReadableValuesTheSameWord()
        {
            MethodInfo ids = typeof(EnumWords).GetMethod(nameof(EnumWords.Ids));

            var enums = new[] { typeof(Ability).Assembly, typeof(Feature).Assembly }
                        .SelectMany(a => a.GetTypes())
                        .Where(t => t.IsEnum && t.IsPublic)
                        .ToList();

            Assert.NotEmpty(enums);

            foreach (Type type in enums)
            {
                var words = ((System.Collections.Generic.IReadOnlyList<string>)
                             ids.MakeGenericMethod(type).Invoke(null, null)).ToList();

                Assert.True(words.Distinct(StringComparer.OrdinalIgnoreCase).Count() == words.Count,
                            $"{type.Name}: {string.Join(", ", words)}");
            }
        }

        [Fact]
        public void TheContentEnumsKeepTheirWords()
        {
            Assert.Equal("one_per_short_rest", Recharge.ShortOne.Id());
            Assert.Equal("lawful_good", Alignment.LawfulGood.Id());
            Assert.Equal("action_grant", Trait.ActionGrant.Id());

            Assert.False(EnumWords.TryParse("lawful", out Alignment alignment));
            Assert.Equal(Alignment.Neutral, alignment);
        }
    }
}
