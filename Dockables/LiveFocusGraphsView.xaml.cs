using System.Windows;
using System.Windows.Controls;

namespace Cwseo.NINA.LiveFocus.Dockables
{
    public partial class LiveFocusGraphsView : UserControl
    {
        private bool showStarProfile = true;
        public bool ShowStarProfile
        {
            get => showStarProfile;
            set { showStarProfile = value; ArrangeGraphs(); }
        }
        public LiveFocusGraphsView()
        {
            InitializeComponent();
            SizeChanged += (_, _) => ArrangeGraphs();
        }
        private void ArrangeGraphs()
        {
            bool horizontal = ShowStarProfile && ActualWidth >= 450;
            ProfileCard.Visibility = ShowStarProfile ? Visibility.Visible : Visibility.Collapsed;
            MeasurementsPanel.ColumnDefinitions[1].Width = horizontal ? new GridLength(.7, GridUnitType.Star) : new GridLength(0);
            MeasurementsPanel.RowDefinitions[0].Height = new GridLength(horizontal ? 1 : 2, GridUnitType.Star);
            MeasurementsPanel.RowDefinitions[1].Height = !ShowStarProfile || horizontal ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Grid.SetRow(ProfileCard, horizontal ? 0 : 1);
            Grid.SetColumn(ProfileCard, horizontal ? 1 : 0);
            HfrCard.Margin = !ShowStarProfile ? new Thickness(0) : horizontal ? new Thickness(0, 0, 8, 0) : new Thickness(0, 0, 0, 8);
        }
    }
}
