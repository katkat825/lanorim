using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Core.Combat;
using Game.Screens;
using Godot;

namespace Game.Play
{
    public partial class PlayDirector
    {
        // --- a fight ---------------------------------------------------------------------------------

        void StartFight()
        {
            _fighting = true;

            var asker = new TableAsker(this, _auto);
            var chooser = new PolicyChooser(GameState.Settings.Reactions, asker);

            _rules.Post(() =>
            {
                var log = new FightLog();
                log.Listen(GameState.Resolver);

                BoardShow show = _combat.Show(a => ReferenceEquals(a, Run.Hero.Actor));
                log.Wrote += show.Log;

                Battle battle = Run.BattleFor(GameState.Resolver, chooser, new Observers(log, show));

                if (battle == null)
                {
                    // a fight with no map to stand on reads as won (the author sees why in the log)
                    GD.PushWarning("play: the fight has no map - counted as won");
                    Run.EndFight(Outcome.HeroesWon);
                    MainQueue.Post(() => _fighting = false);
                    return;
                }

                var places = battle.Fight.Actors
                                   .Select(a => (Actor: a, At: battle.Fight.Field.Where(a)))
                                   .Where(p => p.At.HasValue)
                                   .Select(p => (p.Actor, p.At.Value))
                                   .ToList();

                IReadOnlyList<Content.Maps.Prop> props = Run.Pack.PropsOn(Run.Fight?.MapId);
                MainQueue.Post(() => _combat.Lay(battle, places, props));

                var session = new CombatSession(battle, GameState.Content.Items, GameState.Content.Forms);
                session.Start();

                MainQueue.Post(() => _combat.Started(session));
            }, Refresh);
        }

        void FightOver(Outcome outcome)
        {
            if (outcome == Outcome.HeroesLost) _deaths++;

            _rules.Post(() => Run.EndFight(outcome == Outcome.Open ? Outcome.Fled : outcome), () =>
            {
                _fighting = false;
                _combat.Clear();
                LayChapterMap();
                Refresh();
            });
        }

        // the question behind a reaction set to Ask: a card, and the rules wait for the answer
        sealed class TableAsker : IReactionAsker
        {
            readonly PlayDirector _director;
            readonly bool _auto;

            public TableAsker(PlayDirector director, bool auto)
            {
                _director = director;
                _auto = auto;
            }

            public bool Ask(ReactionQuestion question)
            {
                if (_auto || MainQueue.OnMain) return true;

                return MainQueue.Ask<bool>(answer =>
                {
                    string other = LogText.NameOf(question.Other, _director._combat.Battle, _director.Run.Hero.Name);
                    _director._ui.AddChild(new AskCard(question, other, answer));
                    return true;
                });
            }
        }
    }
}
