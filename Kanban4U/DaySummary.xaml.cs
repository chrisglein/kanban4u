using Kanban4U.Models;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Kanban4U
{
    public sealed partial class DaySummary : UserControl
    {
        public DaySummaryModel Model
        {
            get { return (DaySummaryModel)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        public static readonly DependencyProperty ModelProperty =
            DependencyProperty.Register("Model", typeof(WorkItem), typeof(DaySummaryModel), new PropertyMetadata(null));

        public DaySummary()
        {
            this.InitializeComponent();
        }

        private string DateFormatNoWorky(DateTime date, string format)
        {
            return date.ToString(format);
        }
    }
}
