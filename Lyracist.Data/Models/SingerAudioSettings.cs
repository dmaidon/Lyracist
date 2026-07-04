namespace Lyracist.Data.Models
{
    public class SingerAudioSettings
    {
        public int SingerAudioSettingsId { get; set; }
        public int SingerId { get; set; }

        public double Treble { get; set; }
        public double Mid { get; set; }
        public double Bass { get; set; }
        public double Gain { get; set; }
        public int Key { get; set; }
        public double Tempo { get; set; }
        public double Compressor { get; set; }
        public double Limiter { get; set; }
        public string Notes { get; set; } = string.Empty;

        // Navigation Properties
        public Singer? Singer { get; set; }
    }
}
