// Edited on Aug 25, 2026 @ 06:15:00 -> Remove dead suppression targets (IDE0076)
// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>", Scope = "member", Target = "~P:KSRotation.Models.RotationItemDto.artist")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>", Scope = "member", Target = "~P:KSRotation.Models.RotationItemDto.isCurrent")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>", Scope = "member", Target = "~P:KSRotation.Models.RotationItemDto.isInactive")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>", Scope = "member", Target = "~P:KSRotation.Models.RotationItemDto.isNext")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>", Scope = "member", Target = "~P:KSRotation.Models.RotationItemDto.name")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>", Scope = "member", Target = "~P:KSRotation.Models.RotationItemDto.song")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.LoggerService.CleanupLogs")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.LoggerService.LogError(System.String,System.Exception)")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.NightDatabaseService.Flush")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.NightDatabaseService.Load~KSRotation.Services.NightDbState")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.NightDatabaseService.Save(System.Collections.Generic.IEnumerable{KSRotation.Models.SingerEntry},System.Collections.Generic.IEnumerable{KSRotation.Models.SongPerformance})")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.PatronRequestServer.AcceptConnectionsAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.PatronRequestServer.HandleClientAsync(System.Net.Sockets.TcpClient)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.PatronRequestServer.Start")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.PatronRequestServer.Stop")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.RotationReportService.CreateEmlDraft(System.String,System.String,System.String,System.String)")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.ThemeService.Apply(System.String)")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.ThemeService.IsSystemDarkMode~System.Boolean")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.ViewModels.MainViewModel.#ctor")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.ViewModels.MainViewModel.GetLocalIPAddress~System.String")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.ViewModels.MainViewModel.LoadKnownSingers")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.ViewModels.MainViewModel.SaveKnownSingers")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.ViewModels.MainViewModel.SaveRotation~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.ViewModels.MainViewModel.SaveSettingsNow")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.ViewModels.MainViewModel.StartRequestServer")]
[assembly: SuppressMessage("Performance", "CA1835:Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.PatronRequestServer.SendBadRequestAsync(System.Net.Sockets.NetworkStream,System.String)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1835:Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.PatronRequestServer.SendCorsPreflightResponseAsync(System.Net.Sockets.NetworkStream)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1835:Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.PatronRequestServer.SendJsonResponseAsync(System.Net.Sockets.NetworkStream,System.String)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1835:Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'", Justification = "<Pending>", Scope = "member", Target = "~M:KSRotation.Services.PatronRequestServer.SendNotFoundAsync(System.Net.Sockets.NetworkStream)~System.Threading.Tasks.Task")]
