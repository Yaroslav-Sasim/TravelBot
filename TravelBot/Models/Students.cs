namespace TravelBot.Models
{
    public class Students
    {
        public int Id { get; set; }

        public string Name { get; set; }
        public string Surname { get; set; }
        public string Group { get; set; }

        public List<TestAttempt> Attempts { get; set; } = new();

    }
}
