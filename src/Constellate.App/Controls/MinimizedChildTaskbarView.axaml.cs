using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Constellate.App.Controls
{
    public partial class MinimizedChildTaskbarView : UserControl
    {
        public MinimizedChildTaskbarView()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
