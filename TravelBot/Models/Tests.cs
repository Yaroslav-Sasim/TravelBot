namespace TravelBot.Models
{
    public class Tests
    {
        public int Id { get; set; }

        public string Title { get; set; }

        public int SubjectId { get; set; }
        public Subject Subject { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public int DurationMinutes { get; set; }

        public List<Questions> Questions { get; set; } = new();
        public List<TestAttempt> Attempts { get; set; } = new();
    }
}
