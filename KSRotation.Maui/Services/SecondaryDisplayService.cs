// Edited on Sep 3, 2026 @ 11:09:00 -> Enforce display-specific context and MatchParent layout for landscape TV presentation
using System;
using KSRotation.ViewModels;
using KSRotation.Maui.Views;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

#if ANDROID
using Android.App;
using Android.Content;
using Android.Hardware.Display;
using Android.OS;
using Android.Views;
using Microsoft.Maui.Platform;
#endif

namespace KSRotation.Maui.Services
{
    public class SecondaryDisplayService
    {
        private static readonly Lazy<SecondaryDisplayService> _instance = new(() => new SecondaryDisplayService());
        public static SecondaryDisplayService Instance => _instance.Value;

        private MainViewModel? _viewModel;
        private IMauiContext? _mauiContext;

#if ANDROID
        private DisplayManager? _displayManager;
        private DisplayListenerImpl? _displayListener;
        private BillboardPresentation? _activePresentation;
#elif WINDOWS
        private Microsoft.Maui.Controls.Window? _secondaryWindow;
#endif

        public event Action<bool>? DisplayConnectionChanged;

        public bool IsSecondaryDisplayConnected
        {
            get
            {
#if ANDROID
                if (_displayManager == null) return false;
                Android.Views.Display[]? displays = _displayManager.GetDisplays(DisplayManager.DisplayCategoryPresentation);
                return displays != null && displays.Length > 0;
#elif WINDOWS
                return true; // Windows always allows opening secondary billboard window
#else
                return false;
#endif
            }
        }

        public bool IsSecondaryDisplayActive
        {
            get
            {
#if ANDROID
                return _activePresentation != null && _activePresentation.IsShowing;
#elif WINDOWS
                return _secondaryWindow != null;
#else
                return false;
#endif
            }
        }

        public void Initialize(MainViewModel vm, IMauiContext mauiContext)
        {
            _viewModel = vm;
            _mauiContext = mauiContext;

#if ANDROID
            try
            {
                Context context = Android.App.Application.Context;
                _displayManager = (DisplayManager?)context.GetSystemService(Context.DisplayService);
                if (_displayManager != null)
                {
                    _displayListener = new DisplayListenerImpl(this);
                    Looper? mainLooper = Looper.MainLooper;
                    if (mainLooper != null)
                    {
#pragma warning disable CS0618 // Type or member is obsolete in newer Android SDKs
                        using Handler handler = new(mainLooper);
                        _displayManager.RegisterDisplayListener(_displayListener, handler);
#pragma warning restore CS0618
                    }
                    CheckAndAutoShowPresentation();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SecondaryDisplayService] Android DisplayManager error: {ex.Message}");
            }
#endif
        }

        public void ToggleSecondaryBillboard()
        {
            if (_viewModel == null) return;

            if (IsSecondaryDisplayActive)
            {
                HideSecondaryBillboard();
            }
            else
            {
                ShowSecondaryBillboard();
            }
        }

        public void ShowSecondaryBillboard()
        {
            if (_viewModel == null) return;

#if ANDROID
            CheckAndAutoShowPresentation();
#elif WINDOWS
            if (_secondaryWindow != null) return;
            try
            {
                _secondaryWindow = new Microsoft.Maui.Controls.Window(new BillboardPage(_viewModel))
                {
                    Title = "KSRotation - Audience Billboard",
                    Width = 1280,
                    Height = 720
                };
                _secondaryWindow.Destroying += (_, _) =>
                {
                    _secondaryWindow = null;
                    DisplayConnectionChanged?.Invoke(false);
                };
                Microsoft.Maui.Controls.Application.Current?.OpenWindow(_secondaryWindow);
                DisplayConnectionChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SecondaryDisplayService] Windows window error: {ex.Message}");
            }
#endif
        }

        public void HideSecondaryBillboard()
        {
#if ANDROID
            if (_activePresentation != null)
            {
                try
                {
                    _activePresentation.Dismiss();
                }
                catch { }
                _activePresentation = null;
                DisplayConnectionChanged?.Invoke(false);
            }
#elif WINDOWS
            if (_secondaryWindow != null)
            {
                try
                {
                    Microsoft.Maui.Controls.Application.Current?.CloseWindow(_secondaryWindow);
                }
                catch { }
                _secondaryWindow = null;
                DisplayConnectionChanged?.Invoke(false);
            }
#endif
        }

#if ANDROID
        internal void CheckAndAutoShowPresentation()
        {
            if (_displayManager == null || _viewModel == null || _mauiContext == null) return;

            try
            {
                Android.Views.Display[]? displays = _displayManager.GetDisplays(DisplayManager.DisplayCategoryPresentation);
                if (displays != null && displays.Length > 0)
                {
                    Android.Views.Display presentationDisplay = displays[0];
                    if (_activePresentation == null || !_activePresentation.IsShowing)
                    {
                        Activity? currentActivity = Platform.CurrentActivity;
                        Context baseContext = (Context?)currentActivity ?? Android.App.Application.Context;
                        Context presentationContext = baseContext.CreateDisplayContext(presentationDisplay) ?? baseContext;

                        _activePresentation = new BillboardPresentation(presentationContext, presentationDisplay, _viewModel, _mauiContext);
                        _activePresentation.Show();
                        DisplayConnectionChanged?.Invoke(true);
                    }
                }
                else
                {
                    if (_activePresentation != null)
                    {
                        _activePresentation.Dismiss();
                        _activePresentation = null;
                        DisplayConnectionChanged?.Invoke(false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SecondaryDisplayService] Presentation display error: {ex.Message}");
            }
        }

        private sealed class DisplayListenerImpl(SecondaryDisplayService service) : Java.Lang.Object, DisplayManager.IDisplayListener
        {
            private readonly SecondaryDisplayService _service = service;

            public void OnDisplayAdded(int displayId)
            {
                _service.CheckAndAutoShowPresentation();
            }

            public void OnDisplayChanged(int displayId)
            {
                _service.CheckAndAutoShowPresentation();
            }

            public void OnDisplayRemoved(int displayId)
            {
                _service.CheckAndAutoShowPresentation();
            }
        }

        private sealed class BillboardPresentation : Presentation
        {
            private readonly MainViewModel _vm;
            private readonly IMauiContext _mauiContext;

            public BillboardPresentation(Context outerContext, Android.Views.Display display, MainViewModel vm, IMauiContext mauiContext)
                : base(outerContext, display)
            {
                _vm = vm;
                _mauiContext = mauiContext;
            }

            protected override void OnCreate(Bundle? savedInstanceState)
            {
                base.OnCreate(savedInstanceState);
                try
                {
                    Window?.RequestFeature(WindowFeatures.NoTitle);
                    Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
                    BillboardView billboardView = new() { BindingContext = _vm };
                    Android.Views.View nativeView = billboardView.ToPlatform(_mauiContext);
                    SetContentView(nativeView, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BillboardPresentation] OnCreate error: {ex.Message}");
                }
            }
        }
#endif
    }
}
