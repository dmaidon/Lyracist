# Lyracist Pro - KJ Feature Review Guide

Welcome to **Lyracist Pro**, a state-of-the-art karaoke hosting platform designed specifically to give Karaoke Jockeys (KJs) complete control over their show while keeping singers and crowds engaged. Below is a comprehensive breakdown of the advanced features and gamified experiences available in Lyracist.

---

## 🎡 1. Crowd Engagement & "Scaryoke" Mode

Give your crowd a reason to stay in their seats with interactive show events!
- **Projected Scaryoke Spinning Wheel**: Toggling **Scaryoke Mode** launches a color-coded, animated category wheel that projects directly onto the rotation billboard screen. As the wheel spins, the crowd watches it slow down with realistic physics, deceleration easing, ticking sound effects, and a final selection output.
- **Dynamic Mobile Requests & QR Code**: The billboard projection display hosts a floating, real-time QR code. Patrons can scan this QR code with their mobile phone or tablet to instantly join the request client web page, browse the library, and submit songs without leaving their tables.

---

## 🏆 2. Performer Gamification & XP Leaderboard

Keep your regulars coming back week after week by turning your show into a game!
- **XP Progression & Levels**: Singers automatically earn experience points based on their performance history and crowd rating scores (`XP = TotalSongsSung * 100 + Score`). They level up and earn custom titles such as *Shower Singer*, *Pub Regular*, *Vocal Powerhouse*, or *Karaoke Legend*.
- **Achievement Badges**: Performers unlock custom visual badges based on their milestones (e.g. `🎤 Debut`, `🔥 Rising Star`, `👑 Legend`, `⭐ Crowd Pleaser`, `🎯 High Scorer`) which are displayed alongside their names on the rotation display and active queue list.
- **Dynamic Leaderboard**: The crowd billboard has a built-in Leaderboard view mode that displays the top performers of the night, ranked by level, score, and unlocked badges.

---

## 🎥 3. Real-Time Pitch Shifting & Key Transposition

No more restarting songs when a singer needs a key adjustment!
- **Active Performance Key Transposition**: Adjust a singer's key on the fly (transposing semitones from `-6` to `+6`) using the control slider. Lyracist will hot-reload the audio engine, rebuild the FFmpeg pitch-shifting filters (`asetrate` + `atempo`), seek back to the exact millisecond, and resume playback in under 150ms.
- **Singer Profiles default keys**: Save a singer's preferred key transpose defaults so that their songs are automatically set to their vocal range when queued.

---

## 🌌 4. Advanced Chroma-Key & Visual FX Backdrops

Get rid of boring, retro blue/black background blocks on old CDG files!
- **Dynamic Chroma-Keying**: Lyracist reads the active background/border color index (pixel `0,0`) of CDG files in real time and automatically renders it transparent while keeping lyrics text completely sharp and opaque.
- **GPU-Accelerated 4K Backdrops**: Transparent lyrics are layered over beautiful, responsive visual effects that scale to any display size:
  1. **Neon Waveform**: Shifting and morphing neon cyan and magenta curves.
  2. **Nebula Bokeh**: Colored liquid glow bokeh bubbles floating smoothly.
  3. **Retro Synthwave**: An endless scrolling 3D perspective grid under a neon sunset horizon.
  4. **Space Starfield**: Multi-layer parallax stardust drifting through deep space.

---

## 🎛️ 5. Pro-Audio Controls & Equalization

Keep your venue sounding professional with full-featured audio processing.
- **10-Band EQ & Tone Controls**: Direct control sliders for global Treble, Midrange, and Bass levels.
- **Vocal Dynamics Processing**: Protect your speakers and equalize singer volume discrepancies with a built-in **Vocal Compressor** and **Peak Limiter**.
- **ASIO Output support**: Low-latency professional audio driver output routing with selectable buffer sizes (64 to 1024 samples) to ensure zero vocal delay.

---

## 📋 6. Symmetrical Queue Management

Manage your rotation with a highly efficient, dual-column control panel.
- **Deselection Safety**: Click an active singer in the list to select them (highlighted in red) or click them again to deselect, allowing you to easily correct accidental queue loads.
- **Quick Preset Ratings**: Add custom feedback symbols (e.g., ⭐, ❤️, 🔥, 🏆, 👑) with preset positive emoji buttons. Positive validation rules strictly enforce the *"nothing negative or detrimental"* policy to keep the atmosphere positive.

---

## 💻 7. Recommended System & Equipment Requirements

Because Lyracist Pro leverages cutting-edge .NET 10 core performance, low-latency audio processing, and hardware-accelerated 4K graphics rendering, upgrading from Windows Vista is mandatory.

### Operating System (OS)
- **Minimum**: Windows 10 (64-bit, Version 1809 / Build 17763 or later).
- **Recommended**: Windows 11 (64-bit).
- **Important Note**: Windows Vista, XP, 7, and 8/8.1 are **not supported** due to modern .NET runtime and WPF UI architecture requirements.

### Hardware Specifications
- **Processor (CPU)**: 
  - *Minimum*: Intel Core i5 or AMD Ryzen 5 (4th Generation or newer).
  - *Recommended*: Intel Core i7 or AMD Ryzen 7 (8th Generation or newer) for smooth real-time pitch processing and dual displays.
- **Memory (RAM)**: 
  - *Minimum*: 8 GB RAM.
  - *Recommended*: 16 GB RAM (handles background database requests, web-server connections, and dual display monitors simultaneously).
- **Graphics Card (GPU)**: 
  - *Minimum*: DirectX 11 or 12 compatible integrated graphics (e.g. Intel UHD Graphics).
  - *Recommended*: Dedicated graphics card (NVIDIA GTX 1050 / AMD RX 560 or newer) for smooth 60 FPS visualizer backdrops, real-time CDG transparency blending, and lag-free dual 4K outputs.
- **Display Connections**: 
  - Multi-display capable video output (HDMI, DisplayPort, or USB-C) to support separate windows: one for the host console, one for the singer's lyrics screen, and one for the crowd's rotation billboard.

### Audio Interface / Gear
- For professional ASIO routing, a USB Mixer or Audio Interface with native **ASIO drivers** (e.g., Focusrite Scarlett, Presonus AudioBox, or Yamaha MG-XU series mixing console) is highly recommended to guarantee zero-latency vocal compression, limiting, and pitch transpositions.
