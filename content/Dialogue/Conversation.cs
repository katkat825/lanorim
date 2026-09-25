using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;
using Yarn;

namespace Content.Dialogue
{
    // One conversation, running. Godot-free on purpose: the branching is a rules-shaped question
    // and the table is only what draws it, so this is testable headless and the scene that shows
    // it holds no logic worth testing.
    //
    // Nothing here ever returns the .yarn source. A line comes out as a KEY and the presentation
    // resolves it - which is the whole reason the pseudolocale can prove the words came through
    // the localizer and not out of the campaign's dialogue folder.
    public sealed class Conversation
    {
        readonly DialogueBook _book;

        readonly Yarn.Dialogue _dialogue;

        readonly List<Said> _heard = new List<Said>();

        // exactly one of these is set when the machine stops; all null means it is over
        Said _saying;

        List<Choice> _choosing;

        Request _pending;

        bool _stopped;

        bool _answered;

        public Conversation(DialogueBook book, IVariableStorage storage = null)
        {
            _book = book ?? throw new ArgumentNullException(nameof(book));

            if (book.Program == null)
                throw new ArgumentException("This dialogue book did not compile, so nothing in it can be played.",
                                            nameof(book));

            _dialogue = new Yarn.Dialogue(storage ?? new MemoryVariableStore());
            _dialogue.SetProgram(book.Program);

            _dialogue.LineHandler = OnLine;
            _dialogue.OptionsHandler = OnOptions;
            _dialogue.CommandHandler = OnCommand;
            _dialogue.NodeCompleteHandler = _ => { };
            _dialogue.NodeStartHandler = node => NodeStarted?.Invoke(node);
            _dialogue.DialogueCompleteHandler = () => _stopped = true;

            // Yarn writes to the console otherwise, and a campaign is not allowed to print
            _dialogue.LogDebugMessage = _ => { };
            _dialogue.LogErrorMessage = message => Complained.Add(message);
        }

        // developer diagnostics from the runtime itself, not localized and never shown
        public IList<string> Complained { get; } = new List<string>();

        public string Node => _dialogue.CurrentNode;

        // a node begins - before anything in it has run, so a save can note the variables as the
        // node found them
        public Action<string> NodeStarted { get; set; }

        // the line waiting to be read, or null while a choice is open or the talk is over
        public Said Saying => _saying;

        public IReadOnlyList<Choice> Choosing =>
            (IReadOnlyList<Choice>)_choosing ?? Array.Empty<Choice>();

        public bool IsChoosing => _choosing != null;

        // THE STORY HAS STOPPED TO ASK THE TABLE FOR SOMETHING - a check, a fight, a hidden roll.
        // nothing goes on until Answer is called with what happened
        public Request Pending => _pending;

        public bool IsWaiting => _pending != null;

        public bool IsOver => _stopped;

        // everything said this conversation, in order; what a transcript and a headless check read
        public IReadOnlyList<Said> Heard => _heard;

        // a command the campaign wrote that the engine does not know; named, never run
        public IList<string> Commands { get; } = new List<string>();


        public bool Start(string node)
        {
            if (!_book.Has(node)) return false;

            _heard.Clear();
            _stopped = false;
            _saying = null;
            _choosing = null;
            _pending = null;

            _dialogue.SetNode(node);

            return Advance();
        }

        // true while there is more to read; false once the conversation has ended
        public bool Advance()
        {
            if (_stopped) return false;

            // an unanswered ask holds the story where it is: stepping past a check would take the
            // branch for a roll nobody made
            if (_pending != null) return true;

            _saying = null;
            _choosing = null;
            _answered = false;

            try
            {
                _dialogue.Continue();
            }
            catch (Exception threw)
            {
                // a campaign cannot be allowed to take the table down with it
                Complained.Add(threw.Message);
                _stopped = true;
                return false;
            }

            return !_stopped;
        }

        public bool Choose(int option)
        {
            if (_choosing == null || option < 0 || option >= _choosing.Count) return false;

            if (!_choosing[option].Offered) return false;

            _dialogue.SetSelectedOption(_choosing[option].Index);
            _answered = true;

            return Advance();
        }

        // whether the last choice was answered; a caller that advances without choosing gets nowhere
        public bool Answered => _answered;

        // what the table made of the ask. the answer goes into the variables the ask's kind
        // writes (RequestVariables) and the story carries on from the line after the command
        public bool Answer(Answer answer)
        {
            if (_pending == null || answer == null) return false;

            IVariableStorage store = _dialogue.VariableStorage;

            switch (_pending.Kind)
            {
                case RequestKind.Check:
                case RequestKind.Save:
                    store.SetValue(RequestVariables.Passed, answer.Passed);
                    store.SetValue(RequestVariables.Total, (float)answer.Total);
                    store.SetValue(RequestVariables.Natural, (float)answer.Natural);
                    store.SetValue(RequestVariables.Consequence, answer.DrawsConsequence);
                    break;

                case RequestKind.Roll:
                    store.SetValue(RequestVariables.Roll, (float)answer.Total);
                    break;

                case RequestKind.Encounter:
                    store.SetValue(RequestVariables.Encounter, answer.Entry ?? "");
                    store.SetValue(RequestVariables.EncounterFight, answer.StartsAFight);
                    break;

                case RequestKind.Fight:
                    store.SetValue(RequestVariables.Fight, RequestVariables.Word(answer.Outcome));
                    break;

                case RequestKind.Loot:
                    store.SetValue(RequestVariables.LootGold, (float)answer.Gold);
                    break;
            }

            _pending = null;

            return Advance();
        }


        void OnLine(Line line)
        {
            DialogueLine known = _book.Line(line.ID);

            // a line with no key was refused at load and named there; it is skipped, not shown
            if (known == null) return;

            _saying = new Said(known, line.Substitutions ?? Array.Empty<string>());
            _heard.Add(_saying);
        }

        void OnOptions(OptionSet options)
        {
            var open = new List<Choice>();

            foreach (OptionSet.Option option in options.Options)
            {
                DialogueLine known = _book.Line(option.Line.ID);

                if (known == null) continue;

                open.Add(new Choice(option.ID, known,
                                    option.Line.Substitutions ?? Array.Empty<string>(),
                                    option.IsAvailable));
            }

            _choosing = open;
        }

        // content is data, never code (CONVENTIONS section 2). A << >> the engine does not itself
        // define is recorded and stepped over - there is deliberately no hook by which a campaign
        // could make one mean something, because on a storefront that is a security boundary.
        //
        // THE ENGINE'S OWN VERBS ARE THE EXCEPTION (Asks.cs): <<check>>, <<fight>> and the rest stop
        // the story and wait for the table. a verb written wrong is a complaint and is stepped
        // over, the same as a command nobody defined - the branch after it reads the variable's
        // default, which the author can see in a playthrough.
        void OnCommand(Command command)
        {
            Commands.Add(command.Text);

            Request request = Request.Parse(command.Text, out string problem);

            if (problem != null) Complained.Add(problem);

            if (request != null) _pending = request;
        }


        // a line, ready to be put through a localizer
        public sealed class Said
        {
            public Said(DialogueLine line, IReadOnlyList<string> substitutions)
            {
                Line = line;
                Substitutions = substitutions ?? Array.Empty<string>();
            }

            public DialogueLine Line { get; }

            public string Key => Line.Key;

            public string Speaker => Line.Speaker;

            // who says it with this companion at the table; a 'companion' line becomes theirs
            public string SpeakerFor(string companion) => Line.SpeakerFor(companion);

            // Yarn's {0} interpolations, already rendered to strings by the runtime
            public IReadOnlyList<string> Substitutions { get; }

            // the one place a dialogue key becomes text, and it is the caller's localizer that does it
            public string Text(ILocalizer text) => Text(text, null);

            // the same, knowing whose companion is listening: their own row of a 'companion' line
            // where the campaign wrote one, the generic row where it did not (DialogueLine.AnyCompanion)
            public string Text(ILocalizer text, string companion)
            {
                if (text == null) throw new ArgumentNullException(nameof(text));

                string key = Line.KeyFor(companion, text.Has);

                return Substitutions.Count == 0
                    ? text.Get(key)
                    : text.Format(key, Substitutions.Cast<object>().ToArray());
            }

            public override string ToString() => Key;
        }

        public sealed class Choice
        {
            public Choice(int index, DialogueLine line, IReadOnlyList<string> substitutions,
                          bool offered)
            {
                Index = index;
                Line = line;
                Substitutions = substitutions ?? Array.Empty<string>();
                Offered = offered;
            }

            // Yarn's own option number, which is what SetSelectedOption wants
            public int Index { get; }

            public DialogueLine Line { get; }

            public string Key => Line.Key;

            public IReadOnlyList<string> Substitutions { get; }

            // a choice whose condition failed: shown greyed, never taken
            public bool Offered { get; }

            public string Text(ILocalizer text) =>
                new Said(Line, Substitutions).Text(text);

            public override string ToString() => $"{Index}: {Key}" + (Offered ? "" : " (closed)");
        }
    }
}
