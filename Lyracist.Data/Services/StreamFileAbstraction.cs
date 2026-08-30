// Created on Aug 30, 2026 @ 09:40:00 -> Implement TagLib.File.IFileAbstraction for in-memory stream reading
using System;
using System.IO;

namespace Lyracist.Data.Services
{
    /// <summary>
    /// Provides an in-memory stream abstraction for TagLibSharp, allowing direct metadata extraction
    /// from ZIP archive entries and memory buffers without writing temporary files to disk.
    /// </summary>
    public sealed class StreamFileAbstraction : TagLib.File.IFileAbstraction
    {
        public string Name { get; }
        public Stream ReadStream { get; }
        public Stream WriteStream => throw new NotSupportedException("In-memory stream abstraction is read-only.");

        public StreamFileAbstraction(string name, Stream stream)
        {
            Name = name ?? "stream.mp3";
            ReadStream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public void CloseStream(Stream stream)
        {
            // Stream lifecycle is managed by caller
        }
    }
}
