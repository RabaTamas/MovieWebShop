namespace MovieShop.Server.DTOs
{
    public class VideoProgressDto
    {
        public double ProgressSeconds { get; set; }
        public DateTime LastWatched { get; set; }
    }

    public class SaveProgressDto
    {
        public double ProgressSeconds { get; set; }
    }
}
