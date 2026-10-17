using System;

namespace Leeway.MainMenu.Navigation
{
    /// <summary>A back-stack of menu pages: push forward, pop back, reset to a root.</summary>
    public interface IScreenNavigator
    {
        MenuScreen Current { get; }

        void Push(MenuScreen screen);

        /// <summary>Returns to the previous page. False when already at the root.</summary>
        bool Back();

        /// <summary>Clears the history and shows <paramref name="screen"/> as the new root.</summary>
        void ResetTo(MenuScreen screen);

        event Action<MenuScreen> Changed;
    }
}
