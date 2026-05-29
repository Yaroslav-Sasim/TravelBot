using static System.Net.Mime.MediaTypeNames;

namespace TravelBot.Models
{
    public class TestAttempt
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Students Student { get; set; }

        public int TestId { get; set; }
        public Tests Test { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }

        public int TotalScore { get; set; }

        public List<StudentAnswer> Answers { get; set; } = new();
    }
}
