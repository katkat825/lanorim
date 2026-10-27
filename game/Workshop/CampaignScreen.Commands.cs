using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Dialogue;
using Content.Workshop;
using Content.Workshop.Story;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Core.Words;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE COMMANDS' FORMS (cc_task_p Parts 1-3): every engine command a block, a form over its words, and what happens
    // after it - a check's pass and fail, a fight's won and ran, a roll's "at least" - as indented block lists. No "$",
    // no "<<": the author picks a skill by name, how hard in words, a fight from the Fights tab
    public partial class CampaignScreen
    {
        void CommandForm(VBoxContainer form, string card, int[] path, Do act)
        {
            string id = StoryPath.Name(path);

            switch (act.Verb)
            {
                case "check":
                case "save":
                    TestForm(form, card, path, act, id);
                    break;

                case "roll":
                    RollForm(form, card, path, act, id);
                    break;

                case "fight":
                case "encounter":
                case "loot":
                case "shop":
                case "armory":
                    Pick(form, card, path, act, id);
                    break;

                case "gold":
                case "silver":
                case "copper":
                    CoinsForm(form, card, path, act, id);
                    break;

                case "give":
                    GiveForm(form, card, path, act, id);
                    break;

                case "level":
                case "xp":
                    LevelForm(form, card, path, act, id);
                    break;

                case "rest":
                    form.AddChild(Forms.Field(BlockWords.RestKey,
                        Forms.Choice("Rest_" + id, new[] { "short", "long" }, act.Words == "long" ? "long" : "short", BlockWords.Rest,
                                     r => Editor.EditBlock<Do>(card, path, d => d.Words = r)), BlockWords.RestSavesKey));
                    break;

                case "bark":
                    EnumWords.TryParse(act.Words, out Bark pool);
                    form.AddChild(Forms.Field(BlockWords.PoolKey,
                        Forms.Choice("Bark_" + id, BarkPools.Spoken.ToList(), pool, BlockWords.Pool, b => Editor.EditBlock<Do>(card, path, d => d.Words = b.Id())), null));
                    break;
            }

            if (act.Verb is "check" or "save" or "roll" or "fight" or "encounter" or "loot") After(form, card, path, act, id);

            if (_advanced) form.AddChild(Forms.Note($"<<{act.Verb} {act.Words}>>"));
        }

        // --- check a skill, saving throw -----------------------------------------------------------------------------

        void TestForm(VBoxContainer form, string card, int[] path, Do act, string id)
        {
            (string what, int dc) = Rolls.Read(act);

            if (act.Verb == "check")
            {
                // the 18 skills by name, each with its ability; the abilities alone under "More"
                var whats = Enum.GetValues<Skill>().Select(s => s.Id()).Concat(Enum.GetValues<Ability>().Select(a => a.Id())).ToList();
                OptionButton skill = Forms.Choice("Skill_" + id, whats, whats.Contains(what) ? what : "athletics", _ => null,
                    w => { Editor.EditBlock<Do>(card, path, d => Rolls.Write(d, w, Rolls.Read(d).Dc)); Redraw(); });
                for (int i = 0; i < whats.Count; i++)
                    skill.SetItemText(i, i < 18
                        ? $"{Ui.Say(KeyConventions.Key("skill", whats[i], "name"))}, {Ui.Say(KeyConventions.Key("ability", Rolls.AbilityOf(whats[i]).Id(), "name"))}"
                        : $"{Ui.Say(BlockWords.MoreKey)}: {Ui.Say(KeyConventions.Key("ability", whats[i], "name"))}");
                form.AddChild(Forms.Field(BlockWords.SkillKey, skill, null));
            }
            else
            {
                var abilities = Enum.GetValues<Ability>().Select(a => a.Id()).ToList();
                form.AddChild(Forms.Field(BlockWords.SaveAbilityKey,
                    Forms.Choice("SaveAbility_" + id, abilities, abilities.Contains(what) ? what : "dex", a => KeyConventions.Key("ability", a, "name"),
                                 a => Editor.EditBlock<Do>(card, path, d => Rolls.Write(d, a, Rolls.Read(d).Dc))), null));
            }

            // how hard: Easy, Medium, Hard, Very hard (a number in Advanced)
            var rungs = Rolls.Ladder.Select(r => r.Dc).ToList();
            Control hard = _advanced || !rungs.Contains(dc)
                ? Forms.Number("Dc_" + id, 1, 40, dc, v => Editor.EditBlock<Do>(card, path, d => Rolls.Write(d, Rolls.Read(d).What, v), typing: true))
                : Forms.Choice("Dc_" + id, rungs, dc, r => Rolls.Ladder.First(l => l.Dc == r).Rung.NameKey(), r => Editor.EditBlock<Do>(card, path, d => Rolls.Write(d, Rolls.Read(d).What, r)));
            form.AddChild(Forms.Field(BlockWords.HowHardKey, hard, null));

            form.AddChild(Forms.Hint(BlockWords.NaturalKey));

            // the two-keys band: only heroes good at this skill get past here, and the two fixes
            if (act.Verb == "check" && Rolls.NoWayPast(Draft.Story.Card(card), act)) form.AddChild(TwoKeysBand(card, path, act, id));
        }

        Control TwoKeysBand(string card, int[] path, Do act, string id)
        {
            string skill = Ui.Say(KeyConventions.Key("skill", Rolls.Read(act).What, "name"));

            Label says = Ui.Label(BlockWords.TwoKeysKey, skill);
            says.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            Skill other = Rolls.OtherWays(Rolls.Read(act).What).FirstOrDefault(s => s == Skill.Investigation || s == Skill.Perception);
            string otherName = Ui.Say(KeyConventions.Key("skill", other.Id(), "name"));

            Button another = Ui.Button(BlockWords.AnotherWayKey, () => Change(() => Editor.Edit("block", d => Wrap(d, card, path,
                (c, check) => Rolls.AnotherWay(d.Story, c, check, other, Ui.Say(BlockWords.TryItKey, skill), Ui.Say(BlockWords.FindAnotherKey, otherName))))));
            another.Name = "AnotherWay_" + id;

            Button round = Ui.Button(BlockWords.WithoutRollKey, () => Change(() => Editor.Edit("block", d => Wrap(d, card, path,
                (c, check) => Rolls.WithoutARoll(d.Story, c, check, Ui.Say(BlockWords.TryItKey, skill), Ui.Say(BlockWords.GoRoundKey))))));
            round.Name = "WithoutRoll_" + id;

            var band = Ui.Column(4, says, Ui.Row(8, another, round));
            PanelContainer panel = Ui.Panel(band);
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.98f, 0.8f, 0.4f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6 });
            panel.Name = "TwoKeys_" + id;
            return panel;
        }

        // the check at `path` put inside a note the fix makes
        static void Wrap(CampaignDraft d, string card, int[] path, Func<CardDraft, Do, Choice> fix)
        {
            CardDraft c = d.Story.Card(card);
            if (c?.Blocks == null || StoryPath.Find(c.Blocks, path) is not { } found || found.List[found.At] is not Do check) return;

            found.List[found.At] = fix(c, check);
        }

        // --- the GM rolls dice ----------------------------------------------------------------------------------------

        void RollForm(VBoxContainer form, string card, int[] path, Do act, string id)
        {
            string[] args = act.Args;
            Core.Dice.DiceRoll.TryParse(args.FirstOrDefault() ?? "1d20", out Core.Dice.DiceRoll dice, out _);
            int count = Math.Max(1, dice.Count), sides = dice.RollsAnything ? dice.Die.Sides() : 20;
            bool heard = args.Length > 1 && args[1] == Request.HeardWord;

            void Write(int c, int s, bool h) => Editor.EditBlock<Do>(card, path, d => d.Words = $"{c}d{s}{(h ? " " + Request.HeardWord : "")}");

            var sideses = new[] { 4, 6, 8, 10, 12, 20, 100 };
            form.AddChild(Forms.Field(BlockWords.DiceKey, Ui.Row(8,
                Forms.Number("RollCount_" + id, 1, 10, count, v => Write(v, sides, heard)),
                Forms.Choice("RollSides_" + id, sideses, sideses.Contains(sides) ? sides : 20, _ => null, v => Write(count, v, heard)).Also(o => { for (int i = 0; i < sideses.Length; i++) o.SetItemText(i, "d" + sideses[i]); })), null));
            form.AddChild(Forms.Tick("RollHeard_" + id, BlockWords.HearKey, heard, h => Write(count, sides, h)));
        }

        // --- fights, encounters, loot, shops: one of N's, picked -------------------------------------------------------

        void Pick(VBoxContainer form, string card, int[] path, Do act, string id)
        {
            (string label, List<string> ids, Func<string, string> named) = act.Verb switch
            {
                "fight" => (BlockWords.PickFightKey, Draft.Tables.Fights.Select(f => f.Fight.Id).ToList(), (Func<string, string>)(f => Draft.Tables.NameOf("fight", f))),
                "encounter" => (BlockWords.PickTableKey, Draft.Tables.RandomTables(_ => true).Select(t => t.Id).ToList(), t => Draft.Tables.NameOf("encounters", t)),
                "loot" => (BlockWords.PickLootKey, Draft.Tables.Loot.Select(t => t.Id).ToList(), t => Draft.Tables.NameOf("loot", t)),
                "shop" => (BlockWords.PickShopKey, Draft.Tables.Shops.Where(s => !s.Armory).Select(s => s.Id).Append(MerchantReader.StartingShopId).Distinct().ToList(), s => Draft.Tables.Shop(s)?.Name is { Length: > 0 } n ? n : Content.Workshop.Tables.TablesDraft.Plain(s)),
                _ => (BlockWords.PickArmoryKey, Draft.Tables.Shops.Where(s => s.Armory).Select(s => s.Id).ToList(), s => Draft.Tables.Shop(s)?.Name is { Length: > 0 } n ? n : Content.Workshop.Tables.TablesDraft.Plain(s)),
            };

            string now = act.Words;
            if (!ids.Contains(now)) ids.Add(now);

            OptionButton list = Forms.Choice("Pick_" + id, ids, now, _ => null, v => { Editor.EditBlock<Do>(card, path, d => d.Words = v); Redraw(); });
            for (int i = 0; i < ids.Count; i++) list.SetItemText(i, named(ids[i]));
            form.AddChild(Forms.Field(label, list, null));

            // a fight's map and monsters, in a line (N's card has the rest)
            if (act.Verb == "fight" && Draft.Tables.Fight(now).Fight is { } fight)
                form.AddChild(Ui.Row(8, new MapThumb { CustomMinimumSize = new Vector2(Ui.Px(64), Ui.Px(48)) }.Also(t => t.Show(Editor.Maps, fight.Map)),
                                     Forms.Note(string.Join(", ", fight.Monsters.Select(m => $"{(m.Count.Length == 0 ? "1" : m.Count)} {Pickers.MonsterName(Things.Bestiary, m.Monster)}")))));
        }

        // --- coins, an item, levels -------------------------------------------------------------------------------------

        void CoinsForm(VBoxContainer form, string card, int[] path, Do act, string id)
        {
            int.TryParse(act.Words, out int amount);

            form.AddChild(Ui.Row(8,
                Forms.Field(BlockWords.CoinKey, Forms.Choice("Coin_" + id, new[] { "gold", "silver", "copper" }, act.Verb, Content.Workshop.Tables.TableWords.Coin,
                    c => { Editor.EditBlock<Do>(card, path, d => d.Verb = c); Redraw(); }), null),
                Forms.Field(BlockWords.HowMuchKey, Forms.Number("CoinAmount_" + id, -9999, 9999, amount,
                    v => Editor.EditBlock<Do>(card, path, d => d.Words = v.ToString(), typing: true)), null)));

            string coin = Ui.Say(Content.Workshop.Tables.TableWords.Coin(act.Verb)).ToLowerInvariant();
            form.AddChild(Forms.Hint(amount >= 0 ? BlockWords.GetsKey : BlockWords.PaysKey, Math.Abs(amount), coin));
        }

        void GiveForm(VBoxContainer form, string card, int[] path, Do act, string id)
        {
            string[] args = act.Args;
            string item = args.FirstOrDefault() ?? "";
            int count = args.Length > 1 && int.TryParse(args[1], out int n) ? n : 1;

            form.AddChild(Ui.Row(8, Forms.Note(Pickers.ItemName(item)), Ui.Label(Content.Workshop.Tables.TableWords.HowManyKey),
                Forms.Number("GiveCount_" + id, 1, 99, count, v => Editor.EditBlock<Do>(card, path, d => d.Words = $"{d.Args.FirstOrDefault()}{(v > 1 ? " " + v : "")}", typing: true))));

            form.AddChild(Forms.Field(BlockWords.ItemKey, Pickers.Item("GiveItem_" + id, Things.Items,
                picked => Change(() => Editor.EditBlock<Do>(card, path, d => d.Words = picked + (count > 1 ? " " + count : "")))), null));
        }

        void LevelForm(VBoxContainer form, string card, int[] path, Do act, string id)
        {
            int.TryParse(act.Words, out int amount);
            bool xp = act.Verb == "xp";

            form.AddChild(Forms.Field(xp ? BlockWords.XpAmountKey : BlockWords.LevelToKey,
                Forms.Number("LevelAmount_" + id, 1, xp ? 100000 : 20, Math.Max(1, amount), v => Editor.EditBlock<Do>(card, path, d => d.Words = v.ToString(), typing: true)), null));

            // the one the campaign doesn't level by is said, not hidden, when a card uses it
            if (xp != (Draft.Leveling == Leveling.Xp)) form.AddChild(Forms.Hint(BlockWords.OtherLevelingKey, Ui.Say(WorkshopWords.LevelingChoiceKey(Draft.Leveling))));
        }

        // --- afterwards -------------------------------------------------------------------------------------------------

        // WHAT HAPPENS AFTER IT: the "Only if" that reads what the command answered, each arm in words ("If they pass",
        // "They won", "If it comes up at least 15"), with Otherwise; and buttons for the arms it doesn't have yet
        void After(VBoxContainer form, string card, int[] path, Do act, string id)
        {
            Branch after = act.After;

            if (after != null)
            {
                for (int i = 0; i < after.Arms.Count; i++)
                {
                    int arm = i;
                    form.AddChild(ArmLabel(act, after.Arms[i].Condition, card, path, arm));
                    form.AddChild(BlockList(card, StoryPath.Then(path, i), after.Arms[i].Blocks));
                }

                if (after.Else != null)
                {
                    form.AddChild(Ui.Label(act.Verb is "check" or "save" && after.Arms.Count == 1 && after.Arms[0].Condition == RequestVariables.Passed
                        ? BlockWords.IfFailKey : BlockWords.OtherwiseKey));

                    int at = after.Arms.Count;

                    // failing moves forward: an empty fail asks what it costs, with starters
                    if (after.Else.Count == 0 && act.Verb is "check" or "save") form.AddChild(Starters(card, path, at, id));

                    form.AddChild(BlockList(card, StoryPath.Then(path, at), after.Else));
                }
            }

            // the arms it could have and doesn't
            var adds = Ui.Row(6);
            adds.Name = "AddAfter_" + id;

            foreach ((string condition, string key, object[] args) in Possible(act).Where(p => after?.Arms.All(a => a.Condition != p.Condition) ?? true))
            {
                string c = condition;
                adds.AddChild(Ui.Button(BlockWords.AddOutcomeKey, () => Change(() => Editor.EditBlock<Do>(card, path, d =>
                {
                    d.After ??= new Branch();
                    d.After.Arms.Add(new Arm { Condition = c, Blocks = c == RequestVariables.EncounterFight ? EncounterFight() : new List<Block>() });
                })), Ui.Say(key, args)));
            }

            if (after == null || after.Else == null)
                adds.AddChild(Ui.Button(BlockWords.AddOtherwiseKey, () => Change(() => Editor.EditBlock<Do>(card, path, d =>
                {
                    d.After ??= new Branch { Arms = { new Arm { Condition = Possible(d).First().Condition } } };
                    d.After.Else ??= new List<Block>();
                }))));

            if (adds.GetChildCount() > 0) form.AddChild(adds);

            if (act.Verb == "fight" && Draft.Defeat == Defeat.Death) form.AddChild(Forms.Hint(BlockWords.DiesKey));
        }

        // the sample's pattern for a fight that came up: <<fight {$encounter}>>, and what happens if it's won or run from
        static List<Block> EncounterFight() => new()
        {
            new Do { Verb = "fight", Words = "{" + RequestVariables.Encounter + "}", After = new Branch { Arms = { new Arm { Condition = "$fight == \"won\"" } } } },
        };

        // the arms a command's afterwards can have, each with its words
        IEnumerable<(string Condition, string Key, object[] Args)> Possible(Do act)
        {
            switch (act.Verb)
            {
                case "check":
                case "save":
                    yield return (RequestVariables.Passed, BlockWords.IfPassKey, Array.Empty<object>());
                    break;

                case "roll":
                    yield return ("$roll >= 15", BlockWords.AtLeastThenKey, new object[] { 15 });
                    break;

                case "fight":
                    foreach (string outcome in BlockWords.Outcomes.Where(o => Draft.Defeat != Defeat.Death || o is "won" or "fled"))
                        yield return ($"$fight == \"{outcome}\"", BlockWords.Outcome(outcome), Array.Empty<object>());
                    yield return (RequestVariables.FightBroke, BlockWords.BrokeKey, Array.Empty<object>());
                    break;

                case "encounter":
                    yield return (RequestVariables.EncounterFight, BlockWords.FightCameKey, Array.Empty<object>());
                    foreach (var entry in Draft.Tables.Table(act.Words)?.Entries.Where(e => e.Kind == Core.Tables.EntryKind.Line) ?? Enumerable.Empty<Content.Workshop.Tables.EncounterDraft>())
                        yield return ($"$encounter == \"{entry.Id}\"", BlockWords.MomentCameKey, new object[] { Draft.Tables.NameOf("entry", entry.Id) });
                    break;

                case "loot":
                    yield return ("$loot_gold > 0", BlockWords.FoundCoinsKey, Array.Empty<object>());
                    break;
            }
        }

        // an arm's words: "If they pass", "They won", "If it comes up at least [15]" (the number a box)
        Control ArmLabel(Do act, string condition, string card, int[] path, int arm)
        {
            Clause clause = OnlyIf.Of(condition).Clauses.First();

            if (clause.Term == Term.RollAtLeast)
                return Ui.Row(6, Ui.Label(BlockWords.AtLeastThenKey, ""),
                    Forms.Number($"AtLeast_{StoryPath.Name(path)}_{arm}", 1, 100, clause.Number,
                                 v => Editor.EditBlock<Do>(card, path, d => d.After.Arms[arm].Condition = $"$roll >= {v}", typing: true)));

            Label label = clause.Term switch
            {
                Term.Passed => Ui.Label(BlockWords.IfPassKey),
                Term.FightWas when BlockWords.Outcomes.Contains(clause.Name) => Ui.Label(BlockWords.Outcome(clause.Name)),
                Term.FightBroke => Ui.Label(BlockWords.BrokeKey),
                Term.EncounterFight => Ui.Label(BlockWords.FightCameKey),
                Term.EncounterWas => Ui.Label(BlockWords.MomentCameKey, Draft.Tables.NameOf("entry", clause.Name)),
                Term.FoundCoins => Ui.Label(BlockWords.FoundCoinsKey),
                _ => Ui.Label(BlockWords.Clause(clause.Term), ClauseArgs(clause)),
            };

            label.Name = $"Arm_{StoryPath.Name(path)}_{arm}";
            return label;
        }

        // failing moves forward: "Something should still happen when they fail", and the starters
        Control Starters(string card, int[] path, int at, string id)
        {
            var row = Ui.Row(6);
            row.Name = "Starters_" + id;

            foreach (string starter in Rolls.Starters)
            {
                string s = starter;
                Button button = Ui.Button(BlockWords.Starter(s), () => Change(() => Editor.Edit("block", d =>
                {
                    CardDraft c = d.Story.Card(card);
                    if (StoryPath.At(c.Blocks, path) is Do { After.Else: { } fail }) fail.AddRange(Rolls.Starter(s, Ui.Say(BlockWords.StarterLine(s)), d.Story, c, d.Tables));
                })));
                button.Name = $"Starter_{s}_{id}";
                row.AddChild(button);
            }

            return Ui.Column(4, Forms.Hint(BlockWords.FailEmptyKey), row);
        }
    }

    static class ControlSetUp
    {
        // a control set up in passing
        public static T Also<T>(this T control, Action<T> set) where T : Control
        {
            set(control);
            return control;
        }
    }
}
