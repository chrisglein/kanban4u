using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban4U
{
    public class VstsIteration
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Path { get; set; }
        public VstsIterationAttributes Attributes { get; set; }

        [JsonIgnore]
        public DateTime? StartDate => Attributes.StartDate;
        [JsonIgnore]
        public DateTime? FinishDate => Attributes.FinishDate;
        [JsonIgnore]
        public string TimeFrame => Attributes.TimeFrame;

        public string GetShortStartDate() { return StartDate?.ToShortDateString(); }
        public string GetShortFinishDate() { return FinishDate?.ToShortDateString(); }
    }

    public class VstsIterationAttributes
    {
        public DateTime? StartDate { get; set; }
        public DateTime? FinishDate { get; set; }
        public string TimeFrame { get; set; }
    }
}
