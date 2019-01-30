using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Kanban4U.Models;
using System;
using System.Collections.ObjectModel;

namespace Kanban4U
{
    public sealed partial class OverheadTracker : UserControl
    {
        public WorkItem Model
        {
            get { return (WorkItem)GetValue(ModelProperty); }
            set
            {
                SetValue(ModelProperty, value);
            }
        }

        public static readonly DependencyProperty ModelProperty =
            DependencyProperty.Register("Model", typeof(WorkItem), typeof(OverheadTracker), new PropertyMetadata(null));

        public ObservableCollection<string> CommentSuggestions = new ObservableCollection<string>();

        public OverheadTracker()
        {
            this.InitializeComponent();

            CommentSuggestions.Add("Meetings");
            CommentSuggestions.Add("Investigations");
            CommentSuggestions.Add("Hardware/Software Issues");
        }

        private void AddToCost(object sender, RoutedEventArgs e)
        {
            double value = Double.Parse((string)((Button)sender).DataContext);
            double newValue = Math.Max(Model.Cost + value, 0.0);
            Model.Cost = newValue;
        }

        private void Reset(object sender, RoutedEventArgs e)
        {
            Model.Reset();
        }

        private async void Commit(object sender, RoutedEventArgs e)
        {
            bool updated = await Logic.UpdateBug(Model);
            if (updated)
            {
                Model.Commit();
            }
        }

        private void CheckIsValidNumberOnTextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            bool badValue = false;
            double value;
            if (sender.Text != String.Empty)
            {
                if (!Double.TryParse(sender.Text, out value))
                {
                    badValue = true;
                }
                else
                {
                    badValue = (value < 0.0);
                }
            }

            if (badValue)
            {
                int selectionPosition = sender.SelectionStart - 1;
                sender.Text = sender.Text.Remove(selectionPosition, 1);
                sender.SelectionStart = selectionPosition;
            }
        }
    }
}
