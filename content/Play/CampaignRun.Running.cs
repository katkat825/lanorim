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

            if (!Talk.Start(node))
            {
                Now = Scene.Over;
                return Now;
            }

            Autosave(SaveKind.ChapterStart);

            return Settle();
        }

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
                    Request request = Talk.Pending;

                    if (request.Kind == RequestKind.Fight)
                    {
                        Fight = Call(request.Id);

                        if (Fight == null)
                        {
                            // a fight the campaign does not have reads as won, and the author sees
                            // the complaint in a playthrough
                            Talk.Complained.Add($"no fight '{request.Id}' in this campaign");
                            Reply(new Answer { Outcome = Outcome.HeroesWon });
                            continue;
                        }

                        Autosave(SaveKind.FightStart);
                        return Now = Scene.Fight;
                    }

                    if (request.Kind == RequestKind.Shop)
                    {
                        Shop = Pack.Merchant(request.Id);

                        if (Shop == null)
                        {
                            Talk.Complained.Add($"no merchant '{request.Id}' in this campaign");
                            Reply(new Answer());
                            continue;
                        }

                        return Now = Scene.Shop;
                    }

                    Settled settled = Referee.Settle(request);
                    _settled.Add(settled);

                    if (request.Kind == RequestKind.Encounter && settled.Table?.Fights == true)
                        _lastEncounter = settled.Table;

                    Reply(settled.Answer ?? new Answer());

                    if (request.Kind == RequestKind.Level) Autosave(SaveKind.LevelUp);
                    if (request.Kind == RequestKind.Rest || settled.Rested)
                    {
                        Day.Slept();
                        Autosave(SaveKind.Rest);
                    }

                    continue;
                }

                if (Talk.IsOver) return Now = Scene.Over;

                if (Talk.IsChoosing) return Now = Scene.Choice;

                if (Talk.Saying != null) return Now = Scene.Line;

                if (!Talk.Advance() && !Talk.IsWaiting) return Now = Scene.Over;
            }

            return Now = Scene.Over;
        }
    }
}
