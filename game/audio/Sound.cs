using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;

namespace Game.Audio
{
    // EVERY ATMOSPHERIC SOUND THE GAME MAKES THAT CARRIES SOMETHING, AND THE WORDS FOR IT (AX4).
    //
    // Captions for atmospheric non-text audio are first-class rather than the afterthought they
    // first look like: a creak behind the screen carries mood, and sometimes it carries meaning.
    // Every WORD in this game is already text, so this is the other half - the sounds.
    //
    // THE LIST IS SHORT BECAUSE IT IS ONLY THE SOUNDS THAT SAY SOMETHING. A mini being set down is
    // not here, and that is a decision rather than an omission: you can see the figure move, so the
    // sound tells a deaf player nothing they do not already have, and a caption for it would be
    // noise in the one channel they are relying on. A rattle behind the screen is the opposite - it
    // is the ONLY thing that says the DM just rolled something, and a player who cannot hear it
    // loses a beat of the game rather than a flourish.
    //
    // Each member names the folder its recordings come out of, so the list cannot drift from the
    // pools that play them: the folder is the list (SurfaceVoice, ImpactPool), and this is the words
    // for the folder.
    public enum Sound
    {
        // onyx on a wooden table - the game's own signature noise
        Dice,

        // the quiet under everything - a table in a room, heard rather than seen
        Room,

        // pages, a pencil, a chair, dice in a cup
        Busy,

        // the barely-audible "hm" when you do something unexpected, which is worth more than a
        // thousand words of dialogue and therefore worth a caption
        Reacting,

        // the secret roll. Something was rolled and you are not being shown the number
        Behind,
    }

    public static class Sounds
    {
        public static string Word(this Sound sound) => sound.ToString().ToLowerInvariant();

        public static IReadOnlyList<string> Words => Enum.GetValues<Sound>().Select(Word).ToArray();

        public const string Subject = "caption";

        // ui.*, because the sounds are the engine's: a campaign that ships its own ambience ships its
        // own captions for it in its own locale, the way it ships its monsters' names
        public static string CaptionKey(this Sound sound) =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, Word(sound), "name");

        public static IEnumerable<string> Keys()
        {
            foreach (Sound sound in Enum.GetValues<Sound>()) yield return CaptionKey(sound);
        }

        // where the recordings are. Named off the pools rather than written out again
        public static string Folder(this Sound sound) => sound switch
        {
            Sound.Dice => ImpactPool.Default,
            Sound.Room => Gm + "tone/",
            Sound.Busy => Gm + "busy/",
            Sound.Reacting => Gm + "reacting/",
            Sound.Behind => Gm + "behind/",
            _ => ImpactPool.Default,
        };

        // The referee behind the screen. Nothing is recorded yet and may never be - the game is
        // text before voice - but the captions are owed either way, because a player who cannot
        // hear needs to be told the GM rolled something behind the screen whether or not there is
        // a sound of it. Recorded() below is how a check tells the two apart.
        public const string Gm = "res://audio/samples/gm/";

        // A CAPTION FOR A FOLDER WITH NOTHING IN IT IS A LIE. The GM's audio ships as a system
        // with no recordings in it and will until somebody sits down with a microphone, so a check
        // can say which captions describe a silence today without either failing the build or
        // pretending they do not.
        public static bool Recorded(this Sound sound) => ImpactPool.Has(Folder(sound));

        public static bool TryWord(string word, out Sound sound)
        {
            sound = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            foreach (Sound one in Enum.GetValues<Sound>())
            {
                if (Word(one) != trimmed) continue;

                sound = one;
                return true;
            }

            return false;
        }
    }
}
