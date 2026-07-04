using System;
using System.Threading.Tasks;

namespace Lyracist.Services.Tablet;

public interface ILyricsController
{
    Task UpdateLyricsAsync(string title, string currentLine, string nextLine, TimeSpan position);
}
