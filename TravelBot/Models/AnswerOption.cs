namespace TravelBot.Models
{
    public class AnswerOption
    {
        public int Id { get; set; }

        public string Text { get; set; }

        public bool IsCorrect { get; set; }

        public int QuestionId { get; set; }
        public Questions Question { get; set; }
    }
}
