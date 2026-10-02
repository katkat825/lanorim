using Content.Screens;
using Game.Screens;
using Godot;

namespace Game.Play
{
    public partial class PlayDirector
    {
        // --- a shop ------------------------------------------------------------------------------------

        void OpenShop()
        {
            // every new hero opens on the starting shop (cc_task_f 1.3); a run that takes the story as it comes
            // (--autostory, for a probe or a screenshot of a fight) walks on past it, as it does a line
            if (_auto || _autoStory && Run.InStartingShop)
            {
                Do(() => Run.LeaveShop());
                return;
            }

            var view = new PackView(Run.Hero, Run.Items, Run.Shop.Open(Run.Items));
            Open(new PackScreen(view, GameState.Resolver), () => Do(() => Run.LeaveShop()));
        }

        // --- death and the end -------------------------------------------------------------------

        void Died()
        {
            var death = new DeathView(GameState.Saves, Run.Pack.Id, Run.Slot);

            if (_auto)
            {
                GD.Print($"play    died ({_deaths})");

                if (_deaths > 12 || !death.CanReload)
                {
                    GD.PrintErr("play    FAILED - died too often to finish");
                    Quit(1);
                    return;
                }

                Scenes.Resume(GetTree(), death.Reload);
                return;
            }

            Open(new MenuCard(DeathView.TitleKey, death.CanReload ? null : DeathView.NoSaveKey, false,
                (DeathView.ReloadKey, () => Scenes.Resume(GetTree(), death.Reload), death.CanReload),
                (DeathView.BookKey, ToTheBook, true)));
        }

        void TheEnd()
        {
            if (_auto)
            {
                GD.Print($"play    the end - {Run.Hero}, {_deaths} deaths");
                Quit(0);
                return;
            }

            Open(new MenuCard(ScreenWords.TheEnd, ScreenWords.TheEndBlurb, false,
                (ScreenWords.ToTheBook, ToTheBook, true)));
        }

        void ToTheBook()
        {
            GameState.Leave();
            Scenes.Go(GetTree(), Scenes.Book);
        }
    }
}
