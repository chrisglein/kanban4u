using Newtonsoft.Json;

namespace Kanban4U.Vsts
{
    [JsonObject(MemberSerialization.OptIn)]
    public class VstsWorkItemFields
    {
        [JsonProperty(PropertyName = "System.Title")]
        public string Title { get; set; }

        [JsonProperty(PropertyName = "System.State")]
        public string State { get; set; }

        // TODO: We need zee 'Issue Type' field
        // Code Defect, Spec Defect, Test Defect, etc... which needs to get pushed into ItemViewModel.IssueType in VstsModelToViewModel.ItemViewModelFromVstsWorkItem

        // TODO: What is the 'Deliverable' field that can be used for a new ItemViewModel.Deliverable property?

        [JsonProperty(PropertyName = "System.AreaPath")]
        public string AreaPath { get; set; }

        [JsonProperty(PropertyName = "System.IterationPath")]
        public string IterationPath { get; set; }

        [JsonProperty(PropertyName = "System.WorkItemType")]
        public string WorkItemType { get; set; }

        [JsonProperty(PropertyName = "System.Description")]
        public string Description { get; set; }

        [JsonProperty(PropertyName = "Microsoft.VSTS.Common.Triage")]
        public string Triage { get; set; }

        [JsonProperty(PropertyName = "OSG.Rank")]
        public string Rank { get; set; }

        [JsonProperty(PropertyName = "Microsoft.VSTS.Common.Priority")]
        public string Priority { get; set; }

        [JsonProperty(PropertyName = "Microsoft.VSTS.Common.Severity")]
        public string Severity { get; set; }

        [JsonProperty(PropertyName = "System.AssignedTo")]
        public string AssignedTo { get; set; }

        [JsonProperty(PropertyName = "OSG.Substatus")]
        public string Substatus { get; set; }

        [JsonProperty(PropertyName = "Microsoft.VSTS.TCM.ReproSteps")]
        public string ReproSteps { get; set; }

        [JsonProperty(PropertyName = "OSG.Cost")]
        public double Cost { get; set; }

        [JsonProperty(PropertyName = "Microsoft.VSTS.Scheduling.OriginalEstimate")]
        public string OriginalEstimate { get; set; }

        [JsonProperty(PropertyName = "OSG.RemainingDays")]
        public double RemainingDays { get; set; }

        [JsonProperty(PropertyName = "System.Tags")]
        public string Tags { get; set; }

        [JsonProperty(PropertyName = "Microsoft.VSTS.Common.CustomString04")]
        public string CustomString04 { get; set; }
    }
}
