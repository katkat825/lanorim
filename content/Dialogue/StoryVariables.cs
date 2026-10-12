using System;
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

        // VARIABLES READ LIVE, NEVER STORED (cc_task_i 4.1): the hero's ($hero_level and the rest). a value here is
        // what the story reads; null leaves the name to the store. a write to one is ignored, so it never lands in a save
        public Func<string, IConvertible> Live { get; set; }

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

        public bool TryGetValue<T>(string variableName, out T result)
        {
            if (Live?.Invoke(variableName) is IConvertible live)
            {
                object value = typeof(T) == typeof(object) || typeof(T).IsInstanceOfType(live)
                    ? live
                    : Convert.ChangeType(live, typeof(T), System.Globalization.CultureInfo.InvariantCulture);

                result = (T)value;
                return true;
            }

            return _inner.TryGetValue(variableName, out result);
        }

        bool IsLive(string variableName) => Live?.Invoke(variableName) != null;

        public VariableKind GetVariableKind(string name) => _inner.GetVariableKind(name);

        public void SetValue(string variableName, string stringValue)
        {
            if (IsLive(variableName)) return;

            _inner.SetValue(variableName, stringValue);
            Words[variableName] = stringValue;
        }

        public void SetValue(string variableName, float floatValue)
        {
            if (IsLive(variableName)) return;

            _inner.SetValue(variableName, floatValue);
            Numbers[variableName] = floatValue;
        }

        public void SetValue(string variableName, bool boolValue)
        {
            if (IsLive(variableName)) return;

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
