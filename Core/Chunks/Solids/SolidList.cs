namespace Core.Solids
{
    public class SolidList : BasicResource
    {
        public string PipelinePath { get; set; }

        public string Filename { get; set; }

        public List<SolidObject> Objects { get; } = new List<SolidObject>();
    }
}