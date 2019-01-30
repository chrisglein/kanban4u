using Kanban4U.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Kanban4U
{
    public sealed partial class TeamBurndown : Page, INotifyPropertyChanged
    {
        private ObservableCollection<WorkItem> _workItems;
        private ObservableCollection<WorkItem> _completedWorkItems;
        private List<AuditIssue> _auditIssues;
        public CollectionViewSource IterationWorkItems { get; private set; } = new CollectionViewSource { IsSourceGrouped = true };
        public CollectionViewSource BurndownSummaryGrouped { get; private set; } = new CollectionViewSource { IsSourceGrouped = true };
        public ObservableCollection<string> Iterations { get; private set; } = new ObservableCollection<string>();
        private Dictionary<string, VstsIteration> _iterations;
        public ObservableCollection<string> DaysInIteration { get; private set; } = new ObservableCollection<string>();

        public ObservableCollection<BurndownSummaryGroup> Burndown { get; private set; } = new ObservableCollection<BurndownSummaryGroup>();

        public string CurrentIteration
        {
            get
            {
                return _currentIteration;
            }
            set
            {
                if (this.SetProperty(PropertyChanged, ref _currentIteration, value))
                {
                    OnCurrentIterationChanged();
                }
            }
        }
        private string _currentIteration;

        public event PropertyChangedEventHandler PropertyChanged;


        public TeamBurndown()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Refresh(null, null);
        }

        private async void Refresh(object sender, RoutedEventArgs e)
        {
            _iterations = await Logic.GetTeamIterations(GlobalSettings.Instance.Team?.TeamId);
            Iterations.Clear();
            foreach (var iteration in _iterations)
            {
                if (iteration.Value.TimeFrame != "past")
                {
                    Iterations.Add(iteration.Value.Name);
                }
                if (iteration.Value.Attributes.TimeFrame == "current")
                {
                    CurrentIteration = iteration.Value.Name;
                }
            }

            var result = await Logic.GetWorkItemsByQuery(false);
            if (result != null)
            {
                _workItems = result.WorkItems;
                _completedWorkItems = result.CompletedWorkItems;
                _auditIssues = result.AuditIssues;
                OnCurrentIterationChanged();

                foreach (var completed in _completedWorkItems.Union(_workItems))
                {
                    var notes = completed.Notes;
                }
            }
        }

        private void OnCurrentIterationChanged()
        {
            DaysInIteration.Clear();
            if (CurrentIteration != null && _iterations.TryGetValue(CurrentIteration, out var iteration))
            {
                var now = DateTime.Now;
                now = new DateTime(now.Year, now.Month, now.Day);
                var startDate = iteration.StartDate;
                var finishDate = iteration.FinishDate;
                if (iteration.StartDate <= now && now <= iteration.FinishDate)
                {
                    startDate = now;
                }

                if (startDate.HasValue && finishDate.HasValue)
                {
                    for (var current = startDate.Value; current <= finishDate.Value; current = current.AddDays(1))
                    {
                        if (1 <= (int)current.DayOfWeek && (int)current.DayOfWeek <= 5)
                        {
                            DaysInIteration.Add(current.ToString("M/dd") + " " + current.ToString("ddd")[0]);
                        }
                    }
                }
            }

            // Combine both source lists into a single WorkItems list
            var grouped = _workItems
                ?.Where(x => x.IterationPath.Contains(CurrentIteration))
                ?.GroupBy(x => x.AssignedTo, (key, items) => new GroupedWorkItems { Key = key, Items = items.OrderBy(y => y.Rank) })
                ?.OrderBy(x => x.Key);
            IterationWorkItems.Source = grouped;


            // Sort and group to get burndown information
            Burndown.Clear();
            var individuals = new Dictionary<string, BurndownSummaryGroup>();
            Func<string, BurndownSummaryGroup> getGroup = (string who) =>
            {
                if (individuals.TryGetValue(who, out var group))
                {
                    return group;
                }

                return individuals[who] = new BurndownSummaryGroup { Who = who };
            };

            if (_workItems != null && _completedWorkItems != null)
            {
                foreach (var workitem in _workItems.Union(_completedWorkItems))
                {
                    BurndownSummaryGroup group;
                    if (workitem.Type == WorkItemType.Bug && !String.IsNullOrEmpty(workitem.ResolvedBy))
                    {
                        group = getGroup(workitem.ResolvedBy);
                    }
                    else
                    {
                        group = getGroup(workitem.AssignedTo);
                    }

                    workitem.TimeSpentOnDate.ForEach(x => group.Add(x));
                }

                // Make sure there's a group for each team member even if they didn't do burndown.
                foreach (var teamMember in grouped)
                {
                    getGroup(teamMember.Key);
                }

                // Incorporate audit issues.
                foreach (var auditIssue in _auditIssues)
                {
                    var group = getGroup(auditIssue.Owner);
                    group.AuditIssues.Add(auditIssue);
                }

                // Only include burndown from the last 2 weeks.
                DateTime cutoff = DateTime.Now.AddDays(-14);
                double workingDaysSinceCutoff = 10;

                foreach (var group in individuals.Values.OrderBy(x => x.Who))
                {
                    group.SortAndFilter(x => x.Date, x => cutoff <= x.Date);
                    group.TotalAccounted = group.Sum(x => x.CostChange);
                    group.PercentAccounted = group.TotalAccounted / workingDaysSinceCutoff;
                    group.TotalRemainingBurndown = group.Sum(x => x.RemainingDaysChange);
                    group.PercentRemainingBurndown = group.TotalRemainingBurndown / workingDaysSinceCutoff;
                    Burndown.Add(group);
                }

                BurndownSummaryGrouped.Source = Burndown;
            }
            else
            {
                BurndownSummaryGrouped.Source = null;
            }
        }

        private void DataGridLoadingRowGroup(object sender, Microsoft.Toolkit.Uwp.UI.Controls.DataGridRowGroupHeaderEventArgs e)
        {
            var collectionViewGroup = e.RowGroupHeader.CollectionViewGroup as ICollectionViewGroup;
            var group = collectionViewGroup?.Group as BurndownSummaryGroup;
            e.RowGroupHeader.PropertyValue = 
                $"{group?.Who} - Accounted {group?.PercentAccounted.ToString("P0")}, Burndown {group?.PercentRemainingBurndown.ToString("P0")}";
        }
    }

    public class GroupedWorkItems : IGrouping<string, WorkItem>
    {
        public string Key { get; set; }
        public IEnumerable<WorkItem> Items { get; set; }

        string IGrouping<string, WorkItem>.Key => throw new NotImplementedException();

        public IEnumerator<WorkItem> GetEnumerator() => Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();
    }

    public class BurndownSummaryGroup : ObservableCollection<BurndownSummary>
    {
        public string Who { get; set; }
        public double TotalAccounted { get; set; }
        public double PercentAccounted { get; set; }
        public double TotalRemainingBurndown { get; set; }
        public double PercentRemainingBurndown { get; set; }

        public List<AuditIssue> AuditIssues { get; set; } = new List<AuditIssue>();
    }

}
