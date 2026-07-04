# Lyracist Project-Scoped Rules

## CDG Decoding Reference Repositories

When implementing `.cdg` frame parsing, rendering, and timing synchronization in the future, refer to the following open-source implementations for algorithms and decoding guides:

1. **Go Karaoke Player / CDG Decoder**: [gozzar/karaoke](https://github.com/gozzar/karaoke)
   - Good reference for binary layout unpacking, color table parsing, and index-based canvas drawing.
2. **C# WPF Karaoke Player**: [spektor56/KaraokePlayer](https://github.com/spektor56/KaraokePlayer)
   - Useful for C# WPF rendering controls, thread synchronization, and BitmapSource buffering matching our stack.
3. **Node/C++ CDG Decoder**: [nopperl/karaoke-cdg](https://github.com/nopperl/karaoke-cdg)
   - Detailed specifications of CDG command packets (preset, border, tile block, scroll, define color, etc.).
