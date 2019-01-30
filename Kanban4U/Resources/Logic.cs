using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;
using Windows.Web.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Kanban4U;
using Kanban4U.Models;
using System.Diagnostics;
using System.Linq;
using System.Threading;


namespace Kanban4U
{
    public class Logic
    {
        // Dates in VSTS are sometimes absurd values like 1/1/9999 (note, distinct from DateTime.MaxValue). Ignore these
        private static bool IsDateLegitimate(DateTime dateTime)
        {
            if (dateTime.Year > 9000)
            {
                return false;
            }

            return true;
        }

        private static SemaphoreSlim s_authenticationLock = new SemaphoreSlim(1);


        private static async Task<HttpClient> GetClient()
        {
            await s_authenticationLock.WaitAsync();

            try
            {

                // Don't let two threads try to do the authentication flow at once. 
                string personalAccessToken = _pendingPersonalAccessToken ?? GlobalSettings.Instance.PersonalAccessToken;
                PersonalAccessTokenAuthenticationFailed = false;

                // Use ADAL to authenticate if we don't have any cached credential
                if (String.IsNullOrEmpty(personalAccessToken))
                {
                    try
                    {
                        return await Kanban4U.Vsts.VstsApiWrapper.CreateHttpClientAsync();
                    }
                    catch (Microsoft.IdentityModel.Clients.ActiveDirectory.AdalException)
                    {
                    }

                    // It didn't work, _and_ we don't have a saved access token, so ask the user for one
                    var dialog = new PersonalAccessTokenDialog();
                    await dialog.ShowAsync();
                    personalAccessToken = dialog.PersonalAccessToken;
                    personalAccessToken = personalAccessToken.Trim();

                    GlobalSettings.Instance.PersonalAccessToken = personalAccessToken;
                    // Save this out as what we're probably going to store in LocalSettings, but don't do it yet.
                    // If it's wrong we won't find out until later. So wait for a success before commiting it.
                    _pendingPersonalAccessToken = personalAccessToken;
                }

                // Authenticate using the personal access token
                var client = new HttpClient();

                string credentials = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", personalAccessToken)));

                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new HttpMediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new HttpCredentialsHeaderValue("Basic", credentials);

                return client;
            }
            finally
            {
                s_authenticationLock.Release();
            }
        }

        // If we get a successful query save this out in LocalSettings 
        private static string _pendingPersonalAccessToken;

        public static bool PersonalAccessTokenAuthenticationFailed { get; private set; }

        private static void OnSuccessfulQuery()
        {
            if (!String.IsNullOrEmpty(_pendingPersonalAccessToken))
            {
                GlobalSettings.Instance.PersonalAccessToken = _pendingPersonalAccessToken;
                _pendingPersonalAccessToken = null;
            }
        }

        public static async Task<LogicQueryResult> GetWorkItemsByQuery(bool me = true)
        {
            var result = new LogicQueryResult();

            using (var client = await GetClient())
            {
                try
                {
                    // Need TeamSettings to be able to compute current/next iteration.
                    await EnsureTeamSettings(client);
                    OnSuccessfulQuery();

                    var workItems = await Task.WhenAll(new[] {
                    GetWorkItems(client, me ? Configuration.CurrentConfig.MeQueryWiql : Configuration.CurrentConfig.TeamMeQueryWiql, true),
                    GetWorkItems(client, me ? Configuration.CurrentConfig.CompletedQueryWiql : Configuration.CurrentConfig.TeamCompletedQueryWiql, true)
                });

                    result.WorkItems = workItems[0];
                    if (workItems.Length > 1)
                    {
                        result.CompletedWorkItems = workItems[1];
                    }

                    result.AuditIssues = PerformAudit(workItems[0].Concat(workItems[1]));
                }
                catch (Exception e)
                {
                    // Often this is because the response body wasn't valid Json, because there was 
                    // something wrong with the HttpClient's authentication and we got HTML back
                    // instead. Don't tank the app, just tell the user something went wrong.
                    await ExceptionDialog.ShowAsync(e);

                    return null;
                }

                return result;
            }
        }

        private static async Task EnsureTeamSettings(HttpClient client)
        {
            if (TeamSettings.ProjectUrl == null)
            {
                await GetTeamSettings(client, TeamSettings, GlobalSettings.Instance.Team?.TeamId);
            }
        }

        public static TeamSettings TeamSettings { get; private set; } = new TeamSettings();

        private static async Task<ObservableCollection<WorkItem>> GetWorkItems(HttpClient client, string query, bool iswiql)
        {
            string baseAddress = Configuration.CurrentConfig.TeamVSTSUri + "/DefaultCollection/_apis/wit/";

            HttpResponseMessage response;
            if (iswiql)
            {
                // TODO: use iterations from team definition
                var user = GlobalSettings.Instance.GetUserToImpersonateDisplayName() ?? (await GetCurrentUser()).DisplayName;
                var currentIteration = Configuration.CurrentConfig.TeamProjectName + "\\" + DateTime.Now.ToString("yyMM");
                var currentIterationPlusOne = Configuration.CurrentConfig.TeamProjectName + "\\" + DateTime.Now.AddMonths(1).ToString("yyMM");
                var currentIterationMinusOne = Configuration.CurrentConfig.TeamProjectName + "\\" + DateTime.Now.AddMonths(-1).ToString("yyMM");

                var team = GlobalSettings.Instance.Team?.ToWiqlString();

                query = query.Replace("@Me", $"'{user}'", StringComparison.OrdinalIgnoreCase);
                query = query.Replace("@team", $"'{team}'", StringComparison.OrdinalIgnoreCase);
                query = query.Replace("@currentiteration+1", $"'{currentIterationPlusOne}'", StringComparison.OrdinalIgnoreCase);
                query = query.Replace("@currentiteration-1", $"'{currentIterationMinusOne}'", StringComparison.OrdinalIgnoreCase);
                query = query.Replace("@currentiteration", $"'{currentIteration}'", StringComparison.OrdinalIgnoreCase);
                var bodyDictionary = new Dictionary<string, string> { { "query", query } };
                var bodyJson = JsonConvert.SerializeObject(bodyDictionary);
                var content = new HttpStringContent(bodyJson, UnicodeEncoding.Utf8, "application/json");
                var queryUri = $"{baseAddress}wiql?api-version=4.1";
                response = await client.PostAsync(new Uri(queryUri), content);
            }
            else
            {
                var queryUri = $"{baseAddress}wiql/{query}?expand=clauses&api-version=2.0";
                response = await client.GetAsync(new Uri(queryUri));
            }

            if (CheckHttpResponse(response))
            {
                return new ObservableCollection<WorkItem>();
            }

            var result = await response.Content.ReadAsStringAsync();

            // We get html back if authentication failed
            if (response.Content.Headers.ContentType.MediaType != "application/json")
            {
                throw new InvalidOperationException(result);
            }

            return await GetWorkItemsWorker(client, result);
        }

        private static async Task<List<JToken>> GetWorkItemDetailsBatch(HttpClient client, string[] ids)
        {
            string baseAddress = Configuration.CurrentConfig.TeamVSTSUri + "/DefaultCollection/_apis/wit/";
            // todo: recraft this using vstsapiwrapper
            //using the queryId in the url, we can execute the query
            var detailsUri = $"{baseAddress}WorkItemsBatch?api-version=5.0-preview.1";

            // WorkItemsBatch API has a limit on the request size.
            const int MaxBatchSize = 200;
            var batches = ids.Select((id, index) => new { id, index })
                             .GroupBy(x => x.index / MaxBatchSize);

            var result = new List<JToken>();

            foreach (var batch in batches)
            {
                var bodyDictionary = new Dictionary<string, string[]> {
                    { "ids", batch.Select(x => x.id).ToArray() },
                };
                var bodyJson = JsonConvert.SerializeObject(bodyDictionary);
                var content = new HttpStringContent(bodyJson, UnicodeEncoding.Utf8, "application/json");

                var workItemResponse = await client.PostAsync(new Uri(detailsUri), content);

                if (workItemResponse.IsSuccessStatusCode)
                {
                    string response = await workItemResponse.Content.ReadAsStringAsync();
                    var responseParsed = JObject.Parse(response);
                    var subList = responseParsed["value"].ToList();

                    result.AddRange(subList);
                }
            }

            return result;
        }

        private static async Task<ObservableCollection<WorkItem>> GetWorkItemsWorker(HttpClient client, string responseBody)
        {
            WorkItemQueryResult queryResult = JsonConvert.DeserializeObject<WorkItemQueryResult>(responseBody);

            var workItemDetails = await GetWorkItemDetailsBatch(client, queryResult.workItems.Select(x => x.Id).ToArray());

            // now that we have a bunch of work items, build a list of id's so we can get details
            var workItemTasks = workItemDetails.Select(async details =>
            {
                string id = details["id"].ToObject<string>();
                DateTime changedDateUtc = details["fields"]["System.ChangedDate"].ToObject<DateTime>();

                if (GlobalSettings.Instance.TryGetCachedWorkItem(id, out var cachedItem))
                {
                    if (cachedItem.ChangedDateUtc == changedDateUtc)
                    {
                        return cachedItem;
                    }
                }

                // This should be replaced with 
                // var vstsWorkItem = await Kanban4U.Vsts.VstsApiWrapper.GetWorkItemByIdAsync(workItem.Id);
                // but too lazy to add SYstem.* fields to the model object r/n

                WorkItem item = new WorkItem();
                item.Id = id;
                item.ChangedDateUtc = changedDateUtc;
                item.SelfUrl = details["url"].ToObject<string>();

                // The URL in the payload is the REST API url to get more information about the work item. Swap the URL
                // contents to make the "Url" property on the workitem take you to the human-usable work item URL.
                item.Url = item.SelfUrl.Replace("_apis/wit/workItems", Configuration.CurrentConfig.TeamProjectName + "/_workitems/edit");

                var fields = details["fields"];

                var type = fields["System.WorkItemType"].ToObject<string>();
                switch (type)
                {
                    case "Task": item.Type = WorkItemType.Task; break;
                    case "Feature": item.Type = WorkItemType.Task; break;
                    default: item.Type = WorkItemType.Bug; break;
                }

                item.Title = fields["System.Title"].ToObject<string>();
                item.AssignedTo = fields["System.AssignedTo"]?["uniqueName"]?.ToObject<string>();
                item.ResolvedBy = fields["Microsoft.VSTS.Common.ResolvedBy"]?["uniqueName"]?.ToObject<string>();
                item.ResolvedDate = fields["Microsoft.VSTS.Common.ResolvedDate"]?.ToObject<DateTime>().ToLocalTime();

                item.IterationPath = fields["System.IterationPath"].ToObject<string>();
                if (Configuration.CurrentConfig.RankFieldName != null)
                {
                    item.Rank = fields[Configuration.CurrentConfig.RankFieldName]?.ToObject<int>() ?? 1000;
                }

                var remainingDays = Configuration.CurrentConfig.RemainingDaysFieldName != null ? fields[Configuration.CurrentConfig.RemainingDaysFieldName] : null;
                if (remainingDays != null)
                {
                    item.RemainingDays = remainingDays.ToObject<double>();
                }

                var cost = Configuration.CurrentConfig.CostFieldName != null ? fields[Configuration.CurrentConfig.CostFieldName] : null;
                if (cost != null)
                {
                    item.Cost = cost.ToObject<double>();
                }
                else
                {
                    item.Cost = 0;
                }

                item.CreatedDate = fields["System.CreatedDate"].ToObject<DateTime>().ToLocalTime();
                item.ChangedDate = changedDateUtc.ToLocalTime();

                string stateString = fields["System.State"].ToObject<string>();
                State state;
                if (!Enum.TryParse<State>(stateString, out state))
                {
                    state = State.Active;
                }
                item.State = state;

                string substatusString = Configuration.CurrentConfig.SubstatusFieldName != null ? fields[Configuration.CurrentConfig.SubstatusFieldName]?.ToObject<string>() : null;
                item.Substatus = SubstatusFromRawString(substatusString);

                var activatedDate = fields["Microsoft.VSTS.Common.ActivatedDate"];
                if (activatedDate != null)
                {
                    item.ActivatedDate = DateTime.Parse(activatedDate.ToString());
                }
                else
                {
                    item.ActivatedDate = item.CreatedDate;
                }
                if (!IsDateLegitimate(item.ActivatedDate))
                {
                    item.ActivatedDate = item.CreatedDate;
                }

                item.AssignedToMeDate = item.CreatedDate;
                item.CostsChangedDate = item.CreatedDate;

                var resolvedReasonString = fields["Microsoft.VSTS.Common.ResolvedReason"]?.ToObject<string>();
                if (resolvedReasonString != null && _stringToResolvedReason.TryGetValue(resolvedReasonString, out var resolvedReason))
                {
                    item.ResolvedReason = resolvedReason;
                }

                await ProcessUpdateHistory(client, item);

                GlobalSettings.Instance.CacheWorkItem(item);

                return item;
            }).ToList();

            // Above queued the tasks in parallel, now wait for them to finish.
            var workItems = await Task.WhenAll(workItemTasks);

            GlobalSettings.Instance.SaveCachedWorkItems();

            return new ObservableCollection<WorkItem>(workItems);
        }

        // Fetch the update history for a work item and fill in any WorkItem properties that are calculated from that.
        public static async Task ProcessUpdateHistory(HttpClient client, WorkItem item)
        {
            string baseAddress = Configuration.CurrentConfig.TeamVSTSUri + "/DefaultCollection/_apis/wit/";
            HttpResponseMessage updatesResponse = await client.GetAsync(new Uri($"{baseAddress}WorkItems/{item.Id}/updates?&api-version=1.0"));

            if (!updatesResponse.IsSuccessStatusCode)
            {
                return;
            }

            string updatesString = await updatesResponse.Content.ReadAsStringAsync();
            JObject updatesParsed = JObject.Parse(updatesString);

            var updateCount = updatesParsed["count"].ToObject<double>();

            LastTimeFieldUpdated assignedToChanged = new LastTimeFieldUpdated("System.AssignedTo");
            LastTimeFieldUpdated remainingChanged = new LastTimeFieldUpdated(Configuration.CurrentConfig.RemainingDaysFieldName);
            LastTimeFieldUpdated costChanged = new LastTimeFieldUpdated(Configuration.CurrentConfig.CostFieldName);
            TimeSpentByCategory timeSpentByCategory = new TimeSpentByCategory();
            TimeSpentLog timeSpentLog = new TimeSpentLog { Item = item };

            for (int i = 0; i < updateCount; i++)
            {
                var update = updatesParsed["value"][i];
                assignedToChanged.Check(update);
                remainingChanged.Check(update);
                costChanged.Check(update);
                timeSpentByCategory.Check(update);
                timeSpentLog.Check(update);
            }

            item.AssignedToMeDate = assignedToChanged.LastRevised ?? item.ActivatedDate;

            // Take the newest update to Cost or RemainingDays
            DateTime lastTimeCostsChanged = item.CreatedDate;
            if (remainingChanged.LastRevised.HasValue && (remainingChanged.LastRevised.Value > lastTimeCostsChanged))
            {
                lastTimeCostsChanged = remainingChanged.LastRevised.Value;
            }
            if (costChanged.LastRevised.HasValue && (costChanged.LastRevised.Value > lastTimeCostsChanged))
            {
                lastTimeCostsChanged = costChanged.LastRevised.Value;
            }
            item.CostsChangedDate = lastTimeCostsChanged;

            string notes = "";
            int lines = 0;
            foreach (var pair in timeSpentByCategory.TotalSpentOnCategory)
            {
                if (lines >= 3)
                {
                    break;
                }

                string line = $"Total spent on \"{pair.Key}\": {pair.Value}";
                if (lines > 0)
                {
                    notes += "\n";
                }
                notes += line;
                lines++;
            }
            item.Notes = notes;


            TimeSpentTrace($"TimeLog for '{item.Title}'");
            double totalSpent = 0;
            foreach (var burndown in timeSpentLog.TimeSpentOnDate)
            {
                TimeSpentTrace($"  {burndown.Date} : {burndown.CostChange}, {burndown.RemainingDaysChange}");
                totalSpent += burndown.CostChange;
            }
            TimeSpentTrace($"TimeLog total {totalSpent}");

            item.TimeSpentOnDate = timeSpentLog.TimeSpentOnDate;
        }

        [Conditional("DEBUG")]
        public static void TimeSpentTrace(string value)
        {
            Debug.WriteLine(value);
        }

        private class TimeSpentByCategory
        {
            public void Check(JToken token)
            {
                var comment = token["fields"]?["System.History"]?["newValue"]?.ToObject<string>();
                if (comment == null)
                {
                    return;
                }

                var split = comment.Split(" days: ");
                if (split == null || split.Length != 2)
                {
                    return;
                }

                double amount;
                if (!double.TryParse(split[0], out amount))
                {
                    return;
                }

                string category = split[1];
                double sumAmount = 0.0;
                if (TotalSpentOnCategory.TryGetValue(category, out sumAmount))
                {
                    amount += sumAmount;
                }
                TotalSpentOnCategory[category] = amount;
            }

            public Dictionary<string, double> TotalSpentOnCategory { get; private set; } = new Dictionary<string, double>();
        }

        private static DateTime GetRevisedDate(JToken token)
        {
            var updateDate = token["revisedDate"].ToObject<DateTime>().ToLocalTime();
            if (!IsDateLegitimate(updateDate))
            {
                var changedDate = token["fields"]?["System.ChangedDate"]?["newValue"].ToObject<DateTime>().ToLocalTime();
                if (changedDate.HasValue)
                {
                    return changedDate.Value;
                }
            }

            return updateDate;
        }

        // Looks at the list of updates and reports when a field was last updated
        private class LastTimeFieldUpdated
        {
            public LastTimeFieldUpdated(string fieldName)
            {
                _lastRevised = DateTime.MinValue;
                _fieldName = fieldName;
            }

            public void Check(JToken token)
            {
                var updateDate = GetRevisedDate(token);

                string value = _fieldName != null ? token["fields"]?[_fieldName]?["newValue"]?.ToObject<string>() : null;

                if (value != null)
                {
                    // Only look at the most recent update
                    if (IsDateLegitimate(updateDate) && (!_lastRevised.HasValue || (updateDate > _lastRevised.Value)))
                    {
                        _lastRevised = updateDate;
                    }
                }
            }

            public DateTime? LastRevised
            {
                get
                {
                    return _lastRevised;
                }
            }

            DateTime? _lastRevised;
            string _fieldName;
        }

        private class TimeSpentLog
        {
            public void Check(JToken token)
            {
                var updateDate = GetRevisedDate(token);
                double? oldCost = Configuration.CurrentConfig.CostFieldName != null ? token["fields"]?[Configuration.CurrentConfig.CostFieldName]?["oldValue"]?.ToObject<double>() : null;
                double? newCost = Configuration.CurrentConfig.CostFieldName != null ? token["fields"]?[Configuration.CurrentConfig.CostFieldName]?["newValue"]?.ToObject<double>() : null;
                double? oldRemaining = Configuration.CurrentConfig.RemainingDaysFieldName != null ? token["fields"]?[Configuration.CurrentConfig.RemainingDaysFieldName]?["oldValue"]?.ToObject<double>() : null;
                double? newRemaining = Configuration.CurrentConfig.RemainingDaysFieldName != null ? token["fields"]?[Configuration.CurrentConfig.RemainingDaysFieldName]?["newValue"]?.ToObject<double>() : null;

                double costChange = 0;
                if (newCost.HasValue)
                { 
                    // Ignore changes of the cost to 0, these happen when triage is assigning out work items.
                    if (newCost.Value != 0)
                    {
                        double oldCostValue = (oldCost ?? 0);
                        costChange = (newCost.Value - oldCostValue);
                        TimeSpentTrace($"{updateDate}  Cost {oldCostValue} -> {newCost.Value} = {costChange}");
                    }
                }

                double remainingDaysChange = 0;
                if (newRemaining.HasValue && oldRemaining.HasValue)
                {
                    remainingDaysChange = (newRemaining.Value - oldRemaining.Value);
                    TimeSpentTrace($"{updateDate}  Remaining Days {oldRemaining.Value} -> {newRemaining.Value} = {remainingDaysChange}");
                }

                if (costChange != 0 || remainingDaysChange != 0)
                {
                    TimeSpentOnDate.Add(new BurndownSummary
                    {
                        Date = updateDate,
                        CostChange = costChange,
                        RemainingDaysChange = remainingDaysChange,
                        WorkItem = Item,
                    });
                }
            }

            public List<BurndownSummary> TimeSpentOnDate { get; private set; } = new List<BurndownSummary>();

            public WorkItem Item { get; set; }
        }

        public static async Task<bool> UpdateBug(WorkItem item)
        {
            var id = item.Id;
            List<Object> patchDocument = new List<Object>();

            if (item.Cost != item.CommittedCost)
            {
                patchDocument.Add(new { op = "add", path = "/fields/" + Configuration.CurrentConfig.CostFieldName, value = item.Cost });
            }

            if (item.RemainingDays != item.CommittedRemainingDays)
            {
                patchDocument.Add(new { op = "add", path = "/fields/" + Configuration.CurrentConfig.RemainingDaysFieldName, value = item.RemainingDays });
            }

            if (item.State != item.CommittedState)
            {
                patchDocument.Add(new { op = "add", path = "/fields/System.State", value = item.State.ToString() });
            }

            if (item.Substatus != item.CommittedSubstatus)
            {
                patchDocument.Add(new { op = "add", path = "/fields/" + Configuration.CurrentConfig.SubstatusFieldName, value = SubstatusToRawString(item.Substatus.Value) });
            }

            if (!String.IsNullOrEmpty(item.Comment))
            {
                string comment = item.Comment;
                double timeSpent = 0.0;
                timeSpent += (item.Cost - item.CommittedCost);
                if (timeSpent != 0.0)
                {
                    comment = $"{timeSpent} days: {comment}";
                }
                patchDocument.Add(new { op = "add", path = "/fields/System.History", value = comment });
            }

            using (var client = await GetClient())
            {
                var patchValue = new HttpStringContent(JsonConvert.SerializeObject(patchDocument.ToArray()), UnicodeEncoding.Utf8, "application/json-patch+json"); // serialize the fields array into a json string
                var method = new HttpMethod("PATCH");
                var request = new HttpRequestMessage(method, new Uri(Configuration.CurrentConfig.TeamVSTSUri + $"/_apis/wit/workitems/{id}?api-version=2.2")) { Content = patchValue };
                var response = await client.SendRequestAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }

            return false;
        }

        public static async Task ViewBug(WorkItem item)
        {
            Uri uri = new Uri(Configuration.CurrentConfig.TeamVSTSUri + "/" + Configuration.CurrentConfig.TeamProjectName +  "/ft_xamlcon/_workitems/edit/" + item.Id);
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        public static async Task ViewQuery()
        {
            Uri uri = new Uri(Configuration.CurrentConfig.TeamVSTSUri + "/" + Configuration.CurrentConfig.TeamProjectName + "/ft_xamlcon/_queries/query/" + Configuration.CurrentConfig.ViewOnWebQueryId);
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        private static Substatus? SubstatusFromRawString(string value)
        {
            if (value == null)
            {
                return null;
            }

            Substatus substatus;
            if (_stringToSubstatus.TryGetValue(value, out substatus))
            {
                return substatus;
            }

            return null;
        }

        private static string SubstatusToRawString(Substatus value)
        {
            foreach (var pair in _stringToSubstatus)
            {
                if (pair.Value == value)
                {
                    return pair.Key;
                }
            }

            return String.Empty;
        }

        private static Dictionary<string, Substatus> _stringToSubstatus = new Dictionary<string, Substatus>
    {
        {"Blocked", Substatus.Blocked},
        {"Bug Understood", Substatus.BugUnderstood},
        {"Checked-In", Substatus.CheckedIn},
        {"Consider", Substatus.Consider},
        {"Deployed", Substatus.Deployed},
        {"Fix Verified", Substatus.FixVerified},
        {"In Code Review", Substatus.InCodeReview},
        {"In Customer Validation", Substatus.InCustomerValidation},
        {"Investigating", Substatus.Investigating},
        {"Mitgated", Substatus.Mitgated},
        {"Postponed", Substatus.Postponed},
        {"Queued For Checkin", Substatus.QueuedForCheckin},
        {"Ready For Deployment", Substatus.ReadyForDeployment},
        {"TestingFix", Substatus.TestingFix},
        {"Under Development", Substatus.UnderDevelopment},
    };

        private static Dictionary<string, ResolvedReason> _stringToResolvedReason = new Dictionary<string, ResolvedReason>
    {
        { "By Design", ResolvedReason.ByDesign },
        { "Duplicate", ResolvedReason.Duplicate },
        { "External", ResolvedReason.External },
        { "Fixed", ResolvedReason.Fixed },
        { "Not Repro", ResolvedReason.NotRepro },
        { "Won't Fix", ResolvedReason.WontFix },
    };

        public static async Task<VstsUser> GetCurrentUser()
        {
            using (var client = await GetClient())
            {
                string url = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me";
                HttpResponseMessage response = await client.GetAsync(new Uri(url));

                if (CheckHttpResponse(response))
                {
                    return new VstsUser { DisplayName = "Unknown", Email = "unknown" };
                }

                string responseString = await response.Content.ReadAsStringAsync();
                JObject responseParsed = JObject.Parse(responseString);

                var user = new VstsUser();
                user.DisplayName = responseParsed["displayName"].ToObject<string>();
                user.Email = responseParsed["emailAddress"].ToObject<string>();

                return user;
            }
        }

        private static bool CheckHttpResponse(HttpResponseMessage response)
        {
            if (response.StatusCode != HttpStatusCode.Ok)
            {
                if (!String.IsNullOrEmpty(_pendingPersonalAccessToken ?? GlobalSettings.Instance.PersonalAccessToken) 
                    && response.StatusCode == HttpStatusCode.NonAuthoritativeInformation)
                {
                    PersonalAccessTokenAuthenticationFailed = true;
                }
                return true;
            }

            return false;
        }

        public static async Task GetTeamSettings(HttpClient client, TeamSettings teamSettings, string teamId)
        {
            string url = Configuration.CurrentConfig.TeamVSTSUri + 
                ((teamId != null) ? $"/DefaultCollection/" + Configuration.CurrentConfig.TeamProjectName + "/_apis/work/teamsettings?team={teamId}"
                                  : "/DefaultCollection/" + Configuration.CurrentConfig.TeamProjectName + "/_apis/work/teamsettings");
            HttpResponseMessage response = await client.GetAsync(new Uri(url));

            if (CheckHttpResponse(response))
            {
                return;
            }

            string responseString = await response.Content.ReadAsStringAsync();
            JObject responseParsed = JObject.Parse(responseString);

            teamSettings.Iteration = responseParsed["defaultIteration"]["name"].ToObject<string>();
            teamSettings.IterationPath = "OS" + responseParsed["defaultIteration"]["path"].ToObject<string>();

            var currentIterationUrl = responseParsed["defaultIteration"]["url"].ToObject<string>();

            teamSettings.ProjectUrl = responseParsed["_links"]["project"]["href"].ToObject<string>();

            await ProcessIteration(client, currentIterationUrl, teamSettings);
        }

        public static async Task<Dictionary<string, VstsIteration>> GetTeamIterations(string teamId)
        {
            using (var client = await GetClient())
            {
                var result = new Dictionary<string, VstsIteration>();
                string url = Configuration.CurrentConfig.TeamVSTSUri +
                    ((teamId != null) ? $"/DefaultCollection/" + Configuration.CurrentConfig.TeamProjectName + "/_apis/work/teamsettings/iterations?team={teamId}"
                                      : "/DefaultCollection/" + Configuration.CurrentConfig.TeamProjectName + "/_apis/work/teamsettings/iterations");
                HttpResponseMessage response = await client.GetAsync(new Uri(url));

                if (CheckHttpResponse(response))
                {
                    return result;
                }

                string responseString = await response.Content.ReadAsStringAsync();
                JObject responseParsed = JObject.Parse(responseString);

                foreach (var iteration in responseParsed["value"].ToList())
                {
                    var vstsIteration = iteration.ToObject<VstsIteration>();
                    result[vstsIteration.Name] = vstsIteration;
                }

                return result;
            }
        }

        public static async Task<List<VstsTeam>> GetTeams(bool mine = false)
        {
            using (var client = await GetClient())
            {
                await EnsureTeamSettings(client);

                var result = new List<VstsTeam>();

                if (TeamSettings.ProjectUrl == null)
                {
                    return result;
                }

                int numAdded = 0;
                do
                {
                    numAdded = 0;

                    string url = $"{TeamSettings.ProjectUrl}/teams?$mine={mine}&$top=1000&$skip={result.Count}";
                    HttpResponseMessage response = await client.GetAsync(new Uri(url));

                    if (CheckHttpResponse(response))
                    {
                        return result;
                    }

                    string responseString = await response.Content.ReadAsStringAsync();
                    JObject responseParsed = JObject.Parse(responseString);

                    foreach (var team in responseParsed["value"].ToList())
                    {
                        result.Add(team.ToObject<VstsTeam>());
                        numAdded++;
                    }

                }
                while (numAdded > 0);

                return result.OrderBy(x => x.Name).ToList();
            }
        }

        public static async Task GetGroupMembers(HttpClient client, string id, Dictionary<string, VstsTeamMember> result)
        {
            string url = Configuration.CurrentConfig.TeamVSTSUri + $"/" + Configuration.CurrentConfig.TeamProjectName + "/_api/_identity/ReadGroupMembers?__v=5&scope={id}&readMembers=true";
            var response = await client.GetAsync(new Uri(url));

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();
                if (String.IsNullOrEmpty(responseString)) // sometimes returns empty string instead of empty json
                {
                    return;
                }

                JObject responseParsed = JObject.Parse(responseString);

                foreach (var member in responseParsed["identities"].ToList())
                {
                    var teamMember = new VstsTeamMember
                    {
                        DisplayName = member["FriendlyDisplayName"]?.ToObject<string>(),
                        Id = member["TeamFoundationId"]?.ToObject<string>(),
                        UniqueName = member["AccountName"]?.ToObject<string>(),
                        IsContainer = member["IdentityType"]?.ToObject<string>() == "group"
                    };

                    if (teamMember.IsContainer)
                    {
                        await GetGroupMembers(client, teamMember.Id, result);
                    }
                    else
                    {
                        if (!result.ContainsKey(teamMember.UniqueName))
                        {
                            result[teamMember.UniqueName] = teamMember;
                        }
                    }
                }
            }
        }

        public static async Task<List<VstsTeamMember>> GetTeamMembers(VstsTeam team, bool flatten = true)
        {
            using (var client = await GetClient())
            {
                var result = new Dictionary<string, VstsTeamMember>();

                string url = $"{team.Url}/members";
                HttpResponseMessage response = await client.GetAsync(new Uri(url));

                if (CheckHttpResponse(response))
                {
                    return new List<VstsTeamMember>();
                }

                string responseString = await response.Content.ReadAsStringAsync();
                JObject responseParsed = JObject.Parse(responseString);

                foreach (var member in responseParsed["value"].ToList())
                {
                    var teamMember = member["identity"].ToObject<VstsTeamMember>();

                    if (teamMember.IsContainer && flatten)
                    {
                        var groupMembers = new Dictionary<string, VstsTeamMember>();
                        await GetGroupMembers(client, teamMember.Id, groupMembers);
                        foreach (var kvp in groupMembers)
                        {
                            result[kvp.Key] = kvp.Value;
                        }
                    }
                    else
                    {
                        result[teamMember.UniqueName] = teamMember;
                    }
                }

                return result.Values.OrderBy(x => x.DisplayName).ToList();
            }
        }


        public static async Task ProcessIteration(HttpClient client, string url, TeamSettings teamSettings)
        {
            HttpResponseMessage response = await client.GetAsync(new Uri(url));

            if (CheckHttpResponse(response))
            {
                return;
            }

            string responseString = await response.Content.ReadAsStringAsync();
            JObject responseParsed = JObject.Parse(responseString);

            var startDate = responseParsed["attributes"]?["startDate"].ToObject<DateTime>();
            var finishDate = responseParsed["attributes"]?["finishDate"].ToObject<DateTime>();

            if (startDate.HasValue && finishDate.HasValue)
            {
                int weekdaysInIteration = CountWeekdays(startDate.Value, finishDate.Value);
                int weekdaysRemaining = CountWeekdays(DateTime.Now, finishDate.Value);

                teamSettings.DaysInIteration = weekdaysInIteration;
                teamSettings.DaysRemaining = weekdaysRemaining;
            }
        }

        public static int CountWeekdays(DateTime start, DateTime finish)
        {
            int dayOfWeekStart = ((int)start.DayOfWeek == 0 ? 7 : (int)start.DayOfWeek);
            int dayOfWeekFinish = ((int)finish.DayOfWeek == 0 ? 7 : (int)finish.DayOfWeek);
            TimeSpan span = finish - start;
            if (dayOfWeekStart < dayOfWeekFinish)
            {
                return (((span.Days / 7) * 5) + Math.Max((Math.Min((dayOfWeekFinish + 1), 6) - dayOfWeekStart), 0));
            }
            return (((span.Days / 7) * 5) + Math.Min((dayOfWeekFinish + 6) - Math.Min(dayOfWeekStart, 6), 5));
        }

        private static List<AuditIssue> PerformAudit(IEnumerable<WorkItem> workItems)
        {
            DateTime cutoff = DateTime.UtcNow.AddDays(-7);
            var issues = new List<AuditIssue>();

            foreach (var workitem in workItems)
            {
                string owner;
                if (workitem.Type == WorkItemType.Bug && !String.IsNullOrEmpty(workitem.ResolvedBy))
                {
                    owner = workitem.ResolvedBy;
                }
                else
                {
                    owner = workitem.AssignedTo;
                }

                if (workitem.TimeSpentOnDate.Count == 0)
                {
                    if (workitem.State == State.Completed ||
                        (workitem.ResolvedReason == ResolvedReason.Fixed && workitem.ResolvedDate.HasValue && workitem.ResolvedDate > cutoff))
                    {
                        if (workitem.Cost == 0)
                        {
                            // If work item is completed / fixed and Cost is 0 or unset, that's bad.
                            issues.Add(new AuditIssue
                            {
                                Owner = owner,
                                WorkItem = workitem,
                                Issue = "Item completed with Cost = 0",
                                ActionRequired = "Set cost to time spent (or 0.01 if no time spent)"
                            });
                        }
                        else if (workitem.RemainingDays != 0)
                        {
                            // If work item is completed / fixed and no time was spent on it, that's a red flag.
                            issues.Add(new AuditIssue
                            {
                                Owner = owner,
                                WorkItem = workitem,
                                Issue = "Item completed with Remaining Days != 0",
                                ActionRequired = "Set Remaining Days = 0 and adjust Cost field if necessary"
                            });
                        }
                        else
                        {
                            // TODO: In this case the Cost and Remaining Days were set when the bug was resolved. This isn't showing up as credit for the dev.
                        }
                    }
                }
            }

            return issues;
        }
    }

    public class LogicQueryResult
    {
        public ObservableCollection<WorkItem> WorkItems { get; set; } = new ObservableCollection<WorkItem>();
        public ObservableCollection<WorkItem> CompletedWorkItems { get; set; } = new ObservableCollection<WorkItem>();
        public List<AuditIssue> AuditIssues { get; set; } = new List<AuditIssue>();
    }

    public class BurndownSummary
    {
        public DateTime Date { get; set; }

        [JsonIgnore]
        public WorkItem WorkItem { get; set; }
        public double CostChange { get; set; }
        public double RemainingDaysChange { get; set; }

        public string ToStringEmptyZero(double d)
        {
            return (d == 0) ? "" : d.ToString("F1");
        }
    }
}