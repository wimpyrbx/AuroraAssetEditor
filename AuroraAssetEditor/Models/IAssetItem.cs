namespace AuroraAssetEditor.Models
{
    public interface IAssetItem
    {
        string Title { get; }
        string TitleId { get; }
        System.Windows.Media.Brush BackgroundColor { get; set; }
    }
} 