using Kanban4U.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

using MUX = Microsoft.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Kanban4U
{
    public class WorkItemDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate DefaultTemplate { get; set; }
        public DataTemplate CollapsedTemplate { get; set; }
        public DataTemplate OverheadTemplate { get; set; }
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            FrameworkElement element = container as FrameworkElement;

            WorkItem workItem = (WorkItem)item;

            switch (workItem.UberStatus)
            {
                case UberStatus.NotYet:
                    return CollapsedTemplate;
                default:
                    if ((workItem.Type == WorkItemType.Task) && (workItem.Title.StartsWith("Overhead Tracking")))
                    {
                        return OverheadTemplate;
                    }
                    return DefaultTemplate;
            }
        }
    }
    
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public ObservableCollection<WorkItem> WorkItems { get; private set; } = new ObservableCollection<WorkItem>();

        public GlobalSettings Settings
        {
            get
            {
                return GlobalSettings.Instance;
            }
        }
        
        public ObservableCollection<WorkItem> StartedWorkItems { get; private set; } = new ObservableCollection<WorkItem>();
        public ObservableCollection<WorkItem> CompletedWorkItems { get; private set; } = new ObservableCollection<WorkItem>();
        public ObservableCollection<AuditIssue> AuditIssues { get; private set; } = new ObservableCollection<AuditIssue>();

        public ObservableCollection<DaySummaryModel> Days { get; private set; } = new ObservableCollection<DaySummaryModel>();

        public bool IsHorizontal
        {
            get { return _horizontal; }
            set
            {
                if (_horizontal != value)
                {
                    _horizontal = value;
                    ApplyState();
                }
            }
        }
        private bool _horizontal = true;

        private void ApplyState()
        {
            VisualStateManager.GoToState(this, IsHorizontal ? "Horizontal" : "Vertical", true);
        }
        
        public MainPage()
        {
            // TODO: Derive from DaySummaryLength
            for (int i = 0; i < 10; i++)
            {
                Days.Add(new DaySummaryModel());
            }
            this.InitializeComponent();
        }

#pragma warning disable 1998
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            IsHorizontal = (bool)(e.Parameter ?? true);

            ApplyState();

#if DEBUG_STARTUP
            ContentDialog waitDialog = new ContentDialog
            {
                Title = "Hello!",
                Content = "I won't do anything until you press OK, promise!",
                IsPrimaryButtonEnabled = true,
                PrimaryButtonText = "OK"
            };
            await waitDialog.ShowAsync();
#endif
            if (String.IsNullOrWhiteSpace(Configuration.CurrentConfig.TeamVSTSUri))
            {
                var projectDialog = new ProjectDialog();
                await projectDialog.ShowAsync();
                Configuration.CurrentConfig.SetTeamVSTSUri(projectDialog.ProjectUrl);
            }

            Refresh(null, null);
        }

        private async void ItemClick(object sender, ItemClickEventArgs e)
        {
            WorkItem item = (WorkItem)e.ClickedItem;
            await Logic.ViewBug(item);
        }

        private async void ViewQuery(object sender, RoutedEventArgs e)
        {
            await Logic.ViewQuery();
        }

        private async void Refresh(object sender, RoutedEventArgs e)
        {
            foreach (var item in WorkItems)
            {
                item.PropertyChanged -= new PropertyChangedEventHandler(OnWorkItemCostingPropertyChanged);
            }
            WorkItems.Clear();
            CompletedWorkItems.Clear();

            var result = _lastResult = await Logic.GetWorkItemsByQuery();
            if (result != null)
            {
                // Combine both source lists into a single WorkItems list

                foreach (var item in result.WorkItems)
                {
                    WorkItems.Add(item);

                    item.PropertyChanged += new PropertyChangedEventHandler(OnWorkItemCostingPropertyChanged);
                }
                foreach (var item in result.CompletedWorkItems)
                {
                    WorkItems.Add(item);

                    item.PropertyChanged += new PropertyChangedEventHandler(OnWorkItemCostingPropertyChanged);
                }

                AuditIssues.Clear();
                foreach (var issue in result.AuditIssues)
                {
                    AuditIssues.Add(issue);
                }
            }

            UpdateCosts();
            Regroup();
            UpdateDirty();
        }


        private void OnWorkItemCostingPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case nameof(WorkItem.RemainingDays):
                case nameof(WorkItem.Cost):
                    UpdateCosts();
                    break;

                case nameof(WorkItem.State):
                case nameof(WorkItem.Substatus):
                    Regroup();
                    break;

                case nameof(WorkItem.Dirty):
                    UpdateDirty();
                    break;

                default:
                    break;
            }
        }

        private void UpdateDirty()
        {
            bool dirty = false;
            foreach (var item in WorkItems)
            {
                dirty |= item.Dirty;
            }
            Dirty = dirty;
        }

        // Caching the results of the last query so that Regroup can use it.
        private LogicQueryResult _lastResult;

        private void Regroup()
        {
            var notStartedWorkItems = WorkItems
                .Where(x => x.UberStatus == UberStatus.NotYet)
                .GroupBy(x => x.IterationPath, 
                         (key, items) => new GroupedWorkItems { Key = key, Items = items.OrderBy(y => y.Rank) })
                .OrderBy(x => x.Key);

            ReplaceContents(from item in WorkItems where item.UberStatus == UberStatus.Working select item, StartedWorkItems);
            ReplaceContents(_lastResult.CompletedWorkItems, CompletedWorkItems);

            NotStartedWorkItems.Source = notStartedWorkItems;
        }

        private static void ReplaceContents<T>(IEnumerable<T> source, ObservableCollection<T> target)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(item);
            }
        }

        private void UpdateCosts()
        {
            AnalyzeTimeReportedFromLogs(Days);
        }

        private void AnalyzeTimeReportedFromLogs(ObservableCollection<DaySummaryModel> days)
        {
            DateTime nowLocalTime = DateTime.Now;
            DateTime endOfTodayLocal = new DateTime(nowLocalTime.Year, nowLocalTime.Month, nowLocalTime.Day, 23, 59, 59, DateTimeKind.Local);
            DateTime endOfRange = endOfTodayLocal;

            int daysIndex = 0;

            // Figure out how much total time has been reported across all active cards
            double timeAccountedFor = 0.0;
            foreach (WorkItem item in WorkItems)
            {
                double drilledUp = (item.Cost - item.CommittedCost);
                timeAccountedFor += drilledUp;
            }

            // Prepend an entry for time that is reported but not committed
            if (timeAccountedFor > 0)
            {
                string formattedDate = nowLocalTime.ToString("D");
                Logic.TimeSpentTrace($"Prepending {timeAccountedFor} spent on {formattedDate} for pending items");

                DaySummaryModel dayModel = days[daysIndex];
                dayModel.Date = nowLocalTime;
                dayModel.Amount = timeAccountedFor;
                dayModel.Details = "Pending";
                daysIndex++;
            }

            TimeSpan increment = TimeSpan.FromDays(1);
            for (int i = 0; i < DaySummaryRange; i++)
            {
                DateTime beginningOfRange = endOfRange - increment;
                Logic.TimeSpentTrace($"Looking for time spent between {beginningOfRange} and {endOfRange}");

                List<string> subLog = new List<string>();
                double timeSpentInRange = AnalyzeTimeReportedFromLogs(beginningOfRange, endOfRange, subLog);

                if (timeSpentInRange > 0)
                {
                    string formattedDate = endOfRange.ToString("D");
                    Logic.TimeSpentTrace($"{timeSpentInRange} spent on {formattedDate}");
                    string details = $"{timeSpentInRange} spent on {formattedDate}";

                    foreach (string entry in subLog)
                    {
                        Logic.TimeSpentTrace("  " + entry);
                        details += "\n" + entry;
                    }

                    if (daysIndex < days.Count)
                    {
                        DaySummaryModel dayModel = days[daysIndex];

                        // The logic above may prepend a day for pending items. If there happen to be edits in the same day we should collapse them into one entry.
                        if ((daysIndex > 0) && (days[daysIndex - 1].Date.DayOfYear == endOfRange.DayOfYear))
                        {
                            dayModel = days[daysIndex - 1];
                            dayModel.Amount += timeSpentInRange;
                            dayModel.Details = details;
                        }
                        else
                        {
                            dayModel.Date = endOfRange;
                            dayModel.Amount = timeSpentInRange;
                            dayModel.Details = details;
                            daysIndex++;
                        }

                    }
                }

                endOfRange = beginningOfRange;
            }

            for (int i = daysIndex; i < days.Count; i++)
            {
                DaySummaryModel dayModel = days[i];
                dayModel.Date = DateTime.Now;
                dayModel.Amount = 0.0;
                dayModel.Details = null;
            }
        }

        private double AnalyzeTimeReportedFromLogs(DateTime beginningOfRange, DateTime endOfRange, List<string> log)
        {
            // Figure out how much time has been reported over a specific range
            double timeSpentInRange = 0;
            foreach (WorkItem item in WorkItems)
            {
                foreach (var burndown in item.TimeSpentOnDate)
                {
                    if ((burndown.Date > endOfRange) || (burndown.Date < beginningOfRange))
                    {
                        continue;
                    }

                    timeSpentInRange += burndown.CostChange;
                    log.Add($"{burndown.CostChange} : {burndown.Date} on '{item.Title}'");
                }
            }
            
            return timeSpentInRange;
        }

        private int InvertRemaining(int remaining, int cost)
        {
            return (cost - remaining);
        }

        private int Ternary(bool condition, int valueIfTrue, int valueIfFalse)
        {
            return (condition ? valueIfTrue : valueIfFalse);
        }

        private GridLength MultiplyGridLength(double a, double b)
        {
            return new GridLength(a * b);
        }

        private double Multiply(double a, double b)
        {
            return (a * b);
        }

        private double MultiplyToMax(double a, double b, double max)
        {
            return Math.Min(a * b, max);
        }

        private double Sum(double a, double b, double c, double d, double e)
        {
            return a + b + c + d + e;
        }
        
        public static double CountCosts(IEnumerable<WorkItem> list)
        {
            double total = 0;
            foreach (WorkItem item in list)
            {
                total += item.Cost;
            }
            return total;
        }

        public static double CountRemaining(IEnumerable<WorkItem> list)
        {
            double total = 0;
            foreach (WorkItem item in list)
            {
                total += item.RemainingDays;
            }
            return total;
        }

        private void MiniCardUpdated(object sender, EventArgs e)
        {
            Regroup();
        }

        // Remove all changes to all cards
        private void AbandonUpdates(object sender, RoutedEventArgs e)
        {
            foreach (WorkItem item in WorkItems)
            {
                item.Reset();
            }
        }

        // Commit the pending changes on all cards
        private async void CommitUpdates(object sender, RoutedEventArgs e)
        {
            foreach (WorkItem item in WorkItems)
            {
                if (item.Dirty)
                {
                    bool updated = await Logic.UpdateBug(item);
                    if (updated)
                    {
                        item.Commit();
                    }
                }
            }
        }

        public bool Dirty
        {
            get { return (bool)GetValue(DirtyProperty); }
            set { SetValue(DirtyProperty, value); }
        }

        public static readonly DependencyProperty DirtyProperty =
            DependencyProperty.Register("Dirty", typeof(bool), typeof(MainPage), new PropertyMetadata(false));

        public int DaySummaryRange
        {
            get { return (int)GetValue(DaySummaryRangeProperty); }
            set { SetValue(DaySummaryRangeProperty, value); }
        }

        public static readonly DependencyProperty DaySummaryRangeProperty =
            DependencyProperty.Register("DaySummaryRange", typeof(int), typeof(MainPage), new PropertyMetadata(7));

        public TeamSettings TeamSettings
        {
            get
            {
                return Logic.TeamSettings;
            }
        }

        private void UserToImpersonateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Refresh(null, null);
        }
    }
}
