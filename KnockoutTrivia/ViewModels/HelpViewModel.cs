// Edited on Aug 28, 2026 @ 09:32:30 -> Added Topic 9 for Phone/Tablet Connect and Mobile Buzzer to HelpViewModel
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using KnockoutTrivia.Models;

namespace KnockoutTrivia.ViewModels;

public partial class HelpViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<HelpTopic> _helpTopics;

    [ObservableProperty]
    private HelpTopic? _selectedTopic;

    public HelpViewModel()
    {
        _helpTopics =
        [
            new()
            {
                Title = "1. Game Overview & Rules",
                Icon = "Trophy24",
                AccentColor = "#10B981",
                DescriptionHeader = "Knockout Trivia Rules & Elimination Mechanics",
                DescriptionContent = "• Game Concept: Knockout Trivia is a high-energy, bar-friendly elimination game where players or teams compete to survive while racking up points.\n\n• Strike System: Each player starts healthy (0 strikes) and accumulates strikes when missing questions (unless protected by a Shield Token). Reaching 3 strikes results in a Knockout (elimination).\n\n• Winning Condition: The last remaining active player wins the game, or the highest score after the question limit is reached.\n\n• Fast Gameplay: High-visibility 16:9 boards make questions, options, timer countdowns, and scoreboard changes clearly visible from anywhere in the bar or venue."
            },
            new()
            {
                Title = "2. Scoreboard & Strike Colors",
                Icon = "Table24",
                AccentColor = "#38BDF8",
                DescriptionHeader = "Horizontal Status Bars & Dynamic Strike Tinting",
                DescriptionContent = "• Green Bar (0 Strikes - Active): Healthy status with full visibility and active play.\n\n• Yellow Bar (1 Strike - Warning): Player has missed one question; caution is advised.\n\n• Orange Bar (2 Strikes - Danger Zone): Player is one miss away from knockout elimination.\n\n• Red Bar (3 Strikes - Knocked Out): Bar turns dimmed red and status becomes 'KNOCKED OUT'. Eliminated players stop accumulating points for regular rounds.\n\n• Player Bar Layout: [PLAYER NAME] [SHIELD TOKENS] [5-BLOCK STREAK METER] [SCORE]."
            },
            new()
            {
                Title = "3. Shield Tokens & Absorption",
                Icon = "Shield24",
                AccentColor = "#0284C7",
                DescriptionHeader = "Shield Tokens (Armor against Strikes)",
                DescriptionContent = "• Armor Functionality: Shield tokens protect players from receiving strikes. When a player answers incorrectly but has at least 1 shield token, 1 shield token is consumed instead of adding a strike.\n\n• Token Limit: Players can hold up to a configurable maximum of shield tokens (Default: 3 max shields).\n\n• Earning Shields: Players earn additional shield tokens by completing 5-block streak meters (5 consecutive correct answers) or through fallback bonus rewards.\n\n• Starting Tokens: Players start each new game with 1 complimentary shield token."
            },
            new()
            {
                Title = "4. Streak Meters (5 Blocks)",
                Icon = "ArrowTrendingLines24",
                AccentColor = "#F59E0B",
                DescriptionHeader = "5-Block Streak Progress & Token Rewards",
                DescriptionContent = "• Progress Blocks: Every consecutive correct answer fills 1 block on the player's 5-block streak meter.\n\n• Token Reward Milestone: When all 5 blocks are filled (5 correct answers in a row), the player is automatically awarded 1 Shield Token (if below max cap), and the meter resets to 0.\n\n• Streak Reset: If a player answers incorrectly, their active streak count immediately resets to 0."
            },
            new()
            {
                Title = "5. ⚡ Super Streak & Target Wheel",
                Icon = "Flash24",
                AccentColor = "#8B5CF6",
                DescriptionHeader = "Scaryoke-Style Super Streak Rotary Target Wheel",
                DescriptionContent = "• Super Streak Milestone: Reaching 20 consecutive correct answers unlocks the Super Streak challenge!\n\n• Wheel Generation: The game pauses regular question flow and opens the Rotary Wheel populated with wedges for all opponents who currently hold shield tokens (excluding the Super Streak player).\n\n• Spin & Target Strike: The DJ spins the wheel. When it slows and lands on a player's wedge, that target opponent is stripped of 1 shield token (stealing/destroying their defense).\n\n• Fallback Reward: If no opponents hold shield tokens when Super Streak is unlocked, the wheel awards +50 Bonus Points directly to the Super Streak player."
            },
            new()
            {
                Title = "6. DJ Host Console & Operations",
                Icon = "Headset24",
                AccentColor = "#EC4899",
                DescriptionHeader = "Live Question Control & Fast Player Adjudication",
                DescriptionContent = "• Question Flow: The DJ controls the active question display with 'Reveal Answer' and 'Next Question ▶'.\n\n• Player Adjudication: Click the green checkmark '✓' to mark a player's answer correct (+Points, +Streak), or click the red '✗' to mark incorrect (consumes Shield or adds Strike).\n\n• Quick Player Management: Add new walk-up players anytime using the '+ Add' quick input bar on the host console.\n\n• Manual vs. Automatic Modes: Manual mode gives the DJ full microphone control to read questions at their own pace; Automatic mode runs question countdowns and transitions on automated timers."
            },
            new()
            {
                Title = "7. 16:9 Presentation & Banners",
                Icon = "Tv24",
                AccentColor = "#F97316",
                DescriptionHeader = "16:9 Auto-Scaling Display & Event Screens",
                DescriptionContent = "• Proportional Scaling: Designed for standard 16:9 logical canvas (1920x1080) with automatic uniform scaling, letterboxing, and pillarboxing for any projector or TV resolution.\n\n• Dynamic Event Banners: One-click display of Intro Screen, Round Intermission, Champion Celebration, Knockout Elimination, and Super Streak Alerts.\n\n• Official Branding: High-resolution official Knockout Trivia logos (kotrv_logo.png / kotrv_logo.webp) are rendered with high-quality antialiasing and glow shaders."
            },
            new()
            {
                Title = "8. Settings & Multi-Database Selection",
                Icon = "Settings24",
                AccentColor = "#10B981",
                DescriptionHeader = "3-Column Settings Panel & Database Management",
                DescriptionContent = "• 3-Column Layout: Organizes scoring, shields/streaks, and database sources side-by-side to eliminate unnecessary scrolling.\n\n• Database & Pack Selection ListBox: The DJ can check/uncheck active SQLite trivia databases (from KoTrivia/Data) and themed JSON packs (from KoTrivia/Packs) to mix categories (e.g. 80s Rock + Pop Culture + General Knowledge).\n\n• Display & Monitor Assignment: Choose which monitor displays the DJ Host Console and which monitor projects the big screen audience views.\n\n• Audio/Visual FX: Toggle sound effects (countdown beeps, strike chimes, wheel ticks) and smooth color transition animations."
            },
            new()
            {
                Title = "9. 📱 Phone/Tablet Connect & Buzzer",
                Icon = "Phone24",
                AccentColor = "#38BDF8",
                DescriptionHeader = "Mobile Player Companion & Automatic Internal Scoring",
                DescriptionContent = "• Dual QR Code Connect Screen: The DJ can navigate to the '📱 Player Connect' tab or push it to the Audience Big Screen. Players simply scan Card 1 (Wi-Fi QR) to connect to venue Wi-Fi, then scan Card 2 (Game QR) to open the mobile buzzer in their web browser with no app download required.\n\n• Mobile Player Companion: Connects players to the live game, allowing them to tap their answer (A, B, C, D / True-False), view their live strike count, shield tokens, and streak meters, receive real-time answer results, and listen to synthesized Web Audio sound FX and haptics.\n\n• Automatic Scoring: When the question timer expires or the DJ clicks 'Reveal Answer', the game engine automatically grades all submitted answers across connected phones, awards points, manages shields/strikes, and updates the scoreboard internally.\n\n• Automatic Mode: In Automatic Mode, the game autonomously runs the countdown timer, evaluates answers, waits for the configured buffer, and advances to the next question automatically."
            }
        ];

        _selectedTopic = _helpTopics[0];
    }
}

