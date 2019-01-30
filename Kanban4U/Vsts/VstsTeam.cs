using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Kanban4U
{
    public class VstsTeam
    {
        [JsonProperty("id")]
        public string TeamId { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string IdentityUrl { get; set; }
        public string ProjectName { get; set; }

        public string ToWiqlString()
        {
            return $"[{ProjectName}]\\{Name} <id:{TeamId}>";
        }
    }
}
