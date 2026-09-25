using System.Collections.Generic;
using Yarn;

namespace Content.Dialogue
{
    // THE STORY'S VARIABLES, KEPT WHERE A SAVE CAN READ THEM: Yarn's memory store remembers what a
    // story set but cannot list it, so this one writes through and keeps its own copy of each value
    public sealed class StoryVariables : IVariableStorage
    {
        readonly MemoryVariableStore _inner = new MemoryVariableStore();

        public Dictionary<string, float> Numbers { get; } = new Dictionary<string, float>();

        public Dictionary<string, string> Words { get; } = new Dictionary<string, string>();

        public Dictionary<string, bool> Flags { get; } = new Dictionary<string, bool>();

        public Program Program
        {
            get => _inner.Program;
            set => _inner.Program = value;
        }

        public ISmartVariableEvaluator SmartVariableEvaluator
        {
            get => _inner.SmartVariableEvaluator;
            set => _inner.SmartVariableEvaluator = value;
        }

        public bool TryGetValue<T>(string variableName, out T result) =>
            _inner.TryGetValue(variableName, out result);

        public VariableKind GetVariableKind(string name) => _inner.GetVariableKind(name);

        public void SetValue(string variableName, string stringValue)
        {
            _inner.SetValue(variableName, stringValue);
            Words[variableName] = stringValue;
        }

        public void SetValue(string variableName, float floatValue)
        {
            _inner.SetValue(variableName, floatValue);
            Numbers[variableName] = floatValue;
        }

        public void SetValue(string variableName, bool boolValue)
        {
            _inner.SetValue(variableName, boolValue);
            Flags[variableName] = boolValue;
        }

        public void Clear()
        {
            _inner.Clear();
            Numbers.Clear();
            Words.Clear();
            Flags.Clear();
        }
    }
}
