using System;
using System.Collections.Generic;
using UnityEngine;

namespace Leeway.MainMenu.Navigation
{
    /// <summary>
    /// Shows exactly one <see cref="ScreenView"/> at a time and remembers how the player got there.
    /// </summary>
    public sealed class ScreenNavigator : IScreenNavigator
    {
        private readonly Dictionary<MenuScreen, ScreenView> _views = new();
        private readonly Stack<MenuScreen> _history = new();

        public ScreenNavigator(IReadOnlyList<ScreenView> views)
        {
            foreach (var view in views)
            {
                if (_views.ContainsKey(view.Screen))
                {
                    Debug.LogError($"[ScreenNavigator] Two views claim {view.Screen}: {_views[view.Screen].name} and {view.name}.", view);
                    continue;
                }
                _views.Add(view.Screen, view);
                view.SetVisibleImmediate(false);
            }

            Current = MenuScreen.Main;
            if (_views.TryGetValue(Current, out var root)) root.SetVisibleImmediate(true);
        }

        public MenuScreen Current { get; private set; }

        public event Action<MenuScreen> Changed;

        public void Push(MenuScreen screen)
        {
            if (screen == Current) return;
            _history.Push(Current);
            SwitchTo(screen);
        }

        public bool Back()
        {
            if (_history.Count == 0) return false;
            SwitchTo(_history.Pop());
            return true;
        }

        public void ResetTo(MenuScreen screen)
        {
            _history.Clear();
            SwitchTo(screen);
        }

        private void SwitchTo(MenuScreen screen)
        {
            if (_views.TryGetValue(Current, out var from) && screen != Current) from.Hide();
            Current = screen;
            if (_views.TryGetValue(screen, out var to)) to.Show();
            else Debug.LogError($"[ScreenNavigator] No view for {screen}.");
            Changed?.Invoke(screen);
        }
    }
}
