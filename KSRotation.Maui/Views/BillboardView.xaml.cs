// Created on Sep 3, 2026 @ 08:32:00 -> Add code-behind for BillboardView with live clock
using Microsoft.Maui.Controls;
using System;

namespace KSRotation.Maui.Views
{
    public partial class BillboardView : ContentView
    {
        private IDispatcherTimer? _clockTimer;

        public BillboardView()
        {
            InitializeComponent();
            UpdateClock();
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
            if (Handler != null)
            {
                StartClock();
            }
            else
            {
                StopClock();
            }
        }

        private void StartClock()
        {
            if (_clockTimer != null) return;
            _clockTimer = Dispatcher.CreateTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (_, _) => UpdateClock();
            _clockTimer.Start();
        }

        private void StopClock()
        {
            _clockTimer?.Stop();
            _clockTimer = null;
        }

        private void UpdateClock()
        {
            if (LiveClockLabel != null)
            {
                LiveClockLabel.Text = DateTime.Now.ToString("h:mm tt");
            }
        }
    }
}
