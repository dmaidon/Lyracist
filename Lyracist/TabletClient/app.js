// Edited on Jul 17, 2026 @ 09:00:00 -> Use relative SignalR path
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/lyricsHub")
    .withAutomaticReconnect()
    .build();

// Update connection status badge
const badge = document.getElementById("statusBadge");

connection.onreconnecting(error => {
    badge.innerText = "Reconnecting...";
    badge.classList.remove("connected");
});

connection.onreconnected(connectionId => {
    badge.innerText = "Live";
    badge.classList.add("connected");
});

connection.onclose(error => {
    badge.innerText = "Disconnected";
    badge.classList.remove("connected");
});

connection.on("LyricsUpdated", message => {
    document.getElementById("title").innerText = message.title || "Lyracist Broadcast";
    document.getElementById("currentLine").innerText = message.currentLine;
    document.getElementById("nextLine").innerText = message.nextLine;
    document.getElementById("position").innerText = formatTime(message.position);
});

function formatTime(ts) {
    if (!ts) return "0:00";
    if (typeof ts === 'string') return ts;
    const totalSeconds = ts.seconds || 0;
    const m = Math.floor(totalSeconds / 60);
    const s = Math.floor(totalSeconds % 60);
    return `${m}:${s.toString().padStart(2, "0")}`;
}

async function start() {
    try {
        await connection.start();
        console.log("SignalR Connected.");
        badge.innerText = "Live";
        badge.classList.add("connected");
    } catch (err) {
        console.error(err);
        badge.innerText = "Connection Failed";
        setTimeout(start, 5000);
    }
}

start();
