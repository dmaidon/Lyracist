using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lyracist.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Lyracist.Data.Services
{
    public class DatabaseService(LyracistDbContext context)
    {
        private readonly LyracistDbContext _context = context;

        // ==========================================
        // SONG OPERATIONS
        // ==========================================

        public async Task<Song> AddSong(Song song)
        {
            _context.Songs.Add(song);
            await _context.SaveChangesAsync();
            return song;
        }

        public async Task UpdateSong(Song song)
        {
            _context.Songs.Update(song);
            await _context.SaveChangesAsync();
        }

        public async Task<Song?> GetSongById(int songId)
        {
            return await _context.Songs
                .Include(s => s.AudioSettings)
                .FirstOrDefaultAsync(s => s.SongId == songId);
        }

        public async Task<List<Song>> GetAllSongs()
        {
            return await _context.Songs
                .Include(s => s.AudioSettings)
                .ToListAsync();
        }

        // ==========================================
        // SINGER OPERATIONS
        // ==========================================

        public async Task<Singer> AddSinger(Singer singer)
        {
            _context.Singers.Add(singer);
            await _context.SaveChangesAsync();
            return singer;
        }

        public async Task UpdateSinger(Singer singer)
        {
            _context.Singers.Update(singer);
            await _context.SaveChangesAsync();
        }

        public async Task<Singer?> GetSingerById(int singerId)
        {
            return await _context.Singers
                .Include(s => s.AudioSettings)
                .Include(s => s.RotationEntries)
                .Include(s => s.MusicRequests)
                .FirstOrDefaultAsync(s => s.SingerId == singerId);
        }

        // ==========================================
        // ROTATION OPERATIONS
        // ==========================================

        public async Task<List<RotationEntry>> GetRotation()
        {
            return await _context.RotationEntries
                .AsNoTracking()
                .Include(r => r.Singer)
                .Include(r => r.Song)
                .OrderBy(r => r.Position)
                .ToListAsync();
        }

        public async Task<RotationEntry> AddRotationEntry(RotationEntry entry)
        {
            _context.RotationEntries.Add(entry);
            await _context.SaveChangesAsync();
            return entry;
        }

        public async Task UpdateRotationEntry(RotationEntry entry)
        {
            _context.RotationEntries.Update(entry);
            await _context.SaveChangesAsync();
        }

        // ==========================================
        // MUSIC REQUEST OPERATIONS
        // ==========================================

        public async Task<MusicRequest> AddMusicRequest(MusicRequest request)
        {
            _context.MusicRequests.Add(request);
            await _context.SaveChangesAsync();
            return request;
        }

        public async Task ApproveMusicRequest(int requestId)
        {
            var request = await _context.MusicRequests.FindAsync(requestId);
            if (request != null)
            {
                request.Status = "Approved";
                _context.MusicRequests.Update(request);
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkMusicRequestPlayed(int requestId)
        {
            var request = await _context.MusicRequests.FindAsync(requestId);
            if (request != null)
            {
                request.Status = "Played";
                _context.MusicRequests.Update(request);
                await _context.SaveChangesAsync();
            }
        }

        // ==========================================
        // OCCASION ITEM OPERATIONS
        // ==========================================

        public async Task<OccasionItem> AddOccasionItem(OccasionItem item)
        {
            _context.OccasionItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        // ==========================================
        // PLAYLIST OPERATIONS (Overloaded)
        // ==========================================

        public async Task AddPlaylistItem(OpeningPlaylistItem item)
        {
            _context.OpeningPlaylistItems.Add(item);
            await _context.SaveChangesAsync();
        }

        public async Task AddPlaylistItem(FillInPlaylistItem item)
        {
            _context.FillInPlaylistItems.Add(item);
            await _context.SaveChangesAsync();
        }

        public async Task AddPlaylistItem(EndRotationPlaylistItem item)
        {
            _context.EndRotationPlaylistItems.Add(item);
            await _context.SaveChangesAsync();
        }

        // ==========================================
        // AUDIO SETTINGS OPERATIONS
        // ==========================================

        public async Task SaveAudioSettingsForSong(SongAudioSettings settings)
        {
            var existing = await _context.SongAudioSettings
                .FirstOrDefaultAsync(s => s.SongId == settings.SongId);

            if (existing == null)
            {
                _context.SongAudioSettings.Add(settings);
            }
            else
            {
                existing.Treble = settings.Treble;
                existing.Mid = settings.Mid;
                existing.Bass = settings.Bass;
                existing.Gain = settings.Gain;
                existing.Key = settings.Key;
                existing.Tempo = settings.Tempo;
                existing.Compressor = settings.Compressor;
                existing.Limiter = settings.Limiter;
                existing.Notes = settings.Notes;
                _context.SongAudioSettings.Update(existing);
            }
            await _context.SaveChangesAsync();
        }

        public async Task SaveAudioSettingsForSinger(SingerAudioSettings settings)
        {
            var existing = await _context.SingerAudioSettings
                .FirstOrDefaultAsync(s => s.SingerId == settings.SingerId);

            if (existing == null)
            {
                _context.SingerAudioSettings.Add(settings);
            }
            else
            {
                existing.Treble = settings.Treble;
                existing.Mid = settings.Mid;
                existing.Bass = settings.Bass;
                existing.Gain = settings.Gain;
                existing.Key = settings.Key;
                existing.Tempo = settings.Tempo;
                existing.Compressor = settings.Compressor;
                existing.Limiter = settings.Limiter;
                existing.Notes = settings.Notes;
                _context.SingerAudioSettings.Update(existing);
            }
            await _context.SaveChangesAsync();
        }
    }
}
