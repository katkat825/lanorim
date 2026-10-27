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
            // (--autostory, for a probe or a screenshot of a fight) walks on past it, as it does a line; so does a
            // screenshot of the companion's bubble (--say-bark)
            if (_auto || (_autoStory || _sayBark != null) && Run.InStartingShop)
            {
                Do(() => Run.LeaveShop());
                return;
            }

            var view = new PackView(Run.Hero, Run.Items, Run.Shop.Open(Run.Items), Run.Shop.Armory);
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
            // a Workshop test's end goes back to the editor, played by hand or by --auto (cc_task_q)
            if (GameState.Testing != null)
            {
                GD.Print($"test    the end at {GameState.Testing.Card}");
                BackToEditor();
                return;
            }

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
            if (GameState.Trying != null)
            {
                BackToBuilder();
                return;
            }

            // a Workshop test's end: back to the editor, the problems it saw listed there (cc_task_q)
            if (GameState.Testing != null)
            {
                BackToEditor();
                return;
            }

            GameState.Leave();
            Scenes.Go(GetTree(), Scenes.Book);
        }

        // PLAY THIS MAP, OVER (cc_task_l Part 5): back to the builder with the map as it was, not to the book
        void BackToBuilder()
        {
            GameState.StopTrying();
            Scenes.Go(GetTree(), Scenes.Table);
        }
    }
}
