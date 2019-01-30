using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Kanban4U.Models
{
    public enum WorkItemType
    {
        Bug,
        Task,
        Feature
    }

    public enum State
    {
        // Bug
        Active,
        Resolved,
        Closed,
        // Task
        Proposed,
        Started,
        Committed,
        Completed,
        Cut,
    }

    public enum Substatus
    {
        Blocked,
        BugUnderstood,
        CheckedIn,
        Consider,
        Deployed,
        FixVerified,
        InCodeReview,
        InCustomerValidation,
        Investigating,
        Mitgated,
        Postponed,
        QueuedForCheckin,
        ReadyForDeployment,
        TestingFix,
        UnderDevelopment,
    }

    public enum ResolvedReason
    {
        None,
        ByDesign,
        Duplicate,
        External,
        Fixed,
        NotRepro,
        WontFix,
    }

    // Status as shown by the board. This is a combination of all the above VSTS states into 3 main buckets.
    public enum UberStatus
    {
        NotYet,
        Working,
        AllDone
    }

    public class WorkItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void FirePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public string AssignedTo { get; set; }
        public string ResolvedBy { get; set; }

        public WorkItemType Type { get; set; }
        public string Id { get; set; }
        public string SelfUrl { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public string IterationPath { get; set; }
        public int Rank { get; set; }

        public string Comment
        {
            get { return _comment; }
            set
            {
                if (_comment != value)
                {
                    _comment = value;
                    FirePropertyChanged(nameof(Comment));
                }
            }
        }
        private string _comment;

        public double Cost
        {
            get { return _cost; }
            set
            {
                if (!_costInitialized)
                {
                    CommittedCost = value;
                    _costInitialized = true;
                }

                if (_cost != value)
                {
                    _cost = value;
                    
                    FirePropertyChanged(nameof(Cost));
                    FirePropertyChanged(nameof(Dirty));
                }
            }
        }
        private double _cost;
        public double CommittedCost { get; private set; }
        private bool _costInitialized;

        public double RemainingDays
        {
            get { return _remainingDays; }
            set
            {
                if (!_remainingDaysInitialized)
                {
                    CommittedRemainingDays = value;
                    _remainingDaysInitialized = true;
                }

                if (_remainingDays != value)
                {
                    _remainingDays = value;
                    
                    FirePropertyChanged(nameof(RemainingDays));
                    FirePropertyChanged(nameof(Dirty));
                }
            }
        }
        private double _remainingDays;
        public double CommittedRemainingDays { get; private set; }
        private bool _remainingDaysInitialized;

        public DateTime CreatedDate { get; set; }
        public DateTime ActivatedDate { get; set; }
        public DateTime ChangedDate { get; set; }
        public DateTime ChangedDateUtc { get; set; }
        public DateTime AssignedToMeDate { get; set; }
        public DateTime CostsChangedDate { get; set; }

        public State State
        {
            get { return _state; }
            set
            {
                if (!_stateInitialized)
                {
                    CommittedState = value;
                    _stateInitialized = true;
                }

                if (_state != value)
                {
                    _state = value;
                    
                    FirePropertyChanged(nameof(State));
                    FirePropertyChanged(nameof(Dirty));
                }
            }
        }
        private State _state;
        public State CommittedState { get; private set; }
        private bool _stateInitialized;

        public Substatus? Substatus
        {
            get { return _substatus; }
            set
            {
                if (!_substatusInitialized)
                {
                    CommittedSubstatus = value;
                    _substatusInitialized = true;
                }

                if (_substatus != value)
                {
                    _substatus = value;
                    
                    FirePropertyChanged(nameof(Substatus));
                    FirePropertyChanged(nameof(Dirty));
                }
            }
        }
        private Substatus? _substatus;
        public Substatus? CommittedSubstatus { get; private set; }
        private bool _substatusInitialized;

        public ResolvedReason ResolvedReason { get; set; }
        public DateTime? ResolvedDate { get; set; }

        public string Notes { get; set; }

        public List<BurndownSummary> TimeSpentOnDate { get; set; } = new List<BurndownSummary>();

        public bool Dirty
        {
            get
            {
                return
                    (RemainingDays != CommittedRemainingDays) ||
                    (Cost != CommittedCost) ||
                    (State != CommittedState) ||
                    (Substatus != CommittedSubstatus) ||
                    !String.IsNullOrEmpty(Comment);
            }
        }

        public void Reset()
        {
            if (!Dirty)
            {
                return;
            }

            RemainingDays = CommittedRemainingDays;
            Cost = CommittedCost;
            State = CommittedState;
            Substatus = CommittedSubstatus;
            Comment = null;
        }

        public void Commit()
        {
            CommittedRemainingDays = RemainingDays;
            CommittedCost = Cost;
            CommittedState = State;
            CommittedSubstatus = Substatus;
            Comment = null;
            FirePropertyChanged(nameof(Dirty));
        }

        public UberStatus UberStatus
        {
            get
            {
                switch (State)
                {
                    case State.Proposed:
                    case State.Committed:
                        return UberStatus.NotYet;

                    case State.Completed:
                    case State.Cut:
                    case State.Resolved:
                    case State.Closed:
                        return UberStatus.AllDone;

                    default:
                        throw new ArgumentException("Unknown value", "State");

                    case State.Active:
                        if (!Substatus.HasValue)
                        {
                            return UberStatus.NotYet;
                        }
                        else
                        {
                            switch (Substatus.Value)
                            {
                                case Kanban4U.Models.Substatus.InCodeReview:
                                case Kanban4U.Models.Substatus.Investigating:
                                case Kanban4U.Models.Substatus.TestingFix:
                                case Kanban4U.Models.Substatus.UnderDevelopment:
                                    return UberStatus.Working;

                                case Kanban4U.Models.Substatus.Blocked:
                                case Kanban4U.Models.Substatus.BugUnderstood:
                                case Kanban4U.Models.Substatus.Consider:
                                case Kanban4U.Models.Substatus.Deployed:
                                case Kanban4U.Models.Substatus.FixVerified:
                                case Kanban4U.Models.Substatus.InCustomerValidation:
                                case Kanban4U.Models.Substatus.Mitgated:
                                case Kanban4U.Models.Substatus.Postponed:
                                case Kanban4U.Models.Substatus.ReadyForDeployment:
                                    return UberStatus.NotYet;

                                case Kanban4U.Models.Substatus.QueuedForCheckin:
                                case Kanban4U.Models.Substatus.CheckedIn:
                                    return UberStatus.AllDone;

                                default:
                                    throw new ArgumentException();
                            }
                        }

                    case State.Started:
                        return UberStatus.Working;
                }
            }
        }
    }

    public class AuditIssue
    {
        public string Owner { get; set; }
        public WorkItem WorkItem { get; set; }
        public string Issue { get; set; }
        public string ActionRequired { get; set; }
    }
}
