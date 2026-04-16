using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Constellate.App.Controls
{
    public partial class ParentPaneView : UserControl
    {
        public ParentPaneView()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
