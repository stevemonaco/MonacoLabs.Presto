using System.Collections.ObjectModel;
using PrestoGallery.Showcase;

namespace PrestoGallery.Builder;

/// <summary>Two drag-to-reorder lists, one lifting with the default <c>{presto:Reorder}</c> and one at <c>Intensity=2</c>.</summary>
public partial class ReorderPage : ShowcasePage
{
    public ReorderPage()
    {
        InitializeComponent();
        DefaultLift.ItemsSource = new ObservableCollection<string>(["Inbox", "Drafts", "Sent", "Archive"]);
        StrongLift.ItemsSource = new ObservableCollection<string>(["Inbox", "Drafts", "Sent", "Archive"]);
    }
}
