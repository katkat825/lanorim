using System;
using System.Collections.Generic;

namespace Content.Schema
{
    // A fault is a thing the loader could not do. A caution is a thing it did, that the author very
    // likely did not mean - "this chapter lists a map twice" is not a refusal, it is a sentence
    // somebody should read once.
    //
    // LIFTED from the old build, and deliberately NOT the string list the SRD readers use. The two
    // kinds of content are read by different people: the SRD data is ours, embedded in the assembly
    // and fixed at build time, so a problem in it is a build error and a sentence is enough. A
    // campaign pack is a Workshop author's, loose on disk, and edited in a text editor - so a
    // problem in one has to say which file, which field, and which line, or the author is hunting.
    public enum Severity
    {
        Fault,

        Caution,
    }

    public sealed class ContentProblem
    {
        public ContentProblem(string file, string where, string what, int line = 0,
                              Severity severity = Severity.Fault)
        {
            File = file ?? "";
            Where = where ?? "";
            What = what ?? "";
            Line = line;
            How = severity;
        }

        // a caution reads exactly like a fault and is counted differently; nothing else changes
        public static ContentProblem Caution(string file, string where, string what, int line = 0) =>
            new ContentProblem(file, where, what, line, Severity.Caution);

        public string File { get; }

        // a JSON path like "chapters.0.maps", or empty for the file itself
        public string Where { get; }

        public string What { get; }

        // 1-based, or 0 when the parser knew no line
        public int Line { get; }

        public Severity How { get; }

        public bool IsACaution => How == Severity.Caution;

        public bool IsAFault => How == Severity.Fault;

        public override string ToString()
        {
            string at = Line > 0 && Where.Length > 0 ? $"line {Line}, {Where}"
                      : Line > 0 ? $"line {Line}"
                      : Where.Length > 0 ? Where
                      : "";

            string said = at.Length > 0 ? $"{File}: {at} - {What}" : $"{File}: {What}";

            return IsACaution ? said + " (this one is a caution, not a refusal)" : said;
        }
    }

    // the thing, or the reasons it isn't; never both half-done, never an exception
    public sealed class Read<T> where T : class
    {
        readonly List<ContentProblem> _problems;

        Read(T value, List<ContentProblem> problems)
        {
            Value = value;
            _problems = problems ?? new List<ContentProblem>();
        }

        public T Value { get; }

        public IReadOnlyList<ContentProblem> Problems => _problems;

        // a caution is a sentence about a thing that WAS read, so it cannot be what makes it unread
        public bool Ok => Value != null && !_problems.Exists(p => p.IsAFault);

        public static Read<T> Good(T value) => new Read<T>(value, null);

        public static Read<T> Bad(IEnumerable<ContentProblem> problems) =>
            new Read<T>(null, new List<ContentProblem>(problems ?? Array.Empty<ContentProblem>()));

        public static Read<T> Bad(ContentProblem problem) => Bad(new[] { problem });

        // a value and a list; Ok is still false (it means "nothing was wrong", not "there is a value")
        public static Read<T> Partial(T value, IEnumerable<ContentProblem> problems) =>
            new Read<T>(value, new List<ContentProblem>(problems ?? Array.Empty<ContentProblem>()));

        // there is a value, understood or not; the question a save asks and a campaign never needs
        public bool Any => Value != null;

        public override string ToString() =>
            Ok ? $"read {typeof(T).Name}"
               : $"{_problems.Count} problems reading {typeof(T).Name}" +
                 (Any ? ", read as far as it could be" : "");
    }
}
