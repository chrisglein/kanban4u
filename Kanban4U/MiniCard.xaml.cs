using Kanban4U.Models;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Kanban4U
{
    public sealed partial class MiniCard : UserControl
    {
        public WorkItem Model
        {
            get { return (WorkItem)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        public static readonly DependencyProperty ModelProperty =
            DependencyProperty.Register("Model", typeof(WorkItem), typeof(MiniCard), new PropertyMetadata(null));

        public MiniCard()
        {
            this.InitializeComponent();
        }

        private void ActivateClick(object sender, RoutedEventArgs e)
        {
            if (Model.Type == WorkItemType.Bug)
            {
                Model.State = State.Active;
                Model.Substatus = Substatus.UnderDevelopment;
            }
            else
            {
                Model.State = State.Started;
            }
        }
    }
}
