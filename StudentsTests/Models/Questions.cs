using static System.Net.Mime.MediaTypeNames;

namespace StudentsTests.Models
{
    public class Questions
    {
        public int Id { get; set; }

        public string Text { get; set; }

        public string? ImageUrl { get; set; }

        public int Points { get; set; }

        public int TestId { get; set; }
        public Tests Test { get; set; }

        public List<AnswerOption> Options { get; set; } = new();
    }
}
