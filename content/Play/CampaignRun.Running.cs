using Content.Dialogue;
using Content.Saves;
using Core.Combat;

namespace Content.Play
{
    public sealed partial class CampaignRun
    {
        // --- running ---------------------------------------------------------------------------

        public Scene Start(string node = null)
        {
            node ??= Pack.Manifest?.Start ?? "";

            // THE STARTING SHOP (cc_task_e-shop-species-and-ui-notes.md 1.4): a new hero spends their gold before the
            // first scene, gear or gold (cc_task_f 1.3). Leaving it (LeaveShop) starts the story where this would have
            // a campaign can skip it (cc_task_i 4.2: the beginner tutorial teaches shopping in its story); the hero
            // then goes in wearing the best of what it has
            if (Hero.ShopsFirst && !(Pack.Manifest?.Rules.StartingShop ?? true))
            {
                Hero.ShopsFirst = false;
                Hero.WearTheBest();
            }

            if (Hero.ShopsFirst && Pack.StartingShop != null)
            {
                _startAfterShop = node;
                Shop = Pack.StartingShop;
                return Now = Scene.Shop;
            }

            if (!Talk.Start(node))
            {
                Now = Scene.Over;
                return Now;
            }

            Autosave(SaveKind.ChapterStart);

            return Settle();
        }

        // where the story starts once the starting shop is left; null when the shop open isn't that one
        string _startAfterShop;

        public bool InStartingShop => _startAfterShop != null;

        // the Continue button
        public Scene Next()
        {
            if (Now != Scene.Line) return Now;

            _settled.Clear();

            if (!Talk.Advance() && !Talk.IsWaiting) return Now = Scene.Over;

            return Settle();
        }

        public Scene Choose(int option)
        {
            if (Now != Scene.Choice) return Now;

            _settled.Clear();

            Choose(Talk, option);

            return Settle();
        }

        Scene Settle()
        {
            for (int guard = 0; guard < 10_000; guard++)
            {
                if (Talk.IsWaiting)
                {
                    if (Answered(Talk.Pending) is Scene stop) return Now = stop;

                    continue;
                }

                if (Talk.IsOver) return Now = Scene.Over;

                if (Talk.IsChoosing) return Now = Scene.Choice;

                if (Talk.Saying != null) return Now = Scene.Line;

                if (!Talk.Advance() && !Talk.IsWaiting) return Now = Scene.Over;
            }

            return Now = Scene.Over;
        }

        // WHAT THE STORY IS WAITING ON, answered: a fight or a shop is a scene the table plays (the scene), anything else
        // the referee settles and the story goes on (null). cc_task_j Part 3 took these steps out of Settle
        Scene? Answered(Request request)
        {
            if (request.Kind == RequestKind.Fight) return FightAsked(request);

            // a merchant's counter, or an armory's racks (cc_task_i 3.3): both the shop scene, which knows which
            if (request.Kind is RequestKind.Shop or RequestKind.Armory) return ShopAsked(request);

            Referees(request);
            return null;
        }

        Scene? FightAsked(Request request)
        {
            Fight = Call(request.Id);

            if (Fight == null)
            {
                // a fight the campaign does not have reads as won, and the author sees
                // the complaint in a playthrough
                Talk.Complained.Add($"no fight '{request.Id}' in this campaign");
                Reply(new Answer { Outcome = Outcome.HeroesWon });
                return null;
            }

            Autosave(SaveKind.FightStart);
            return Scene.Fight;
        }

        Scene? ShopAsked(Request request)
        {
            Shop = Pack.Merchant(request.Id);

            if (Shop == null || request.Kind == RequestKind.Armory && !Shop.Armory)
            {
                Talk.Complained.Add(request.Kind == RequestKind.Armory
                    ? $"no armory '{request.Id}' in this campaign - a merchant with \"armory\": true"
                    : $"no merchant '{request.Id}' in this campaign");
                Shop = null;
                Reply(new Answer());
                return null;
            }

            return Scene.Shop;
        }

        // everything else the story asks of the table: the referee settles it, and the run hears what came of it
        void Referees(Request request)
        {
            int levelWas = Hero.Level;
            Settled settled = Referee.Settle(request);
            _settled.Add(settled);

            // what the referee couldn't do as written, for the author's playthrough (a missing item, an <<xp>>
            // in a milestone campaign)
            if (settled.Problem != null) Talk.Complained.Add(settled.Problem);

            if (settled.Heard) Heard?.Invoke();
            if (settled.Cue is Bark cue) Cued?.Invoke(cue);
            if (settled.Attempt != null) Tested?.Invoke(settled.Attempt);

            if (request.Kind == RequestKind.Encounter && settled.Table?.Fights == true)
                _lastEncounter = settled.Table;

            Reply(settled.Answer ?? new Answer());

            if (request.Kind == RequestKind.Level || request.Kind == RequestKind.Xp && Hero.Level > levelWas)
                Autosave(SaveKind.LevelUp);
            if (request.Kind == RequestKind.Rest || settled.Rested)
            {
                Day.Slept();
                Autosave(SaveKind.Rest);
            }
        }
    }
}
