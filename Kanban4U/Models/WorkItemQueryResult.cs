using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban4U.Models
{
    public class WorkItemQueryResult
    {
        public string queryType { get; set; }
        public string queryResultType { get; set; }
        public DateTime asOf { get; set; }
        public Column[] columns { get; set; }
        public WorkItem[] workItems { get; set; }
    }

    public class WorkItemResult
    {
        public int id { get; set; }
        public string url { get; set; }
    }

    public class Column
    {
        public string referenceName { get; set; }
        public string name { get; set; }
        public string url { get; set; }
    }
}
