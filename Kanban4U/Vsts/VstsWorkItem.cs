using Newtonsoft.Json;

namespace Kanban4U.Vsts
{
    [JsonObject(MemberSerialization.OptIn)]
    public class VstsWorkItem
    {
        [JsonProperty]
        public string Id { get; set; }

        [JsonProperty]
        public string Url { get; set; }

        [JsonProperty]
        public VstsWorkItemFields Fields { get; set; }

        
    }
}
