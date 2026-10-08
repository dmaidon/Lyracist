// Edited on Oct 7, 2026 @ 20:15:00 -> Add singer AllowRecording opt-in and singer performance recordings list
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Lyracist.Services.Database;
using Lyracist.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace Lyracist.ViewModels
{
    public partial class SingerPerformanceHistoryItem : ObservableObject
    {
        public int SingerHistoryId { get; set; }
        public string SingerName { get; set; } = string.Empty;
        public string SongTitle { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Source { get; set; } = "Local";
        public string Key { get; set; } = "0";
        public double Tempo { get; set; } = 1.0;
        public DateTime Timestamp { get; set; }

        public string FormattedDate => Timestamp.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
        public string FormattedKeyTempo => $"Key: {Key} | Speed: {Tempo:0.0}x";
    }

    public partial class SingerItem : ObservableObject
    {
        public int SingerId { get; set; }

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _pinCode = string.Empty;

        [ObservableProperty]
        private string _vocalRange = string.Empty;

        [ObservableProperty]
        private string _customTitle = string.Empty;

        [ObservableProperty]
        private string _notes = string.Empty;

        [ObservableProperty]
        private int _score;

        [ObservableProperty]
        private int _totalSongsSung;

        [ObservableProperty]
        private string _avatarType = "None";

        [ObservableProperty]
        private string _avatarSource = string.Empty;

        [ObservableProperty]
        private ImageSource? _avatarImage;

        [ObservableProperty]
        private bool _allowRecording;

        public string LevelText { get => $"Lvl {SingerXpHelper.CalculateLevel(SingerXpHelper.CalculateXP(TotalSongsSung, Score))}"; set { } }
        public string SongsText { get => $"{TotalSongsSung} songs"; set { } }
    }

    public partial class UsersViewModel : BaseViewModel
    {
        private readonly ILibraryService _libraryService;
        private readonly RotationViewModel _rotationViewModel;

        public ObservableCollection<SingerItem> AllSingers { get; } = [];
        public ObservableCollection<SingerItem> FilteredSingers { get; } = [];
        public ObservableCollection<SingerItem> MergeCandidates { get; } = [];
        public ObservableCollection<SingerPerformanceHistoryItem> PerformanceHistory { get; } = [];

        public List<string> VocalRanges { get; } =
        [
            "Any / Unspecified",
            "Soprano",
            "Mezzo-Soprano",
            "Contralto",
            "Countertenor",
            "Tenor",
            "Baritone",
            "Bass"
        ];

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        partial void OnSearchQueryChanged(string value) => ApplyFilter();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedSinger))]
        [NotifyPropertyChangedFor(nameof(HasNoSelectedSinger))]
        private SingerItem? _selectedSinger;

        public bool HasSelectedSinger => SelectedSinger != null;
        public bool HasNoSelectedSinger => SelectedSinger == null;

        partial void OnSelectedSingerChanged(SingerItem? oldValue, SingerItem? newValue)
        {
            if (newValue != null)
            {
                LoadSingerDetails(newValue);
            }
            else
            {
                ClearSingerDetails();
            }
        }

        // Editable details
        [ObservableProperty] private string _editName = string.Empty;
        [ObservableProperty] private string _editEmail = string.Empty;
        [ObservableProperty] private string _editPinCode = string.Empty;
        [ObservableProperty] private string _editVocalRange = string.Empty;
        [ObservableProperty] private string _editCustomTitle = string.Empty;
        [ObservableProperty] private string _editNotes = string.Empty;
        [ObservableProperty] private int _editScore;
        [ObservableProperty] private int _editTotalSongsSung;
        [ObservableProperty] private string _editAvatarType = "None";
        [ObservableProperty] private string _editAvatarSource = string.Empty;
        [ObservableProperty] private ImageSource? _editAvatarImage;
        [ObservableProperty] private bool _editAllowRecording;
        public ObservableCollection<PerformanceRecording> SingerRecordings { get; } = [];

        // Audio defaults
        [ObservableProperty] private double _editGain = 100.0;
        [ObservableProperty] private int _editKey = 0;
        [ObservableProperty] private double _editTempo = 1.0;
        [ObservableProperty] private double _editTreble = 0.0;
        [ObservableProperty] private double _editMid = 0.0;
        [ObservableProperty] private double _editBass = 0.0;
        [ObservableProperty] private double _editCompressor = 0.0;
        [ObservableProperty] private double _editLimiter = 0.0;
        [ObservableProperty] private string _editAudioNotes = string.Empty;

        // Status & feedback
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isStatusSuccess;
        [ObservableProperty] private bool _hasStatusMessage;

        // Merge Popup
        [ObservableProperty] private bool _isMergePopupOpen;
        [ObservableProperty] private SingerItem? _selectedDuplicateSinger;

        // Webcam Capture
        private readonly WebcamCaptureService _webcamService = new();
        [ObservableProperty] private bool _isWebcamOpen;
        [ObservableProperty] private ObservableCollection<WebcamDeviceInfo> _availableCameras = [];
        [ObservableProperty] private WebcamDeviceInfo? _selectedCamera;
        [ObservableProperty] private ImageSource? _webcamLiveFrame;
        [ObservableProperty] private ImageSource? _webcamCapturedFrame;
        [ObservableProperty] private bool _isWebcamFrozen;
        [ObservableProperty] private bool _hasWebcamDevice;
        [ObservableProperty] private string _webcamStatusMessage = string.Empty;

        public bool HasNoWebcamDevice => !HasWebcamDevice;
        public bool IsWebcamStreaming => HasWebcamDevice && !IsWebcamFrozen;

        partial void OnHasWebcamDeviceChanged(bool value)
        {
            OnPropertyChanged(nameof(HasNoWebcamDevice));
            OnPropertyChanged(nameof(IsWebcamStreaming));
        }

        partial void OnIsWebcamFrozenChanged(bool value)
        {
            OnPropertyChanged(nameof(IsWebcamStreaming));
        }

        partial void OnSelectedCameraChanged(WebcamDeviceInfo? value)
        {
            if (IsWebcamOpen && !IsWebcamFrozen && value != null)
            {
                _ = StartCameraStreamingAsync(value);
            }
        }

        public UsersViewModel(ILibraryService libraryService, RotationViewModel rotationViewModel)
        {
            _libraryService = libraryService;
            _rotationViewModel = rotationViewModel;
            _ = LoadSingersAsync();
        }

        public async Task LoadSingersAsync()
        {
            try
            {
                await using var context = new LyracistDbContext();
                var dbSingers = await context.Singers
                    .Include(s => s.AudioSettings)
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                AllSingers.Clear();
                foreach (var s in dbSingers)
                {
                    var item = new SingerItem
                    {
                        SingerId = s.SingerId,
                        Name = s.Name,
                        Email = s.Email,
                        PinCode = s.PinCode,
                        VocalRange = string.IsNullOrWhiteSpace(s.VocalRange) ? "Any / Unspecified" : s.VocalRange,
                        CustomTitle = s.CustomTitle,
                        Notes = s.Notes,
                        Score = s.Score,
                        TotalSongsSung = s.TotalSongsSung,
                        AvatarType = s.AvatarType,
                        AvatarSource = s.AvatarSource,
                        AvatarImage = ResolveAvatarImage(s.AvatarType, s.AvatarSource),
                        AllowRecording = s.AllowRecording
                    };
                    AllSingers.Add(item);
                }

                ApplyFilter();

                if (SelectedSinger != null)
                {
                    var reselected = AllSingers.FirstOrDefault(s => s.SingerId == SelectedSinger.SingerId);
                    SelectedSinger = reselected ?? AllSingers.FirstOrDefault();
                }
                else if (AllSingers.Count > 0)
                {
                    SelectedSinger = AllSingers[0];
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UsersViewModel.LoadSingersAsync: failed to load singers");
            }
        }

        private void ApplyFilter()
        {
            FilteredSingers.Clear();
            string query = SearchQuery?.Trim() ?? string.Empty;

            foreach (var singer in AllSingers)
            {
                if (string.IsNullOrWhiteSpace(query) ||
                    singer.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    singer.Email.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    singer.CustomTitle.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    FilteredSingers.Add(singer);
                }
            }
        }

        private void LoadSingerDetails(SingerItem singer)
        {
            EditName = singer.Name;
            EditEmail = singer.Email;
            EditPinCode = singer.PinCode;
            EditVocalRange = string.IsNullOrWhiteSpace(singer.VocalRange) ? "Any / Unspecified" : singer.VocalRange;
            EditCustomTitle = singer.CustomTitle;
            EditNotes = singer.Notes;
            EditScore = singer.Score;
            EditTotalSongsSung = singer.TotalSongsSung;
            EditAvatarType = singer.AvatarType;
            EditAvatarSource = singer.AvatarSource;
            EditAvatarImage = ResolveAvatarImage(singer.AvatarType, singer.AvatarSource);

            // Load audio settings from LibraryService
            var audio = _libraryService.GetSingerSettings(singer.Name);
            EditGain = audio.Gain;
            EditKey = audio.Key;
            EditTempo = audio.Tempo <= 0 ? 1.0 : audio.Tempo;
            EditTreble = audio.Treble;
            EditMid = audio.Mid;
            EditBass = audio.Bass;
            EditCompressor = audio.Compressor;
            EditLimiter = audio.Limiter;
            EditAudioNotes = audio.Notes ?? string.Empty;

            // Load song performance history
            LoadPerformanceHistory(singer.Name);
            LoadSingerRecordings(singer.Name);
            EditAllowRecording = singer.AllowRecording;
            ClearStatusMessage();
        }

        private void ClearSingerDetails()
        {
            EditName = string.Empty;
            EditEmail = string.Empty;
            EditPinCode = string.Empty;
            EditVocalRange = "Any / Unspecified";
            EditCustomTitle = string.Empty;
            EditNotes = string.Empty;
            EditScore = 0;
            EditTotalSongsSung = 0;
            EditAllowRecording = false;
            SingerRecordings.Clear();
            AvatarImageHelper.DeleteRawCopy(Globals.AvatarsDir, EditAvatarSource);
            EditAvatarType = "None";
            EditAvatarSource = string.Empty;
            EditAvatarImage = null;

            EditGain = 100.0;
            EditKey = 0;
            EditTempo = 1.0;
            EditTreble = 0.0;
            EditMid = 0.0;
            EditBass = 0.0;
            EditCompressor = 0.0;
            EditLimiter = 0.0;
            EditAudioNotes = string.Empty;

            PerformanceHistory.Clear();
            ClearStatusMessage();
        }

        public void LoadPerformanceHistory(string singerName)
        {
            PerformanceHistory.Clear();
            if (string.IsNullOrWhiteSpace(singerName)) return;

            try
            {
                var historyEntries = SingerHistoryService.GetHistory(singerName);
                foreach (var h in historyEntries)
                {
                    PerformanceHistory.Add(new SingerPerformanceHistoryItem
                    {
                        SingerHistoryId = h.SingerHistoryId,
                        SingerName = h.SingerName,
                        SongTitle = h.SongTitle,
                        Artist = h.Artist,
                        Link = h.Link,
                        Source = h.Source,
                        Key = h.Key,
                        Tempo = h.Tempo,
                        Timestamp = h.Timestamp
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UsersViewModel.LoadPerformanceHistory");
            }
        }

        [RelayCommand]
        private async Task SaveSingerAsync()
        {
            if (SelectedSinger == null) return;
            if (string.IsNullOrWhiteSpace(EditName))
            {
                ShowStatus("Singer name is required.", false);
                return;
            }

            string cleanName = NameFormatting.ProperCase(EditName.Trim());
            string oldName = SelectedSinger.Name;

            try
            {
                await using var context = new LyracistDbContext();
                var dbSinger = await context.Singers.FirstOrDefaultAsync(s => s.SingerId == SelectedSinger.SingerId);
                if (dbSinger == null)
                {
                    ShowStatus("Singer account not found in database.", false);
                    return;
                }

                // Check for name collisions if name changed
                if (!string.Equals(oldName, cleanName, StringComparison.OrdinalIgnoreCase))
                {
                    bool exists = await context.Singers.AnyAsync(s => s.SingerId != SelectedSinger.SingerId && s.Name.ToLower() == cleanName.ToLower());
                    if (exists)
                    {
                        ShowStatus($"An account with the name '{cleanName}' already exists. Use 'Merge Accounts' instead.", false);
                        return;
                    }
                }

                dbSinger.Name = cleanName;
                dbSinger.Email = EditEmail?.Trim() ?? string.Empty;
                dbSinger.PinCode = EditPinCode?.Trim() ?? string.Empty;
                dbSinger.VocalRange = EditVocalRange == "Any / Unspecified" ? string.Empty : (EditVocalRange ?? string.Empty);
                dbSinger.CustomTitle = EditCustomTitle?.Trim() ?? string.Empty;
                dbSinger.Notes = EditNotes?.Trim() ?? string.Empty;
                dbSinger.Score = EditScore;
                dbSinger.TotalSongsSung = EditTotalSongsSung;
                dbSinger.AvatarType = EditAvatarType;
                dbSinger.AvatarSource = EditAvatarSource;
                dbSinger.AllowRecording = EditAllowRecording;

                await context.SaveChangesAsync();

                // Save audio defaults
                var audioSettings = new SingerAudioSettings
                {
                    Gain = EditGain,
                    Key = EditKey,
                    Tempo = EditTempo,
                    Treble = EditTreble,
                    Mid = EditMid,
                    Bass = EditBass,
                    Compressor = EditCompressor,
                    Limiter = EditLimiter,
                    Notes = EditAudioNotes
                };
                _libraryService.SaveSingerSettings(cleanName, audioSettings);

                // If name changed, migrate history and audio settings
                if (!string.Equals(oldName, cleanName, StringComparison.OrdinalIgnoreCase))
                {
                    SingerHistoryService.MergeHistory(oldName, cleanName);
                }

                // Update in-memory item
                SelectedSinger.Name = cleanName;
                SelectedSinger.Email = dbSinger.Email;
                SelectedSinger.PinCode = dbSinger.PinCode;
                SelectedSinger.VocalRange = dbSinger.VocalRange;
                SelectedSinger.CustomTitle = dbSinger.CustomTitle;
                SelectedSinger.Notes = dbSinger.Notes;
                SelectedSinger.Score = dbSinger.Score;
                SelectedSinger.TotalSongsSung = dbSinger.TotalSongsSung;
                SelectedSinger.AvatarType = dbSinger.AvatarType;
                SelectedSinger.AvatarSource = dbSinger.AvatarSource;
                SelectedSinger.AvatarImage = ResolveAvatarImage(dbSinger.AvatarType, dbSinger.AvatarSource);
                SelectedSinger.AllowRecording = dbSinger.AllowRecording;

                ShowStatus($"Successfully saved profile for '{cleanName}'.", true);
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UsersViewModel.SaveSingerAsync");
                ShowStatus("Failed to save singer profile.", false);
            }
        }

        [RelayCommand]
        private async Task AddNewSingerAsync()
        {
            try
            {
                string baseName = "New Singer";
                string finalName = baseName;
                int counter = 1;

                await using var context = new LyracistDbContext();
                while (await context.Singers.AnyAsync(s => s.Name.ToLower() == finalName.ToLower()))
                {
                    counter++;
                    finalName = $"{baseName} {counter}";
                }

                var newSinger = new Singer
                {
                    Name = finalName,
                    JoinDate = DateTime.UtcNow,
                    AvatarType = "None",
                    AvatarSource = string.Empty
                };

                context.Singers.Add(newSinger);
                await context.SaveChangesAsync();

                var item = new SingerItem
                {
                    SingerId = newSinger.SingerId,
                    Name = newSinger.Name,
                    AvatarType = "None",
                    AvatarSource = string.Empty
                };

                AllSingers.Add(item);
                ApplyFilter();
                SelectedSinger = item;
                ShowStatus($"Created new singer account '{finalName}'. Edit details and click Save.", true);
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UsersViewModel.AddNewSingerAsync");
                ShowStatus("Failed to create new singer.", false);
            }
        }

        [RelayCommand]
        private async Task DeleteSingerAsync()
        {
            if (SelectedSinger == null) return;
            string singerName = SelectedSinger.Name;
            int singerId = SelectedSinger.SingerId;

            try
            {
                await using var context = new LyracistDbContext();
                var dbSinger = await context.Singers.FindAsync(singerId);
                if (dbSinger != null)
                {
                    context.Singers.Remove(dbSinger);
                    await context.SaveChangesAsync();
                }

                AllSingers.Remove(SelectedSinger);
                ApplyFilter();
                SelectedSinger = FilteredSingers.FirstOrDefault();
                ShowStatus($"Deleted singer '{singerName}'.", true);
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UsersViewModel.DeleteSingerAsync");
                ShowStatus("Failed to delete singer.", false);
            }
        }

        [RelayCommand]
        private void UploadAvatar()
        {
            if (SelectedSinger == null) return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"Select Avatar Photo for {SelectedSinger.Name}",
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string avatarsDir = Globals.AvatarsDir;
                    Directory.CreateDirectory(avatarsDir);
                    AvatarImageHelper.DeleteRawCopy(avatarsDir, EditAvatarSource);
                    AvatarImageHelper.PurgeStaleRawCopies(avatarsDir, TimeSpan.FromDays(7));
                    string targetFileName = $"avatar_{Guid.NewGuid():N}.jpg";
                    string targetPath = Path.Combine(avatarsDir, targetFileName);
                    string rawPath = Path.Combine(avatarsDir, $"raw_{targetFileName}");

                    byte[] rawBytes = File.ReadAllBytes(dlg.FileName);
                    byte[] normalizedBytes = AvatarImageHelper.NormalizeImageBytes(rawBytes);
                    File.WriteAllBytes(rawPath, normalizedBytes);
                    File.WriteAllBytes(targetPath, normalizedBytes);

                    EditAvatarType = "Uploaded";
                    EditAvatarSource = targetFileName;
                    EditAvatarImage = AvatarImageHelper.LoadOrientedBitmap(normalizedBytes);

                    var adjustWin = new Windows.AdjustAvatarWindow(rawPath, targetPath, SelectedSinger.Name)
                    {
                        Owner = System.Windows.Application.Current?.MainWindow
                    };
                    if (adjustWin.ShowDialog() == true)
                    {
                        EditAvatarImage = AvatarImageHelper.LoadOrientedBitmapFromFile(targetPath);
                        SelectedSinger.AvatarImage = EditAvatarImage;
                        ShowStatus("Avatar selected, centered, and saved. Click 'Save Changes' to apply.", true);
                    }
                    else
                    {
                        ShowStatus("Avatar selected. Click 'Center Face' anytime to reposition, or 'Save Changes' to apply.", true);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.LogError(ex, "UsersViewModel.UploadAvatar");
                    ShowStatus("Failed to copy avatar image.", false);
                }
            }
        }

        [RelayCommand]
        private void AdjustAvatar()
        {
            if (SelectedSinger == null || EditAvatarType != "Uploaded" || string.IsNullOrWhiteSpace(EditAvatarSource))
            {
                ShowStatus("Can only center uploaded photos.", false);
                return;
            }

            string avatarPath = Path.Combine(Globals.AvatarsDir, EditAvatarSource);
            string rawPath = Path.Combine(Globals.AvatarsDir, $"raw_{EditAvatarSource}");
            string sourcePath = File.Exists(rawPath) ? rawPath : avatarPath;

            if (!File.Exists(sourcePath))
            {
                ShowStatus("Photo file not found on disk.", false);
                return;
            }

            try
            {
                var adjustWin = new Windows.AdjustAvatarWindow(sourcePath, avatarPath, SelectedSinger.Name)
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };

                if (adjustWin.ShowDialog() == true)
                {
                    EditAvatarImage = AvatarImageHelper.LoadOrientedBitmapFromFile(avatarPath);
                    SelectedSinger.AvatarImage = EditAvatarImage;
                    ShowStatus("Avatar repositioned and centered. Click 'Save Changes' to apply.", true);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UsersViewModel.AdjustAvatar");
                ShowStatus("Failed to open photo centering tool.", false);
            }
        }

        [RelayCommand]
        private void RotateAvatar()
        {
            if (SelectedSinger == null || EditAvatarType != "Uploaded" || string.IsNullOrWhiteSpace(EditAvatarSource)) return;

            string fullPath = Path.Combine(Globals.AvatarsDir, EditAvatarSource);
            if (!File.Exists(fullPath)) return;

            try
            {
                byte[] rawBytes = File.ReadAllBytes(fullPath);
                byte[] rotated = AvatarImageHelper.RotateImage90Degrees(rawBytes);
                File.WriteAllBytes(fullPath, rotated);
                AvatarImageHelper.RotateRawCopy(Globals.AvatarsDir, EditAvatarSource);

                EditAvatarImage = AvatarImageHelper.LoadOrientedBitmap(rotated);
                SelectedSinger.AvatarImage = EditAvatarImage;
                ShowStatus("Avatar rotated 90°. Click 'Save Changes' to apply.", true);
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UsersViewModel.RotateAvatar");
                ShowStatus("Failed to rotate avatar.", false);
            }
        }

        [RelayCommand]
        private void ClearAvatar()
        {
            AvatarImageHelper.DeleteRawCopy(Globals.AvatarsDir, EditAvatarSource);
            EditAvatarType = "None";
            EditAvatarSource = string.Empty;
            EditAvatarImage = null;
            ShowStatus("Avatar cleared. Click 'Save Changes' to apply.", true);
        }

        [RelayCommand]
        private async Task OpenWebcamCaptureAsync()
        {
            if (SelectedSinger == null) return;

            AvailableCameras.Clear();
            var cameras = WebcamCaptureService.GetAvailableCameras();
            foreach (var cam in cameras)
            {
                AvailableCameras.Add(cam);
            }

            WebcamCapturedFrame = null;
            IsWebcamFrozen = false;
            WebcamLiveFrame = null;
            IsWebcamOpen = true;

            if (AvailableCameras.Count == 0)
            {
                HasWebcamDevice = false;
                WebcamStatusMessage = "No webcam or video capture device detected on this system. Please plug in a webcam and try again.";
                SelectedCamera = null;
            }
            else
            {
                HasWebcamDevice = true;
                WebcamStatusMessage = string.Empty;
                SelectedCamera = AvailableCameras[0];
                await StartCameraStreamingAsync(SelectedCamera);
            }
        }

        private async Task StartCameraStreamingAsync(WebcamDeviceInfo camera)
        {
            await _webcamService.StopCaptureAsync();

            bool started = await _webcamService.StartCaptureAsync(camera.Descriptor, frame =>
            {
                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
                {
                    if (IsWebcamOpen && !IsWebcamFrozen)
                    {
                        WebcamLiveFrame = frame;
                    }
                });
            });

            if (!started)
            {
                WebcamStatusMessage = "Failed to start camera preview. The camera may be in use by another application.";
            }
        }

        [RelayCommand]
        private void CaptureWebcamSnapshot()
        {
            if (WebcamLiveFrame != null)
            {
                WebcamCapturedFrame = WebcamLiveFrame;
                IsWebcamFrozen = true;
            }
        }

        [RelayCommand]
        private async Task RetakeWebcamSnapshotAsync()
        {
            WebcamCapturedFrame = null;
            IsWebcamFrozen = false;
            if (SelectedCamera != null)
            {
                await StartCameraStreamingAsync(SelectedCamera);
            }
        }

        [RelayCommand]
        private async Task ApplyWebcamSnapshotAsync()
        {
            if (WebcamCapturedFrame is BitmapSource source)
            {
                var result = WebcamCaptureService.SaveSquarePhoto(source, Globals.AvatarsDir);
                if (result.HasValue)
                {
                    AvatarImageHelper.DeleteRawCopy(Globals.AvatarsDir, EditAvatarSource);
                    EditAvatarType = "Uploaded";
                    EditAvatarSource = result.Value.fileName;
                    EditAvatarImage = result.Value.squareBitmap;

                    await CloseWebcamCaptureAsync();
                    ShowStatus("Performer photo captured from webcam. Click 'Save Changes' to apply.", true);
                    return;
                }
            }

            ShowStatus("Failed to save webcam photo.", false);
        }

        [RelayCommand]
        private async Task CloseWebcamCaptureAsync()
        {
            await _webcamService.StopCaptureAsync();
            IsWebcamOpen = false;
            IsWebcamFrozen = false;
            WebcamLiveFrame = null;
            WebcamCapturedFrame = null;
        }

        // Merge Accounts
        [RelayCommand]
        private void OpenMergePopup()
        {
            if (SelectedSinger == null) return;

            MergeCandidates.Clear();
            foreach (var s in AllSingers.Where(s => s.SingerId != SelectedSinger.SingerId))
            {
                MergeCandidates.Add(s);
            }

            SelectedDuplicateSinger = MergeCandidates.FirstOrDefault();
            IsMergePopupOpen = true;
        }

        [RelayCommand]
        private void CloseMergePopup()
        {
            IsMergePopupOpen = false;
        }

        [RelayCommand]
        private async Task ExecuteMergeAccountsAsync()
        {
            if (SelectedSinger == null || SelectedDuplicateSinger == null) return;
            if (SelectedSinger.SingerId == SelectedDuplicateSinger.SingerId) return;

            string targetName = SelectedSinger.Name;
            string duplicateName = SelectedDuplicateSinger.Name;
            int targetId = SelectedSinger.SingerId;
            int duplicateId = SelectedDuplicateSinger.SingerId;

            try
            {
                await using var context = new LyracistDbContext();
                var dbService = new DatabaseService(context);
                bool merged = await dbService.MergeSingers(targetId, duplicateId);
                if (merged)
                {
                    SingerHistoryService.MergeHistory(duplicateName, targetName);
                    IsMergePopupOpen = false;
                    await LoadSingersAsync();
                    SelectedSinger = AllSingers.FirstOrDefault(s => s.SingerId == targetId);
                    ShowStatus($"Successfully merged '{duplicateName}' into '{targetName}'. All performances and XP were consolidated.", true);
                }
                else
                {
                    ShowStatus("Failed to merge singer accounts.", false);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UsersViewModel.ExecuteMergeAccountsAsync");
                ShowStatus("An error occurred while merging accounts.", false);
            }
        }

        [RelayCommand]
        private void QueueHistoricalSong(SingerPerformanceHistoryItem item)
        {
            if (item == null || SelectedSinger == null) return;

            try
            {
                _rotationViewModel.AddSinger(
                    SelectedSinger.Name,
                    item.SongTitle,
                    item.Artist,
                    item.Key,
                    notes: "",
                    source: item.Source,
                    externalLink: item.Link,
                    tempo: item.Tempo);

                ShowStatus($"Queued '{item.SongTitle}' for {SelectedSinger.Name} into rotation!", true);
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UsersViewModel.QueueHistoricalSong");
                ShowStatus("Failed to add song to rotation.", false);
            }
        }

        [RelayCommand]
        private void DeleteHistoricalSong(SingerPerformanceHistoryItem item)
        {
            if (item == null) return;

            try
            {
                SingerHistoryService.DeleteHistoryEntry(item.SingerHistoryId);
                PerformanceHistory.Remove(item);
                ShowStatus($"Removed '{item.SongTitle}' from history.", true);
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UsersViewModel.DeleteHistoricalSong");
                ShowStatus("Failed to remove song from history.", false);
            }
        }

        private void ShowStatus(string message, bool isSuccess)
        {
            StatusMessage = message;
            IsStatusSuccess = isSuccess;
            HasStatusMessage = true;
        }

        private void ClearStatusMessage()
        {
            StatusMessage = string.Empty;
            HasStatusMessage = false;
        }

        private static ImageSource? ResolveAvatarImage(string avatarType, string avatarSource)
        {
            if (string.IsNullOrWhiteSpace(avatarSource)) return null;

            try
            {
                if (avatarType == "Uploaded")
                {
                    string fullPath = Path.Combine(Globals.AvatarsDir, avatarSource);
                    if (File.Exists(fullPath))
                    {
                        return AvatarImageHelper.LoadOrientedBitmapFromFile(fullPath);
                    }
                }
                else if (avatarType == "Gravatar")
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri($"https://www.gravatar.com/avatar/{avatarSource}?d=identicon&s=150", UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    return bitmap;
                }
            }
            catch
            {
                // Fallback null
            }

            return null;
        }

        private void LoadSingerRecordings(string singerName)
        {
            SingerRecordings.Clear();
            if (string.IsNullOrWhiteSpace(singerName)) return;

            try
            {
                using var context = new LyracistDbContext();
                var list = context.PerformanceRecordings
                    .AsNoTracking()
                    .Where(r => r.SingerName == singerName)
                    .OrderByDescending(r => r.StartedUtc)
                    .ToList();

                foreach (var rec in list)
                {
                    SingerRecordings.Add(rec);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UsersViewModel.LoadSingerRecordings");
            }
        }

        [RelayCommand]
        private void PlayRecording(PerformanceRecording? recording)
        {
            if (recording == null || !File.Exists(recording.FilePath)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = recording.FilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "PlayRecording failed");
            }
        }

        [RelayCommand]
        private void OpenRecordingFolder(PerformanceRecording? recording)
        {
            if (recording == null) return;
            try
            {
                string? folder = Path.GetDirectoryName(recording.FilePath);
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "OpenRecordingFolder failed");
            }
        }

        [RelayCommand]
        private async Task DeleteRecordingAsync(PerformanceRecording? recording)
        {
            if (recording == null) return;
            try
            {
                using var context = new LyracistDbContext();
                var dbRec = await context.PerformanceRecordings.FindAsync(recording.Id);
                if (dbRec != null)
                {
                    context.PerformanceRecordings.Remove(dbRec);
                    await context.SaveChangesAsync();
                }

                if (File.Exists(recording.FilePath))
                {
                    File.Delete(recording.FilePath);
                }

                SingerRecordings.Remove(recording);
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "DeleteRecordingAsync failed");
            }
        }
    }
}
