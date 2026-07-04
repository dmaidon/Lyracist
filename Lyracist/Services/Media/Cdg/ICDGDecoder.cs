using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Lyracist.Services.Media.Cdg;

public interface ICDGDecoder
{
    Task LoadAsync(string cdgPath);
    WriteableBitmap GetNextFrame(TimeSpan audioPosition);
}
