using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Saves;
using Content.Schema;
using Content.Sheet;
using Core.Dice;
using Core.Resolution;
using Library = Content.Schema.Library;

namespace Content.Play
{
    public sealed partial class CampaignRun
    {
        // --- saving ----------------------------------------------------------------------------

        public SaveGame Capture(SaveKind kind = SaveKind.Manual)
        {
            var game = new SaveGame
            {
                Campaign = Pack.Id,
                CampaignFormat = Pack.Manifest?.Format ?? 0,
                Chapter = "",
                Map = MapId ?? "",
                Hero = HeroSaves.Capture(Hero),
                Node = Talk.Node ?? "",
                Kind = kind,
                Slot = Slot,
            };

            // the variables as the node found them, and the steps taken in it since
            foreach (KeyValuePair<string, float> n in _entryNumbers) game.Numbers[n.Key] = n.Value;
            foreach (KeyValuePair<string, string> w in _entryWords) game.Words[w.Key] = w.Value;
            foreach (KeyValuePair<string, bool> f in _entryFlags) game.Flags[f.Key] = f.Value;
            foreach (string step in _steps) game.Steps.Add(step);

            return game;
        }

        public string Save(SaveKind kind = SaveKind.Manual, string label = "") =>
            _saves?.Save(Capture(kind), kind, label);

        void Autosave(SaveKind kind)
        {
            if (_saves != null) _saves.Save(Capture(kind), kind);
        }

        // BACK FROM A SAVE: the hero rebuilt, the variables put back, and the story restarted at
        // the node it was on (a node is where a story can be entered; a save mid-node replays from
        // the node's start, which is what an autosave at a chapter or a fight is anyway)
        public static CampaignRun Resume(SaveGame save, Library library, Package pack,
                                         IResolver heroDice, IRng gm, SaveLibrary saves,
                                         out IReadOnlyList<ContentProblem> problems)
        {
            Hero hero = HeroSaves.Restore(save.Hero, library, out problems);

            if (hero == null) return null;

            var run = new CampaignRun(library, pack, hero, heroDice, gm, saves, save.Slot)
            {
                MapId = save.Map,
            };

            foreach (KeyValuePair<string, float> n in save.Numbers) run._store.SetValue(n.Key, n.Value);
            foreach (KeyValuePair<string, string> w in save.Words) run._store.SetValue(w.Key, w.Value);
            foreach (KeyValuePair<string, bool> f in save.Flags) run._store.SetValue(f.Key, f.Value);

            run._resumeAt = save.Node;
            run._replay = save.Steps.ToList();

            return run;
        }

        string _resumeAt;

        List<string> _replay = new List<string>();

        // the node a resumed run starts at, replayed to the spot it was saved at
        public Scene Continue()
        {
            if (string.IsNullOrEmpty(_resumeAt)) return Start();

            if (!Talk.Start(_resumeAt)) return Now = Scene.Over;

            foreach (string step in _replay)
            {
                // lines already read are not read again
                while (!Talk.IsOver && !Talk.IsWaiting && !Talk.IsChoosing) Talk.Advance();

                if (!Replay(step))
                {
                    // the story is not the one that was saved (it was edited since): stop here and
                    // play on from wherever this is, rather than guess
                    Talk.Complained.Add($"a saved step '{step}' no longer fits node '{Talk.Node}'");
                    break;
                }
            }

            _replay.Clear();

            return Settle();
        }
    }
}
