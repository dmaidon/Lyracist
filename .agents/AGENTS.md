# Lyracist Project-Scoped Rules

## CDG Decoding Reference Repositories

When implementing `.cdg` frame parsing, rendering, and timing synchronization in the future, refer to the following open-source implementations for algorithms and decoding guides:

1. **Go Karaoke Player / CDG Decoder**: [gozzar/karaoke](https://github.com/gozzar/karaoke)
   - Good reference for binary layout unpacking, color table parsing, and index-based canvas drawing.
2. **C# WPF Karaoke Player**: [spektor56/KaraokePlayer](https://github.com/spektor56/KaraokePlayer)
   - Useful for C# WPF rendering controls, thread synchronization, and BitmapSource buffering matching our stack.
3. **Node/C++ CDG Decoder**: [nopperl/karaoke-cdg](https://github.com/nopperl/karaoke-cdg)
   - Detailed specifications of CDG command packets (preset, border, tile block, scroll, define color, etc.).
   
   ## File Modification Header Rule
Whenever creating, editing, or modifying a source file, always add or update a comment line at the very top of the page in the following format:
`// <What> on <MMM d, yyyy> @ <HH:mm:ss> -> <brief synopsis of changes or additions>`
Where `<What>` can be "Created", "Edited", "Added", etc. (Adjust comment characters appropriately based on file language, e.g. `/* */`, `#`, or `<!-- -->`).

