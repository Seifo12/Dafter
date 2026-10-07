namespace CourtDesk.UI.ViewModels
{
    public sealed class Sentence
    {
        public Sentence(string text)
        {
            Text = text;
        }

        public string Text { get; private set; }
    }
}