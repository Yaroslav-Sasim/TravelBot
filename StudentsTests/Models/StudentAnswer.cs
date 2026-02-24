namespace StudentsTests.Models
{
    public class StudentAnswer
    {
        public int Id { get; set; }

        public int TestAttemptId { get; set; }
        public TestAttempt TestAttempt { get; set; }

        public int QuestionId { get; set; }

        public int SelectedOptionId { get; set; }

        public bool IsCorrect { get; set; }
    }
}
