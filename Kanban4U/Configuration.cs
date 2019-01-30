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

        public string TeamProjectName { get; private set; } = "DefaultCollection";
        public string TeamVSTSUri { get; private set; }
        public string TeamId { get; private set; }
        public string TeamName { get; private set; }
        public string TeamProjectId { get; private set; }
        public string RemainingDaysFieldName { get; private set; } = "Microsoft.VSTS.Scheduling.Effort";
        public string CostFieldName { get; private set; }
        public string SubstatusFieldName { get; private set; }
        public string RankFieldName { get; private set; }
        public string ViewOnWebQueryId { get; private set; }

        public string MeQueryWiql { get; private set; } = @"
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

        public string TeamMeQueryWiql { get; private set; } = @"
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

        public string CompletedQueryWiql { get; private set; } = @"
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

        public string TeamCompletedQueryWiql { get; private set; } = @"
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

            object value;
            if (_roamingSettings.Values.TryGetValue("TeamVSTSUri", out value))
            {
                TeamVSTSUri = value.ToString();
            }
        }

        public void SetTeamVSTSUri(string value)
        {
            _roamingSettings.Values["TeamVSTSUri"] = value;
            TeamVSTSUri = value;
        }

    }
}
