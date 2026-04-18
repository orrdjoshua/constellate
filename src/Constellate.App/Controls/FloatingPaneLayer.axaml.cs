using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Constellate.App.Controls
{
    public partial class FloatingPaneLayer : UserControl
    {
        public static readonly StyledProperty<IEnumerable<ParentPaneModel>?> ParentPanesProperty =
            AvaloniaProperty.Register<FloatingPaneLayer, IEnumerable<ParentPaneModel>?>(nameof(ParentPanes));

        public static readonly StyledProperty<IEnumerable<ChildPaneDescriptor>?> ChildPanesProperty =
            AvaloniaProperty.Register<FloatingPaneLayer, IEnumerable<ChildPaneDescriptor>?>(nameof(ChildPanes));

        private FloatingPaneLayerController? _controller;

        public FloatingPaneLayer()
        {
            InitializeComponent();
        }

        public IEnumerable<ParentPaneModel>? ParentPanes
        {
            get => GetValue(ParentPanesProperty);
            set => SetValue(ParentPanesProperty, value);
        }

        public IEnumerable<ChildPaneDescriptor>? ChildPanes
        {
            get => GetValue(ChildPanesProperty);
            set => SetValue(ChildPanesProperty, value);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            var canvas = this.FindControl<Canvas>("PART_Canvas");
            if (canvas is null)
            {
                return;
            }

            _controller = new FloatingPaneLayerController(
                this,
                canvas,
                () => ParentPanes,
                () => ChildPanes);
        }
    }
}
