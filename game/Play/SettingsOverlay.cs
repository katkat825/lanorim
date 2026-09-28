using Game.Screens;

namespace Game.Play
{
    // settings from the pause menu: the same panel as the book's, in a card
    public partial class SettingsOverlay : Overlay
    {
        protected override void Draw()
        {
            Body.AddChild(SettingsPanel.Scrolled(Close, 520));
        }
    }
}
