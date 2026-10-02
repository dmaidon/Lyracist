// Edited on Oct 2, 2026 @ 14:10:00 -> Rotate and clean up raw_ avatar copies
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
using KSRotation.Models;
using KSRotation.Services;
using Lyracist.Core.Helpers;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Lyracist.Services.Database;
using Lyracist.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace KSRotation.ViewModels
{
#if !MAUI
    public partial class SingerUserItem : ObservableObject
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

        public string LevelText { get => $"Lvl {SingerXpHelper.CalculateLevel(SingerXpHelper.CalculateXP(TotalSongsSung, Score))}"; set { } }
        public string SongsText { get => $"{TotalSongsSung} songs"; set { } }
    }

    public partial class SingerUserHistoryItem : ObservableObject
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

    public partial class MainViewModel
    {
        public ObservableCollection<SingerUserItem> AllUsers { get; } = [];
        public ObservableCollection<SingerUserItem> FilteredUsers { get; } = [];
        public ObservableCollection<SingerUserItem> UserMergeCandidates { get; } = [];
        public ObservableCollection<SingerUserHistoryItem> UserPerformanceHistory { get; } = [];

        public List<string> VocalRangeOptions { get; } =
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
        [NotifyPropertyChangedFor(nameof(HasUserSearchText))]
        private string _userSearchText = string.Empty;

        public bool HasUserSearchText => !string.IsNullOrWhiteSpace(UserSearchText);

        [RelayCommand]
        private void ClearUserSearch()
        {
            UserSearchText = string.Empty;
        }

        [ObservableProperty] private SingerUserItem? _selectedUser;
        [ObservableProperty] private SingerUserItem? _selectedMergeUser;
        [ObservableProperty] private bool _isUserMergeOpen;
        [ObservableProperty] private string _userStatusMessage = string.Empty;
        [ObservableProperty] private bool _hasUserStatusMessage;
        [ObservableProperty] private bool _isUserStatusSuccess;

        public bool HasSelectedUser => SelectedUser != null;
        public bool HasNoSelectedUser => SelectedUser == null;

        // Webcam Capture
        private readonly WebcamCaptureService _userWebcamService = new();
        [ObservableProperty] private bool _isUserWebcamOpen;
        [ObservableProperty] private ObservableCollection<WebcamDeviceInfo> _userAvailableCameras = [];
        [ObservableProperty] private WebcamDeviceInfo? _selectedUserCamera;
        [ObservableProperty] private ImageSource? _userWebcamLiveFrame;
        [ObservableProperty] private ImageSource? _userWebcamCapturedFrame;
        [ObservableProperty] private bool _isUserWebcamFrozen;
        [ObservableProperty] private bool _hasUserWebcamDevice;
        [ObservableProperty] private string _userWebcamStatusMessage = string.Empty;

        public bool HasNoUserWebcamDevice => !HasUserWebcamDevice;
        public bool IsUserWebcamStreaming => HasUserWebcamDevice && !IsUserWebcamFrozen;

        partial void OnHasUserWebcamDeviceChanged(bool value)
        {
            OnPropertyChanged(nameof(HasNoUserWebcamDevice));
            OnPropertyChanged(nameof(IsUserWebcamStreaming));
        }

        partial void OnIsUserWebcamFrozenChanged(bool value)
        {
            OnPropertyChanged(nameof(IsUserWebcamStreaming));
        }

        partial void OnSelectedUserCameraChanged(WebcamDeviceInfo? value)
        {
            if (IsUserWebcamOpen && !IsUserWebcamFrozen && value != null)
            {
                _ = StartUserCameraStreamingAsync(value);
            }
        }

        // Editing fields
        [ObservableProperty] private string _editUserName = string.Empty;
        [ObservableProperty] private string _editUserEmail = string.Empty;
        [ObservableProperty] private string _editUserPin = string.Empty;
        [ObservableProperty] private string _editUserVocalRange = "Any / Unspecified";
        [ObservableProperty] private string _editUserCustomTitle = string.Empty;
        [ObservableProperty] private string _editUserNotes = string.Empty;
        [ObservableProperty] private int _editUserScore;
        [ObservableProperty] private int _editUserTotalSongs;
        [ObservableProperty] private string _editUserAvatarType = "None";
        [ObservableProperty] private string _editUserAvatarSource = string.Empty;
        [ObservableProperty] private ImageSource? _editUserAvatarImage;

        // Audio defaults
        [ObservableProperty] private double _editUserGain = 100.0;
        [ObservableProperty] private int _editUserKey = 0;
        [ObservableProperty] private double _editUserTempo = 1.0;
        [ObservableProperty] private double _editUserTreble = 0.0;
        [ObservableProperty] private double _editUserMid = 0.0;
        [ObservableProperty] private double _editUserBass = 0.0;
        [ObservableProperty] private double _editUserCompressor = 0.0;
        [ObservableProperty] private double _editUserLimiter = 0.0;
        [ObservableProperty] private string _editUserSoundCheck = string.Empty;

        public async Task LoadAllUsersAsync()
        {
            try
            {
                int? previousSingerId = SelectedUser?.SingerId;
                string? previousSingerName = SelectedUser?.Name;

                await using var context = new LyracistDbContext();
                var dbSingers = await context.Singers
                    .Include(s => s.AudioSettings)
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                AllUsers.Clear();
                foreach (var s in dbSingers)
                {
                    var item = new SingerUserItem
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
                        AvatarImage = ResolveUserAvatarImage(s.AvatarType, s.AvatarSource)
                    };
                    AllUsers.Add(item);
                }

                ApplyUserFilter();

                // Preserve existing selection if still available
                SingerUserItem? match = null;
                if (previousSingerId.HasValue)
                {
                    match = FilteredUsers.FirstOrDefault(u => u.SingerId == previousSingerId.Value);
                }
                if (match == null && !string.IsNullOrWhiteSpace(previousSingerName))
                {
                    match = FilteredUsers.FirstOrDefault(u => string.Equals(u.Name, previousSingerName, StringComparison.OrdinalIgnoreCase));
                }

                SelectedUser = match ?? FilteredUsers.FirstOrDefault();
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.LoadAllUsersAsync", ex);
            }
        }

        partial void OnUserSearchTextChanged(string value)
        {
            ApplyUserFilter();
        }

        private void ApplyUserFilter()
        {
            FilteredUsers.Clear();
            string query = (UserSearchText ?? string.Empty).Trim();

            var matches = string.IsNullOrWhiteSpace(query)
                ? AllUsers.ToList()
                : AllUsers.Where(s =>
                    s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    s.Email.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    s.CustomTitle.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var match in matches)
            {
                FilteredUsers.Add(match);
            }

            if (SelectedUser != null && !FilteredUsers.Contains(SelectedUser))
            {
                SelectedUser = FilteredUsers.FirstOrDefault();
            }
        }

        partial void OnSelectedUserChanged(SingerUserItem? value)
        {
            OnPropertyChanged(nameof(HasSelectedUser));
            OnPropertyChanged(nameof(HasNoSelectedUser));

            if (value == null)
            {
                ClearUserEditingFields();
                UserPerformanceHistory.Clear();
                return;
            }

            EditUserName = value.Name;
            EditUserEmail = value.Email;
            EditUserPin = value.PinCode;
            EditUserVocalRange = string.IsNullOrWhiteSpace(value.VocalRange) ? "Any / Unspecified" : value.VocalRange;
            EditUserCustomTitle = value.CustomTitle;
            EditUserNotes = value.Notes;
            EditUserScore = value.Score;
            EditUserTotalSongs = value.TotalSongsSung;
            EditUserAvatarType = value.AvatarType;
            EditUserAvatarSource = value.AvatarSource;
            EditUserAvatarImage = value.AvatarImage;

            _ = LoadUserAudioSettingsAndHistoryAsync(value);
        }

        private async Task LoadUserAudioSettingsAndHistoryAsync(SingerUserItem singer)
        {
            try
            {
                await using var context = new LyracistDbContext();
                var dbSinger = await context.Singers
                    .Include(s => s.AudioSettings)
                    .FirstOrDefaultAsync(s => s.SingerId == singer.SingerId);

                if (dbSinger?.AudioSettings != null)
                {
                    EditUserGain = dbSinger.AudioSettings.Gain;
                    EditUserKey = dbSinger.AudioSettings.Key;
                    EditUserTempo = dbSinger.AudioSettings.Tempo;
                    EditUserTreble = dbSinger.AudioSettings.Treble;
                    EditUserMid = dbSinger.AudioSettings.Mid;
                    EditUserBass = dbSinger.AudioSettings.Bass;
                    EditUserCompressor = dbSinger.AudioSettings.Compressor;
                    EditUserLimiter = dbSinger.AudioSettings.Limiter;
                    EditUserSoundCheck = dbSinger.AudioSettings.Notes ?? string.Empty;
                }
                else
                {
                    ResetUserAudioDefaults();
                }

                // Load performance history
                var historyEntries = SingerHistoryService.GetHistory(singer.Name);
                UserPerformanceHistory.Clear();
                foreach (var h in historyEntries)
                {
                    UserPerformanceHistory.Add(new SingerUserHistoryItem
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
                LoggerService.LogError("MainViewModel.LoadUserAudioSettingsAndHistoryAsync", ex);
            }
        }

        private void ResetUserAudioDefaults()
        {
            EditUserGain = 100.0;
            EditUserKey = 0;
            EditUserTempo = 1.0;
            EditUserTreble = 0.0;
            EditUserMid = 0.0;
            EditUserBass = 0.0;
            EditUserCompressor = 0.0;
            EditUserLimiter = 0.0;
            EditUserSoundCheck = string.Empty;
        }

        private void ClearUserEditingFields()
        {
            EditUserName = string.Empty;
            EditUserEmail = string.Empty;
            EditUserPin = string.Empty;
            EditUserVocalRange = "Any / Unspecified";
            EditUserCustomTitle = string.Empty;
            EditUserNotes = string.Empty;
            EditUserScore = 0;
            EditUserTotalSongs = 0;
            EditUserAvatarType = "None";
            EditUserAvatarSource = string.Empty;
            EditUserAvatarImage = null;
            ResetUserAudioDefaults();
        }

        [RelayCommand]
        private async Task AddNewUserAsync()
        {
            try
            {
                string baseName = "New Singer";
                string candidateName = baseName;
                int counter = 2;

                while (AllUsers.Any(s => string.Equals(s.Name, candidateName, StringComparison.OrdinalIgnoreCase)))
                {
                    candidateName = $"{baseName} {counter++}";
                }

                await using var context = new LyracistDbContext();
                var newSinger = new Singer
                {
                    Name = candidateName,
                    PinCode = string.Empty,
                    Email = string.Empty,
                    VocalRange = "Any / Unspecified",
                    AvatarType = "None",
                    AvatarSource = string.Empty,
                    Score = 0,
                    TotalSongsSung = 0,
                    AudioSettings = new SingerAudioSettings()
                };

                context.Singers.Add(newSinger);
                await context.SaveChangesAsync();

                var item = new SingerUserItem
                {
                    SingerId = newSinger.SingerId,
                    Name = newSinger.Name,
                    Email = newSinger.Email,
                    PinCode = newSinger.PinCode,
                    VocalRange = newSinger.VocalRange,
                    Score = 0,
                    TotalSongsSung = 0,
                    AvatarType = "None",
                    AvatarSource = string.Empty
                };

                AllUsers.Add(item);
                ApplyUserFilter();
                SelectedUser = item;
                AddKnownSinger(candidateName);

                ShowUserStatus($"Created new performer account '{candidateName}'.", true);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.AddNewUserAsync", ex);
                ShowUserStatus("Failed to create new performer account.", false);
            }
        }

        [RelayCommand]
        private async Task DeleteUserAsync()
        {
            if (SelectedUser == null) return;

            string singerName = SelectedUser.Name;
            int singerId = SelectedUser.SingerId;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete performer '{singerName}'? This will remove their profile and credentials.",
                "Delete Performer",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                await using var context = new LyracistDbContext();
                var dbSinger = await context.Singers
                    .Include(s => s.AudioSettings)
                    .FirstOrDefaultAsync(s => s.SingerId == singerId);

                if (dbSinger != null)
                {
                    context.Singers.Remove(dbSinger);
                    await context.SaveChangesAsync();
                }

                AllUsers.Remove(SelectedUser);
                ApplyUserFilter();
                ShowUserStatus($"Performer '{singerName}' was removed.", true);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.DeleteUserAsync", ex);
                ShowUserStatus("Failed to delete performer account.", false);
            }
        }

        [RelayCommand]
        private async Task SaveUserAsync()
        {
            if (SelectedUser == null) return;

            string trimmedName = (EditUserName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                ShowUserStatus("Singer Name cannot be empty.", false);
                return;
            }

            if (AllUsers.Any(s => s.SingerId != SelectedUser.SingerId &&
                                  string.Equals(s.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
            {
                ShowUserStatus($"Another performer is already named '{trimmedName}'.", false);
                return;
            }

            try
            {
                await using var context = new LyracistDbContext();
                var dbSinger = await context.Singers
                    .Include(s => s.AudioSettings)
                    .FirstOrDefaultAsync(s => s.SingerId == SelectedUser.SingerId);

                if (dbSinger == null)
                {
                    ShowUserStatus("Performer account not found in database.", false);
                    return;
                }

                string oldName = dbSinger.Name;
                dbSinger.Name = trimmedName;
                dbSinger.Email = (EditUserEmail ?? string.Empty).Trim();
                dbSinger.PinCode = (EditUserPin ?? string.Empty).Trim();
                dbSinger.VocalRange = EditUserVocalRange;
                dbSinger.CustomTitle = (EditUserCustomTitle ?? string.Empty).Trim();
                dbSinger.Notes = (EditUserNotes ?? string.Empty).Trim();
                dbSinger.Score = EditUserScore;
                dbSinger.TotalSongsSung = EditUserTotalSongs;
                dbSinger.AvatarType = EditUserAvatarType;
                dbSinger.AvatarSource = EditUserAvatarSource;

                if (dbSinger.AudioSettings == null)
                {
                    dbSinger.AudioSettings = new SingerAudioSettings { SingerId = dbSinger.SingerId };
                    context.SingerAudioSettings.Add(dbSinger.AudioSettings);
                }

                dbSinger.AudioSettings.Gain = EditUserGain;
                dbSinger.AudioSettings.Key = EditUserKey;
                dbSinger.AudioSettings.Tempo = EditUserTempo;
                dbSinger.AudioSettings.Treble = EditUserTreble;
                dbSinger.AudioSettings.Mid = EditUserMid;
                dbSinger.AudioSettings.Bass = EditUserBass;
                dbSinger.AudioSettings.Compressor = EditUserCompressor;
                dbSinger.AudioSettings.Limiter = EditUserLimiter;
                dbSinger.AudioSettings.Notes = (EditUserSoundCheck ?? string.Empty).Trim();

                await context.SaveChangesAsync();

                if (!string.Equals(oldName, trimmedName, StringComparison.OrdinalIgnoreCase))
                {
                    SingerHistoryService.MergeHistory(oldName, trimmedName);
                    AddKnownSinger(trimmedName);
                }

                SelectedUser.Name = trimmedName;
                SelectedUser.Email = dbSinger.Email;
                SelectedUser.PinCode = dbSinger.PinCode;
                SelectedUser.VocalRange = dbSinger.VocalRange;
                SelectedUser.CustomTitle = dbSinger.CustomTitle;
                SelectedUser.Notes = dbSinger.Notes;
                SelectedUser.Score = dbSinger.Score;
                SelectedUser.TotalSongsSung = dbSinger.TotalSongsSung;
                SelectedUser.AvatarType = dbSinger.AvatarType;
                SelectedUser.AvatarSource = dbSinger.AvatarSource;
                SelectedUser.AvatarImage = ResolveUserAvatarImage(dbSinger.AvatarType, dbSinger.AvatarSource);

                ShowUserStatus($"Saved profile and audio defaults for '{trimmedName}'.", true);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.SaveUserAsync", ex);
                ShowUserStatus("Failed to save performer profile.", false);
            }
        }

        [RelayCommand]
        private void UploadUserAvatar()
        {
            if (SelectedUser == null) return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Performer Photo / Selfie",
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                Directory.CreateDirectory(Globals.AvatarsDir);
                AvatarImageHelper.DeleteRawCopy(Globals.AvatarsDir, EditUserAvatarSource);
                AvatarImageHelper.PurgeStaleRawCopies(Globals.AvatarsDir, TimeSpan.FromDays(7));
                string newFileName = $"{Guid.NewGuid():N}.jpg";
                string destPath = Path.Combine(Globals.AvatarsDir, newFileName);
                string rawPath = Path.Combine(Globals.AvatarsDir, $"raw_{newFileName}");

                byte[] rawBytes = File.ReadAllBytes(dlg.FileName);
                byte[] normalizedBytes = AvatarImageHelper.NormalizeImageBytes(rawBytes);
                File.WriteAllBytes(rawPath, normalizedBytes);
                File.WriteAllBytes(destPath, normalizedBytes);

                EditUserAvatarType = "Uploaded";
                EditUserAvatarSource = newFileName;
                EditUserAvatarImage = AvatarImageHelper.LoadOrientedBitmap(normalizedBytes);

                // Open the interactive cropper immediately so the DJ can center the face
                var adjustWin = new Windows.AdjustAvatarWindow(rawPath, destPath, SelectedUser.Name)
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };
                if (adjustWin.ShowDialog() == true)
                {
                    EditUserAvatarImage = AvatarImageHelper.LoadOrientedBitmapFromFile(destPath);
                    if (SelectedUser != null)
                    {
                        SelectedUser.AvatarImage = EditUserAvatarImage;
                    }
                    ShowUserStatus("Photo uploaded, centered, and saved. Click 'Save Changes' to apply.", true);
                }
                else
                {
                    ShowUserStatus("Photo uploaded. Click 'Center Face' anytime to reposition, or 'Save Changes' to apply.", true);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.UploadUserAvatar", ex);
                ShowUserStatus("Failed to upload performer photo.", false);
            }
        }

        [RelayCommand]
        private void AdjustUserAvatar()
        {
            if (SelectedUser == null) return;
            if (EditUserAvatarType != "Uploaded" || string.IsNullOrWhiteSpace(EditUserAvatarSource))
            {
                ShowUserStatus("Can only center uploaded photos.", false);
                return;
            }

            string avatarPath = Path.Combine(Globals.AvatarsDir, EditUserAvatarSource);
            string rawPath = Path.Combine(Globals.AvatarsDir, $"raw_{EditUserAvatarSource}");
            string sourcePath = File.Exists(rawPath) ? rawPath : avatarPath;

            if (!File.Exists(sourcePath))
            {
                ShowUserStatus("Photo file not found on disk.", false);
                return;
            }

            try
            {
                var adjustWin = new Windows.AdjustAvatarWindow(sourcePath, avatarPath, SelectedUser.Name)
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };

                if (adjustWin.ShowDialog() == true)
                {
                    EditUserAvatarImage = AvatarImageHelper.LoadOrientedBitmapFromFile(avatarPath);
                    if (SelectedUser != null)
                    {
                        SelectedUser.AvatarImage = EditUserAvatarImage;
                    }
                    ShowUserStatus("Photo centered and saved. Click 'Save Changes' to apply.", true);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.AdjustUserAvatar", ex);
                ShowUserStatus("Failed to open photo centering tool.", false);
            }
        }

        [RelayCommand]
        private void RotateUserAvatar()
        {
            if (SelectedUser == null) return;
            if (EditUserAvatarType != "Uploaded" || string.IsNullOrWhiteSpace(EditUserAvatarSource))
            {
                ShowUserStatus("Can only rotate uploaded photos.", false);
                return;
            }

            string fullPath = Path.Combine(Globals.AvatarsDir, EditUserAvatarSource);
            if (!File.Exists(fullPath))
            {
                ShowUserStatus("Uploaded photo file not found on disk.", false);
                return;
            }

            try
            {
                byte[] rawBytes = File.ReadAllBytes(fullPath);
                byte[] rotated = AvatarImageHelper.RotateImage90Degrees(rawBytes);
                File.WriteAllBytes(fullPath, rotated);
                AvatarImageHelper.RotateRawCopy(Globals.AvatarsDir, EditUserAvatarSource);

                EditUserAvatarImage = AvatarImageHelper.LoadOrientedBitmap(rotated);
                if (SelectedUser != null)
                {
                    SelectedUser.AvatarImage = EditUserAvatarImage;
                }

                ShowUserStatus("Photo rotated 90°. Click 'Save Changes' to apply.", true);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.RotateUserAvatar", ex);
                ShowUserStatus("Failed to rotate photo.", false);
            }
        }

        [RelayCommand]
        private async Task RefreshUsersAsync()
        {
            await LoadAllUsersAsync();
            ShowUserStatus("Performer list refreshed.", true);
        }

        [RelayCommand]
        private async Task EditSingerProfileFromRotationAsync(SingerEntry? entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Name)) return;

            string singerName = entry.Name.Trim();
            await LoadAllUsersAsync();

            var existing = AllUsers.FirstOrDefault(u => string.Equals(u.Name, singerName, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                try
                {
                    await using var context = new LyracistDbContext();
                    var newSinger = new Singer
                    {
                        Name = singerName,
                        Email = string.Empty,
                        PinCode = string.Empty,
                        VocalRange = "Any / Unspecified",
                        AvatarType = "None",
                        AvatarSource = string.Empty
                    };
                    context.Singers.Add(newSinger);
                    await context.SaveChangesAsync();

                    await LoadAllUsersAsync();
                    existing = AllUsers.FirstOrDefault(u => u.SingerId == newSinger.SingerId || string.Equals(u.Name, singerName, StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("MainViewModel.EditSingerProfileFromRotation", ex);
                }
            }

            UserSearchText = string.Empty;
            if (existing != null)
            {
                SelectedUser = existing;
            }

            SelectedMainTabIndex = 3;
        }

        [RelayCommand]
        private void ClearUserAvatar()
        {
            if (SelectedUser == null) return;

            AvatarImageHelper.DeleteRawCopy(Globals.AvatarsDir, EditUserAvatarSource);
            EditUserAvatarType = "None";
            EditUserAvatarSource = string.Empty;
            EditUserAvatarImage = null;
            ShowUserStatus("Performer photo reset to default avatar. Click 'Save Changes' to apply.", true);
        }

        [RelayCommand]
        private async Task OpenUserWebcamCaptureAsync()
        {
            if (SelectedUser == null) return;

            UserAvailableCameras.Clear();
            var cameras = WebcamCaptureService.GetAvailableCameras();
            foreach (var cam in cameras)
            {
                UserAvailableCameras.Add(cam);
            }

            UserWebcamCapturedFrame = null;
            IsUserWebcamFrozen = false;
            UserWebcamLiveFrame = null;
            IsUserWebcamOpen = true;

            if (UserAvailableCameras.Count == 0)
            {
                HasUserWebcamDevice = false;
                UserWebcamStatusMessage = "No webcam or video capture device detected on this system. Please plug in a webcam and try again.";
                SelectedUserCamera = null;
            }
            else
            {
                HasUserWebcamDevice = true;
                UserWebcamStatusMessage = string.Empty;
                SelectedUserCamera = UserAvailableCameras[0];
                await StartUserCameraStreamingAsync(SelectedUserCamera);
            }
        }

        private async Task StartUserCameraStreamingAsync(WebcamDeviceInfo camera)
        {
            await _userWebcamService.StopCaptureAsync();

            bool started = await _userWebcamService.StartCaptureAsync(camera.Descriptor, frame =>
            {
                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(() =>
                {
                    if (IsUserWebcamOpen && !IsUserWebcamFrozen)
                    {
                        UserWebcamLiveFrame = frame;
                    }
                });
            });

            if (!started)
            {
                UserWebcamStatusMessage = "Failed to start camera preview. The camera may be in use by another application.";
            }
        }

        [RelayCommand]
        private void CaptureUserWebcamSnapshot()
        {
            if (UserWebcamLiveFrame != null)
            {
                UserWebcamCapturedFrame = UserWebcamLiveFrame;
                IsUserWebcamFrozen = true;
            }
        }

        [RelayCommand]
        private async Task RetakeUserWebcamSnapshotAsync()
        {
            UserWebcamCapturedFrame = null;
            IsUserWebcamFrozen = false;
            if (SelectedUserCamera != null)
            {
                await StartUserCameraStreamingAsync(SelectedUserCamera);
            }
        }

        [RelayCommand]
        private async Task ApplyUserWebcamSnapshotAsync()
        {
            if (UserWebcamCapturedFrame is BitmapSource source)
            {
                var result = WebcamCaptureService.SaveSquarePhoto(source, Globals.AvatarsDir);
                if (result.HasValue)
                {
                    EditUserAvatarType = "Uploaded";
                    AvatarImageHelper.DeleteRawCopy(Globals.AvatarsDir, EditUserAvatarSource);
                    EditUserAvatarSource = result.Value.fileName;
                    EditUserAvatarImage = result.Value.squareBitmap;

                    await CloseUserWebcamCaptureAsync();
                    ShowUserStatus("Performer photo captured from webcam. Click 'Save Changes' to apply.", true);
                    return;
                }
            }

            ShowUserStatus("Failed to save webcam photo.", false);
        }

        [RelayCommand]
        private async Task CloseUserWebcamCaptureAsync()
        {
            await _userWebcamService.StopCaptureAsync();
            IsUserWebcamOpen = false;
            IsUserWebcamFrozen = false;
            UserWebcamLiveFrame = null;
            UserWebcamCapturedFrame = null;
        }

        [RelayCommand]
        private void OpenUserMergePopup()
        {
            if (SelectedUser == null) return;

            UserMergeCandidates.Clear();
            foreach (var singer in AllUsers.Where(s => s.SingerId != SelectedUser.SingerId).OrderBy(s => s.Name))
            {
                UserMergeCandidates.Add(singer);
            }

            SelectedMergeUser = UserMergeCandidates.FirstOrDefault();
            IsUserMergeOpen = true;
        }

        [RelayCommand]
        private void CancelUserMerge()
        {
            IsUserMergeOpen = false;
        }

        [RelayCommand]
        private async Task ExecuteUserMergeAsync()
        {
            if (SelectedUser == null || SelectedMergeUser == null) return;

            int targetId = SelectedUser.SingerId;
            int duplicateId = SelectedMergeUser.SingerId;
            string targetName = SelectedUser.Name;
            string duplicateName = SelectedMergeUser.Name;

            var confirm = System.Windows.MessageBox.Show(
                $"Are you sure you want to merge '{duplicateName}' into '{targetName}'?\n\nAll song performances, requests, and XP will be transferred to '{targetName}', and '{duplicateName}' will be permanently deleted.",
                "Confirm Account Merge",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirm != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                await using var context = new LyracistDbContext();
                var dbService = new DatabaseService(context);
                bool merged = await dbService.MergeSingers(targetId, duplicateId);
                if (merged)
                {
                    SingerHistoryService.MergeHistory(duplicateName, targetName);
                    IsUserMergeOpen = false;
                    await LoadAllUsersAsync();
                    SelectedUser = AllUsers.FirstOrDefault(s => s.SingerId == targetId);
                    ShowUserStatus($"Successfully merged '{duplicateName}' into '{targetName}'. All performances and XP were consolidated.", true);
                }
                else
                {
                    ShowUserStatus("Failed to merge singer accounts.", false);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.ExecuteUserMergeAsync", ex);
                ShowUserStatus("An error occurred while merging accounts.", false);
            }
        }

        [RelayCommand]
        private void QueueUserHistoricalSong(SingerUserHistoryItem item)
        {
            if (item == null || SelectedUser == null) return;

            try
            {
                AddActiveSinger(new SingerEntry
                {
                    Name = SelectedUser.Name,
                    Song = item.SongTitle,
                    Artist = item.Artist
                });

                AddKnownSinger(SelectedUser.Name);
                ShowUserStatus($"Queued '{item.SongTitle}' for {SelectedUser.Name} into rotation!", true);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.QueueUserHistoricalSong", ex);
                ShowUserStatus("Failed to add song to rotation.", false);
            }
        }

        [RelayCommand]
        private void DeleteUserHistoricalSong(SingerUserHistoryItem item)
        {
            if (item == null) return;

            try
            {
                SingerHistoryService.DeleteHistoryEntry(item.SingerHistoryId);
                UserPerformanceHistory.Remove(item);
                ShowUserStatus($"Removed '{item.SongTitle}' from history.", true);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.DeleteUserHistoricalSong", ex);
                ShowUserStatus("Failed to remove song from history.", false);
            }
        }

        private void ShowUserStatus(string message, bool isSuccess)
        {
            UserStatusMessage = message;
            IsUserStatusSuccess = isSuccess;
            HasUserStatusMessage = true;
        }

        private static ImageSource? ResolveUserAvatarImage(string avatarType, string avatarSource)
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
    }
#endif
}
