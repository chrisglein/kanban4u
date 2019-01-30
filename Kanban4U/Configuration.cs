using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Kanban4U
{
    public class Configuration
    {
        private Windows.Storage.ApplicationDataContainer _roamingSettings;
        private static Configuration s_instance;

        public string TeamProjectName { get; set; }
        public string TeamVSTSUri { get; set; }
        public string TeamVSSPSUri { get; set; }
        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public string TeamProjectId { get; set; }
        public string RemainingDaysFieldName { get; set; } = "Microsoft.VSTS.Scheduling.Effort";
        public string CostFieldName { get; set; }
        public string SubstatusFieldName { get; set; }
        public string RankFieldName { get; set; }
        public string ViewOnWebQueryId { get; set; }

        public string MeQueryWiql { get; set; } = @"
SELECT
    [System.Id],
    [System.Title],
    [System.State],
    [System.IterationPath],
    [Microsoft.VSTS.Scheduling.Effort]
FROM workitems
WHERE
    [System.AssignedTo] = @me
ORDER BY [System.IterationPath]
";

        public string TeamMeQueryWiql { get; set; } = @"
SELECT
    [System.Id],
    [System.Title],
    [System.AssignedTo],
    [System.State],
    [System.IterationPath],
    [System.WorkItemType],
    [Microsoft.VSTS.Scheduling.Effort]
FROM workitems
WHERE
    [System.AssignedTo] IN GROUP @team
ORDER BY [System.AssignedTo],
    [System.IterationPath]
";

        public string CompletedQueryWiql { get; set; } = @"
SELECT
    [System.Id],
    [System.Title],
    [System.State],
    [System.IterationPath],
    [Microsoft.VSTS.Scheduling.Effort]
FROM workitems
WHERE
    (
        [Microsoft.VSTS.Common.ResolvedBy] = @me
        OR (
            [System.AssignedTo] = @me
            AND [System.State] = 'Completed'
        )
    )
ORDER BY [System.IterationPath]
";

        public string TeamCompletedQueryWiql { get; set; } = @"
SELECT
    [System.Id],
    [System.Title],
    [System.State],
    [System.AssignedTo],
    [System.IterationPath],
    [Microsoft.VSTS.Scheduling.Effort]
FROM workitems
WHERE
    (
        [Microsoft.VSTS.Common.ResolvedBy] IN GROUP @team
        OR (
            [System.AssignedTo] IN GROUP @team
            AND [System.State] = 'Completed'
        )
    )
ORDER BY [System.WorkItemType],
    [System.AssignedTo]
";

        public static Configuration CurrentConfig
        {
            get
            {
                if (s_instance == null)
                    s_instance = new Configuration();

                return s_instance;
            }
        }

        private Configuration()
        {
            _roamingSettings = Windows.Storage.ApplicationData.Current.RoamingSettings;
        }

        public string GetRoamingGroupTaskID()
        {
            return (string)_roamingSettings.Values["scram_group_id"];
        }

        public void SetRoamingGroupTaskID(string taskID)
        {
            _roamingSettings.Values["scram_group_id"] = taskID;
        }

    }
}
