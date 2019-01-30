using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Kanban4U.Models;
using System;

namespace Kanban4U
{
    public sealed partial class Card : UserControl
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
            DependencyProperty.Register("Model", typeof(WorkItem), typeof(Card), new PropertyMetadata(null));
                
        public Card()
        {
            this.InitializeComponent();
        }
        
        private string ElapsedTimeToString(string prefix, DateTime dateTime, string suffix)
        {
            string formated;

            TimeSpan elapsed = DateTime.UtcNow - dateTime;
            if (elapsed.TotalDays < 1)
            {
                if (elapsed.TotalHours < 0)
                {
                    formated = dateTime.ToShortDateString();
                }
                else
                {
                    int hours = (int)Math.Floor(elapsed.TotalHours);
                    if (hours == 0)
                    {
                        formated = "moments";
                    }
                    else
                    {
                        formated = String.Format("{0} hour{1}", hours, hours == 1 ? "" : "s");
                    }
                }
            }
            else
            {
                int days = (int)Math.Floor(elapsed.TotalDays);
                formated = String.Format("{0} day{1}", days, days == 1 ? "" : "s");
            }

            return prefix + formated + suffix;
        }

        private double Add(double a, double b)
        {
            return a + b;
        }

        private void BurnDown(object sender, RoutedEventArgs e)
        {
            double value = Double.Parse((string)((Button)sender).DataContext);
            double newRemaining = Math.Max(Model.RemainingDays + value, 0.01);
            double newCost = Math.Max(Model.Cost - value, 0.0);
            Model.RemainingDays = newRemaining;
            Model.Cost = newCost;
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

    public class WorkItemTypeConverter : Windows.UI.Xaml.Data.IValueConverter
    {
        public WorkItemTypeConverter()
        {
        }

        public object Bug { get; set; }
        public object Task { get; set; }
        public object FutureTask { get; set; }
        public object CompletedTask { get; set; }
        public object ResolvedBug { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            WorkItem item = (WorkItem)value;
            WorkItemType type = item.Type;

            switch (type)
            {
                case WorkItemType.Bug:
                    if (((item.State == State.Resolved) || (item.State == State.Closed)) && (ResolvedBug != null))
                    {
                        return ResolvedBug;
                    }
                    return Bug;

                case WorkItemType.Task:
                    if ((item.State == State.Completed) && (CompletedTask != null))
                    {
                        return CompletedTask;
                    }
                    if ((item.IterationPath != Logic.TeamSettings.IterationPath) && (FutureTask != null))
                    {
                        return FutureTask;
                    }
                    return Task;
            }

            throw new ArgumentException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
