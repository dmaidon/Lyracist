<!-- Edited on Aug 10, 2026 @ 14:38:00 -> Add Spitballing / Idea Exploration Rule -->
# Lyracist Project-Scoped Rules

## CDG Decoding Reference Repositories

When implementing `.cdg` frame parsing, rendering, and timing synchronization in the future, refer to the following open-source implementations for algorithms and decoding guides:

1. **Go Karaoke Player / CDG Decoder**: [gozzar/karaoke](https://github.com/gozzar/karaoke)
   - Good reference for binary layout unpacking, color table parsing, and index-based canvas drawing.
2. **C# WPF Karaoke Player**: [spektor56/KaraokePlayer](https://github.com/spektor56/KaraokePlayer)
   - Useful for C# WPF rendering controls, thread synchronization, and BitmapSource buffering matching our stack.
3. **Node/C++ CDG Decoder**: [nopperl/karaoke-cdg](https://github.com/nopperl/karaoke-cdg)
   - Detailed specifications of CDG command packets (preset, border, tile block, scroll, define color, etc.).

   ## Update & Commit instructions

   Whenever "update and commit" is entered, update the Readmew.md and ChangeLog.md files, If changes have been made that affect the help system in any app, modify the help system, commit and sync all changed or modified files to github.

   ## File Modification Header Rule

Whenever creating, editing, or modifying a source file, always add or update a comment line at the very top of the page in the following format:
`// <What> on <MMM d, yyyy> @ <HH:mm:ss> -> <brief synopsis of changes or additions>`
Where `<What>` can be "Created", "Edited", "Added", etc. (Adjust comment characters appropriately based on file language, e.g. `/* */`, `#`, or `<!-- -->`).
There should never be but one edited header at the top of a page.  it should always be the last edit.

## User Manual Documentation Rule

Whenever changes are made that affect how a user uses the applications in this solution (excluding the keygen), you must update the user manuals in `C:\VB26\Lyracist\Documentation` (`Lyracist_User_Manual.docx` and the corresponding PDF file) accordingly. This documentation must be updated whenever there is an important change that needs documenting.

## Error List Cleanliness Rule

Whenever writing, editing, or refactoring code, always verify that your changes do not introduce new compiler warnings, analyzer warnings, or messages. Keep the compiler error list (including IDE warnings/messages) completely clean. If any warnings are expected or unavoidable (e.g. cross-platform API compatibility checks that are handled safely), use targeted `#pragma warning disable` and `#pragma warning restore` or local suppressions to keep the error list at zero warnings.

## Spitballing / Idea Exploration Rule

Whenever the user mentions "spitballing", "thinking out loud", "tossing around ideas", or explores hypothetical features, DO NOT make source code modifications or execute implementation plans automatically. Brainstorm, discuss design options, and answer questions. DO NOT proceed to code execution until explicitly instructed to do so.

## Application Update/Change Instructions

Whenever changes are made to the source code of any application in this solution, you must update the Readme.md and ChangeLog.md files accordingly. These files should be updated whenever there is an important change that needs documenting. Also, if the changes can be applied to the other applications, the necessary changes and updates should be applied to all applicable applications, if possible.
