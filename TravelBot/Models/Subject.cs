using static System.Net.Mime.MediaTypeNames;

namespace TravelBot.Models
{
    public class Subject
    {
        public int Id { get; set; }
        public string Title { get; set; }

        public List<Tests> Tests { get; set; } = new();
    }
}
