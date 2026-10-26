using Godot;

namespace Game.Screens
{
    // A PAGE AS TALL AS THE WINDOW ALLOWS, AND NO TALLER (cc_task_m3 Part 1): a long kit in the description area pushed
    // Back and Next off the bottom of the window. The list and the description area are both scroll boxes; each is as
    // tall as what's in it, the description first (what's selected is the thing being read), the list the room that's
    // left, and neither under its usual height. So a short page is as it was, a long one fills the window and scrolls,
    // and Back and Next stay on screen at every size (check-layout's worst pages, Dev/CreationWorst.cs).
    public partial class CreationScreen
    {
        // the usual heights, in the 1080-line canvas's pixels (Ui.Px scales them)
        const float ListHeight = 300f;

        static float AboutHeight => HudLayout.Current.CreationAboutHeight;

        void Refit() => Callable.From(Fit).CallDeferred();

        void Fit()
        {
            if (!IsInsideTree() || _scroll == null || !IsInstanceValid(_scroll) || !IsInstanceValid(_aboutScroll)) return;

            // what the list stands in: the scroll, or the row it shares with the mini (whose own height may be more)
            Control listRow = _scroll.GetParent() == this ? _scroll : _scroll.GetParent<Control>();
            bool about = _aboutScroll.Visible;

            float room = Holder();
            float others = GetCombinedMinimumSize().Y - listRow.GetCombinedMinimumSize().Y -
                           (about ? _aboutScroll.GetCombinedMinimumSize().Y : 0);

            float aboutUsual = Ui.Px(AboutHeight), listUsual = Ui.Px(ListHeight);

            float aboutTall = about
                ? Mathf.Max(aboutUsual, Mathf.Min(_about.GetCombinedMinimumSize().Y, room - others - listUsual))
                : aboutUsual;
            float listTall = Mathf.Max(listUsual,
                                       Mathf.Min(_body.GetCombinedMinimumSize().Y, room - others - (about ? aboutTall : 0)));

            Tall(_aboutScroll, aboutTall);
            Tall(_scroll, listTall);
        }

        static void Tall(Control control, float tall)
        {
            if (!Mathf.IsEqualApprox(control.CustomMinimumSize.Y, tall)) control.CustomMinimumSize = new Vector2(0, tall);
        }

        // the height this page may take: the window's, less the margins and panel edges between it and the page. Never a
        // box's own height, which grows with the page and would let it grow again. Outside a centring box (Launch.Show),
        // as in the creation probe's fixed box, nothing grows: each keeps its usual height
        float Holder()
        {
            float room = GetViewport().GetVisibleRect().Size.Y;
            bool centred = false;
            Control inside = this;

            for (Node up = GetParent(); up is Control box; up = box.GetParent())
            {
                if (box is CenterContainer) centred = true;
                else if (box is MarginContainer) room -= box.GetThemeConstant("margin_top") + box.GetThemeConstant("margin_bottom");
                else if (box is PanelContainer) room -= box.Size.Y - inside.Size.Y;

                inside = box;
            }

            return centred ? room : 0;
        }
    }
}
