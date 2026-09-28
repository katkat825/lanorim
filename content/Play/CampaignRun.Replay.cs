using System.Collections.Generic;
using Content.Dialogue;
using Core.Combat;

namespace Content.Play
{
    public sealed partial class CampaignRun
    {
        // --- where in the node ----------------------------------------------------------------

        readonly List<string> _steps = new List<string>();

        readonly Dictionary<string, float> _entryNumbers = new Dictionary<string, float>();
        readonly Dictionary<string, string> _entryWords = new Dictionary<string, string>();
        readonly Dictionary<string, bool> _entryFlags = new Dictionary<string, bool>();

        void Entered(string node)
        {
            _steps.Clear();

            _entryNumbers.Clear();
            _entryWords.Clear();
            _entryFlags.Clear();

            foreach (KeyValuePair<string, float> n in _store.Numbers) _entryNumbers[n.Key] = n.Value;
            foreach (KeyValuePair<string, string> w in _store.Words) _entryWords[w.Key] = w.Value;
            foreach (KeyValuePair<string, bool> f in _store.Flags) _entryFlags[f.Key] = f.Value;
        }

        const string ChoseStep = "choose";
        const string AnsweredStep = "answer";

        void Choose(Conversation talk, int option)
        {
            _steps.Add($"{ChoseStep}:{option}");
            talk.Choose(option);
        }

        // the story's answer, noted so a load can give it again without rolling it again
        void Reply(Answer answer)
        {
            _steps.Add(string.Join(":", AnsweredStep, answer.Passed ? 1 : 0, answer.Total,
                                   answer.Natural, answer.DrawsConsequence ? 1 : 0, answer.Entry ?? "",
                                   answer.StartsAFight ? 1 : 0, (int)answer.Outcome, answer.Gold));
            Talk.Answer(answer);
        }

        bool Replay(string step)
        {
            string[] parts = step.Split(':');

            if (parts[0] == ChoseStep && parts.Length == 2 && Talk.IsChoosing &&
                int.TryParse(parts[1], out int option))
            {
                Choose(Talk, option);
                return true;
            }

            if (parts[0] == AnsweredStep && parts.Length == 9 && Talk.IsWaiting)
            {
                int Int(int i) => int.TryParse(parts[i], out int v) ? v : 0;

                Reply(new Answer
                {
                    Passed = Int(1) == 1,
                    Total = Int(2),
                    Natural = Int(3),
                    DrawsConsequence = Int(4) == 1,
                    Entry = parts[5],
                    StartsAFight = Int(6) == 1,
                    Outcome = (Outcome)Int(7),
                    Gold = Int(8),
                });

                return true;
            }

            return false;
        }
    }
}
