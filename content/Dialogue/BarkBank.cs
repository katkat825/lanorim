using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;

namespace Content.Dialogue
{
    // One speaker's short reactions. The file says how many lines each bank holds and the locale
    // holds the lines - so adding a bark is one number and one row, and never a code change.
    //
    // Keyed by creature and not by class (CONVENTIONS section 7): the class picks the companion,
    // the companion owns the voice, and a campaign that writes new barks for the wolf writes them
    // for every Barbarian who ever plays it.
    //
    // A CAMPAIGN ADDS LINES ON TOP (cc_task_i 2.2): the game-wide bank is the importer's (content/srd/barks), and a
    // pack's barks/ folder holds the same shape for the same speaker. Its lines are keyed under the campaign
    // (dialogue.wolf.bark.victory.bramble.001) so the two never collide, and Adding deals them in one bag.
    public sealed class BarkBank
    {
        readonly IReadOnlyDictionary<Bark, IReadOnlyList<string>> _keys;

        public BarkBank(string speaker, IReadOnlyDictionary<Bark, int> banks, bool readsTheThrow = false,
                        string campaign = "")
        {
            Speaker = speaker ?? "";
            ReadsTheThrow = readsTheThrow;
            _keys = (banks ?? new Dictionary<Bark, int>())
                .Where(b => b.Value > 0)
                .ToDictionary(b => b.Key, b => (IReadOnlyList<string>)DialogueKeys.Barks(Speaker, b.Key, b.Value, campaign).ToList());
        }

        BarkBank(string speaker, IReadOnlyDictionary<Bark, IReadOnlyList<string>> keys, bool readsTheThrow)
        {
            Speaker = speaker;
            _keys = keys;
            ReadsTheThrow = readsTheThrow;
        }

        public string Speaker { get; }

        // this bank's lines and another's for the same speaker, dealt together: the game-wide wolf and a campaign's
        public BarkBank Adding(BarkBank more)
        {
            if (more == null || more.Speaker != Speaker) return this;

            var keys = new Dictionary<Bark, IReadOnlyList<string>>();

            foreach (Bark situation in _keys.Keys.Union(more._keys.Keys))
                keys[situation] = KeysOf(situation).Concat(more.KeysOf(situation)).ToList();

            return new BarkBank(Speaker, keys, ReadsTheThrow || more.ReadsTheThrow);
        }

        IReadOnlyList<string> KeysOf(Bark situation) =>
            _keys.TryGetValue(situation, out IReadOnlyList<string> keys) ? keys : Array.Empty<string>();

        // the key of a bank's line, 1-based like the bag that deals it
        public string Key(Bark situation, int index)
        {
            IReadOnlyList<string> keys = KeysOf(situation);

            return index >= 1 && index <= keys.Count ? keys[index - 1] : null;
        }

        // whether this voice, rather than the DM, says what the dice came to (W2, DM_PRESENCE.md D5)
        public bool ReadsTheThrow { get; }

        public int Count(Bark situation) => KeysOf(situation).Count;

        public bool Has(Bark situation) => Count(situation) > 0;

        public IEnumerable<Bark> Situations =>
            _keys.Where(b => b.Value.Count > 0).Select(b => b.Key).OrderBy(s => s);

        public int Lines => _keys.Values.Sum(k => k.Count);

        // every key this speaker can put in front of a player, which is what the locale audit demands
        public IEnumerable<string> Keys()
        {
            foreach (Bark situation in Situations)
                foreach (string key in KeysOf(situation))
                    yield return key;

            if (!ReadsTheThrow) yield break;

            foreach (string key in DialogueKeys.ReadoutKeys(Speaker)) yield return key;
        }

        // the readout keys are the ones that format numbers in; a bank's own barks never do
        public bool TakesAnArgument(string key) =>
            ReadsTheThrow && DialogueKeys.ReadoutKeys(Speaker).Contains(key, StringComparer.Ordinal);


        // one bag per situation, each dealt rather than drawn, all off the one seeded stream
        public Speaking Open(IRng rng) => new Speaking(this, rng);

        public override string ToString() =>
            $"{Speaker}: {Lines} barks in {Situations.Count()} banks" +
            (ReadsTheThrow ? ", reads the throw" : "");
    }

    // a bank in use: the shuffle state that makes forty lines sound like a creature and not a table
    public sealed class Speaking
    {
        readonly BarkBank _bank;

        readonly IRng _rng;

        readonly Dictionary<Bark, ShuffleBag> _bags = new Dictionary<Bark, ShuffleBag>();

        public Speaking(BarkBank bank, IRng rng)
        {
            _bank = bank ?? throw new ArgumentNullException(nameof(bank));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public string Speaker => _bank.Speaker;

        public bool Has(Bark situation) => _bank.Has(situation);

        // the key of the next thing said, or null when this speaker has nothing for the moment
        public string Next(Bark situation)
        {
            if (!_bank.Has(situation)) return null;

            if (!_bags.TryGetValue(situation, out ShuffleBag bag))
                _bags[situation] = bag = new ShuffleBag(_bank.Count(situation), _rng);

            int index = bag.Next();

            return index == 0 ? null : _bank.Key(situation, index);
        }

        public string Readout(Readout which) =>
            _bank.ReadsTheThrow ? DialogueKeys.Readout(_bank.Speaker, which) : null;

        public override string ToString() => _bank.ToString();
    }
}
